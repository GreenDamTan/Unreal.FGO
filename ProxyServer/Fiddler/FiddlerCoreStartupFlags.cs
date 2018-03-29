using System;
namespace Fiddler
{
	[Flags]
	public enum FiddlerCoreStartupFlags
	{
		None = 0,
		RegisterAsSystemProxy = 1,
		DecryptSSL = 2,
		AllowRemoteClients = 8,
		ChainToUpstreamGateway = 16,
		MonitorAllConnections = 32,
		CaptureLocalhostTraffic = 128,
		CaptureFTP = 256,
		Default = 187
	}
}
