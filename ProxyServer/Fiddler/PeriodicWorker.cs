using System;
using System.Collections.Generic;
using System.Threading;
namespace Fiddler
{
	internal class PeriodicWorker
	{
		internal class taskItem
		{
			public ulong _ulLastRun;
			public uint _iPeriod;
			public SimpleEventHandler _oTask;
			public taskItem(SimpleEventHandler oTask, uint iPeriod)
			{
				this._ulLastRun = Utilities.GetTickCount();
				this._iPeriod = iPeriod;
				this._oTask = oTask;
			}
		}
		private const int CONST_MIN_RESOLUTION = 500;
		private Timer timerInternal;
		private List<PeriodicWorker.taskItem> oTaskList = new List<PeriodicWorker.taskItem>();
		internal PeriodicWorker()
		{
			this.timerInternal = new Timer(new TimerCallback(this.doWork), null, 500, 500);
		}
		private void doWork(object objState)
		{
			if (FiddlerApplication.isClosing)
			{
				this.timerInternal.Dispose();
				return;
			}
			List<PeriodicWorker.taskItem> obj;
			Monitor.Enter(obj = this.oTaskList);
			PeriodicWorker.taskItem[] array;
			try
			{
				array = new PeriodicWorker.taskItem[this.oTaskList.Count];
				this.oTaskList.CopyTo(array);
			}
			finally
			{
				Monitor.Exit(obj);
			}
			PeriodicWorker.taskItem[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				PeriodicWorker.taskItem taskItem = array2[i];
				if (Utilities.GetTickCount() > taskItem._ulLastRun + (ulong)taskItem._iPeriod)
				{
					taskItem._oTask();
					taskItem._ulLastRun = Utilities.GetTickCount();
				}
			}
		}
		internal PeriodicWorker.taskItem assignWork(SimpleEventHandler workFunction, uint iMS)
		{
			PeriodicWorker.taskItem taskItem = new PeriodicWorker.taskItem(workFunction, iMS);
			List<PeriodicWorker.taskItem> obj;
			Monitor.Enter(obj = this.oTaskList);
			try
			{
				this.oTaskList.Add(taskItem);
			}
			finally
			{
				Monitor.Exit(obj);
			}
			return taskItem;
		}
		internal void revokeWork(PeriodicWorker.taskItem oToRevoke)
		{
			if (oToRevoke == null)
			{
				return;
			}
			List<PeriodicWorker.taskItem> obj;
			Monitor.Enter(obj = this.oTaskList);
			try
			{
				this.oTaskList.Remove(oToRevoke);
			}
			finally
			{
				Monitor.Exit(obj);
			}
		}
	}
}
