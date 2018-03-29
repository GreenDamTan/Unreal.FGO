using System;
namespace Fiddler
{
	public enum WebSocketFrameTypes : byte
	{
		Continuation,
		Text,
		Binary,
		Reservedx3,
		Reservedx4,
		Reservedx5,
		Reservedx6,
		Reservedx7,
		Close,
		Ping,
		Pong,
		ReservedxB,
		ReservedxC,
		ReservedxD,
		ReservedxE,
		ReservedxF
	}
}
