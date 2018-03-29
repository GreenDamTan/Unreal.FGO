using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
namespace Fiddler
{
	public class WinINETCache
	{
		private enum WININETCACHEENTRYTYPE
		{
			None,
			NORMAL_CACHE_ENTRY,
			STICKY_CACHE_ENTRY = 4,
			EDITED_CACHE_ENTRY = 8,
			TRACK_OFFLINE_CACHE_ENTRY = 16,
			TRACK_ONLINE_CACHE_ENTRY = 32,
			SPARSE_CACHE_ENTRY = 65536,
			COOKIE_CACHE_ENTRY = 1048576,
			URLHISTORY_CACHE_ENTRY = 2097152,
			ALL = 3211325
		}
		[StructLayout(LayoutKind.Sequential)]
		private class INTERNET_CACHE_ENTRY_INFOA
		{
			public uint dwStructureSize;
			public IntPtr lpszSourceUrlName;
			public IntPtr lpszLocalFileName;
			public WinINETCache.WININETCACHEENTRYTYPE CacheEntryType;
			public uint dwUseCount;
			public uint dwHitRate;
			public uint dwSizeLow;
			public uint dwSizeHigh;
			public System.Runtime.InteropServices.ComTypes.FILETIME LastModifiedTime;
			public System.Runtime.InteropServices.ComTypes.FILETIME ExpireTime;
			public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
			public System.Runtime.InteropServices.ComTypes.FILETIME LastSyncTime;
			public IntPtr lpHeaderInfo;
			public uint dwHeaderInfoSize;
			public IntPtr lpszFileExtension;
			public WinINETCache.WININETCACHEENTRYINFOUNION _Union;
		}
		[StructLayout(LayoutKind.Explicit)]
		private struct WININETCACHEENTRYINFOUNION
		{
			[FieldOffset(0)]
			public uint dwReserved;
			[FieldOffset(0)]
			public uint dwExemptDelta;
		}
		private const int CACHEGROUP_SEARCH_ALL = 0;
		private const int CACHEGROUP_FLAG_FLUSHURL_ONDELETE = 2;
		private const int ERROR_FILE_NOT_FOUND = 2;
		private const int ERROR_NO_MORE_ITEMS = 259;
		private const int ERROR_INSUFFICENT_BUFFER = 122;
		internal static string GetCacheItemInfo(string sURL)
		{
			int num = 0;
			IntPtr intPtr = IntPtr.Zero;
			bool urlCacheEntryInfoA = WinINETCache.GetUrlCacheEntryInfoA(sURL, intPtr, ref num);
			int lastWin32Error = Marshal.GetLastWin32Error();
			if (!urlCacheEntryInfoA)
			{
				if (lastWin32Error == 122)
				{
					int num2 = num;
					intPtr = Marshal.AllocHGlobal(num2);
					urlCacheEntryInfoA = WinINETCache.GetUrlCacheEntryInfoA(sURL, intPtr, ref num);
					lastWin32Error = Marshal.GetLastWin32Error();
					if (!urlCacheEntryInfoA)
					{
						Marshal.FreeHGlobal(intPtr);
						return "GetUrlCacheEntryInfoA with buffer failed. 2=filenotfound 122=insufficient buffer, 259=nomoreitems. Last error: " + lastWin32Error.ToString() + "\n";
					}
					WinINETCache.INTERNET_CACHE_ENTRY_INFOA iNTERNET_CACHE_ENTRY_INFOA = (WinINETCache.INTERNET_CACHE_ENTRY_INFOA)Marshal.PtrToStructure(intPtr, typeof(WinINETCache.INTERNET_CACHE_ENTRY_INFOA));
					num = num2;
					long fileTime = (long)iNTERNET_CACHE_ENTRY_INFOA.LastModifiedTime.dwHighDateTime << 32 | (long)((ulong)iNTERNET_CACHE_ENTRY_INFOA.LastModifiedTime.dwLowDateTime);
					long fileTime2 = (long)iNTERNET_CACHE_ENTRY_INFOA.LastAccessTime.dwHighDateTime << 32 | (long)((ulong)iNTERNET_CACHE_ENTRY_INFOA.LastAccessTime.dwLowDateTime);
					long fileTime3 = (long)iNTERNET_CACHE_ENTRY_INFOA.LastSyncTime.dwHighDateTime << 32 | (long)((ulong)iNTERNET_CACHE_ENTRY_INFOA.LastSyncTime.dwLowDateTime);
					long fileTime4 = (long)iNTERNET_CACHE_ENTRY_INFOA.ExpireTime.dwHighDateTime << 32 | (long)((ulong)iNTERNET_CACHE_ENTRY_INFOA.ExpireTime.dwLowDateTime);
					string result = string.Concat(new string[]
					{
						"Url:\t\t",
						Marshal.PtrToStringAnsi(iNTERNET_CACHE_ENTRY_INFOA.lpszSourceUrlName),
						"\nCache File:\t",
						Marshal.PtrToStringAnsi(iNTERNET_CACHE_ENTRY_INFOA.lpszLocalFileName),
						"\nSize:\t\t",
						(((ulong)iNTERNET_CACHE_ENTRY_INFOA.dwSizeHigh << 32) + (ulong)iNTERNET_CACHE_ENTRY_INFOA.dwSizeLow).ToString("0,0"),
						" bytes\nFile Extension:\t",
						Marshal.PtrToStringAnsi(iNTERNET_CACHE_ENTRY_INFOA.lpszFileExtension),
						"\nHit Rate:\t",
						iNTERNET_CACHE_ENTRY_INFOA.dwHitRate.ToString(),
						"\nUse Count:\t",
						iNTERNET_CACHE_ENTRY_INFOA.dwUseCount.ToString(),
						"\nDon't Scavenge for:\t",
						iNTERNET_CACHE_ENTRY_INFOA._Union.dwExemptDelta.ToString(),
						" seconds\nLast Modified:\t",
						DateTime.FromFileTime(fileTime).ToString(),
						"\nLast Accessed:\t",
						DateTime.FromFileTime(fileTime2).ToString(),
						"\nLast Synced:  \t",
						DateTime.FromFileTime(fileTime3).ToString(),
						"\nEntry Expires:\t",
						DateTime.FromFileTime(fileTime4).ToString(),
						"\n"
					});
					Marshal.FreeHGlobal(intPtr);
					return result;
				}
			}
			return string.Format("This URL is not present in the WinINET cache. [Code: {0}]", lastWin32Error);
		}
		public static void ClearCookies()
		{
			WinINETCache.ClearCacheItems(false, true);
		}
		public static void ClearFiles()
		{
			WinINETCache.ClearCacheItems(true, false);
		}
		[CodeDescription("Delete all permanent WinINET cookies for sHost; won't clear memory-only session cookies. Supports hostnames with an optional leading wildcard, e.g. *example.com. NOTE: Will not work on VistaIE Protected Mode cookies.")]
		public static void ClearCookiesForHost(string sHost)
		{
			sHost = sHost.Trim();
			if (sHost.Length < 1)
			{
				return;
			}
			string text;
			if (sHost == "*")
			{
				text = string.Empty;
				if (Environment.OSVersion.Version.Major > 5)
				{
					WinINETCache.VistaClearTracks(false, true);
					return;
				}
			}
			else
			{
				text = (sHost.StartsWith("*") ? sHost.Substring(1).ToLower() : ("@" + sHost.ToLower()));
			}
			int num = 0;
			IntPtr intPtr = IntPtr.Zero;
			IntPtr intPtr2 = IntPtr.Zero;
			intPtr2 = WinINETCache.FindFirstUrlCacheEntryA("cookie:", IntPtr.Zero, ref num);
			if (intPtr2 == IntPtr.Zero && 259 == Marshal.GetLastWin32Error())
			{
				return;
			}
			int num2 = num;
			intPtr = Marshal.AllocHGlobal(num2);
			intPtr2 = WinINETCache.FindFirstUrlCacheEntryA("cookie:", intPtr, ref num);
			while (true)
			{
				WinINETCache.INTERNET_CACHE_ENTRY_INFOA iNTERNET_CACHE_ENTRY_INFOA = (WinINETCache.INTERNET_CACHE_ENTRY_INFOA)Marshal.PtrToStructure(intPtr, typeof(WinINETCache.INTERNET_CACHE_ENTRY_INFOA));
				num = num2;
				if (WinINETCache.WININETCACHEENTRYTYPE.COOKIE_CACHE_ENTRY == (iNTERNET_CACHE_ENTRY_INFOA.CacheEntryType & WinINETCache.WININETCACHEENTRYTYPE.COOKIE_CACHE_ENTRY))
				{
					bool flag;
					if (text.Length == 0)
					{
						flag = true;
					}
					else
					{
						string text2 = Marshal.PtrToStringAnsi(iNTERNET_CACHE_ENTRY_INFOA.lpszSourceUrlName);
						int num3 = text2.IndexOf('/');
						if (num3 > 0)
						{
							text2 = text2.Remove(num3);
						}
						text2 = text2.ToLower();
						flag = text2.EndsWith(text);
					}
					if (flag)
					{
						bool flag2 = WinINETCache.DeleteUrlCacheEntryA(iNTERNET_CACHE_ENTRY_INFOA.lpszSourceUrlName);
					}
				}
				while (true)
				{
					bool flag2;
					if (!(flag2 = WinINETCache.FindNextUrlCacheEntryA(intPtr2, intPtr, ref num)))
					{
						if (259 == Marshal.GetLastWin32Error())
						{
							goto IL_19E;
						}
					}
					if (flag2 || num <= num2)
					{
						break;
					}
					num2 = num;
					intPtr = Marshal.ReAllocHGlobal(intPtr, (IntPtr)num2);
				}
			}
			IL_19E:
			Marshal.FreeHGlobal(intPtr);
		}
		public static void ClearCacheItems(bool bClearFiles, bool bClearCookies)
		{
			if (!bClearCookies && !bClearFiles)
			{
				throw new ArgumentException("You must call ClearCacheItems with at least one target");
			}
			if (!FiddlerApplication.DoClearCache(bClearFiles, bClearCookies))
			{
				FiddlerApplication.Log.LogString("Cache clearing was handled by an extension. Default clearing was skipped.");
				return;
			}
			if (Environment.OSVersion.Version.Major > 5)
			{
				FiddlerApplication.Log.LogString(string.Format("Windows Vista+ detected. Calling INETCPL to clear [{0}{1}].", bClearFiles ? "CacheFiles" : string.Empty, bClearCookies ? "Cookies" : string.Empty));
				WinINETCache.VistaClearTracks(bClearFiles, bClearCookies);
				return;
			}
			if (bClearCookies)
			{
				WinINETCache.ClearCookiesForHost("*");
			}
			if (!bClearFiles)
			{
				return;
			}
			FiddlerApplication.Log.LogString("Beginning WinINET Cache clearing...");
			long groupId = 0L;
			int num = 0;
			IntPtr intPtr = IntPtr.Zero;
			IntPtr intPtr2 = IntPtr.Zero;
			intPtr2 = WinINETCache.FindFirstUrlCacheGroup(0, 0, IntPtr.Zero, 0, ref groupId, IntPtr.Zero);
			int lastWin32Error = Marshal.GetLastWin32Error();
			bool flag;
			if (intPtr2 != IntPtr.Zero && 259 != lastWin32Error && 2 != lastWin32Error)
			{
				while (true)
				{
					flag = WinINETCache.DeleteUrlCacheGroup(groupId, 2, IntPtr.Zero);
					lastWin32Error = Marshal.GetLastWin32Error();
					if (!flag && 2 == lastWin32Error)
					{
						flag = WinINETCache.FindNextUrlCacheGroup(intPtr2, ref groupId, IntPtr.Zero);
						lastWin32Error = Marshal.GetLastWin32Error();
					}
					if (!flag)
					{
						if (259 == lastWin32Error)
						{
							break;
						}
						if (2 == lastWin32Error)
						{
							break;
						}
					}
				}
			}
			intPtr2 = WinINETCache.FindFirstUrlCacheEntryExA(null, 0, WinINETCache.WININETCACHEENTRYTYPE.ALL, 0L, IntPtr.Zero, ref num, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
			lastWin32Error = Marshal.GetLastWin32Error();
			if (IntPtr.Zero == intPtr2 && 259 == lastWin32Error)
			{
				return;
			}
			int num2 = num;
			intPtr = Marshal.AllocHGlobal(num2);
			intPtr2 = WinINETCache.FindFirstUrlCacheEntryExA(null, 0, WinINETCache.WININETCACHEENTRYTYPE.ALL, 0L, intPtr, ref num, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
			lastWin32Error = Marshal.GetLastWin32Error();
			do
			{
				WinINETCache.INTERNET_CACHE_ENTRY_INFOA iNTERNET_CACHE_ENTRY_INFOA = (WinINETCache.INTERNET_CACHE_ENTRY_INFOA)Marshal.PtrToStructure(intPtr, typeof(WinINETCache.INTERNET_CACHE_ENTRY_INFOA));
				num = num2;
				if (WinINETCache.WININETCACHEENTRYTYPE.COOKIE_CACHE_ENTRY != (iNTERNET_CACHE_ENTRY_INFOA.CacheEntryType & WinINETCache.WININETCACHEENTRYTYPE.COOKIE_CACHE_ENTRY))
				{
					flag = WinINETCache.DeleteUrlCacheEntryA(iNTERNET_CACHE_ENTRY_INFOA.lpszSourceUrlName);
				}
				flag = WinINETCache.FindNextUrlCacheEntryExA(intPtr2, intPtr, ref num, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
				lastWin32Error = Marshal.GetLastWin32Error();
				if (!flag && 259 == lastWin32Error)
				{
					break;
				}
				if (!flag && num > num2)
				{
					num2 = num;
					intPtr = Marshal.ReAllocHGlobal(intPtr, (IntPtr)num2);
					flag = WinINETCache.FindNextUrlCacheEntryExA(intPtr2, intPtr, ref num, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
				}
			}
			while (flag);
			Marshal.FreeHGlobal(intPtr);
			FiddlerApplication.Log.LogString("Completed WinINET Cache clearing.");
		}
		private static void VistaClearTracks(bool bClearFiles, bool bClearCookies)
		{
			int num = 0;
			if (bClearCookies)
			{
				num |= 2;
			}
			if (bClearFiles)
			{
				num |= 4108;
			}
			try
			{
				using (Process.Start("rundll32.exe", "inetcpl.cpl,ClearMyTracksByProcess " + num.ToString()))
				{
				}
			}
			catch (Exception ex)
			{
				FiddlerApplication.DoNotifyUser("Failed to launch ClearMyTracksByProcess.\n" + ex.Message, "Error");
			}
		}
		[DllImport("wininet.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GetUrlCacheEntryInfoA(string lpszUrlName, IntPtr lpCacheEntryInfo, ref int lpdwCacheEntryInfoBufferSize);
		[DllImport("wininet.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern IntPtr FindFirstUrlCacheGroup(int dwFlags, int dwFilter, IntPtr lpSearchCondition, int dwSearchCondition, ref long lpGroupId, IntPtr lpReserved);
		[DllImport("wininet.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool FindNextUrlCacheGroup(IntPtr hFind, ref long lpGroupId, IntPtr lpReserved);
		[DllImport("wininet.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool DeleteUrlCacheGroup(long GroupId, int dwFlags, IntPtr lpReserved);
		[DllImport("wininet.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		private static extern IntPtr FindFirstUrlCacheEntryA([MarshalAs(UnmanagedType.LPTStr)] string lpszUrlSearchPattern, IntPtr lpFirstCacheEntryInfo, ref int lpdwFirstCacheEntryInfoBufferSize);
		[DllImport("wininet.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool FindNextUrlCacheEntryA(IntPtr hFind, IntPtr lpNextCacheEntryInfo, ref int lpdwNextCacheEntryInfoBufferSize);
		[DllImport("wininet.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		private static extern IntPtr FindFirstUrlCacheEntryExA([MarshalAs(UnmanagedType.LPTStr)] string lpszUrlSearchPattern, int dwFlags, WinINETCache.WININETCACHEENTRYTYPE dwFilter, long GroupId, IntPtr lpFirstCacheEntryInfo, ref int lpdwFirstCacheEntryInfoBufferSize, IntPtr lpReserved, IntPtr pcbReserved2, IntPtr lpReserved3);
		[DllImport("wininet.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool FindNextUrlCacheEntryExA(IntPtr hEnumHandle, IntPtr lpNextCacheEntryInfo, ref int lpdwNextCacheEntryInfoBufferSize, IntPtr lpReserved, IntPtr pcbReserved2, IntPtr lpReserved3);
		[DllImport("wininet.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool DeleteUrlCacheEntryA(IntPtr lpszUrlName);
	}
}
