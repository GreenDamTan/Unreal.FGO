using System;
namespace Fiddler
{
	internal enum ProcessingStates : byte
	{
		GetRequestStart,
		GetRequestHeadersEnd,
		GetRequestEnd,
		RunRequestRulesStart,
		RunRequestRulesEnd,
		DetermineGatewayStart,
		DetermineGatewayEnd,
		ConnectStart,
		ConnectEnd,
		HTTPSHandshakeStart,
		HTTPSHandshakeEnd,
		SendRequestStart,
		SendRequestEnd,
		ReadResponseStart,
		GetResponseHeadersEnd,
		ReadResponseEnd,
		RunResponseRulesStart,
		RunResponseRulesEnd,
		ReturnBufferedResponseStart,
		ReturnBufferedResponseEnd
	}
}
