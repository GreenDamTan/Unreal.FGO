using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
namespace Fiddler
{
	public class WinINETProxyInfo
	{
		private struct INTERNET_PER_CONN_OPTION_LIST
		{
			public int Size;
			public string Connection;
			public int OptionCount;
			public int OptionError;
			public IntPtr pOptions;
		}
		[StructLayout(LayoutKind.Sequential)]
		private class INTERNET_PER_CONN_OPTION
		{
			public int dwOption;
			public WinINETProxyInfo.OptionUnion Value;
		}
		[StructLayout(LayoutKind.Explicit)]
		private struct OptionUnion
		{
			[FieldOffset(0)]
			public int dwValue;
			[FieldOffset(0)]
			public IntPtr pszValue;
			[FieldOffset(0)]
			public System.Runtime.InteropServices.ComTypes.FILETIME ftValue;
		}
		private const int PROXY_TYPE_DIRECT = 1;
		private const int PROXY_TYPE_PROXY = 2;
		private const int PROXY_TYPE_AUTO_PROXY_URL = 4;
		private const int PROXY_TYPE_AUTO_DETECT = 8;
		private const int INTERNET_OPTION_PER_CONNECTION_OPTION = 75;
		private const int INTERNET_OPTION_PROXY_SETTINGS_CHANGED = 95;
		private const int INTERNET_PER_CONN_FLAGS = 1;
		private const int INTERNET_PER_CONN_PROXY_SERVER = 2;
		private const int INTERNET_PER_CONN_PROXY_BYPASS = 3;
		private const int INTERNET_PER_CONN_AUTOCONFIG_URL = 4;
		private const int INTERNET_PER_CONN_AUTODISCOVERY_FLAGS = 5;
		private const int INTERNET_PER_CONN_AUTOCONFIG_SECONDARY_URL = 6;
		private const int INTERNET_PER_CONN_AUTOCONFIG_RELOAD_DELAY_MINS = 7;
		private const int INTERNET_PER_CONN_AUTOCONFIG_LAST_DETECT_TIME = 8;
		private const int INTERNET_PER_CONN_AUTOCONFIG_LAST_DETECT_URL = 9;
		private const int INTERNET_PER_CONN_FLAGS_UI = 10;
		private const int AUTO_PROXY_FLAG_USER_SET = 1;
		private const int ERROR_INVALID_PARAMETER = 87;
		private string _sHostsThatBypass;
		private bool _bDirect;
		private bool _bAutoDetect;
		private bool _bAutoDetectWasUserSet;
		private bool _bUseConfigScript;
		private bool _bProxiesSpecified;
		private string _sScriptURL;
		private string _sFtpProxy;
		private string _sSocksProxy;
		private string _sHttpProxy;
		private string _sHttpsProxy;
		public string sHostsThatBypass
		{
			get
			{
				return this._sHostsThatBypass;
			}
			set
			{
				if (!string.IsNullOrEmpty(value))
				{
					this._sHostsThatBypass = value.Trim().TrimEnd(new char[]
					{
						';'
					});
					return;
				}
				this._sHostsThatBypass = value;
			}
		}
		public bool bUseManualProxies
		{
			get
			{
				return this._bProxiesSpecified;
			}
			set
			{
				this._bProxiesSpecified = value;
			}
		}
		public bool bAllowDirect
		{
			get
			{
				return this._bDirect;
			}
			set
			{
				this._bDirect = value;
			}
		}
		public bool bBypassIntranetHosts
		{
			get
			{
				return !string.IsNullOrEmpty(this._sHostsThatBypass) && this._sHostsThatBypass.OICContains("<local>");
			}
		}
		public string sHttpProxy
		{
			get
			{
				return this._sHttpProxy;
			}
			set
			{
				this._sHttpProxy = value;
			}
		}
		public string sHttpsProxy
		{
			get
			{
				return this._sHttpsProxy;
			}
			set
			{
				this._sHttpsProxy = value;
			}
		}
		public string sFtpProxy
		{
			get
			{
				return this._sFtpProxy;
			}
			set
			{
				this._sFtpProxy = value;
			}
		}
		public string sSocksProxy
		{
			get
			{
				return this._sSocksProxy;
			}
			set
			{
				this._sSocksProxy = value;
			}
		}
		public bool bAutoDetect
		{
			get
			{
				return this._bAutoDetect;
			}
			set
			{
				this._bAutoDetect = value;
			}
		}
		public string sPACScriptLocation
		{
			get
			{
				if (this._bUseConfigScript)
				{
					return this._sScriptURL;
				}
				return null;
			}
			set
			{
				if (string.IsNullOrEmpty(value))
				{
					this._bUseConfigScript = false;
					this._sScriptURL = null;
					return;
				}
				this._bUseConfigScript = true;
				this._sScriptURL = value;
			}
		}
		internal WinINETProxyInfo()
		{
		}
		public static WinINETProxyInfo CreateFromNamedConnection(string sConnectionName)
		{
			WinINETProxyInfo winINETProxyInfo = new WinINETProxyInfo();
			if (winINETProxyInfo.GetFromWinINET(sConnectionName))
			{
				return winINETProxyInfo;
			}
			return null;
		}
		public static WinINETProxyInfo CreateFromStrings(string sProxyString, string sBypassList)
		{
			WinINETProxyInfo winINETProxyInfo = new WinINETProxyInfo();
			WinINETProxyInfo arg_0F_0 = winINETProxyInfo;
			winINETProxyInfo._bAutoDetect = false;
			arg_0F_0._bUseConfigScript = false;
			winINETProxyInfo._bDirect = true;
			if (!string.IsNullOrEmpty(sProxyString))
			{
				winINETProxyInfo._bProxiesSpecified = true;
				winINETProxyInfo.SetManualProxies(sProxyString);
			}
			winINETProxyInfo.sHostsThatBypass = sBypassList;
			return winINETProxyInfo;
		}
		public override string ToString()
		{
			StringBuilder stringBuilder = new StringBuilder(512);
			stringBuilder.AppendFormat("HTTP=\t{0}\n", this._sHttpProxy);
			stringBuilder.AppendFormat("HTTPS=\t{0}\n", this._sHttpsProxy);
			stringBuilder.AppendFormat("FTP=\t{0}\n", this._sFtpProxy);
			stringBuilder.AppendFormat("SOCKS=\t{0}\n", this._sSocksProxy);
			stringBuilder.AppendFormat("Script=\t{0}\n", this._sScriptURL);
			stringBuilder.AppendFormat("Bypass=\t{0}\n", this._sHostsThatBypass);
			int num = 0;
			if (this._bDirect)
			{
				num |= 1;
			}
			if (this._bAutoDetect)
			{
				num |= 8;
			}
			if (this._bUseConfigScript)
			{
				num |= 4;
			}
			if (this._bProxiesSpecified)
			{
				num |= 2;
			}
			stringBuilder.AppendFormat("ProxyType:\t{0}\n", num.ToString());
			if (this._bAutoDetectWasUserSet)
			{
				stringBuilder.AppendLine("AutoProxyDetection was user-set.");
			}
			return stringBuilder.ToString();
		}
		private void Clear()
		{
			this._bAutoDetect = false;
			this._bDirect = false;
			this._bProxiesSpecified = false;
			this._bUseConfigScript = false;
			this._sHttpProxy = null;
			this._sHttpsProxy = null;
			this._sSocksProxy = null;
			this._sFtpProxy = null;
			this._sScriptURL = null;
			this._sHostsThatBypass = null;
		}
		internal string CalculateProxyString()
		{
			if (!(this._sHttpProxy == this._sHttpsProxy) || !(this._sHttpProxy == this._sFtpProxy) || !(this._sHttpProxy == this._sSocksProxy))
			{
				string text = null;
				if (!string.IsNullOrEmpty(this._sHttpProxy))
				{
					text = "http=" + this._sHttpProxy + ";";
				}
				if (!string.IsNullOrEmpty(this._sHttpsProxy))
				{
					text = text + "https=" + this._sHttpsProxy + ";";
				}
				if (!string.IsNullOrEmpty(this._sFtpProxy))
				{
					text = text + "ftp=" + this._sFtpProxy + ";";
				}
				if (!string.IsNullOrEmpty(this._sSocksProxy))
				{
					text = text + "socks=" + this._sSocksProxy + ";";
				}
				return text;
			}
			if (string.IsNullOrEmpty(this._sHttpProxy))
			{
				return null;
			}
			return this._sHttpProxy;
		}
		private bool SetManualProxies(string sProxyString)
		{
			this._sFtpProxy = (this._sSocksProxy = (this._sHttpProxy = (this._sHttpsProxy = null)));
			sProxyString = sProxyString.Trim();
			if (string.IsNullOrEmpty(sProxyString))
			{
				return true;
			}
			sProxyString = sProxyString.ToLower();
			if (!sProxyString.Contains("="))
			{
				sProxyString = Utilities.TrimBeforeLast(sProxyString, '/');
				this.sFtpProxy = (this.sHttpProxy = (this.sHttpsProxy = (this.sSocksProxy = sProxyString)));
				return true;
			}
			string[] array = sProxyString.Split(new char[]
			{
				';'
			});
			for (int i = 0; i < array.Length; i++)
			{
				string text = array[i];
				if (text.IndexOf('=') >= 3)
				{
					string text2 = text.Substring(text.IndexOf('=') + 1).Trim();
					if (text2.IndexOf('/') > 0)
					{
						text2 = Utilities.TrimBeforeLast(text2, '/');
					}
					if (text.OICStartsWith("http="))
					{
						this._sHttpProxy = text2;
					}
					else
					{
						if (text.OICStartsWith("https="))
						{
							this._sHttpsProxy = text2;
						}
						else
						{
							if (text.OICStartsWith("ftp="))
							{
								this._sFtpProxy = text2;
							}
							else
							{
								if (text.StartsWith("socks="))
								{
									this._sSocksProxy = text2;
								}
							}
						}
					}
				}
			}
			return true;
		}
		public bool GetFromWinINET(string sConnectionName)
		{
			this.Clear();
			bool result;
			try
			{
				WinINETProxyInfo.INTERNET_PER_CONN_OPTION_LIST iNTERNET_PER_CONN_OPTION_LIST = default(WinINETProxyInfo.INTERNET_PER_CONN_OPTION_LIST);
				WinINETProxyInfo.INTERNET_PER_CONN_OPTION[] array = new WinINETProxyInfo.INTERNET_PER_CONN_OPTION[5];
				if (sConnectionName == "DefaultLAN")
				{
					sConnectionName = null;
				}
				iNTERNET_PER_CONN_OPTION_LIST.Connection = sConnectionName;
				iNTERNET_PER_CONN_OPTION_LIST.OptionCount = array.Length;
				iNTERNET_PER_CONN_OPTION_LIST.OptionError = 0;
				array[0] = new WinINETProxyInfo.INTERNET_PER_CONN_OPTION();
				array[0].dwOption = 1;
				array[1] = new WinINETProxyInfo.INTERNET_PER_CONN_OPTION();
				array[1].dwOption = 2;
				array[2] = new WinINETProxyInfo.INTERNET_PER_CONN_OPTION();
				array[2].dwOption = 3;
				array[3] = new WinINETProxyInfo.INTERNET_PER_CONN_OPTION();
				array[3].dwOption = 4;
				array[4] = new WinINETProxyInfo.INTERNET_PER_CONN_OPTION();
				array[4].dwOption = 10;
				int num = 0;
				for (int i = 0; i < array.Length; i++)
				{
					num += Marshal.SizeOf(array[i]);
				}
				IntPtr intPtr = Marshal.AllocCoTaskMem(num);
				IntPtr intPtr2 = intPtr;
				for (int j = 0; j < array.Length; j++)
				{
					Marshal.StructureToPtr(array[j], intPtr2, false);
					intPtr2 = (IntPtr)((long)intPtr2 + (long)Marshal.SizeOf(array[j]));
				}
				iNTERNET_PER_CONN_OPTION_LIST.pOptions = intPtr;
				iNTERNET_PER_CONN_OPTION_LIST.Size = Marshal.SizeOf(iNTERNET_PER_CONN_OPTION_LIST);
				int size = iNTERNET_PER_CONN_OPTION_LIST.Size;
				bool flag = WinINETProxyInfo.InternetQueryOption(IntPtr.Zero, 75, ref iNTERNET_PER_CONN_OPTION_LIST, ref size);
				int lastWin32Error = Marshal.GetLastWin32Error();
				if (!flag)
				{
					if (87 == lastWin32Error)
					{
						array[4].dwOption = 5;
						intPtr2 = intPtr;
						for (int k = 0; k < array.Length; k++)
						{
							Marshal.StructureToPtr(array[k], intPtr2, false);
							intPtr2 = (IntPtr)((long)intPtr2 + (long)Marshal.SizeOf(array[k]));
						}
						iNTERNET_PER_CONN_OPTION_LIST.pOptions = intPtr;
						iNTERNET_PER_CONN_OPTION_LIST.Size = Marshal.SizeOf(iNTERNET_PER_CONN_OPTION_LIST);
						size = iNTERNET_PER_CONN_OPTION_LIST.Size;
						flag = WinINETProxyInfo.InternetQueryOption(IntPtr.Zero, 75, ref iNTERNET_PER_CONN_OPTION_LIST, ref size);
						lastWin32Error = Marshal.GetLastWin32Error();
					}
					if (!flag)
					{
						FiddlerApplication.Log.LogFormat("Fiddler was unable to get information about the proxy for '{0}' [0x{1}].\n", new object[]
						{
							sConnectionName ?? "DefaultLAN",
							lastWin32Error.ToString("x")
						});
					}
				}
				if (flag)
				{
					intPtr2 = intPtr;
					for (int l = 0; l < array.Length; l++)
					{
						Marshal.PtrToStructure(intPtr2, array[l]);
						intPtr2 = (IntPtr)((long)intPtr2 + (long)Marshal.SizeOf(array[l]));
					}
					this._bDirect = (1 == (array[0].Value.dwValue & 1));
					this._bUseConfigScript = (4 == (array[0].Value.dwValue & 4));
					this._bAutoDetect = (8 == (array[0].Value.dwValue & 8));
					this._bProxiesSpecified = (2 == (array[0].Value.dwValue & 2));
					if (array[4].dwOption == 10)
					{
						this._bAutoDetectWasUserSet = (8 == (array[4].Value.dwValue & 8));
					}
					else
					{
						this._bAutoDetectWasUserSet = (this._bAutoDetect && 1 == (array[4].Value.dwValue & 1));
					}
					this._sScriptURL = Marshal.PtrToStringAnsi(array[3].Value.pszValue);
					Utilities.GlobalFree(array[3].Value.pszValue);
					if (array[1].Value.pszValue != IntPtr.Zero)
					{
						string manualProxies = Marshal.PtrToStringAnsi(array[1].Value.pszValue);
						Utilities.GlobalFree(array[1].Value.pszValue);
						this.SetManualProxies(manualProxies);
					}
					if (array[2].Value.pszValue != IntPtr.Zero)
					{
						this._sHostsThatBypass = Marshal.PtrToStringAnsi(array[2].Value.pszValue);
						Utilities.GlobalFree(array[2].Value.pszValue);
					}
				}
				Marshal.FreeCoTaskMem(intPtr);
				result = flag;
			}
			catch (Exception eX)
			{
				FiddlerApplication.ReportException(eX, "Unable to get proxy information for " + (sConnectionName ?? "DefaultLAN"));
				result = false;
			}
			return result;
		}
		internal bool SetToWinINET(string sConnectionName)
		{
			bool result;
			try
			{
				WinINETProxyInfo.INTERNET_PER_CONN_OPTION_LIST iNTERNET_PER_CONN_OPTION_LIST = default(WinINETProxyInfo.INTERNET_PER_CONN_OPTION_LIST);
				WinINETProxyInfo.INTERNET_PER_CONN_OPTION[] array = new WinINETProxyInfo.INTERNET_PER_CONN_OPTION[5];
				if (sConnectionName == "DefaultLAN")
				{
					sConnectionName = null;
				}
				iNTERNET_PER_CONN_OPTION_LIST.Connection = sConnectionName;
				iNTERNET_PER_CONN_OPTION_LIST.OptionCount = array.Length;
				iNTERNET_PER_CONN_OPTION_LIST.OptionError = 0;
				int num = 0;
				if (this._bDirect)
				{
					num |= 1;
				}
				if (this._bAutoDetect)
				{
					num |= 8;
				}
				if (this._bAutoDetectWasUserSet)
				{
					num |= 8;
				}
				if (this._bUseConfigScript)
				{
					num |= 4;
				}
				if (this._bProxiesSpecified)
				{
					num |= 2;
				}
				array[0] = new WinINETProxyInfo.INTERNET_PER_CONN_OPTION();
				array[0].dwOption = 1;
				array[0].Value.dwValue = num;
				array[1] = new WinINETProxyInfo.INTERNET_PER_CONN_OPTION();
				array[1].dwOption = 2;
				array[1].Value.pszValue = Marshal.StringToHGlobalAnsi(this.CalculateProxyString());
				array[2] = new WinINETProxyInfo.INTERNET_PER_CONN_OPTION();
				array[2].dwOption = 3;
				array[2].Value.pszValue = Marshal.StringToHGlobalAnsi(this._sHostsThatBypass);
				array[3] = new WinINETProxyInfo.INTERNET_PER_CONN_OPTION();
				array[3].dwOption = 4;
				array[3].Value.pszValue = Marshal.StringToHGlobalAnsi(this._sScriptURL);
				array[4] = new WinINETProxyInfo.INTERNET_PER_CONN_OPTION();
				array[4].dwOption = 5;
				array[4].Value.dwValue = 0;
				if (this._bAutoDetectWasUserSet)
				{
					array[4].Value.dwValue = 1;
				}
				int num2 = 0;
				for (int i = 0; i < array.Length; i++)
				{
					num2 += Marshal.SizeOf(array[i]);
				}
				IntPtr intPtr = Marshal.AllocCoTaskMem(num2);
				IntPtr intPtr2 = intPtr;
				for (int j = 0; j < array.Length; j++)
				{
					Marshal.StructureToPtr(array[j], intPtr2, false);
					intPtr2 = (IntPtr)((long)intPtr2 + (long)Marshal.SizeOf(array[j]));
				}
				iNTERNET_PER_CONN_OPTION_LIST.pOptions = intPtr;
				iNTERNET_PER_CONN_OPTION_LIST.Size = Marshal.SizeOf(iNTERNET_PER_CONN_OPTION_LIST);
				int size = iNTERNET_PER_CONN_OPTION_LIST.Size;
				bool flag = WinINETProxyInfo.InternetSetOption_1(IntPtr.Zero, 75, ref iNTERNET_PER_CONN_OPTION_LIST, size);
				int lastWin32Error = Marshal.GetLastWin32Error();
				if (flag)
				{
					WinINETProxyInfo.InternetSetOption(IntPtr.Zero, 95, IntPtr.Zero, 0);
				}
				else
				{
					Trace.WriteLine("[Fiddler] SetProxy failed. WinINET Error #" + lastWin32Error.ToString("x"));
				}
				Marshal.FreeHGlobal(array[0].Value.pszValue);
				Marshal.FreeHGlobal(array[1].Value.pszValue);
				Marshal.FreeHGlobal(array[2].Value.pszValue);
				Marshal.FreeCoTaskMem(intPtr);
				result = flag;
			}
			catch (Exception ex)
			{
				Trace.WriteLine("[Fiddler] SetProxy failed. " + ex.Message);
				result = false;
			}
			return result;
		}
		[DllImport("wininet.dll", CharSet = CharSet.Ansi, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool InternetSetOption(IntPtr hInternet, int Option, [In] IntPtr buffer, int BufferLength);
		[DllImport("wininet.dll", CharSet = CharSet.Ansi, EntryPoint = "InternetSetOption", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool InternetSetOption_1(IntPtr hInternet, int Option, ref WinINETProxyInfo.INTERNET_PER_CONN_OPTION_LIST OptionList, int size);
		[DllImport("wininet.dll", CharSet = CharSet.Ansi, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool InternetQueryOption(IntPtr hInternet, int Option, ref WinINETProxyInfo.INTERNET_PER_CONN_OPTION_LIST OptionList, ref int size);
	}
}
