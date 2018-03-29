using System;
using System.Collections.Generic;
using System.Threading;
namespace Fiddler
{
	internal static class ScheduledTasks
	{
		private class jobItem
		{
			public ulong _ulRunAfter;
			public SimpleEventHandler _oJob;
			public jobItem(SimpleEventHandler oJob, uint iMaxDelay)
			{
				this._ulRunAfter = (ulong)iMaxDelay + Utilities.GetTickCount();
				this._oJob = oJob;
			}
		}
		private const int CONST_MIN_RESOLUTION = 15;
		private static Dictionary<string, ScheduledTasks.jobItem> _dictSchedule = new Dictionary<string, ScheduledTasks.jobItem>();
		private static Timer _timerInternal = null;
		private static ReaderWriterLock _RWLockDict = new ReaderWriterLock();
		private static void doWork(object objState)
		{
			List<KeyValuePair<string, ScheduledTasks.jobItem>> list = null;
			try
			{
				ScheduledTasks._RWLockDict.AcquireReaderLock(-1);
				ulong tickCount = Utilities.GetTickCount();
				foreach (KeyValuePair<string, ScheduledTasks.jobItem> current in ScheduledTasks._dictSchedule)
				{
					if (tickCount > current.Value._ulRunAfter)
					{
						current.Value._ulRunAfter = 18446744073709551615uL;
						if (list == null)
						{
							list = new List<KeyValuePair<string, ScheduledTasks.jobItem>>();
						}
						list.Add(current);
					}
				}
				if (list == null)
				{
					return;
				}
				LockCookie lockCookie = ScheduledTasks._RWLockDict.UpgradeToWriterLock(-1);
				try
				{
					foreach (KeyValuePair<string, ScheduledTasks.jobItem> current2 in list)
					{
						ScheduledTasks._dictSchedule.Remove(current2.Key);
					}
					if (ScheduledTasks._dictSchedule.Count < 1 && ScheduledTasks._timerInternal != null)
					{
						ScheduledTasks._timerInternal.Dispose();
						ScheduledTasks._timerInternal = null;
					}
				}
				finally
				{
					ScheduledTasks._RWLockDict.DowngradeFromWriterLock(ref lockCookie);
				}
			}
			finally
			{
				ScheduledTasks._RWLockDict.ReleaseReaderLock();
			}
			foreach (KeyValuePair<string, ScheduledTasks.jobItem> current3 in list)
			{
				try
				{
					current3.Value._oJob();
				}
				catch (Exception)
				{
				}
			}
		}
		internal static bool CancelWork(string sTaskName)
		{
			bool result;
			try
			{
				ScheduledTasks._RWLockDict.AcquireWriterLock(-1);
				result = ScheduledTasks._dictSchedule.Remove(sTaskName);
			}
			finally
			{
				ScheduledTasks._RWLockDict.ReleaseWriterLock();
			}
			return result;
		}
		internal static bool ScheduleWork(string sTaskName, uint iMaxDelay, SimpleEventHandler workFunction)
		{
			try
			{
				ScheduledTasks._RWLockDict.AcquireReaderLock(-1);
				if (ScheduledTasks._dictSchedule.ContainsKey(sTaskName))
				{
					bool result = false;
					return result;
				}
			}
			finally
			{
				ScheduledTasks._RWLockDict.ReleaseReaderLock();
			}
			ScheduledTasks.jobItem value = new ScheduledTasks.jobItem(workFunction, iMaxDelay);
			try
			{
				ScheduledTasks._RWLockDict.AcquireWriterLock(-1);
				if (ScheduledTasks._dictSchedule.ContainsKey(sTaskName))
				{
					bool result = false;
					return result;
				}
				ScheduledTasks._dictSchedule.Add(sTaskName, value);
				if (ScheduledTasks._timerInternal == null)
				{
					ScheduledTasks._timerInternal = new Timer(new TimerCallback(ScheduledTasks.doWork), null, 15, 15);
				}
			}
			finally
			{
				ScheduledTasks._RWLockDict.ReleaseWriterLock();
			}
			return true;
		}
	}
}
