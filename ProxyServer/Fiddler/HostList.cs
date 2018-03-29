using System;
using System.Collections.Generic;
using System.Text;
namespace Fiddler
{
	public class HostList
	{
		private class HostPortTuple
		{
			public int _iPort;
			public string _sHostname;
			public bool _bTailMatch;
			internal HostPortTuple(string sHostname, int iPort)
			{
				this._iPort = iPort;
				if (sHostname.StartsWith("*"))
				{
					this._bTailMatch = true;
					this._sHostname = sHostname.Substring(1);
					return;
				}
				this._sHostname = sHostname;
			}
		}
		private List<string> slSimpleHosts = new List<string>();
		private List<HostList.HostPortTuple> hplComplexRules = new List<HostList.HostPortTuple>();
		private bool bEverythingMatches;
		private bool bNonPlainHostnameMatches;
		private bool bPlainHostnameMatches;
		private bool bLoopbackMatches;
		public HostList()
		{
		}
		public HostList(string sInitialList) : this()
		{
			if (!string.IsNullOrEmpty(sInitialList))
			{
				this.AssignFromString(sInitialList);
			}
		}
		public void Clear()
		{
			this.bEverythingMatches = false;
			this.bNonPlainHostnameMatches = false;
			this.bPlainHostnameMatches = false;
			this.bLoopbackMatches = false;
			this.slSimpleHosts.Clear();
			this.hplComplexRules.Clear();
		}
		public bool AssignFromString(string sIn)
		{
			string text;
			return this.AssignFromString(sIn, out text);
		}
		public bool AssignFromString(string sIn, out string sErrors)
		{
			sErrors = string.Empty;
			this.Clear();
			if (sIn == null)
			{
				return true;
			}
			sIn = sIn.Trim();
			if (sIn.Length < 1)
			{
				return true;
			}
			string[] array = sIn.ToLower().Split(new char[]
			{
				',',
				';',
				'\t',
				' ',
				'\r',
				'\n'
			}, StringSplitOptions.RemoveEmptyEntries);
			string[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				string text = array2[i];
				if (text.Equals("*"))
				{
					this.bEverythingMatches = true;
				}
				else
				{
					if (text.StartsWith("<"))
					{
						if (text.Equals("<loopback>"))
						{
							this.bLoopbackMatches = true;
							goto IL_159;
						}
						if (text.Equals("<local>"))
						{
							this.bPlainHostnameMatches = true;
							goto IL_159;
						}
						if (text.Equals("<nonlocal>"))
						{
							this.bNonPlainHostnameMatches = true;
							goto IL_159;
						}
					}
					if (text.Length >= 1)
					{
						if (text.Contains("?"))
						{
							sErrors += string.Format("Ignored invalid rule '{0}'-- ? may not appear.\n", text);
						}
						else
						{
							if (text.LastIndexOf("*") > 0)
							{
								sErrors += string.Format("Ignored invalid rule '{0}'-- * may only appear once, at the front of the string.\n", text);
							}
							else
							{
								int num = -1;
								string text2;
								Utilities.CrackHostAndPort(text, out text2, ref num);
								if (-1 == num && !text2.StartsWith("*"))
								{
									this.slSimpleHosts.Add(text);
								}
								else
								{
									HostList.HostPortTuple item = new HostList.HostPortTuple(text2, num);
									this.hplComplexRules.Add(item);
								}
							}
						}
					}
				}
				IL_159:;
			}
			if (this.bNonPlainHostnameMatches && this.bPlainHostnameMatches)
			{
				this.bEverythingMatches = true;
			}
			return string.IsNullOrEmpty(sErrors);
		}
		public override string ToString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			if (this.bEverythingMatches)
			{
				stringBuilder.Append("*; ");
			}
			if (this.bPlainHostnameMatches)
			{
				stringBuilder.Append("<local>; ");
			}
			if (this.bNonPlainHostnameMatches)
			{
				stringBuilder.Append("<nonlocal>; ");
			}
			if (this.bLoopbackMatches)
			{
				stringBuilder.Append("<loopback>; ");
			}
			foreach (string current in this.slSimpleHosts)
			{
				stringBuilder.Append(current);
				stringBuilder.Append("; ");
			}
			foreach (HostList.HostPortTuple current2 in this.hplComplexRules)
			{
				if (current2._bTailMatch)
				{
					stringBuilder.Append("*");
				}
				stringBuilder.Append(current2._sHostname);
				if (current2._iPort > -1)
				{
					stringBuilder.Append(":");
					stringBuilder.Append(current2._iPort.ToString());
				}
				stringBuilder.Append("; ");
			}
			if (stringBuilder.Length > 1)
			{
				stringBuilder.Remove(stringBuilder.Length - 1, 1);
			}
			return stringBuilder.ToString();
		}
		public bool ContainsHost(string sHost)
		{
			int iPort = -1;
			string sHostname;
			Utilities.CrackHostAndPort(sHost, out sHostname, ref iPort);
			return this.ContainsHost(sHostname, iPort);
		}
		public bool ContainsHostname(string sHostname)
		{
			return this.ContainsHost(sHostname, -1);
		}
		public bool ContainsHost(string sHostname, int iPort)
		{
			if (this.bEverythingMatches)
			{
				return true;
			}
			if (this.bPlainHostnameMatches || this.bNonPlainHostnameMatches)
			{
				bool flag = Utilities.isPlainHostName(sHostname);
				if (this.bPlainHostnameMatches && flag)
				{
					return true;
				}
				if (this.bNonPlainHostnameMatches && !flag)
				{
					return true;
				}
			}
			if (this.bLoopbackMatches && Utilities.isLocalhostname(sHostname))
			{
				return true;
			}
			sHostname = sHostname.ToLower();
			if (this.slSimpleHosts.Contains(sHostname))
			{
				return true;
			}
			foreach (HostList.HostPortTuple current in this.hplComplexRules)
			{
				if (iPort == current._iPort || -1 == current._iPort)
				{
					if (current._bTailMatch && sHostname.EndsWith(current._sHostname))
					{
						bool result = true;
						return result;
					}
					if (current._sHostname == sHostname)
					{
						bool result = true;
						return result;
					}
				}
			}
			return false;
		}
	}
}
