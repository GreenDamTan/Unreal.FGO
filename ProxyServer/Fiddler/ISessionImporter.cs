using System;
using System.Collections.Generic;
namespace Fiddler
{
	public interface ISessionImporter : IDisposable
	{
		Session[] ImportSessions(string sImportFormat, Dictionary<string, object> dictOptions, EventHandler<ProgressCallbackEventArgs> evtProgressNotifications);
	}
}
