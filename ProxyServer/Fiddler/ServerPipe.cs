using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
namespace Fiddler
{
	public class ServerPipe : BasePipe
	{
		internal static int _timeoutSendInitial = FiddlerApplication.Prefs.GetInt32Pref("fiddler.network.timeouts.serverpipe.send.initial", -1);
		internal static int _timeoutSendReused = FiddlerApplication.Prefs.GetInt32Pref("fiddler.network.timeouts.serverpipe.send.reuse", -1);
		internal static int _timeoutReceiveInitial = FiddlerApplication.Prefs.GetInt32Pref("fiddler.network.timeouts.serverpipe.receive.initial", -1);
		internal static int _timeoutReceiveReused = FiddlerApplication.Prefs.GetInt32Pref("fiddler.network.timeouts.serverpipe.receive.reuse", -1);
		private static StringCollection slAcceptableBadCertificates;
		private PipeReusePolicy _reusePolicy;
		internal DateTime dtConnected;
		internal ulong ulLastPooled;
		protected bool _bIsConnectedToGateway;
		private bool _bIsConnectedViaSOCKS;
		protected string _sPoolKey;
		private int _iMarriedToPID;
		private bool _isAuthenticated;
		private string _ServerCertChain;
		public PipeReusePolicy ReusePolicy
		{
			get
			{
				return this._reusePolicy;
			}
			set
			{
				this._reusePolicy = value;
			}
		}
		internal bool isClientCertAttached
		{
			get
			{
				return this._httpsStream != null && this._httpsStream.IsMutuallyAuthenticated;
			}
		}
		internal bool isAuthenticated
		{
			get
			{
				return this._isAuthenticated;
			}
		}
		public bool isConnectedToGateway
		{
			get
			{
				return this._bIsConnectedToGateway;
			}
		}
		public bool isConnectedViaSOCKS
		{
			get
			{
				return this._bIsConnectedViaSOCKS;
			}
			set
			{
				this._bIsConnectedViaSOCKS = value;
			}
		}
		public string sPoolKey
		{
			get
			{
				return this._sPoolKey;
			}
			private set
			{
				if (CONFIG.bDebugSpew && !string.IsNullOrEmpty(this._sPoolKey) && this._sPoolKey != value)
				{
					FiddlerApplication.Log.LogFormat("fiddler.pipes>{0} pooling key changing from '{1}' to '{2}'", new object[]
					{
						this._sPipeName,
						this._sPoolKey,
						value
					});
				}
				this._sPoolKey = value;
			}
		}
		public IPEndPoint RemoteEndPoint
		{
			get
			{
				if (this._baseSocket == null)
				{
					return null;
				}
				return this._baseSocket.RemoteEndPoint as IPEndPoint;
			}
		}
		internal ServerPipe(Socket oSocket, string sName, bool bConnectedToGateway, string sPoolingKey) : base(oSocket, sName)
		{
			this.dtConnected = DateTime.Now;
			this._bIsConnectedToGateway = bConnectedToGateway;
			this.sPoolKey = sPoolingKey;
		}
		internal void MarkAsAuthenticated(int clientPID)
		{
			this._isAuthenticated = true;
			int num = FiddlerApplication.Prefs.GetInt32Pref("fiddler.network.auth.reusemode", 0);
			if (num == 0 && clientPID == 0)
			{
				num = 1;
			}
			if (num == 0)
			{
				this.ReusePolicy = PipeReusePolicy.MarriedToClientProcess;
				this._iMarriedToPID = clientPID;
				this.sPoolKey = string.Format("PID{0}*{1}", clientPID, this.sPoolKey);
				return;
			}
			if (num == 1)
			{
				this.ReusePolicy = PipeReusePolicy.MarriedToClientPipe;
			}
		}
		internal void setTimeouts()
		{
			try
			{
				int num = (this.iUseCount < 2u) ? ServerPipe._timeoutReceiveInitial : ServerPipe._timeoutReceiveReused;
				int num2 = (this.iUseCount < 2u) ? ServerPipe._timeoutSendInitial : ServerPipe._timeoutSendReused;
				if (num > 0)
				{
					this._baseSocket.ReceiveTimeout = num;
				}
				if (num2 > 0)
				{
					this._baseSocket.SendTimeout = num2;
				}
			}
			catch
			{
			}
		}
		public override string ToString()
		{
			return string.Format("{0}[Key: {1}; UseCnt: {2} [{3}]; {4}; {5} (:{6} to {7}:{8} {9}) {10}]", new object[]
			{
				this._sPipeName,
				this._sPoolKey,
				this.iUseCount,
				string.Empty,
				base.bIsSecured ? "Secure" : "PlainText",
				this._isAuthenticated ? "Authenticated" : "Anonymous",
				base.LocalPort,
				base.Address,
				base.Port,
				this.isConnectedToGateway ? "Gateway" : "Direct",
				this._reusePolicy
			});
		}
		private static string SummarizeCert(X509Certificate2 oCert)
		{
			if (!string.IsNullOrEmpty(oCert.FriendlyName))
			{
				return oCert.FriendlyName;
			}
			string subject = oCert.Subject;
			if (string.IsNullOrEmpty(subject))
			{
				return string.Empty;
			}
			if (subject.Contains("CN="))
			{
				return Utilities.TrimAfter(Utilities.TrimBefore(subject, "CN="), ",");
			}
			if (subject.Contains("O="))
			{
				return Utilities.TrimAfter(Utilities.TrimBefore(subject, "O="), ",");
			}
			return subject;
		}
		internal string GetServerCertCN()
		{
			if (this._httpsStream == null)
			{
				return null;
			}
			if (this._httpsStream.RemoteCertificate == null)
			{
				return null;
			}
			string subject = this._httpsStream.RemoteCertificate.Subject;
			if (subject.Contains("CN="))
			{
				return Utilities.TrimAfter(Utilities.TrimBefore(subject, "CN="), ",");
			}
			return subject;
		}
		internal string GetServerCertChain()
		{
			if (this._ServerCertChain != null)
			{
				return this._ServerCertChain;
			}
			if (this._httpsStream == null)
			{
				return string.Empty;
			}
			string result;
			try
			{
				X509Certificate2 x509Certificate = new X509Certificate2(this._httpsStream.RemoteCertificate);
				if (x509Certificate == null)
				{
					result = string.Empty;
				}
				else
				{
					StringBuilder stringBuilder = new StringBuilder();
					X509Chain x509Chain = new X509Chain();
					x509Chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
					x509Chain.Build(x509Certificate);
					for (int i = x509Chain.ChainElements.Count - 1; i >= 1; i--)
					{
						stringBuilder.Append(ServerPipe.SummarizeCert(x509Chain.ChainElements[i].Certificate));
						stringBuilder.Append(" > ");
					}
					if (x509Chain.ChainElements.Count > 0)
					{
						stringBuilder.AppendFormat("{0} [{1}]", ServerPipe.SummarizeCert(x509Chain.ChainElements[0].Certificate), x509Chain.ChainElements[0].Certificate.SerialNumber);
					}
					this._ServerCertChain = stringBuilder.ToString();
					result = stringBuilder.ToString();
				}
			}
			catch (Exception ex)
			{
				result = ex.Message;
			}
			return result;
		}
		public string DescribeConnectionSecurity()
		{
			if (this._httpsStream != null)
			{
				string value = string.Empty;
				if (this._httpsStream.IsMutuallyAuthenticated)
				{
					value = "== Client Certificate ==========\nUnknown.\n";
				}
				if (this._httpsStream.LocalCertificate != null)
				{
					value = "\n== Client Certificate ==========\n" + this._httpsStream.LocalCertificate.ToString(true) + "\n";
				}
				StringBuilder stringBuilder = new StringBuilder(2048);
				stringBuilder.AppendFormat("Secure Protocol: {0}\n", this._httpsStream.SslProtocol.ToString());
				stringBuilder.AppendFormat("Cipher: {0} {1}bits\n", this._httpsStream.CipherAlgorithm.ToString(), this._httpsStream.CipherStrength);
				stringBuilder.AppendFormat("Hash Algorithm: {0} {1}bits\n", this._httpsStream.HashAlgorithm.ToString(), this._httpsStream.HashStrength);
				string text = this._httpsStream.KeyExchangeAlgorithm.ToString();
				if (text == "44550")
				{
					text = "ECDHE_RSA (0xae06)";
				}
				stringBuilder.AppendFormat("Key Exchange: {0} {1}bits\n", text, this._httpsStream.KeyExchangeStrength);
				stringBuilder.Append(value);
				stringBuilder.AppendLine("\n== Server Certificate ==========");
				stringBuilder.AppendLine(this._httpsStream.RemoteCertificate.ToString(true));
				if (FiddlerApplication.Prefs.GetBoolPref("fiddler.network.https.storeservercertchain", false))
				{
					stringBuilder.AppendFormat("[Chain]\n {0}\n", this.GetServerCertChain());
				}
				return stringBuilder.ToString();
			}
			return "No connection security";
		}
		internal string GetConnectionCipherInfo()
		{
			if (this._httpsStream == null)
			{
				return "<none>";
			}
			return string.Format("{0} {1}bits", this._httpsStream.CipherAlgorithm.ToString(), this._httpsStream.CipherStrength);
		}
		internal TransportContext _GetTransportContext()
		{
			if (this._httpsStream != null)
			{
				return this._httpsStream.TransportContext;
			}
			return null;
		}
		private static bool ConfirmServerCertificate(Session oS, string sExpectedCN, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			CertificateValidity certificateValidity = CertificateValidity.Default;
			FiddlerApplication.CheckOverrideCertificatePolicy(oS, sExpectedCN, certificate, chain, sslPolicyErrors, ref certificateValidity);
			if (certificateValidity == CertificateValidity.ForceInvalid)
			{
				return false;
			}
			if (certificateValidity == CertificateValidity.ForceValid)
			{
				return true;
			}
			if ((certificateValidity != CertificateValidity.ConfirmWithUser && (sslPolicyErrors == SslPolicyErrors.None || CONFIG.IgnoreServerCertErrors)) || oS.oFlags.ContainsKey("X-IgnoreCertErrors"))
			{
				return true;
			}
			if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateNameMismatch) == SslPolicyErrors.RemoteCertificateNameMismatch && oS.oFlags.ContainsKey("X-IgnoreCertCNMismatch"))
			{
				sslPolicyErrors &= ~SslPolicyErrors.RemoteCertificateNameMismatch;
				if (sslPolicyErrors == SslPolicyErrors.None)
				{
					return true;
				}
			}
			return false;
		}
		private static X509Certificate _GetDefaultCertificate()
		{
			if (FiddlerApplication.oDefaultClientCertificate != null)
			{
				return FiddlerApplication.oDefaultClientCertificate;
			}
			X509Certificate x509Certificate = null;
			if (File.Exists(CONFIG.GetPath("DefaultClientCertificate")))
			{
				x509Certificate = X509Certificate.CreateFromCertFile(CONFIG.GetPath("DefaultClientCertificate"));
				if (x509Certificate != null && FiddlerApplication.Prefs.GetBoolPref("fiddler.network.https.cacheclientcert", true))
				{
					FiddlerApplication.oDefaultClientCertificate = x509Certificate;
				}
			}
			return x509Certificate;
		}
		private X509Certificate AttachClientCertificate(Session oS, object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers)
		{
			if (localCertificates.Count > 0)
			{
				this.MarkAsAuthenticated(oS.LocalProcessID);
				oS.oFlags["x-client-cert"] = localCertificates[0].Subject + " Serial#" + localCertificates[0].GetSerialNumberString();
				return localCertificates[0];
			}
			if (remoteCertificate == null && acceptableIssuers.Length < 1)
			{
				return null;
			}
			if (FiddlerApplication.ClientCertificateProvider != null)
			{
				X509Certificate x509Certificate = FiddlerApplication.ClientCertificateProvider(oS, targetHost, localCertificates, remoteCertificate, acceptableIssuers);
				if (x509Certificate != null && CONFIG.bDebugSpew)
				{
					Trace.WriteLine(string.Format("Session #{0} Attaching client certificate '{1}' when connecting to host '{2}'", oS.Int32_0, x509Certificate.Subject, targetHost));
				}
				return x509Certificate;
			}
			X509Certificate x509Certificate2 = ServerPipe._GetDefaultCertificate();
			if (x509Certificate2 != null)
			{
				this.MarkAsAuthenticated(oS.LocalProcessID);
				oS.oFlags["x-client-cert"] = x509Certificate2.Subject + " Serial#" + x509Certificate2.GetSerialNumberString();
				return x509Certificate2;
			}
			if (CONFIG.bShowDefaultClientCertificateNeededPrompt && FiddlerApplication.Prefs.GetBoolPref("fiddler.network.https.clientcertificate.ephemeral.prompt-for-missing", true))
			{
				FiddlerApplication.Prefs.SetBoolPref("fiddler.network.https.clientcertificate.ephemeral.prompt-for-missing", false);
				FiddlerApplication.DoNotifyUser("The server [" + targetHost + "] requests a client certificate.\nPlease save a client certificate using the filename:\n\n" + CONFIG.GetPath("DefaultClientCertificate"), "Client Certificate Requested");
			}
			FiddlerApplication.Log.LogFormat("The server [{0}] requested a client certificate, but no client certificate was available.", new object[]
			{
				targetHost
			});
			return null;
		}
		internal bool SecureExistingConnection(Session oS, string sCertCN, string sClientCertificateFilename, ref int iHandshakeTime)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			bool result;
			try
			{
				this.sPoolKey = this.sPoolKey.Replace("->http/", "->https/");
				if (this.sPoolKey.EndsWith("->*"))
				{
					this.sPoolKey = this.sPoolKey.Replace("->*", string.Format("->https/{0}:{1}", oS.hostname, oS.port));
				}
				X509CertificateCollection certificateCollectionFromFile = ServerPipe.GetCertificateCollectionFromFile(sClientCertificateFilename);
				this._httpsStream = new SslStream(new NetworkStream(this._baseSocket, false), false, (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => ServerPipe.ConfirmServerCertificate(oS, sCertCN, certificate, chain, sslPolicyErrors), (object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers) => this.AttachClientCertificate(oS, sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers));
				SslProtocols enabledSslProtocols = CONFIG.oAcceptedServerHTTPSProtocols;
				if (oS.oFlags.ContainsKey("x-OverrideSslProtocols"))
				{
					enabledSslProtocols = Utilities.ParseSSLProtocolString(oS.oFlags["x-OverrideSslProtocols"]);
				}
				this._httpsStream.AuthenticateAsClient(sCertCN, certificateCollectionFromFile, enabledSslProtocols, FiddlerApplication.Prefs.GetBoolPref("fiddler.network.https.checkcertificaterevocation", false));
				iHandshakeTime = (int)stopwatch.ElapsedMilliseconds;
				return true;
			}
			catch (Exception ex)
			{
				iHandshakeTime = (int)stopwatch.ElapsedMilliseconds;
				FiddlerApplication.DebugSpew(ex.StackTrace + "\n" + ex.Message);
				string text = string.Format("fiddler.network.https> Failed to secure existing connection for {0}. {1}{2}", sCertCN, ex.Message, (ex.InnerException != null) ? (" InnerException: " + ex.InnerException.ToString()) : ".");
				if (oS.responseBodyBytes == null || oS.responseBodyBytes.Length == 0)
				{
					oS.responseBodyBytes = Encoding.UTF8.GetBytes(text);
				}
				FiddlerApplication.Log.LogString(text);
				result = false;
			}
			return result;
		}
		private static X509CertificateCollection GetCertificateCollectionFromFile(string sClientCertificateFilename)
		{
			X509CertificateCollection x509CertificateCollection = null;
			if (!string.IsNullOrEmpty(sClientCertificateFilename))
			{
				sClientCertificateFilename = Utilities.EnsurePathIsAbsolute(CONFIG.GetPath("Root"), sClientCertificateFilename);
				if (File.Exists(sClientCertificateFilename))
				{
					x509CertificateCollection = new X509CertificateCollection();
					x509CertificateCollection.Add(X509Certificate.CreateFromCertFile(sClientCertificateFilename));
				}
				else
				{
					FiddlerApplication.Log.LogFormat("!! ERROR: Specified client certificate file '{0}' does not exist.", new object[]
					{
						sClientCertificateFilename
					});
				}
			}
			return x509CertificateCollection;
		}
	}
}
