using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
namespace Fiddler
{
	public class Logger
	{
		private List<string> queueStartupMessages;
		public event EventHandler<LogEventArgs> OnLogString;
		public Logger(bool bQueueStartup)
		{
			if (bQueueStartup)
			{
				this.queueStartupMessages = new List<string>();
				return;
			}
			this.queueStartupMessages = null;
		}
		internal void FlushStartupMessages()
		{
			if (this.OnLogString != null && this.queueStartupMessages != null)
			{
				List<string> list = this.queueStartupMessages;
				this.queueStartupMessages = null;
				using (List<string>.Enumerator enumerator = list.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						string current = enumerator.Current;
						LogEventArgs e = new LogEventArgs(current);
						this.OnLogString(this, e);
					}
					return;
				}
			}
			this.queueStartupMessages = null;
		}
		public void LogFormat(string format, params object[] args)
		{
			this.LogString(string.Format(format, args));
		}
		public void LogString(string sMsg)
		{
			if (CONFIG.bDebugSpew)
			{
				Trace.WriteLine(sMsg);
			}
			if (this.queueStartupMessages != null)
			{
				List<string> obj;
				Monitor.Enter(obj = this.queueStartupMessages);
				try
				{
					this.queueStartupMessages.Add(sMsg);
				}
				finally
				{
					Monitor.Exit(obj);
				}
				return;
			}
			if (this.OnLogString != null)
			{
				LogEventArgs e = new LogEventArgs(sMsg);
				this.OnLogString(this, e);
			}
		}
	}
}
