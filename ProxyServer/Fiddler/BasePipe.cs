using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
namespace Fiddler
{
	public abstract class BasePipe
	{
		protected Socket _baseSocket;
		protected internal uint iUseCount;
		protected SslStream _httpsStream;
		protected internal string _sPipeName;
		private int _iTransmitDelayMS;
		public bool Connected
		{
			get
			{
				return this._baseSocket != null && this._baseSocket.Connected;
			}
		}
		public bool bIsSecured
		{
			get
			{
				return null != this._httpsStream;
			}
		}
		public SslProtocols SecureProtocol
		{
			get
			{
				if (this._httpsStream == null)
				{
					return SslProtocols.None;
				}
				return this._httpsStream.SslProtocol;
			}
		}
		public int Port
		{
			get
			{
				int result;
				try
				{
					if (this._baseSocket != null && this._baseSocket.RemoteEndPoint != null)
					{
						result = (this._baseSocket.RemoteEndPoint as IPEndPoint).Port;
					}
					else
					{
						result = 0;
					}
				}
				catch
				{
					result = 0;
				}
				return result;
			}
		}
		public int LocalPort
		{
			get
			{
				int result;
				try
				{
					if (this._baseSocket != null && this._baseSocket.LocalEndPoint != null)
					{
						result = (this._baseSocket.LocalEndPoint as IPEndPoint).Port;
					}
					else
					{
						result = 0;
					}
				}
				catch
				{
					result = 0;
				}
				return result;
			}
		}
		public IPAddress Address
		{
			get
			{
				IPAddress result;
				try
				{
					if (this._baseSocket != null && this._baseSocket.RemoteEndPoint != null)
					{
						result = (this._baseSocket.RemoteEndPoint as IPEndPoint).Address;
					}
					else
					{
						result = new IPAddress(0L);
					}
				}
				catch
				{
					result = new IPAddress(0L);
				}
				return result;
			}
		}
		public int TransmitDelay
		{
			get
			{
				return this._iTransmitDelayMS;
			}
			set
			{
				this._iTransmitDelayMS = value;
			}
		}
		public BasePipe(Socket oSocket, string sName)
		{
			this._sPipeName = sName;
			this._baseSocket = oSocket;
		}
		public virtual bool HasDataAvailable()
		{
			return this.Connected && this._baseSocket.Poll(0, SelectMode.SelectRead);
		}
		internal void IncrementUse(int iSession)
		{
			this._iTransmitDelayMS = 0;
			this.iUseCount += 1u;
		}
		public void Send(byte[] oBytes)
		{
			this.Send(oBytes, 0, oBytes.Length);
		}
		internal void Send(byte[] oBytes, int iOffset, int iCount)
		{
			if (oBytes == null)
			{
				return;
			}
			if ((long)(iOffset + iCount) > oBytes.LongLength)
			{
				iCount = oBytes.Length - iOffset;
			}
			if (iCount < 1)
			{
				return;
			}
			if (this._iTransmitDelayMS >= 1)
			{
				int num = 1024;
				for (int i = iOffset; i < iOffset + iCount; i += num)
				{
					if (i + num > iOffset + iCount)
					{
						num = iOffset + iCount - i;
					}
					Thread.Sleep(this._iTransmitDelayMS / 2);
					if (this.bIsSecured)
					{
						this._httpsStream.Write(oBytes, i, num);
					}
					else
					{
						this._baseSocket.Send(oBytes, i, num, SocketFlags.None);
					}
					Thread.Sleep(this._iTransmitDelayMS / 2);
				}
				return;
			}
			if (this.bIsSecured)
			{
				this._httpsStream.Write(oBytes, iOffset, iCount);
				return;
			}
			this._baseSocket.Send(oBytes, iOffset, iCount, SocketFlags.None);
		}
		internal IAsyncResult BeginSend(byte[] arrData, int iOffset, int iSize, SocketFlags oSF, AsyncCallback oCB, object oContext)
		{
			if (this.bIsSecured)
			{
				return this._httpsStream.BeginWrite(arrData, iOffset, iSize, oCB, oContext);
			}
			return this._baseSocket.BeginSend(arrData, iOffset, iSize, oSF, oCB, oContext);
		}
		internal void EndSend(IAsyncResult oAR)
		{
			if (this.bIsSecured)
			{
				this._httpsStream.EndWrite(oAR);
				return;
			}
			this._baseSocket.EndSend(oAR);
		}
		internal IAsyncResult BeginReceive(byte[] arrData, int iOffset, int iSize, SocketFlags oSF, AsyncCallback oCB, object oContext)
		{
			if (this.bIsSecured)
			{
				return this._httpsStream.BeginRead(arrData, iOffset, iSize, oCB, oContext);
			}
			return this._baseSocket.BeginReceive(arrData, iOffset, iSize, oSF, oCB, oContext);
		}
		internal int EndReceive(IAsyncResult oAR)
		{
			if (this.bIsSecured)
			{
				return this._httpsStream.EndRead(oAR);
			}
			return this._baseSocket.EndReceive(oAR);
		}
		internal int Receive(byte[] arrBuffer)
		{
			int result;
			if (this.bIsSecured)
			{
				result = this._httpsStream.Read(arrBuffer, 0, arrBuffer.Length);
			}
			else
			{
				result = this._baseSocket.Receive(arrBuffer);
			}
			return result;
		}
		public Socket GetRawSocket()
		{
			return this._baseSocket;
		}
		public void End()
		{
			if (CONFIG.bDebugSpew)
			{
				FiddlerApplication.DebugSpew("Pipe::End() for {0}", new object[]
				{
					this._sPipeName
				});
			}
			try
			{
				if (this._httpsStream != null)
				{
					this._httpsStream.Close();
				}
				if (this._baseSocket != null)
				{
					this._baseSocket.Shutdown(SocketShutdown.Both);
					this._baseSocket.Close();
				}
			}
			catch (Exception)
			{
			}
			this._baseSocket = null;
			this._httpsStream = null;
		}
		public void EndWithRST()
		{
			try
			{
				if (this._baseSocket != null)
				{
					this._baseSocket.LingerState = new LingerOption(true, 0);
					this._baseSocket.Close();
				}
			}
			catch (Exception)
			{
			}
			this._baseSocket = null;
			this._httpsStream = null;
		}
	}
}
