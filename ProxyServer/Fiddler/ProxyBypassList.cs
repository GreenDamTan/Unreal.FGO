using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace Fiddler
{
	internal class ProxyBypassList
	{
		private List<Regex> _RegExBypassList;
		private bool _BypassOnLocal;
		public bool HasEntries
		{
			get
			{
				return this._BypassOnLocal || null != this._RegExBypassList;
			}
		}
		public ProxyBypassList(string sBypassList)
		{
			if (string.IsNullOrEmpty(sBypassList))
			{
				return;
			}
			this.AssignBypassList(sBypassList);
		}
		[Obsolete]
		public bool IsBypass(string sSchemeHostPort)
		{
			string sScheme = Utilities.TrimAfter(sSchemeHostPort, "://");
			string sHostAndPort = Utilities.TrimBefore(sSchemeHostPort, "://");
			return this.IsBypass(sScheme, sHostAndPort);
		}
		public bool IsBypass(string sScheme, string sHostAndPort)
		{
			if (this._BypassOnLocal && Utilities.isPlainHostName(sHostAndPort))
			{
				return true;
			}
			if (this._RegExBypassList != null)
			{
				string input = sScheme + "://" + sHostAndPort;
				for (int i = 0; i < this._RegExBypassList.Count; i++)
				{
					if (this._RegExBypassList[i].IsMatch(input))
					{
						return true;
					}
				}
			}
			return false;
		}
		private void AssignBypassList(string sBypassList)
		{
			this._BypassOnLocal = false;
			this._RegExBypassList = null;
			if (string.IsNullOrEmpty(sBypassList))
			{
				return;
			}
			string[] array = sBypassList.Split(new char[]
			{
				';'
			}, StringSplitOptions.RemoveEmptyEntries);
			if (array.Length < 1)
			{
				return;
			}
			List<string> list = null;
			string[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				string text = array2[i];
				string text2 = text.Trim();
				if (text2.Length != 0)
				{
					if (text2.OICEquals("<local>"))
					{
						this._BypassOnLocal = true;
					}
					else
					{
						if (!text2.OICEquals("<-loopback>"))
						{
							if (!text2.Contains("://"))
							{
								text2 = "*://" + text2;
							}
							bool flag = text2.IndexOf(':') == text2.LastIndexOf(':');
							text2 = Utilities.RegExEscape(text2, true, !flag);
							if (flag)
							{
								text2 += "(:\\d+)?$";
							}
							if (list == null)
							{
								list = new List<string>();
							}
							if (!list.Contains(text2))
							{
								list.Add(text2);
							}
						}
					}
				}
			}
			if (list == null)
			{
				return;
			}
			this._RegExBypassList = new List<Regex>(list.Count);
			foreach (string current in list)
			{
				try
				{
					Regex item = new Regex(current, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
					this._RegExBypassList.Add(item);
				}
				catch
				{
					FiddlerApplication.Log.LogFormat("Invalid rule in Proxy Bypass list. '{0}'", new object[]
					{
						current
					});
				}
			}
			if (this._RegExBypassList.Count < 1)
			{
				this._RegExBypassList = null;
			}
		}
	}
}
