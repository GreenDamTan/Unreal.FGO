using System;
using System.Collections.Generic;
namespace Fiddler
{
	public interface ISessionExporter : IDisposable
	{
		bool ExportSessions(string sExportFormat, Session[] oSessions, Dictionary<string, object> dictOptions, EventHandler<ProgressCallbackEventArgs> evtProgressNotifications);
	}
}
