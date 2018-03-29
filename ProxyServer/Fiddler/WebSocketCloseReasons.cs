using System;
namespace Fiddler
{
	public enum WebSocketCloseReasons : short
	{
		Normal = 1000,
		GoingAway,
		ProtocolError,
		UnsupportedData,
		Undefined1004,
		Reserved1005,
		Reserved1006,
		InvalidPayloadData,
		PolicyViolation,
		MessageTooBig,
		MandatoryExtension,
		InternalServerError,
		Reserved1015 = 1015
	}
}
