using System;
using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
namespace Fiddler
{
	public class ClientPipe : BasePipe
	{
		internal static int _timeoutReceiveInitial = FiddlerApplication.Prefs.GetInt32Pref("fiddler.network.timeouts.clientpipe.receive.initial", 60000);
		internal static int _timeoutReceiveReused = FiddlerApplication.Prefs.GetInt32Pref("fiddler.network.timeouts.clientpipe.receive.reuse", 30000);
		private static bool _bWantClientCert = FiddlerApplication.Prefs.GetBoolPref("fiddler.network.https.requestclientcertificate", false);
		private string _sProcessName;
		private int _iProcessID;
		private byte[] _arrReceivedAndPutBack;
		public int LocalProcessID
		{
			get
			{
				return this._iProcessID;
			}
		}
		public string LocalProcessName
		{
			get
			{
				return this._sProcessName ?? string.Empty;
			}
		}
		internal DateTime dtAccepted
		{
			get;
			set;
		}
		public override bool HasDataAvailable()
		{
			return this._arrReceivedAndPutBack != null || base.HasDataAvailable();
		}
		internal void putBackSomeBytes(byte[] toPutback)
		{
			this._arrReceivedAndPutBack = new byte[toPutback.Length];
			Buffer.BlockCopy(toPutback, 0, this._arrReceivedAndPutBack, 0, toPutback.Length);
		}
		internal new int Receive(byte[] arrBuffer)
		{
			if (this._arrReceivedAndPutBack == null)
			{
				return base.Receive(arrBuffer);
			}
			int num = this._arrReceivedAndPutBack.Length;
			Buffer.BlockCopy(this._arrReceivedAndPutBack, 0, arrBuffer, 0, num);
			this._arrReceivedAndPutBack = null;
			return num;
		}
		internal ClientPipe(Socket oSocket, DateTime dtCreationTime) : base(oSocket, "C")
		{
			try
			{
				this.dtAccepted = dtCreationTime;
				oSocket.NoDelay = true;
				if (CONFIG.bMapSocketToProcess)
				{
					this._iProcessID = Winsock.MapLocalPortToProcessId(((IPEndPoint)oSocket.RemoteEndPoint).Port);
					if (this._iProcessID > 0)
					{
						this._sProcessName = ProcessHelper.GetProcessName(this._iProcessID);
					}
				}
			}
			catch
			{
			}
		}
		internal void setReceiveTimeout()
		{
			try
			{
				this._baseSocket.ReceiveTimeout = ((this.iUseCount < 2u) ? ClientPipe._timeoutReceiveInitial : ClientPipe._timeoutReceiveReused);
			}
			catch
			{
			}
		}
		public override string ToString()
		{
			return string.Format("[ClientPipe: {0}:{1}; UseCnt: {2}[{3}]; Port: {4}; {5} established {6}]", new object[]
			{
				this._sProcessName,
				this._iProcessID,
				this.iUseCount,
				string.Empty,
				base.Port,
				base.bIsSecured ? "SECURE" : "PLAINTTEXT",
				this.dtAccepted
			});
		}
		internal bool SecureClientPipeDirect(X509Certificate2 certServer)
		{
			try
			{
				FiddlerApplication.DebugSpew(string.Format("SecureClientPipeDirect({0})", certServer.Subject));
				this._httpsStream = new SslStream(new NetworkStream(this._baseSocket, false), false);
				this._httpsStream.AuthenticateAsServer(certServer, ClientPipe._bWantClientCert, CONFIG.oAcceptedClientHTTPSProtocols, false);
				return true;
			}
			catch (AuthenticationException eX)
			{
				FiddlerApplication.Log.LogFormat("!SecureClientPipeDirect failed: {1} on pipe to ({0}).", new object[]
				{
					certServer.Subject,
					Utilities.DescribeException(eX)
				});
				Trace.WriteLine(string.Format("!SecureClientPipeDirect failed: {1} on pipe to ({0}).", certServer.Subject, Utilities.DescribeException(eX)));
				base.End();
			}
			catch (Exception eX2)
			{
				FiddlerApplication.Log.LogFormat("!SecureClientPipeDirect failed: {1} on pipe to ({0})", new object[]
				{
					certServer.Subject,
					Utilities.DescribeException(eX2)
				});
				base.End();
			}
			return false;
		}
		internal bool SecureClientPipe(string sHostname, HTTPResponseHeaders oHeaders)
		{
			X509Certificate2 x509Certificate;
			try
			{
				x509Certificate = CertMaker.FindCert(sHostname);
			}
			catch (Exception ex)
			{
				FiddlerApplication.Log.LogFormat("fiddler.https> Failed to obtain certificate for {0} due to {1}", new object[]
				{
					sHostname,
					ex.Message
				});
				x509Certificate = null;
			}
			try
			{
				if (x509Certificate == null)
				{
					FiddlerApplication.DoNotifyUser("Unable to find Certificate for " + sHostname, "HTTPS Interception Failure");
					oHeaders.SetStatus(502, "Fiddler unable to generate certificate");
				}
				if (CONFIG.bDebugSpew)
				{
					FiddlerApplication.DebugSpew("SecureClientPipe for: " + this.ToString() + " sending data to client:\n" + Utilities.ByteArrayToHexView(oHeaders.ToByteArray(true, true), 32));
				}
				base.Send(oHeaders.ToByteArray(true, true));
				bool result;
				if (oHeaders.HTTPResponseCode != 200)
				{
					FiddlerApplication.DebugSpew("SecureClientPipe returning FALSE because HTTPResponseCode != 200");
					result = false;
					return result;
				}
				this._httpsStream = new SslStream(new NetworkStream(this._baseSocket, false), false);
				this._httpsStream.AuthenticateAsServer(x509Certificate, ClientPipe._bWantClientCert, CONFIG.oAcceptedClientHTTPSProtocols, false);
				result = true;
				return result;
			}
			catch (Exception eX)
			{
				FiddlerApplication.Log.LogFormat("SecureClientPipe ({0} failed: {1}.", new object[]
				{
					sHostname,
					Utilities.DescribeException(eX)
				});
				try
				{
					base.End();
				}
				catch (Exception)
				{
				}
			}
			return false;
		}
	}
}
