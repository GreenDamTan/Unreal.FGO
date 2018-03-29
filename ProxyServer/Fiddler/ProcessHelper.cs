using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
namespace Fiddler
{
	internal static class ProcessHelper
	{
		internal struct ProcessNameCacheEntry
		{
			internal readonly ulong ulLastLookup;
			internal readonly string sProcessName;
			internal ProcessNameCacheEntry(string _sProcessName)
			{
				this.ulLastLookup = Utilities.GetTickCount();
				this.sProcessName = _sProcessName;
			}
		}
		private const int QueryLimitedInformation = 4096;
		private const int ERROR_INSUFFICIENT_BUFFER = 122;
		private const int ERROR_SUCCESS = 0;
		private const uint MSEC_PROCESSNAME_CACHE_LIFETIME = 30000u;
		private static bool bDisambiguateWWAHostApps;
		private static readonly Dictionary<int, ProcessHelper.ProcessNameCacheEntry> dictProcessNames;
		[DllImport("kernel32.dll")]
		internal static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);
		[DllImport("kernel32.dll")]
		private static extern bool CloseHandle(IntPtr hHandle);
		[DllImport("kernel32.dll")]
		internal static extern int GetApplicationUserModelId(IntPtr hProcess, ref uint AppModelIDLength, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder sbAppUserModelID);
		static ProcessHelper()
		{
			ProcessHelper.bDisambiguateWWAHostApps = false;
			ProcessHelper.dictProcessNames = new Dictionary<int, ProcessHelper.ProcessNameCacheEntry>();
			FiddlerApplication.Janitor.assignWork(new SimpleEventHandler(ProcessHelper.ScavengeCache), 60000u);
			if (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor > 1 && FiddlerApplication.Prefs.GetBoolPref("fiddler.ProcessInfo.DecorateWithAppName", true))
			{
				ProcessHelper.bDisambiguateWWAHostApps = true;
			}
		}
		internal static void ScavengeCache()
		{
			Dictionary<int, ProcessHelper.ProcessNameCacheEntry> obj;
			Monitor.Enter(obj = ProcessHelper.dictProcessNames);
			try
			{
				List<int> list = new List<int>();
				foreach (KeyValuePair<int, ProcessHelper.ProcessNameCacheEntry> current in ProcessHelper.dictProcessNames)
				{
					if (current.Value.ulLastLookup < Utilities.GetTickCount() - 30000uL)
					{
						list.Add(current.Key);
					}
				}
				foreach (int current2 in list)
				{
					ProcessHelper.dictProcessNames.Remove(current2);
				}
			}
			finally
			{
				Monitor.Exit(obj);
			}
		}
		internal static string GetProcessName(int iPID)
		{
			string result;
			try
			{
				ProcessHelper.ProcessNameCacheEntry processNameCacheEntry;
				if (ProcessHelper.dictProcessNames.TryGetValue(iPID, out processNameCacheEntry))
				{
					if (processNameCacheEntry.ulLastLookup > Utilities.GetTickCount() - 30000uL)
					{
						result = processNameCacheEntry.sProcessName;
						return result;
					}
					Dictionary<int, ProcessHelper.ProcessNameCacheEntry> obj;
					Monitor.Enter(obj = ProcessHelper.dictProcessNames);
					try
					{
						ProcessHelper.dictProcessNames.Remove(iPID);
					}
					finally
					{
						Monitor.Exit(obj);
					}
				}
				string text = Process.GetProcessById(iPID).ProcessName.ToLower();
				if (string.IsNullOrEmpty(text))
				{
					result = string.Empty;
				}
				else
				{
					if (ProcessHelper.bDisambiguateWWAHostApps && text.OICEquals("WWAHost"))
					{
						try
						{
							IntPtr intPtr = ProcessHelper.OpenProcess(4096, false, iPID);
							if (IntPtr.Zero != intPtr)
							{
								uint capacity = 130u;
								StringBuilder stringBuilder = new StringBuilder(130);
								int applicationUserModelId = ProcessHelper.GetApplicationUserModelId(intPtr, ref capacity, stringBuilder);
								if (applicationUserModelId == 0)
								{
									text = string.Format("{0}!{1}", text, stringBuilder);
								}
								else
								{
									if (122 == applicationUserModelId)
									{
										stringBuilder = new StringBuilder((int)capacity);
										if (ProcessHelper.GetApplicationUserModelId(intPtr, ref capacity, stringBuilder) == 0)
										{
											text = string.Format("{0}!{1}", text, stringBuilder);
										}
									}
								}
								ProcessHelper.CloseHandle(intPtr);
							}
						}
						catch
						{
						}
					}
					Dictionary<int, ProcessHelper.ProcessNameCacheEntry> obj2;
					Monitor.Enter(obj2 = ProcessHelper.dictProcessNames);
					try
					{
						if (!ProcessHelper.dictProcessNames.ContainsKey(iPID))
						{
							ProcessHelper.dictProcessNames.Add(iPID, new ProcessHelper.ProcessNameCacheEntry(text));
						}
					}
					finally
					{
						Monitor.Exit(obj2);
					}
					result = text;
				}
			}
			catch (Exception)
			{
				result = string.Empty;
			}
			return result;
		}
	}
}
