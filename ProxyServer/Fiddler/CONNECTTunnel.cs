using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
namespace Fiddler
{
	internal class CONNECTTunnel : ITunnel
	{
		private Socket socketRemote;
		private Socket socketClient;
		private ClientPipe pipeTunnelClient;
		private ServerPipe pipeTunnelRemote;
		private Session _mySession;
		private byte[] arrRequestBytes;
		private byte[] arrResponseBytes;
		private AutoResetEvent oKeepTunnelAlive;
		private bool bIsOpen = true;
		private long _lngEgressByteCount;
		private long _lngIngressByteCount;
		private bool bIsBlind;
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
		internal static void CreateTunnel(Session oSession)
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
			oSession.oRequest.pipeClient = null;
			ServerPipe pipeServer = oSession.oResponse.pipeServer;
			if (pipeServer == null)
			{
				return;
			}
			oSession.oResponse.pipeServer = null;
			CONNECTTunnel cONNECTTunnel = new CONNECTTunnel(oSession, pipeClient, pipeServer);
			oSession.__oTunnel = cONNECTTunnel;
			new Thread(new ThreadStart(cONNECTTunnel.RunTunnel))
			{
				IsBackground = true
			}.Start();
		}
		private CONNECTTunnel(Session oSess, ClientPipe oFrom, ServerPipe oTo)
		{
			this._mySession = oSess;
			this.pipeTunnelClient = oFrom;
			this.pipeTunnelRemote = oTo;
			this._mySession.SetBitFlag(SessionFlags.IsBlindTunnel, true);
		}
		private void WaitForCompletion()
		{
			this.oKeepTunnelAlive = new AutoResetEvent(false);
			this.oKeepTunnelAlive.WaitOne();
			this.oKeepTunnelAlive.Close();
			this.oKeepTunnelAlive = null;
			this.bIsOpen = false;
			this.arrRequestBytes = (this.arrResponseBytes = null);
			this.pipeTunnelClient = null;
			this.pipeTunnelRemote = null;
			this.socketClient = (this.socketRemote = null);
			if (this._mySession.oResponse != null && this._mySession.oResponse.headers != null)
			{
				this._mySession.oResponse.headers["EndTime"] = DateTime.Now.ToString("HH:mm:ss.fff");
				this._mySession.oResponse.headers["ClientToServerBytes"] = this._lngEgressByteCount.ToString();
				this._mySession.oResponse.headers["ServerToClientBytes"] = this._lngIngressByteCount.ToString();
			}
			this._mySession.Timers.ServerDoneResponse = (this._mySession.Timers.ClientBeginResponse = (this._mySession.Timers.ClientDoneResponse = DateTime.Now));
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
			try
			{
				this.bIsBlind = (!CONFIG.bMITM_HTTPS || this._mySession.oFlags.ContainsKey("x-no-decrypt"));
				if (!this.bIsBlind)
				{
					this.bIsBlind = (CONFIG.oHLSkipDecryption != null && CONFIG.oHLSkipDecryption.ContainsHost(this._mySession.PathAndQuery));
				}
				if (!this.bIsBlind && CONFIG.DecryptWhichProcesses != ProcessFilterCategories.All)
				{
					string text = this._mySession.oFlags["x-ProcessInfo"];
					if (CONFIG.DecryptWhichProcesses == ProcessFilterCategories.HideAll)
					{
						if (!string.IsNullOrEmpty(text))
						{
							this.bIsBlind = true;
						}
					}
					else
					{
						if (!string.IsNullOrEmpty(text))
						{
							bool flag = Utilities.IsBrowserProcessName(text);
							if ((CONFIG.DecryptWhichProcesses == ProcessFilterCategories.Browsers && !flag) || (CONFIG.DecryptWhichProcesses == ProcessFilterCategories.NonBrowsers && flag))
							{
								this.bIsBlind = true;
							}
						}
					}
				}
				bool flag2;
				string text3;
				X509Certificate2 x509Certificate;
				while (true)
				{
					this._mySession.SetBitFlag(SessionFlags.IsDecryptingTunnel, !this.bIsBlind);
					this._mySession.SetBitFlag(SessionFlags.IsBlindTunnel, this.bIsBlind);
					if (this.bIsBlind)
					{
						break;
					}
					flag2 = false;
					if (!this._mySession.oFlags.ContainsKey("x-OverrideCertCN"))
					{
						if (CONFIG.bUseSNIForCN)
						{
							string text2 = this._mySession.oFlags["https-Client-SNIHostname"];
							if (!string.IsNullOrEmpty(text2) && text2 != this._mySession.hostname)
							{
								this._mySession.oFlags["x-OverrideCertCN"] = this._mySession.oFlags["https-Client-SNIHostname"];
							}
						}
						if (this._mySession.oFlags["x-OverrideCertCN"] == null && this._mySession.oFlags.ContainsKey("x-UseCertCNFromServer"))
						{
							if (!this.pipeTunnelRemote.SecureExistingConnection(this._mySession, this._mySession.hostname, this._mySession.oFlags["https-Client-Certificate"], ref this._mySession.Timers.HTTPSHandshakeTime))
							{
								goto IL_2F2;
							}
							flag2 = true;
							string serverCertCN = this.pipeTunnelRemote.GetServerCertCN();
							if (!string.IsNullOrEmpty(serverCertCN))
							{
								this._mySession.oFlags["x-OverrideCertCN"] = serverCertCN;
							}
						}
					}
					text3 = (this._mySession.oFlags["x-OverrideCertCN"] ?? this._mySession.hostname);
					try
					{
						x509Certificate = CertMaker.FindCert(text3);
						if (x509Certificate == null)
						{
							throw new Exception("Certificate Maker returned null when asked for a certificate for " + text3);
						}
						goto IL_2FD;
					}
					catch (Exception ex)
					{
						x509Certificate = null;
						FiddlerApplication.Log.LogFormat("fiddler.https> Failed to obtain certificate for {0} due to {1}", new object[]
						{
							text3,
							ex.Message
						});
						this._mySession.oFlags["x-HTTPS-Decryption-Error"] = "Could not find or generate interception certificate.";
						if (flag2 || !FiddlerApplication.Prefs.GetBoolPref("fiddler.network.https.blindtunnelifcertunobtainable", true))
						{
							goto IL_2FD;
						}
						this.bIsBlind = true;
					}
				}
				this.DoBlindTunnel();
				return;
				IL_2F2:
				throw new Exception("HTTPS Early-Handshaking to server did not succeed.");
				IL_2FD:
				if (!this.pipeTunnelClient.SecureClientPipeDirect(x509Certificate))
				{
					throw new Exception("HTTPS Handshaking to client did not succeed.");
				}
				this._mySession["https-Client-Version"] = this.pipeTunnelClient.SecureProtocol.ToString();
				if (!flag2 && !this.pipeTunnelRemote.SecureExistingConnection(this._mySession, text3, this._mySession.oFlags["https-Client-Certificate"], ref this._mySession.Timers.HTTPSHandshakeTime))
				{
					throw new Exception("HTTPS Handshaking to server did not succeed.");
				}
				this._mySession.responseBodyBytes = Encoding.UTF8.GetBytes("Encrypted HTTPS traffic flows through this CONNECT tunnel. HTTPS Decryption is enabled in Fiddler, so decrypted sessions running in this tunnel will be shown in the Web Sessions list.\n\n" + this.pipeTunnelRemote.DescribeConnectionSecurity());
				this._mySession["https-Server-Cipher"] = this.pipeTunnelRemote.GetConnectionCipherInfo();
				this._mySession["https-Server-Version"] = this.pipeTunnelRemote.SecureProtocol.ToString();
				Session session = new Session(this.pipeTunnelClient, this.pipeTunnelRemote);
				session.oFlags["x-serversocket"] = this._mySession.oFlags["x-securepipe"];
				if (this.pipeTunnelRemote != null && this.pipeTunnelRemote.Address != null)
				{
					session.m_hostIP = this.pipeTunnelRemote.Address.ToString();
					session.oFlags["x-hostIP"] = session.m_hostIP;
					session.oFlags["x-EgressPort"] = this.pipeTunnelRemote.LocalPort.ToString();
				}
				session.Execute(null);
			}
			catch (Exception)
			{
				try
				{
					this.pipeTunnelClient.End();
					this.pipeTunnelRemote.End();
				}
				catch (Exception)
				{
				}
			}
		}
		private void DoBlindTunnel()
		{
			this.arrRequestBytes = new byte[16384];
			this.arrResponseBytes = new byte[16384];
			this.socketClient = this.pipeTunnelClient.GetRawSocket();
			this.socketRemote = this.pipeTunnelRemote.GetRawSocket();
			this.socketClient.BeginReceive(this.arrRequestBytes, 0, this.arrRequestBytes.Length, SocketFlags.None, new AsyncCallback(this.OnClientReceive), this.socketClient);
			this.socketRemote.BeginReceive(this.arrResponseBytes, 0, this.arrResponseBytes.Length, SocketFlags.None, new AsyncCallback(this.OnRemoteReceive), this.socketRemote);
			this.WaitForCompletion();
		}
		public void CloseTunnel()
		{
			try
			{
				if (this.pipeTunnelClient != null)
				{
					this.pipeTunnelClient.End();
				}
			}
			catch (Exception)
			{
			}
			try
			{
				if (this.pipeTunnelRemote != null)
				{
					this.pipeTunnelRemote.End();
				}
			}
			catch (Exception)
			{
			}
			try
			{
				if (this.oKeepTunnelAlive != null)
				{
					this.oKeepTunnelAlive.Set();
				}
			}
			catch (Exception)
			{
			}
		}
		protected void OnClientReceive(IAsyncResult iasyncResult_0)
		{
			try
			{
				int num = this.socketClient.EndReceive(iasyncResult_0);
				if (num > 0)
				{
					this._lngEgressByteCount += (long)num;
					FiddlerApplication.DoReadRequestBuffer(this._mySession, this.arrRequestBytes, num);
					if (this._mySession.requestBodyBytes != null)
					{
						if (this._mySession.requestBodyBytes.LongLength != 0L)
						{
							goto IL_FF;
						}
					}
					try
					{
						HTTPSClientHello hTTPSClientHello = new HTTPSClientHello();
						if (hTTPSClientHello.LoadFromStream(new MemoryStream(this.arrRequestBytes, 0, num, false)))
						{
							this._mySession.requestBodyBytes = Encoding.UTF8.GetBytes(hTTPSClientHello.ToString() + "\n");
							this._mySession["https-Client-SessionID"] = hTTPSClientHello.SessionID;
							if (!string.IsNullOrEmpty(hTTPSClientHello.ServerNameIndicator))
							{
								this._mySession["https-Client-SNIHostname"] = hTTPSClientHello.ServerNameIndicator;
							}
						}
					}
					catch (Exception ex)
					{
						this._mySession.requestBodyBytes = Encoding.UTF8.GetBytes("Request HTTPSParse failed: " + ex.Message);
					}
					IL_FF:
					this.socketRemote.BeginSend(this.arrRequestBytes, 0, num, SocketFlags.None, new AsyncCallback(this.OnRemoteSent), this.socketRemote);
				}
				else
				{
					FiddlerApplication.DoReadRequestBuffer(this._mySession, this.arrRequestBytes, 0);
					this.CloseTunnel();
				}
			}
			catch (Exception)
			{
				this.CloseTunnel();
			}
		}
		protected void OnClientSent(IAsyncResult iasyncResult_0)
		{
			try
			{
				int num = this.socketClient.EndSend(iasyncResult_0);
				if (num > 0)
				{
					this.socketRemote.BeginReceive(this.arrResponseBytes, 0, this.arrResponseBytes.Length, SocketFlags.None, new AsyncCallback(this.OnRemoteReceive), this.socketRemote);
				}
			}
			catch (Exception)
			{
			}
		}
		protected void OnRemoteSent(IAsyncResult iasyncResult_0)
		{
			try
			{
				int num = this.socketRemote.EndSend(iasyncResult_0);
				if (num > 0)
				{
					this.socketClient.BeginReceive(this.arrRequestBytes, 0, this.arrRequestBytes.Length, SocketFlags.None, new AsyncCallback(this.OnClientReceive), this.socketClient);
				}
			}
			catch (Exception)
			{
			}
		}
		protected void OnRemoteReceive(IAsyncResult iasyncResult_0)
		{
			try
			{
				int num = this.socketRemote.EndReceive(iasyncResult_0);
				if (num > 0)
				{
					this._lngIngressByteCount += (long)num;
					FiddlerApplication.DoReadResponseBuffer(this._mySession, this.arrResponseBytes, num);
					if (this._mySession.responseBodyBytes != null)
					{
						if (this._mySession.responseBodyBytes.LongLength != 0L)
						{
							goto IL_107;
						}
					}
					try
					{
						HTTPSServerHello hTTPSServerHello = new HTTPSServerHello();
						if (hTTPSServerHello.LoadFromStream(new MemoryStream(this.arrResponseBytes, 0, num, false)))
						{
							string s = string.Format("This is a CONNECT tunnel, through which encrypted HTTPS traffic flows.\n{0}\n\n{1}\n", CONFIG.bMITM_HTTPS ? "Fiddler's HTTPS Decryption feature is enabled, but this specific tunnel was configured not to be decrypted. Settings can be found inside Tools > Fiddler Options > HTTPS." : "To view the encrypted sessions inside this tunnel, enable the Tools > Fiddler Options > HTTPS > Decrypt HTTPS traffic option.", hTTPSServerHello.ToString());
							this._mySession.responseBodyBytes = Encoding.UTF8.GetBytes(s);
							this._mySession["https-Server-SessionID"] = hTTPSServerHello.SessionID;
							this._mySession["https-Server-Cipher"] = hTTPSServerHello.CipherSuite;
						}
					}
					catch (Exception ex)
					{
						this._mySession.requestBodyBytes = Encoding.UTF8.GetBytes("Response HTTPSParse failed: " + ex.Message);
					}
					IL_107:
					this.socketClient.BeginSend(this.arrResponseBytes, 0, num, SocketFlags.None, new AsyncCallback(this.OnClientSent), this.socketClient);
				}
				else
				{
					FiddlerApplication.DoReadResponseBuffer(this._mySession, this.arrResponseBytes, 0);
					this.CloseTunnel();
				}
			}
			catch (Exception)
			{
				this.CloseTunnel();
			}
		}
	}
}
