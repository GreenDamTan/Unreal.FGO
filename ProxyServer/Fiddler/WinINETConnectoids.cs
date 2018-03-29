using System;
using System.Collections.Generic;
using System.Text;
namespace Fiddler
{
	internal class WinINETConnectoids
	{
		internal Dictionary<string, WinINETConnectoid> _oConnectoids = new Dictionary<string, WinINETConnectoid>();
		public override string ToString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendFormat("RAS reports {0} Connectoids\n\n", this._oConnectoids.Count);
			foreach (KeyValuePair<string, WinINETConnectoid> current in this._oConnectoids)
			{
				stringBuilder.AppendFormat("-= WinINET settings for '{0}' =-\n{1}\n", current.Key, current.Value.oOriginalProxyInfo.ToString());
			}
			return stringBuilder.ToString();
		}
		public WinINETConnectoids()
		{
			string[] connectionNames = RASInfo.GetConnectionNames();
			string[] array = connectionNames;
			for (int i = 0; i < array.Length; i++)
			{
				string text = array[i];
				if (CONFIG.bDebugSpew)
				{
					FiddlerApplication.DebugSpew("Collecting information for Connectoid '{0}'", new object[]
					{
						text
					});
				}
				if (!this._oConnectoids.ContainsKey(text))
				{
					try
					{
						WinINETProxyInfo winINETProxyInfo = WinINETProxyInfo.CreateFromNamedConnection(text);
						if (winINETProxyInfo == null)
						{
							FiddlerApplication.Log.LogFormat("!WARNING: Failed to get proxy information for Connection '{0}'.", new object[]
							{
								text
							});
						}
						else
						{
							WinINETConnectoid winINETConnectoid = new WinINETConnectoid();
							winINETConnectoid.sConnectionName = text;
							if (!string.IsNullOrEmpty(winINETProxyInfo.sHttpProxy) && winINETProxyInfo.sHttpProxy.Contains(CONFIG.sFiddlerListenHostPort))
							{
								FiddlerApplication.Log.LogString("When connecting, upstream proxy settings were already pointed at Fiddler. Clearing upstream proxy.");
								winINETProxyInfo.sHttpProxy = (winINETProxyInfo.sHttpsProxy = (winINETProxyInfo.sFtpProxy = null));
								winINETProxyInfo.bUseManualProxies = false;
								winINETProxyInfo.bAllowDirect = true;
							}
							if (!string.IsNullOrEmpty(winINETProxyInfo.sPACScriptLocation) && (winINETProxyInfo.sPACScriptLocation == "file://" + CONFIG.GetPath("Pac") || winINETProxyInfo.sPACScriptLocation == "http://" + CONFIG.sFiddlerListenHostPort + "/proxy.pac"))
							{
								FiddlerApplication.Log.LogString("When connecting, upstream proxy script was already pointed at Fiddler. Clearing upstream proxy.");
								winINETProxyInfo.sPACScriptLocation = null;
							}
							winINETConnectoid.oOriginalProxyInfo = winINETProxyInfo;
							this._oConnectoids.Add(text, winINETConnectoid);
						}
					}
					catch (Exception eX)
					{
						FiddlerApplication.Log.LogFormat("!WARNING: Failed to get proxy information for Connection '{0}' due to {1}", new object[]
						{
							text,
							Utilities.DescribeException(eX)
						});
					}
				}
			}
		}
		internal WinINETProxyInfo GetDefaultConnectionGatewayInfo()
		{
			string text = CONFIG.sHookConnectionNamed;
			if (string.IsNullOrEmpty(text))
			{
				text = "DefaultLAN";
			}
			if (!this._oConnectoids.ContainsKey(text))
			{
				text = "DefaultLAN";
				if (!this._oConnectoids.ContainsKey(text))
				{
					FiddlerApplication.Log.LogString("!WARNING: The DefaultLAN Gateway information could not be obtained.");
					return WinINETProxyInfo.CreateFromStrings(string.Empty, string.Empty);
				}
			}
			return this._oConnectoids[text].oOriginalProxyInfo;
		}
		internal void MarkDefaultLANAsUnhooked()
		{
			foreach (WinINETConnectoid current in this._oConnectoids.Values)
			{
				if (current.sConnectionName == "DefaultLAN")
				{
					current.bIsHooked = false;
					break;
				}
			}
		}
		internal bool MarkUnhookedConnections(string sLookFor)
		{
			if (CONFIG.bIsViewOnly)
			{
				return false;
			}
			bool result = false;
			foreach (WinINETConnectoid current in this._oConnectoids.Values)
			{
				if (current.bIsHooked)
				{
					WinINETProxyInfo winINETProxyInfo = WinINETProxyInfo.CreateFromNamedConnection(current.sConnectionName);
					bool flag = false;
					if (!winINETProxyInfo.bUseManualProxies)
					{
						flag = true;
					}
					else
					{
						string text = winINETProxyInfo.CalculateProxyString();
						if (text != sLookFor && !text.Contains("http=" + sLookFor))
						{
							flag = true;
						}
					}
					if (flag)
					{
						current.bIsHooked = false;
						result = true;
					}
				}
			}
			return result;
		}
		internal bool HookConnections(WinINETProxyInfo oNewInfo)
		{
			if (CONFIG.bIsViewOnly)
			{
				return false;
			}
			bool result = false;
			foreach (WinINETConnectoid current in this._oConnectoids.Values)
			{
				if ((CONFIG.bHookAllConnections || current.sConnectionName == CONFIG.sHookConnectionNamed) && oNewInfo.SetToWinINET(current.sConnectionName))
				{
					result = true;
					current.bIsHooked = true;
				}
			}
			return result;
		}
		internal bool UnhookAllConnections()
		{
			if (CONFIG.bIsViewOnly)
			{
				return true;
			}
			bool result = true;
			foreach (WinINETConnectoid current in this._oConnectoids.Values)
			{
				if (current.bIsHooked)
				{
					if (current.oOriginalProxyInfo.SetToWinINET(current.sConnectionName))
					{
						current.bIsHooked = false;
					}
					else
					{
						result = true;
					}
				}
			}
			return result;
		}
	}
}
