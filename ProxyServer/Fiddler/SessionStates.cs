using System;
namespace Fiddler
{
	public enum SessionStates
	{
		Created,
		ReadingRequest,
		AutoTamperRequestBefore,
		HandTamperRequest,
		AutoTamperRequestAfter,
		SendingRequest,
		ReadingResponse,
		AutoTamperResponseBefore,
		HandTamperResponse,
		AutoTamperResponseAfter,
		SendingResponse,
		Done,
		Aborted
	}
}
