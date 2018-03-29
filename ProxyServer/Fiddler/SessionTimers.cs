using System;
using System.Runtime.InteropServices;
namespace Fiddler
{
	public class SessionTimers
	{
		public DateTime ClientConnected;
		public DateTime ClientBeginRequest;
		public DateTime FiddlerGotRequestHeaders;
		public DateTime ClientDoneRequest;
		public DateTime ServerConnected;
		public DateTime FiddlerBeginRequest;
		public DateTime ServerGotRequest;
		public DateTime ServerBeginResponse;
		public DateTime FiddlerGotResponseHeaders;
		public DateTime ServerDoneResponse;
		public DateTime ClientBeginResponse;
		public DateTime ClientDoneResponse;
		public int GatewayDeterminationTime;
		public int DNSTime;
		public int TCPConnectTime;
		public int HTTPSHandshakeTime;
		private static bool _EnabledHighResTimers;
		public static bool EnableHighResolutionTimers
		{
			get
			{
				return SessionTimers._EnabledHighResTimers;
			}
			set
			{
				if (value == SessionTimers._EnabledHighResTimers)
				{
					return;
				}
				if (value)
				{
					uint num = SessionTimers.timeBeginPeriod(1u);
					SessionTimers._EnabledHighResTimers = (num == 0u);
					return;
				}
				uint num2 = SessionTimers.timeEndPeriod(1u);
				SessionTimers._EnabledHighResTimers = (num2 != 0u);
			}
		}
		public override string ToString()
		{
			return this.ToString(false);
		}
		public string ToString(bool bMultiLine)
		{
			if (bMultiLine)
			{
				return string.Format("ClientConnected:\t{0:HH:mm:ss.fff}\r\nClientBeginRequest:\t{1:HH:mm:ss.fff}\r\nGotRequestHeaders:\t{2:HH:mm:ss.fff}\r\nClientDoneRequest:\t{3:HH:mm:ss.fff}\r\nDetermine Gateway:\t{4,0}ms\r\nDNS Lookup: \t\t{5,0}ms\r\nTCP/IP Connect:\t{6,0}ms\r\nHTTPS Handshake:\t{7,0}ms\r\nServerConnected:\t{8:HH:mm:ss.fff}\r\nFiddlerBeginRequest:\t{9:HH:mm:ss.fff}\r\nServerGotRequest:\t{10:HH:mm:ss.fff}\r\nServerBeginResponse:\t{11:HH:mm:ss.fff}\r\nGotResponseHeaders:\t{12:HH:mm:ss.fff}\r\nServerDoneResponse:\t{13:HH:mm:ss.fff}\r\nClientBeginResponse:\t{14:HH:mm:ss.fff}\r\nClientDoneResponse:\t{15:HH:mm:ss.fff}\r\n\r\n{16}", new object[]
				{
					this.ClientConnected,
					this.ClientBeginRequest,
					this.FiddlerGotRequestHeaders,
					this.ClientDoneRequest,
					this.GatewayDeterminationTime,
					this.DNSTime,
					this.TCPConnectTime,
					this.HTTPSHandshakeTime,
					this.ServerConnected,
					this.FiddlerBeginRequest,
					this.ServerGotRequest,
					this.ServerBeginResponse,
					this.FiddlerGotResponseHeaders,
					this.ServerDoneResponse,
					this.ClientBeginResponse,
					this.ClientDoneResponse,
					(TimeSpan.Zero < this.ClientDoneResponse - this.ClientBeginRequest) ? string.Format("\tOverall Elapsed:\t{0:h\\:mm\\:ss\\.fff}\r\n", this.ClientDoneResponse - this.ClientBeginRequest) : string.Empty
				});
			}
			return string.Format("ClientConnected: {0:HH:mm:ss.fff}, ClientBeginRequest: {1:HH:mm:ss.fff}, GotRequestHeaders: {2:HH:mm:ss.fff}, ClientDoneRequest: {3:HH:mm:ss.fff}, Determine Gateway: {4,0}ms, DNS Lookup: {5,0}ms, TCP/IP Connect: {6,0}ms, HTTPS Handshake: {7,0}ms, ServerConnected: {8:HH:mm:ss.fff},FiddlerBeginRequest: {9:HH:mm:ss.fff}, ServerGotRequest: {10:HH:mm:ss.fff}, ServerBeginResponse: {11:HH:mm:ss.fff}, GotResponseHeaders: {12:HH:mm:ss.fff}, ServerDoneResponse: {13:HH:mm:ss.fff}, ClientBeginResponse: {14:HH:mm:ss.fff}, ClientDoneResponse: {15:HH:mm:ss.fff}{16}", new object[]
			{
				this.ClientConnected,
				this.ClientBeginRequest,
				this.FiddlerGotRequestHeaders,
				this.ClientDoneRequest,
				this.GatewayDeterminationTime,
				this.DNSTime,
				this.TCPConnectTime,
				this.HTTPSHandshakeTime,
				this.ServerConnected,
				this.FiddlerBeginRequest,
				this.ServerGotRequest,
				this.ServerBeginResponse,
				this.FiddlerGotResponseHeaders,
				this.ServerDoneResponse,
				this.ClientBeginResponse,
				this.ClientDoneResponse,
				(TimeSpan.Zero < this.ClientDoneResponse - this.ClientBeginRequest) ? string.Format(", Overall Elapsed: {0:h\\:mm\\:ss\\.fff}", this.ClientDoneResponse - this.ClientBeginRequest) : string.Empty
			});
		}
		[DllImport("winmm.dll")]
		private static extern uint timeBeginPeriod(uint iMS);
		[DllImport("winmm.dll")]
		private static extern uint timeEndPeriod(uint iMS);
	}
}
