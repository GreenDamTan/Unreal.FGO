using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
namespace Fiddler
{
	public class WebSocket : ITunnel
	{
		private ClientPipe oCP;
		private ServerPipe oSP;
		private Session _mySession;
		private string sName = "Unknown";
		private byte[] arrRequestBytes;
		private byte[] arrResponseBytes;
		private int _iMsgCount;
		private MemoryStream strmClientBytes;
		private MemoryStream strmServerBytes;
		public List<WebSocketMessage> listMessages;
		private AutoResetEvent oKeepTunnelAlive;
		private bool bParseMessages = FiddlerApplication.Prefs.GetBoolPref("fiddler.websocket.ParseMessages", true);
		private bool bIsOpen;
		private long _lngEgressByteCount;
		private long _lngIngressByteCount;
		public int MessageCount
		{
			get
			{
				return this.listMessages.Count;
			}
		}
		public bool IsOpen
		{
			get
			{
				return this.bIsOpen;
			}
		}
		internal bool IsBlind
		{
			get
			{
				return !this.bParseMessages;
			}
			set
			{
				this.bParseMessages = !value;
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
		internal bool WriteWebSocketMessageListToStream(Stream oFS)
		{
			oFS.WriteByte(13);
			oFS.WriteByte(10);
			List<WebSocketMessage> obj;
			Monitor.Enter(obj = this.listMessages);
			try
			{
				foreach (WebSocketMessage current in this.listMessages)
				{
					byte[] array = current.ToByteArray();
					string text = current.Timers.ToHeaderString();
					string s = string.Format("{0}: {1}\r\nID: {2}\r\n{3}\r\n", new object[]
					{
						current.IsOutbound ? "Request-Length" : "Response-Length",
						array.Length,
						current.ID,
						text
					});
					byte[] bytes = Encoding.ASCII.GetBytes(s);
					oFS.Write(bytes, 0, bytes.Length);
					oFS.Write(array, 0, array.Length);
					oFS.WriteByte(13);
					oFS.WriteByte(10);
				}
			}
			finally
			{
				Monitor.Exit(obj);
			}
			return true;
		}
		private static string[] _ReadHeadersFromStream(Stream oFS)
		{
			List<byte> list = new List<byte>();
			bool flag = false;
			bool flag2 = true;
			int num = oFS.ReadByte();
			while (-1 != num)
			{
				if (num == 13)
				{
					flag = true;
				}
				else
				{
					if (flag && num == 10)
					{
						if (flag2)
						{
							break;
						}
						flag2 = true;
						list.Add(13);
						list.Add(10);
					}
					else
					{
						flag2 = (flag = false);
						list.Add((byte)num);
					}
				}
				num = oFS.ReadByte();
			}
			string @string = Encoding.ASCII.GetString(list.ToArray());
			return @string.Split(new string[]
			{
				"\r\n"
			}, StringSplitOptions.RemoveEmptyEntries);
		}
		private static DateTime _GetDateTime(string sDateTimeStr)
		{
			DateTime result;
			if (!DateTime.TryParseExact(sDateTimeStr, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out result))
			{
				result = new DateTime(0L);
			}
			return result;
		}
		internal bool ReadWebSocketMessageListFromStream(Stream oFS)
		{
			bool result;
			try
			{
				string[] array = WebSocket._ReadHeadersFromStream(oFS);
				List<WebSocketMessage> list = new List<WebSocketMessage>();
				array = WebSocket._ReadHeadersFromStream(oFS);
				while (array != null && array.Length > 0)
				{
					int num = 0;
					bool bIsOutbound = false;
					DateTime dtDoneRead = new DateTime(0L);
					DateTime dtBeginSend = new DateTime(0L);
					DateTime dtDoneSend = new DateTime(0L);
					string[] array2 = array;
					for (int i = 0; i < array2.Length; i++)
					{
						string text = array2[i];
						if (text.StartsWith("Request-Length:"))
						{
							bIsOutbound = true;
							num = int.Parse(text.Substring(16));
						}
						if (text.StartsWith("Response-Length:"))
						{
							bIsOutbound = false;
							num = int.Parse(text.Substring(17));
						}
						if (text.StartsWith("DoneRead:"))
						{
							dtDoneRead = WebSocket._GetDateTime(text.Substring(10));
						}
						if (text.StartsWith("BeginSend:"))
						{
							dtBeginSend = WebSocket._GetDateTime(text.Substring(11));
						}
						if (text.StartsWith("DoneSend:"))
						{
							dtDoneSend = WebSocket._GetDateTime(text.Substring(10));
						}
					}
					if (num < 1)
					{
						throw new InvalidDataException("Missing size indication.");
					}
					byte[] buffer = new byte[num];
					oFS.Read(buffer, 0, num);
					MemoryStream memoryStream = new MemoryStream(buffer);
					WebSocketMessage[] array3 = WebSocket._ParseMessagesFromStream(this, ref memoryStream, bIsOutbound, false);
					if (array3.Length == 1)
					{
						if (dtDoneRead.Ticks > 0L)
						{
							array3[0].Timers.dtDoneRead = dtDoneRead;
						}
						if (dtBeginSend.Ticks > 0L)
						{
							array3[0].Timers.dtBeginSend = dtBeginSend;
						}
						if (dtDoneSend.Ticks > 0L)
						{
							array3[0].Timers.dtDoneSend = dtDoneSend;
						}
						list.Add(array3[0]);
					}
					if (-1 != oFS.ReadByte())
					{
						if (-1 != oFS.ReadByte())
						{
							array = WebSocket._ReadHeadersFromStream(oFS);
							continue;
						}
					}
					array = null;
				}
				this.listMessages = list;
				result = true;
			}
			catch (Exception)
			{
				result = false;
			}
			return result;
		}
		public override string ToString()
		{
			return string.Format("Session{0}.WebSocket'{1}'", (this._mySession == null) ? -1 : this._mySession.Int32_0, this.sName);
		}
		internal static void LoadWebSocketMessagesFromStream(Session oS, Stream strmWSMessages)
		{
			try
			{
				WebSocket webSocket = new WebSocket(oS, null, null);
				webSocket.sName = string.Format("SAZ-Session#{0}", oS.Int32_0);
				oS.__oTunnel = webSocket;
				webSocket.ReadWebSocketMessageListFromStream(strmWSMessages);
			}
			finally
			{
				strmWSMessages.Dispose();
			}
		}
		internal static void CreateTunnel(Session oSession)
		{
			if (oSession == null || oSession.oRequest == null || oSession.oRequest.headers == null || oSession.oRequest.pipeClient == null)
			{
				return;
			}
			if (oSession.oResponse != null && oSession.oResponse.pipeServer != null)
			{
				ClientPipe pipeClient = oSession.oRequest.pipeClient;
				oSession.oRequest.pipeClient = null;
				ServerPipe pipeServer = oSession.oResponse.pipeServer;
				oSession.oResponse.pipeServer = null;
				WebSocket webSocket = new WebSocket(oSession, pipeClient, pipeServer);
				oSession.__oTunnel = webSocket;
				new Thread(new ThreadStart(webSocket.RunTunnel))
				{
					IsBackground = true
				}.Start();
				return;
			}
		}
		private WebSocket(Session oSess, ClientPipe oFrom, ServerPipe oTo)
		{
			this.sName = "WebSocket #" + oSess.Int32_0.ToString();
			this._mySession = oSess;
			this.oCP = oFrom;
			this.oSP = oTo;
			this._mySession.SetBitFlag(SessionFlags.IsWebSocketTunnel, true);
			if (oSess.oFlags.ContainsKey("x-no-parse"))
			{
				this.bParseMessages = false;
			}
		}
		private void WaitForCompletion()
		{
			this.oKeepTunnelAlive = new AutoResetEvent(false);
			this.oKeepTunnelAlive.WaitOne();
			this.oKeepTunnelAlive.Close();
			this.oKeepTunnelAlive = null;
		}
		private void _CleanupWebSocket()
		{
			this.bIsOpen = false;
			this.arrRequestBytes = (this.arrResponseBytes = null);
			this.strmServerBytes = null;
			this.strmClientBytes = null;
			if (this.oCP != null)
			{
				this.oCP.End();
			}
			if (this.oSP != null)
			{
				this.oSP.End();
			}
			this.oCP = null;
			this.oSP = null;
			if (this._mySession != null)
			{
				if (this._mySession.oResponse != null && this._mySession.oResponse.headers != null)
				{
					this._mySession.oResponse["EndTime"] = DateTime.Now.ToString("HH:mm:ss.fff");
					this._mySession.oResponse["ReceivedBytes"] = this._lngIngressByteCount.ToString();
					this._mySession.oResponse["SentBytes"] = this._lngEgressByteCount.ToString();
				}
				this._mySession.Timers.ServerDoneResponse = (this._mySession.Timers.ClientBeginResponse = (this._mySession.Timers.ClientDoneResponse = DateTime.Now));
				this._mySession = null;
			}
		}
		private void RunTunnel()
		{
			if (FiddlerApplication.oProxy == null)
			{
				return;
			}
			this.arrRequestBytes = new byte[16384];
			this.arrResponseBytes = new byte[16384];
			if (this.bParseMessages)
			{
				this.strmClientBytes = new MemoryStream();
				this.strmServerBytes = new MemoryStream();
				this.listMessages = new List<WebSocketMessage>();
			}
			this.bIsOpen = true;
			try
			{
				this.oCP.BeginReceive(this.arrRequestBytes, 0, this.arrRequestBytes.Length, SocketFlags.None, new AsyncCallback(this.OnReceiveFromClient), this.oCP);
				this.oSP.BeginReceive(this.arrResponseBytes, 0, this.arrResponseBytes.Length, SocketFlags.None, new AsyncCallback(this.OnReceiveFromServer), this.oSP);
				this.WaitForCompletion();
			}
			catch (Exception)
			{
			}
			this.CloseTunnel();
		}
		public void CloseTunnel()
		{
			if (CONFIG.bDebugSpew)
			{
				FiddlerApplication.Log.LogString("Close WebSocket Tunnel: " + Environment.StackTrace);
			}
			try
			{
				if (this.oKeepTunnelAlive != null)
				{
					this.oKeepTunnelAlive.Set();
				}
				else
				{
					this._CleanupWebSocket();
				}
			}
			catch (Exception ex)
			{
				FiddlerApplication.Log.LogString("Error closing oKeepTunnelAlive. " + ex.Message + "\n" + ex.StackTrace);
			}
		}
		private void _PushClientBuffer(int iReadCount)
		{
			this.strmClientBytes.Write(this.arrRequestBytes, 0, iReadCount);
			this._ParseAndSendClientMessages();
		}
		private void _PushServerBuffer(int iReadCount)
		{
			this.strmServerBytes.Write(this.arrResponseBytes, 0, iReadCount);
			this._ParseAndSendServerMessages();
		}
		private static WebSocketMessage[] _ParseMessagesFromStream(WebSocket wsOwner, ref MemoryStream strmData, bool bIsOutbound, bool bTrimAfterParsing)
		{
			List<WebSocketMessage> list = new List<WebSocketMessage>();
			strmData.Position = 0L;
			long num = 0L;
			while (strmData.Length - strmData.Position >= 2L)
			{
				byte[] array = new byte[2];
				strmData.Read(array, 0, array.Length);
				ulong num2 = (ulong)((long)(array[1] & 127));
				if (num2 == 126uL)
				{
					if (strmData.Length < strmData.Position + 2L)
					{
						break;
					}
					byte[] array2 = new byte[2];
					strmData.Read(array2, 0, array2.Length);
					num2 = (ulong)((long)((long)array2[0] << 8) + (long)((ulong)array2[1]));
				}
				else
				{
					if (num2 == 127uL)
					{
						if (strmData.Length < strmData.Position + 8L)
						{
							break;
						}
						byte[] array3 = new byte[8];
						strmData.Read(array3, 0, array3.Length);
						num2 = (ulong)((long)(((int)array3[0] << 24) + ((int)array3[1] << 16) + ((int)array3[2] << 8) + (int)array3[3] + ((int)array3[4] << 24) + ((int)array3[5] << 16) + ((int)array3[6] << 8) + (int)array3[7]));
					}
				}
				bool flag = 128 == (array[1] & 128);
				if (strmData.Length < strmData.Position + (long)num2 + (flag ? 4L : 0L))
				{
					break;
				}
				WebSocketMessage webSocketMessage = new WebSocketMessage(wsOwner, Interlocked.Increment(ref wsOwner._iMsgCount), bIsOutbound);
				webSocketMessage.AssignHeader(array[0]);
				if (flag)
				{
					byte[] array4 = new byte[4];
					strmData.Read(array4, 0, array4.Length);
					webSocketMessage.MaskingKey = array4;
				}
				byte[] array5 = new byte[num2];
				strmData.Read(array5, 0, array5.Length);
				webSocketMessage.PayloadData = array5;
				list.Add(webSocketMessage);
				num = strmData.Position;
			}
			strmData.Position = num;
			if (bTrimAfterParsing)
			{
				byte[] array6 = new byte[strmData.Length - num];
				strmData.Read(array6, 0, array6.Length);
				strmData.Dispose();
				strmData = new MemoryStream();
				strmData.Write(array6, 0, array6.Length);
			}
			return list.ToArray();
		}
		private void _ParseAndSendClientMessages()
		{
			WebSocketMessage[] array = WebSocket._ParseMessagesFromStream(this, ref this.strmClientBytes, true, true);
			WebSocketMessage[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				WebSocketMessage webSocketMessage = array2[i];
				webSocketMessage.Timers.dtDoneRead = DateTime.Now;
				List<WebSocketMessage> obj;
				Monitor.Enter(obj = this.listMessages);
				try
				{
					this.listMessages.Add(webSocketMessage);
				}
				finally
				{
					Monitor.Exit(obj);
				}
				FiddlerApplication.DoOnWebSocketMessage(this._mySession, webSocketMessage);
				if (!webSocketMessage.WasAborted)
				{
					webSocketMessage.Timers.dtBeginSend = DateTime.Now;
					this.oSP.Send(webSocketMessage.ToByteArray());
					webSocketMessage.Timers.dtDoneSend = DateTime.Now;
				}
			}
		}
		private void _ParseAndSendServerMessages()
		{
			WebSocketMessage[] array = WebSocket._ParseMessagesFromStream(this, ref this.strmServerBytes, false, true);
			WebSocketMessage[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				WebSocketMessage webSocketMessage = array2[i];
				webSocketMessage.Timers.dtDoneRead = DateTime.Now;
				List<WebSocketMessage> obj;
				Monitor.Enter(obj = this.listMessages);
				try
				{
					this.listMessages.Add(webSocketMessage);
				}
				finally
				{
					Monitor.Exit(obj);
				}
				FiddlerApplication.DoOnWebSocketMessage(this._mySession, webSocketMessage);
				if (!webSocketMessage.WasAborted)
				{
					webSocketMessage.Timers.dtBeginSend = DateTime.Now;
					this.oCP.Send(webSocketMessage.ToByteArray());
					webSocketMessage.Timers.dtDoneSend = DateTime.Now;
				}
			}
		}
		protected void OnReceiveFromClient(IAsyncResult iasyncResult_0)
		{
			try
			{
				int num = this.oCP.EndReceive(iasyncResult_0);
				if (num > 0)
				{
					this._lngEgressByteCount += (long)num;
					if (this.bParseMessages)
					{
						this._PushClientBuffer(num);
					}
					else
					{
						this.oSP.Send(this.arrRequestBytes, 0, num);
					}
					this.oCP.BeginReceive(this.arrRequestBytes, 0, this.arrRequestBytes.Length, SocketFlags.None, new AsyncCallback(this.OnReceiveFromClient), this.oCP);
				}
				else
				{
					if (this.bParseMessages)
					{
						FiddlerApplication.Log.LogFormat("[{0}] Read from Client returned error: {1}", new object[]
						{
							this.sName,
							num
						});
					}
					this.CloseTunnel();
				}
			}
			catch (Exception ex)
			{
				if (this.bParseMessages)
				{
					FiddlerApplication.Log.LogFormat("[{0}] Read from Client failed... {1}", new object[]
					{
						this.sName,
						ex.Message
					});
				}
				this.CloseTunnel();
			}
		}
		protected void OnReceiveFromServer(IAsyncResult iasyncResult_0)
		{
			try
			{
				int num = this.oSP.EndReceive(iasyncResult_0);
				if (num > 0)
				{
					this._lngIngressByteCount += (long)num;
					if (this.bParseMessages)
					{
						this._PushServerBuffer(num);
					}
					else
					{
						this.oCP.Send(this.arrResponseBytes, 0, num);
					}
					this.oSP.BeginReceive(this.arrResponseBytes, 0, this.arrResponseBytes.Length, SocketFlags.None, new AsyncCallback(this.OnReceiveFromServer), this.oSP);
				}
				else
				{
					if (this.bParseMessages)
					{
						FiddlerApplication.Log.LogFormat("[{0}] Read from Server returned error: {1}", new object[]
						{
							this.sName,
							num
						});
					}
					this.CloseTunnel();
				}
			}
			catch (Exception ex)
			{
				if (this.bParseMessages)
				{
					FiddlerApplication.Log.LogFormat("[{0}] Read from Server failed... {1}", new object[]
					{
						this.sName,
						ex.Message
					});
				}
				this.CloseTunnel();
			}
		}
	}
}
