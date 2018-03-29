using System;
using System.Net.Sockets;
namespace Fiddler
{
	public class ConnectionEventArgs : EventArgs
	{
		private readonly Socket _oSocket;
		private readonly Session _oSession;
		public Socket Connection
		{
			get
			{
				return this._oSocket;
			}
		}
		public Session OwnerSession
		{
			get
			{
				return this._oSession;
			}
		}
		internal ConnectionEventArgs(Session oSession, Socket oSocket)
		{
			this._oSession = oSession;
			this._oSocket = oSocket;
		}
	}
}
