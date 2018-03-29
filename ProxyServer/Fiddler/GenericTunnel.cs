using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
namespace Fiddler
{
	internal class GenericTunnel : ITunnel
	{
		private ClientPipe pipeToClient;
		private ServerPipe pipeToRemote;
		private bool bResponseStreamStarted;
		private Session _mySession;
		private byte[] arrRequestBytes;
		private byte[] arrResponseBytes;
		private AutoResetEvent oKeepTunnelAlive;
		private bool bIsOpen = true;
		private long _lngEgressByteCount;
		private long _lngIngressByteCount;
		public bool IsOpen
		{
			get
			{
				return this.bIsOpen;
			}
		}
		public long IngressByteCount
		{
			get
			{
				return this._lngIngressByteCount;
			}
		}
		public long EgressByteCount
		{
			get
			{
				return this._lngEgressByteCount;
			}
		}
		internal static void CreateTunnel(Session oSession, bool bStreamResponse)
		{
			if (oSession == null || oSession.oRequest == null || oSession.oRequest.headers == null || oSession.oRequest.pipeClient == null || oSession.oResponse == null)
			{
				return;
			}
			ClientPipe pipeClient = oSession.oRequest.pipeClient;
			if (pipeClient == null)
			{
				return;
			}
			if (bStreamResponse)
			{
				oSession.oRequest.pipeClient = null;
			}
			ServerPipe pipeServer = oSession.oResponse.pipeServer;
			if (pipeServer == null)
			{
				return;
			}
			if (bStreamResponse)
			{
				oSession.oResponse.pipeServer = null;
			}
			GenericTunnel genericTunnel = new GenericTunnel(oSession, pipeClient, pipeServer, bStreamResponse);
			oSession.__oTunnel = genericTunnel;
			new Thread(new ThreadStart(genericTunnel.RunTunnel))
			{
				IsBackground = true
			}.Start();
		}
		private GenericTunnel(Session oSess, ClientPipe oFrom, ServerPipe oTo, bool bStreamResponse)
		{
			this._mySession = oSess;
			this.pipeToClient = oFrom;
			this.pipeToRemote = oTo;
			this.bResponseStreamStarted = bStreamResponse;
			this._mySession.SetBitFlag(SessionFlags.IsBlindTunnel, true);
			Trace.WriteLine("[GenericTunnel] For session #" + this._mySession.Int32_0.ToString() + " created...");
		}
		private void WaitForCompletion()
		{
			this.oKeepTunnelAlive = new AutoResetEvent(false);
			Trace.WriteLine("[GenericTunnel] Blocking thread...");
			this.oKeepTunnelAlive.WaitOne();
			Trace.WriteLine("[GenericTunnel] Unblocking thread...");
			this.oKeepTunnelAlive.Close();
			this.oKeepTunnelAlive = null;
			this.bIsOpen = false;
			this.arrRequestBytes = (this.arrResponseBytes = null);
			this.pipeToClient = null;
			this.pipeToRemote = null;
			Trace.WriteLine("[GenericTunnel] Thread for session #" + this._mySession.Int32_0.ToString() + " has died...");
			if (this._mySession.oResponse != null && this._mySession.oResponse.headers != null)
			{
				this._mySession.oResponse.headers["EndTime"] = DateTime.Now.ToString("HH:mm:ss.fff");
				this._mySession.oResponse.headers["ClientToServerBytes"] = this._lngEgressByteCount.ToString();
				this._mySession.oResponse.headers["ServerToClientBytes"] = this._lngIngressByteCount.ToString();
			}
			this._mySession.Timers.ServerDoneResponse = (this._mySession.Timers.ClientBeginResponse = (this._mySession.Timers.ClientDoneResponse = DateTime.Now));
			this._mySession.state = SessionStates.Done;
			this._mySession = null;
		}
		private void RunTunnel()
		{
			if (FiddlerApplication.oProxy == null)
			{
				return;
			}
			try
			{
				this.DoTunnel();
			}
			catch (Exception eX)
			{
				FiddlerApplication.ReportException(eX, "Uncaught Exception in Tunnel; Session #" + this._mySession.Int32_0.ToString());
			}
		}
		private void DoTunnel()
		{
			if (FiddlerApplication.oProxy == null)
			{
				return;
			}
			this.arrRequestBytes = new byte[16384];
			this.arrResponseBytes = new byte[16384];
			this.bIsOpen = true;
			Trace.WriteLine(string.Format("Generic Tunnel for Session #{0}, Response State: {1}, created between\n\t{2}\nand\n\t{3}", new object[]
			{
				this._mySession.Int32_0,
				this.bResponseStreamStarted ? "Streaming" : "Blocked",
				this.pipeToClient,
				this.pipeToRemote
			}));
			try
			{
				this.pipeToClient.BeginReceive(this.arrRequestBytes, 0, this.arrRequestBytes.Length, SocketFlags.None, new AsyncCallback(this.OnClientReceive), null);
				if (this.bResponseStreamStarted)
				{
					this.pipeToRemote.BeginReceive(this.arrResponseBytes, 0, this.arrResponseBytes.Length, SocketFlags.None, new AsyncCallback(this.OnRemoteReceive), null);
				}
				this.WaitForCompletion();
			}
			catch (Exception)
			{
			}
			this.CloseTunnel();
		}
		internal void BeginResponseStreaming()
		{
			Trace.WriteLine(">>> Begin response streaming in GenericTunnel for Session #" + this._mySession.Int32_0);
			this.bResponseStreamStarted = true;
			this._mySession.oResponse.pipeServer = null;
			this._mySession.oRequest.pipeClient = null;
			this.pipeToRemote.BeginReceive(this.arrResponseBytes, 0, this.arrResponseBytes.Length, SocketFlags.None, new AsyncCallback(this.OnRemoteReceive), null);
		}
		public void CloseTunnel()
		{
			Trace.WriteLine("Close Generic Tunnel for Session #" + ((this._mySession != null) ? this._mySession.Int32_0.ToString() : "<unassigned>"));
			try
			{
				if (this.pipeToClient != null)
				{
					this.pipeToClient.End();
				}
			}
			catch (Exception ex)
			{
				Trace.WriteLine("Error closing gatewayFrom tunnel. " + ex.Message + "\n" + ex.StackTrace);
			}
			try
			{
				if (this.pipeToRemote != null)
				{
					this.pipeToRemote.End();
				}
			}
			catch (Exception ex2)
			{
				Trace.WriteLine("Error closing gatewayTo tunnel. " + ex2.Message + "\n" + ex2.StackTrace);
			}
			try
			{
				if (this.oKeepTunnelAlive != null)
				{
					this.oKeepTunnelAlive.Set();
				}
			}
			catch (Exception ex3)
			{
				Trace.WriteLine("Error closing oKeepTunnelAlive. " + ex3.Message + "\n" + ex3.StackTrace);
			}
		}
		protected void OnClientReceive(IAsyncResult iasyncResult_0)
		{
			try
			{
				int num = this.pipeToClient.EndReceive(iasyncResult_0);
				if (num > 0)
				{
					this._lngEgressByteCount += (long)num;
					Trace.WriteLine("[GenericTunnel] Received from client: " + num.ToString() + " bytes. Sending to server...");
					Trace.WriteLine(Utilities.ByteArrayToHexView(this.arrRequestBytes, 16, num));
					FiddlerApplication.DoReadRequestBuffer(this._mySession, this.arrRequestBytes, num);
					this.pipeToRemote.Send(this.arrRequestBytes, 0, num);
					this.pipeToClient.BeginReceive(this.arrRequestBytes, 0, this.arrRequestBytes.Length, SocketFlags.None, new AsyncCallback(this.OnClientReceive), null);
				}
				else
				{
					FiddlerApplication.DoReadRequestBuffer(this._mySession, this.arrRequestBytes, 0);
					this.CloseTunnel();
				}
			}
			catch (Exception ex)
			{
				Trace.WriteLine("[GenericTunnel] OnClientReceive threw... " + ex.Message);
				this.CloseTunnel();
			}
		}
		protected void OnClientSent(IAsyncResult iasyncResult_0)
		{
			try
			{
				Trace.WriteLine("OnClientSent...");
				this.pipeToClient.EndSend(iasyncResult_0);
			}
			catch (Exception ex)
			{
				Trace.WriteLine("[GenericTunnel] OnClientSent failed... " + ex.Message);
				this.CloseTunnel();
			}
		}
		protected void OnRemoteSent(IAsyncResult iasyncResult_0)
		{
			try
			{
				Trace.WriteLine("OnRemoteSent...");
				this.pipeToRemote.EndSend(iasyncResult_0);
			}
			catch (Exception ex)
			{
				Trace.WriteLine("[GenericTunnel] OnRemoteSent failed... " + ex.Message);
				this.CloseTunnel();
			}
		}
		protected void OnRemoteReceive(IAsyncResult iasyncResult_0)
		{
			try
			{
				int num = this.pipeToRemote.EndReceive(iasyncResult_0);
				if (num > 0)
				{
					this._lngIngressByteCount += (long)num;
					Trace.WriteLine("[GenericTunnel] Received from server: " + num.ToString() + " bytes. Sending to client...");
					Trace.WriteLine(Utilities.ByteArrayToHexView(this.arrResponseBytes, 16, num));
					FiddlerApplication.DoReadResponseBuffer(this._mySession, this.arrResponseBytes, num);
					this.pipeToClient.Send(this.arrResponseBytes, 0, num);
					this.pipeToRemote.BeginReceive(this.arrResponseBytes, 0, this.arrResponseBytes.Length, SocketFlags.None, new AsyncCallback(this.OnRemoteReceive), null);
				}
				else
				{
					Trace.WriteLine("[GenericTunnel] ReadFromRemote failed, ret=" + num.ToString());
					FiddlerApplication.DoReadResponseBuffer(this._mySession, this.arrResponseBytes, 0);
					this.CloseTunnel();
				}
			}
			catch (Exception ex)
			{
				Trace.WriteLine("[GenericTunnel] OnRemoteReceive failed... " + ex.Message);
				this.CloseTunnel();
			}
		}
	}
}
