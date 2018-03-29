using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
namespace Fiddler
{
	internal static class Winsock
	{
		private enum TcpTableType
		{
			BasicListener,
			BasicConnections,
			BasicAll,
			OwnerPidListener,
			OwnerPidConnections,
			OwnerPidAll,
			OwnerModuleListener,
			OwnerModuleConnections,
			OwnerModuleAll
		}
		private const int AF_INET = 2;
		private const int AF_INET6 = 23;
		private const int ERROR_INSUFFICIENT_BUFFER = 122;
		private const int NO_ERROR = 0;
		[DllImport("iphlpapi.dll", ExactSpelling = true, SetLastError = true)]
		private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref uint dwTcpTableLength, [MarshalAs(UnmanagedType.Bool)] bool sort, uint ipVersion, Winsock.TcpTableType tcpTableType, uint reserved);
		internal static int MapLocalPortToProcessId(int iPort)
		{
			return Winsock.FindPIDForPort(iPort);
		}
		private static int FindPIDForConnection(int iTargetPort, uint iAddressType)
		{
			IntPtr intPtr = IntPtr.Zero;
			uint num = 32768u;
			try
			{
				intPtr = Marshal.AllocHGlobal(32768);
				uint extendedTcpTable = Winsock.GetExtendedTcpTable(intPtr, ref num, false, iAddressType, Winsock.TcpTableType.OwnerPidConnections, 0u);
				while (122u == extendedTcpTable)
				{
					Trace.WriteLine(string.Format("[Thread {0}] - Buffer too small; need {1} bytes.", Thread.CurrentThread.ManagedThreadId, num));
					Marshal.FreeHGlobal(intPtr);
					intPtr = Marshal.AllocHGlobal((int)num);
					extendedTcpTable = Winsock.GetExtendedTcpTable(intPtr, ref num, false, iAddressType, Winsock.TcpTableType.OwnerPidConnections, 0u);
				}
				if (extendedTcpTable != 0u)
				{
					FiddlerApplication.Log.LogFormat("!GetExtendedTcpTable() returned error #0x{0:x}", new object[]
					{
						extendedTcpTable
					});
					int result = 0;
					return result;
				}
				int num2;
				int ofs;
				int num3;
				if (iAddressType == 2u)
				{
					num2 = 12;
					ofs = 12;
					num3 = 24;
				}
				else
				{
					num2 = 24;
					ofs = 32;
					num3 = 56;
				}
				int num4 = ((iTargetPort & 255) << 8) + ((iTargetPort & 65280) >> 8);
				int num5 = Marshal.ReadInt32(intPtr);
				if (num5 == 0)
				{
					int result = 0;
					return result;
				}
				IntPtr intPtr2 = (IntPtr)((long)intPtr + (long)num2);
				for (int i = 0; i < num5; i++)
				{
					if (num4 == Marshal.ReadInt32(intPtr2))
					{
						int result = Marshal.ReadInt32(intPtr2, ofs);
						return result;
					}
					intPtr2 = (IntPtr)((long)intPtr2 + (long)num3);
				}
			}
			finally
			{
				Marshal.FreeHGlobal(intPtr);
			}
			return 0;
		}
		private static int FindPIDForPort(int iTargetPort)
		{
			try
			{
				int num = Winsock.FindPIDForConnection(iTargetPort, 2u);
				int result;
				if (num <= 0 && CONFIG.bEnableIPv6)
				{
					result = Winsock.FindPIDForConnection(iTargetPort, 23u);
					return result;
				}
				result = num;
				return result;
			}
			catch (Exception ex)
			{
				FiddlerApplication.Log.LogFormat("Fiddler.Network.TCPTable> Unable to call IPHelperAPI function: {0}", new object[]
				{
					ex.Message
				});
			}
			return 0;
		}
	}
}
