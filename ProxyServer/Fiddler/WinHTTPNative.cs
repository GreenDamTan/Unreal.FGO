using System;
using System.Runtime.InteropServices;
namespace Fiddler
{
	internal class WinHTTPNative
	{
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		internal struct WINHTTP_AUTOPROXY_OPTIONS
		{
			[MarshalAs(UnmanagedType.U4)]
			public int dwFlags;
			[MarshalAs(UnmanagedType.U4)]
			public int dwAutoDetectFlags;
			[MarshalAs(UnmanagedType.LPWStr)]
			public string lpszAutoConfigUrl;
			public IntPtr lpvReserved;
			[MarshalAs(UnmanagedType.U4)]
			public int dwReserved;
			[MarshalAs(UnmanagedType.Bool)]
			public bool fAutoLoginIfChallenged;
		}
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		internal struct WINHTTP_PROXY_INFO
		{
			[MarshalAs(UnmanagedType.U4)]
			public int dwAccessType;
			public IntPtr lpszProxy;
			public IntPtr lpszProxyBypass;
		}
		internal const int WINHTTP_ACCESS_TYPE_DEFAULT_PROXY = 0;
		internal const int WINHTTP_ACCESS_TYPE_NO_PROXY = 1;
		internal const int WINHTTP_ACCESS_TYPE_NAMED_PROXY = 3;
		internal const int WINHTTP_AUTOPROXY_AUTO_DETECT = 1;
		internal const int WINHTTP_AUTOPROXY_CONFIG_URL = 2;
		internal const int WINHTTP_AUTOPROXY_RUN_INPROCESS = 65536;
		internal const int WINHTTP_AUTOPROXY_RUN_OUTPROCESS_ONLY = 131072;
		internal const int WINHTTP_AUTO_DETECT_TYPE_DHCP = 1;
		internal const int WINHTTP_AUTO_DETECT_TYPE_DNS_A = 2;
		internal const int ERROR_WINHTTP_LOGIN_FAILURE = 12015;
		internal const int ERROR_WINHTTP_UNABLE_TO_DOWNLOAD_SCRIPT = 12167;
		internal const int ERROR_WINHTTP_UNRECOGNIZED_SCHEME = 12006;
		internal const int ERROR_WINHTTP_AUTODETECTION_FAILED = 12180;
		internal const int ERROR_WINHTTP_BAD_AUTO_PROXY_SCRIPT = 12166;
		[DllImport("winhttp.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool WinHttpGetProxyForUrl(IntPtr hSession, [MarshalAs(UnmanagedType.LPWStr)] string lpcwszUrl, [In] ref WinHTTPNative.WINHTTP_AUTOPROXY_OPTIONS pAutoProxyOptions, out WinHTTPNative.WINHTTP_PROXY_INFO pProxyInfo);
		[DllImport("winhttp.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		internal static extern IntPtr WinHttpOpen([MarshalAs(UnmanagedType.LPWStr)] [In] string pwszUserAgent, [In] int dwAccessType, [In] IntPtr pwszProxyName, [In] IntPtr pwszProxyBypass, [In] int dwFlags);
		[DllImport("winhttp.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool WinHttpDetectAutoProxyConfigUrl([MarshalAs(UnmanagedType.U4)] int dwAutoDetectFlags, out IntPtr ppwszAutoConfigUrl);
		[DllImport("winhttp.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool WinHttpCloseHandle([In] IntPtr hInternet);
	}
}
