using System;
using System.Runtime.InteropServices;
namespace Fiddler
{
	internal class RASInfo
	{
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
		private struct RASENTRYNAME
		{
			public int dwSize;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)]
			public string szEntryName;
			public int dwFlags;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 261)]
			public string szPhonebook;
		}
		[Flags]
		internal enum InternetConnectionState
		{
			INTERNET_CONNECTION_MODEM = 1,
			INTERNET_CONNECTION_LAN = 2,
			INTERNET_CONNECTION_PROXY = 4,
			INTERNET_RAS_INSTALLED = 16,
			INTERNET_CONNECTION_OFFLINE = 32,
			INTERNET_CONNECTION_CONFIGURED = 64
		}
		private const int MAX_PATH = 260;
		private const int RAS_MaxEntryName = 256;
		[DllImport("rasapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern uint RasEnumEntries(IntPtr reserved, IntPtr lpszPhonebook, [In] [Out] RASInfo.RASENTRYNAME[] lprasentryname, ref int lpcb, ref int lpcEntries);
		[DllImport("rasapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern uint RasEnumEntries(IntPtr reserved, IntPtr lpszPhonebook, [In] [Out] IntPtr lprasentryname, ref int lpcb, ref int lpcEntries);
		internal static string[] GetConnectionNames()
		{
			if (CONFIG.bDebugSpew)
			{
				FiddlerApplication.DebugSpew("WinINET indicates connectivity is via: " + RASInfo.GetConnectedState());
			}
			string[] result;
			try
			{
				int dwSize = Marshal.SizeOf(typeof(RASInfo.RASENTRYNAME));
				int num = 0;
				uint num2;
				if (Environment.OSVersion.Version.Major < 6)
				{
					RASInfo.RASENTRYNAME[] array = new RASInfo.RASENTRYNAME[1];
					array[0].dwSize = dwSize;
					num2 = RASInfo.RasEnumEntries(IntPtr.Zero, IntPtr.Zero, array, ref dwSize, ref num);
				}
				else
				{
					num2 = RASInfo.RasEnumEntries(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ref dwSize, ref num);
				}
				if (num2 != 0u && 603u != num2)
				{
					FiddlerApplication.Log.LogFormat("! WARNING: RasEnumEntries failed (0x{0:x}). Ignoring non-LAN Connectoids. ", new object[]
					{
						num2
					});
					num = 0;
				}
				string[] array2 = new string[num + 1];
				array2[0] = "DefaultLAN";
				if (num == 0)
				{
					result = array2;
				}
				else
				{
					RASInfo.RASENTRYNAME[] array = new RASInfo.RASENTRYNAME[num];
					for (int i = 0; i < num; i++)
					{
						array[i].dwSize = Marshal.SizeOf(typeof(RASInfo.RASENTRYNAME));
					}
					num2 = RASInfo.RasEnumEntries(IntPtr.Zero, IntPtr.Zero, array, ref dwSize, ref num);
					if (num2 != 0u)
					{
						FiddlerApplication.Log.LogFormat("! WARNING: RasEnumEntries failed (0x{0:x}). Ignoring non-LAN Connectoids. ", new object[]
						{
							num2
						});
						result = new string[]
						{
							"DefaultLAN"
						};
					}
					else
					{
						for (int j = 0; j < num; j++)
						{
							array2[j + 1] = array[j].szEntryName;
						}
						result = array2;
					}
				}
			}
			catch (Exception eX)
			{
				FiddlerApplication.Log.LogFormat("! WARNING: Enumerating connections failed. Ignoring non-LAN Connectoids.\n{0}", new object[]
				{
					Utilities.DescribeException(eX)
				});
				result = new string[]
				{
					"DefaultLAN"
				};
			}
			return result;
		}
		[DllImport("wininet.dll", CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool InternetGetConnectedState(ref RASInfo.InternetConnectionState lpdwFlags, int dwReserved);
		private static string GetConnectedState()
		{
			RASInfo.InternetConnectionState internetConnectionState = (RASInfo.InternetConnectionState)0;
			if (RASInfo.InternetGetConnectedState(ref internetConnectionState, 0))
			{
				return "CONNECTED (" + internetConnectionState.ToString() + ")";
			}
			return "NOT_CONNECTED (" + internetConnectionState.ToString() + ")";
		}
	}
}
