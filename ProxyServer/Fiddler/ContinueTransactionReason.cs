using System;
namespace Fiddler
{
	public enum ContinueTransactionReason : byte
	{
		None,
		Authenticate,
		Redirect,
		Tunnel
	}
}
