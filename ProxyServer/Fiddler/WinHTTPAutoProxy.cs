using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
namespace Fiddler
{
	internal class WinHTTPAutoProxy : IDisposable
	{
		internal int iAutoProxySuccessCount;
		private readonly string _sPACScriptLocation;
		private readonly bool _bUseAutoDiscovery = true;
		private readonly IntPtr _hSession;
		private WinHTTPNative.WINHTTP_AUTOPROXY_OPTIONS _oAPO;
		private static string GetPACFileText(string sURI)
		{
			string result;
			try
			{
				Uri uri = new Uri(sURI);
				if (!uri.IsFile)
				{
					result = null;
				}
				else
				{
					string localPath = uri.LocalPath;
					if (!File.Exists(localPath))
					{
						FiddlerApplication.Log.LogFormat("! Failed to find the configured PAC script '{0}'", new object[]
						{
							localPath
						});
						result = string.Empty;
					}
					else
					{
						result = File.ReadAllText(localPath);
					}
				}
			}
			catch (Exception ex)
			{
				FiddlerApplication.Log.LogFormat("! Failed to host the configured PAC script {0}", new object[]
				{
					ex
				});
				result = null;
			}
			return result;
		}
		public WinHTTPAutoProxy(bool bAutoDiscover, string sAutoConfigUrl)
		{
			this._bUseAutoDiscovery = bAutoDiscover;
			if (!string.IsNullOrEmpty(sAutoConfigUrl))
			{
				if (sAutoConfigUrl.OICStartsWith("file:") || sAutoConfigUrl.StartsWith("\\\\") || (sAutoConfigUrl.Length > 2 && sAutoConfigUrl[1] == ':'))
				{
					Proxy.sUpstreamPACScript = WinHTTPAutoProxy.GetPACFileText(sAutoConfigUrl);
					if (!string.IsNullOrEmpty(Proxy.sUpstreamPACScript))
					{
						FiddlerApplication.Log.LogFormat("!WARNING: System proxy was configured to use a file-protocol sourced script ({0}). Proxy scripts delivered by the file protocol are not supported by many clients. Please see http://blogs.msdn.com/b/ieinternals/archive/2013/10/11/web-proxy-configuration-and-ie11-changes.aspx for more information.", new object[]
						{
							sAutoConfigUrl
						});
						sAutoConfigUrl = "http://" + CONFIG.sFiddlerListenHostPort + "/UpstreamProxy.pac";
					}
				}
				this._sPACScriptLocation = sAutoConfigUrl;
			}
			this._oAPO = WinHTTPAutoProxy.GetAutoProxyOptionsStruct(this._sPACScriptLocation, this._bUseAutoDiscovery);
			this._hSession = WinHTTPNative.WinHttpOpen("Fiddler", 1, IntPtr.Zero, IntPtr.Zero, 0);
		}
		public override string ToString()
		{
			string text = null;
			if (this.iAutoProxySuccessCount < 0)
			{
				text = "\tOffline/disabled\n";
			}
			else
			{
				if (this._bUseAutoDiscovery)
				{
					string text2 = WinHTTPAutoProxy.GetWPADUrl();
					if (string.IsNullOrEmpty(text2))
					{
						text2 = "Not detected";
					}
					text = string.Format("\tWPAD: {0}\n", text2);
				}
				if (this._sPACScriptLocation != null)
				{
					text = text + "\tConfig script: " + this._sPACScriptLocation + "\n";
				}
			}
			return text ?? "\tDisabled";
		}
		private static string GetWPADUrl()
		{
			IntPtr intPtr;
			bool flag;
			if (!(flag = WinHTTPNative.WinHttpDetectAutoProxyConfigUrl(3, out intPtr)))
			{
				Marshal.GetLastWin32Error();
			}
			string result;
			if (flag && IntPtr.Zero != intPtr)
			{
				result = Marshal.PtrToStringUni(intPtr);
			}
			else
			{
				result = string.Empty;
			}
			Utilities.GlobalFreeIfNonZero(intPtr);
			return result;
		}
		private static WinHTTPNative.WINHTTP_AUTOPROXY_OPTIONS GetAutoProxyOptionsStruct(string sPAC, bool bUseAutoDetect)
		{
			WinHTTPNative.WINHTTP_AUTOPROXY_OPTIONS result = default(WinHTTPNative.WINHTTP_AUTOPROXY_OPTIONS);
			if (FiddlerApplication.Prefs.GetBoolPref("fiddler.network.gateway.DetermineInProcess", false))
			{
				result.dwFlags = 65536;
			}
			else
			{
				result.dwFlags = 0;
			}
			if (bUseAutoDetect)
			{
				result.dwFlags |= 1;
				result.dwAutoDetectFlags = 3;
			}
			if (sPAC != null)
			{
				result.dwFlags |= 2;
				result.lpszAutoConfigUrl = sPAC;
			}
			result.fAutoLoginIfChallenged = CONFIG.bAutoProxyLogon;
			return result;
		}
		public bool GetAutoProxyForUrl(string sUrl, out IPEndPoint ipepResult)
		{
			int num = 0;
			WinHTTPNative.WINHTTP_PROXY_INFO wINHTTP_PROXY_INFO;
			bool flag;
			if (!(flag = WinHTTPNative.WinHttpGetProxyForUrl(this._hSession, sUrl, ref this._oAPO, out wINHTTP_PROXY_INFO)))
			{
				num = Marshal.GetLastWin32Error();
			}
			if (flag)
			{
				if (IntPtr.Zero != wINHTTP_PROXY_INFO.lpszProxy)
				{
					string text = Marshal.PtrToStringUni(wINHTTP_PROXY_INFO.lpszProxy);
					ipepResult = Utilities.IPEndPointFromHostPortString(text);
					if (ipepResult == null)
					{
						FiddlerApplication.Log.LogFormat("Proxy Configuration Script specified an unreachable proxy: {0} for URL: {1}", new object[]
						{
							text,
							sUrl
						});
					}
				}
				else
				{
					ipepResult = null;
				}
				Utilities.GlobalFreeIfNonZero(wINHTTP_PROXY_INFO.lpszProxy);
				Utilities.GlobalFreeIfNonZero(wINHTTP_PROXY_INFO.lpszProxyBypass);
				return true;
			}
			int num2 = num;
			if (num2 <= 12015)
			{
				if (num2 == 12006)
				{
					FiddlerApplication._Log.LogString("Fiddler.Network.ProxyPAC> PAC Script download failure; Fiddler only supports HTTP/HTTPS for PAC script URLs.");
					goto IL_13D;
				}
				if (num2 == 12015)
				{
					FiddlerApplication._Log.LogString("Fiddler.Network.ProxyPAC> PAC Script download failure; you must set the AutoProxyLogon registry key to TRUE.");
					goto IL_13D;
				}
			}
			else
			{
				switch (num2)
				{
				case 12166:
					FiddlerApplication._Log.LogString("Fiddler.Network.ProxyPAC> PAC Script contents were not valid.");
					goto IL_13D;
				case 12167:
					FiddlerApplication._Log.LogString("Fiddler.Network.ProxyPAC> PAC Script download failed.");
					goto IL_13D;
				default:
					if (num2 == 12180)
					{
						FiddlerApplication._Log.LogString("Fiddler.Network.AutoProxy> AutoProxy Detection failed.");
						goto IL_13D;
					}
					break;
				}
			}
			FiddlerApplication._Log.LogString("Fiddler.Network.ProxyPAC> Proxy determination failed with error code: " + num);
			IL_13D:
			Utilities.GlobalFreeIfNonZero(wINHTTP_PROXY_INFO.lpszProxy);
			Utilities.GlobalFreeIfNonZero(wINHTTP_PROXY_INFO.lpszProxyBypass);
			ipepResult = null;
			return false;
		}
		public void Dispose()
		{
			WinHTTPNative.WinHttpCloseHandle(this._hSession);
		}
	}
}
