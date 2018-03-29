using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
namespace Fiddler
{
	internal class RegistryWatcher
	{
		[Flags]
		private enum RegistryEventFilter : uint
		{
			Key = 1u,
			Attributes = 2u,
			Values = 4u,
			ACLs = 8u
		}
		private const int KEY_QUERY_VALUE = 1;
		private const int KEY_NOTIFY = 16;
		private const int STANDARD_RIGHTS_READ = 131072;
		private static readonly UIntPtr HKEY_CLASSES_ROOT = (UIntPtr)2147483648u;
		private static readonly UIntPtr HKEY_CURRENT_USER = (UIntPtr)2147483649u;
		private static readonly UIntPtr HKEY_LOCAL_MACHINE = (UIntPtr)2147483650u;
		private static readonly UIntPtr HKEY_USERS = (UIntPtr)2147483651u;
		private static readonly UIntPtr HKEY_PERFORMANCE_DATA = (UIntPtr)2147483652u;
		private static readonly UIntPtr HKEY_CURRENT_CONFIG = (UIntPtr)2147483653u;
		private static readonly UIntPtr HKEY_DYN_DATA = (UIntPtr)2147483654u;
		private UIntPtr _hiveToWatch;
		private string _sSubKey;
		private object _lockForThread = new object();
		private Thread _threadWaitForChanges;
		private bool _disposed;
		private ManualResetEvent _eventTerminate = new ManualResetEvent(false);
		private RegistryWatcher.RegistryEventFilter _regFilter = RegistryWatcher.RegistryEventFilter.Values;
		public event EventHandler KeyChanged;
		public bool IsWatching
		{
			get
			{
				return null != this._threadWaitForChanges;
			}
		}
		[DllImport("advapi32.dll", SetLastError = true)]
		private static extern int RegOpenKeyEx(UIntPtr hKey, string subKey, uint options, int samDesired, out IntPtr phkResult);
		[DllImport("advapi32.dll", SetLastError = true)]
		private static extern int RegNotifyChangeKeyValue(IntPtr hKey, [MarshalAs(UnmanagedType.Bool)] bool bWatchSubtree, RegistryWatcher.RegistryEventFilter dwNotifyFilter, IntPtr hEvent, [MarshalAs(UnmanagedType.Bool)] bool fAsynchronous);
		[DllImport("advapi32.dll", SetLastError = true)]
		private static extern int RegCloseKey(IntPtr hKey);
		protected virtual void OnKeyChanged()
		{
			EventHandler keyChanged = this.KeyChanged;
			if (keyChanged != null)
			{
				keyChanged(this, null);
			}
		}
		internal static RegistryWatcher WatchKey(RegistryHive registryHive, string subKey, EventHandler oToNotify)
		{
			RegistryWatcher registryWatcher = new RegistryWatcher(registryHive, subKey);
			registryWatcher.KeyChanged += oToNotify;
			registryWatcher.Start();
			return registryWatcher;
		}
		private RegistryWatcher(RegistryHive registryHive, string subKey)
		{
			this.InitRegistryKey(registryHive, subKey);
		}
		public void Dispose()
		{
			this.Stop();
			this._disposed = true;
			GC.SuppressFinalize(this);
		}
		private void InitRegistryKey(RegistryHive hive, string name)
		{
			switch (hive)
			{
			case RegistryHive.ClassesRoot:
				this._hiveToWatch = RegistryWatcher.HKEY_CLASSES_ROOT;
				break;
			case RegistryHive.CurrentUser:
				this._hiveToWatch = RegistryWatcher.HKEY_CURRENT_USER;
				break;
			case RegistryHive.LocalMachine:
				this._hiveToWatch = RegistryWatcher.HKEY_LOCAL_MACHINE;
				break;
			case RegistryHive.Users:
				this._hiveToWatch = RegistryWatcher.HKEY_USERS;
				break;
			case RegistryHive.PerformanceData:
				this._hiveToWatch = RegistryWatcher.HKEY_PERFORMANCE_DATA;
				break;
			case RegistryHive.CurrentConfig:
				this._hiveToWatch = RegistryWatcher.HKEY_CURRENT_CONFIG;
				break;
			case RegistryHive.DynData:
				this._hiveToWatch = RegistryWatcher.HKEY_DYN_DATA;
				break;
			default:
				throw new InvalidEnumArgumentException("hive", (int)hive, typeof(RegistryHive));
			}
			this._sSubKey = name;
		}
		private void Start()
		{
			if (this._disposed)
			{
				throw new ObjectDisposedException(null, "This instance is already disposed");
			}
			object lockForThread;
			Monitor.Enter(lockForThread = this._lockForThread);
			try
			{
				if (!this.IsWatching)
				{
					this._eventTerminate.Reset();
					this._threadWaitForChanges = new Thread(new ThreadStart(this.MonitorThread));
					this._threadWaitForChanges.IsBackground = true;
					this._threadWaitForChanges.Start();
				}
			}
			finally
			{
				Monitor.Exit(lockForThread);
			}
		}
		public void Stop()
		{
			if (this._disposed)
			{
				throw new ObjectDisposedException(null, "This instance is already disposed");
			}
			object lockForThread;
			Monitor.Enter(lockForThread = this._lockForThread);
			try
			{
				Thread threadWaitForChanges = this._threadWaitForChanges;
				if (threadWaitForChanges != null)
				{
					this._eventTerminate.Set();
					threadWaitForChanges.Join();
				}
			}
			finally
			{
				Monitor.Exit(lockForThread);
			}
		}
		private void MonitorThread()
		{
			try
			{
				this.WatchAndNotify();
			}
			catch (Exception)
			{
			}
			this._threadWaitForChanges = null;
		}
		private void WatchAndNotify()
		{
			IntPtr intPtr;
			int num = RegistryWatcher.RegOpenKeyEx(this._hiveToWatch, this._sSubKey, 0u, 131089, out intPtr);
			if (num != 0)
			{
				throw new Win32Exception(num);
			}
			try
			{
				AutoResetEvent autoResetEvent = new AutoResetEvent(false);
				WaitHandle[] waitHandles = new WaitHandle[]
				{
					autoResetEvent,
					this._eventTerminate
				};
				while (!this._eventTerminate.WaitOne(0, true))
				{
					num = RegistryWatcher.RegNotifyChangeKeyValue(intPtr, false, this._regFilter, autoResetEvent.SafeWaitHandle.DangerousGetHandle(), true);
					if (num != 0)
					{
						throw new Win32Exception(num);
					}
					if (WaitHandle.WaitAny(waitHandles) == 0)
					{
						this.OnKeyChanged();
					}
				}
			}
			finally
			{
				if (IntPtr.Zero != intPtr)
				{
					RegistryWatcher.RegCloseKey(intPtr);
				}
			}
		}
	}
}
