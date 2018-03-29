using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
namespace Fiddler
{
	[DebuggerDisplay("Session #{m_requestID}, {m_state}, {fullUrl}, [{BitFlags}]")]
	public class Session
	{
		private WebRequest __WebRequestForAuth;
		public ITunnel __oTunnel;
		private SessionFlags _bitFlags;
		private static int cRequests;
		private Session nextSession;
		public bool bBufferResponse;
		public SessionTimers Timers;
		private SessionStates m_state;
		private bool _bypassGateway;
		private int m_requestID;
		private int _LocalProcessID;
		public object ViewItem;
		private bool _bAllowClientPipeReuse;
		[CodeDescription("Object representing the HTTP Response.")]
		public ServerChatter oResponse;
		[CodeDescription("Object representing the HTTP Request.")]
		public ClientChatter oRequest;
		[CodeDescription("Fiddler-internal flags set on the session.")]
		public readonly StringDictionary oFlags;
		[CodeDescription("Contains the bytes of the request body.")]
		public byte[] requestBodyBytes;
		[CodeDescription("Contains the bytes of the response body.")]
		public byte[] responseBodyBytes;
		[CodeDescription("IP Address of the client for this session.")]
		public string m_clientIP;
		[CodeDescription("Client port attached to Fiddler.")]
		public int m_clientPort;
		[CodeDescription("IP Address of the server for this session.")]
		public string m_hostIP;
		private AutoResetEvent oSyncEvent;
		public event EventHandler<StateChangeEventArgs> OnStateChanged;
		public event EventHandler<ContinueTransactionEventArgs> OnContinueTransaction;
		public SessionFlags BitFlags
		{
			get
			{
				return this._bitFlags;
			}
			internal set
			{
				if (CONFIG.bDebugSpew && value != this._bitFlags)
				{
					FiddlerApplication.DebugSpew(string.Format("Session #{0} bitflags adjusted from {1} to {2} @ {3}", new object[]
					{
						this.Int32_0,
						this._bitFlags,
						value,
						Environment.StackTrace
					}));
				}
				this._bitFlags = value;
			}
		}
		public bool isTunnel
		{
			get;
			internal set;
		}
		public object Tag
		{
			get;
			set;
		}
		public bool TunnelIsOpen
		{
			get
			{
				return this.__oTunnel != null && this.__oTunnel.IsOpen;
			}
		}
		public long TunnelIngressByteCount
		{
			get
			{
				if (this.__oTunnel != null)
				{
					return this.__oTunnel.IngressByteCount;
				}
				return 0L;
			}
		}
		public long TunnelEgressByteCount
		{
			get
			{
				if (this.__oTunnel != null)
				{
					return this.__oTunnel.EgressByteCount;
				}
				return 0L;
			}
		}
		[CodeDescription("Gets or Sets the Request body bytes; Setter fixes up headers.")]
		public byte[] RequestBody
		{
			get
			{
				return this.requestBodyBytes ?? Utilities.emptyByteArray;
			}
			set
			{
				if (value == null)
				{
					value = Utilities.emptyByteArray;
				}
				this.oRequest.headers.Remove("Transfer-Encoding");
				this.oRequest.headers.Remove("Content-Encoding");
				this.requestBodyBytes = value;
				this.oRequest.headers["Content-Length"] = value.LongLength.ToString();
			}
		}
		[CodeDescription("Gets or Sets the request's Method (e.g. GET, POST, etc).")]
		public string RequestMethod
		{
			get
			{
				if (!Utilities.HasHeaders(this.oRequest))
				{
					return string.Empty;
				}
				return this.oRequest.headers.HTTPMethod;
			}
			set
			{
				if (!Utilities.HasHeaders(this.oRequest))
				{
					return;
				}
				this.oRequest.headers.HTTPMethod = value;
			}
		}
		[CodeDescription("Gets or Sets the Response body bytes; Setter fixes up headers.")]
		public byte[] ResponseBody
		{
			get
			{
				return this.responseBodyBytes ?? Utilities.emptyByteArray;
			}
			set
			{
				if (value == null)
				{
					value = Utilities.emptyByteArray;
				}
				this.oResponse.headers.Remove("Transfer-Encoding");
				this.oResponse.headers.Remove("Content-Encoding");
				this.responseBodyBytes = value;
				this.oResponse.headers["Content-Length"] = value.LongLength.ToString();
			}
		}
		[CodeDescription("When true, this session was conducted using the HTTPS protocol.")]
		public bool isHTTPS
		{
			get
			{
				return Utilities.HasHeaders(this.oRequest) && "HTTPS".OICEquals(this.oRequest.headers.UriScheme);
			}
		}
		[CodeDescription("When true, this session was conducted using the FTPS protocol.")]
		public bool isFTP
		{
			get
			{
				return Utilities.HasHeaders(this.oRequest) && "FTP".OICEquals(this.oRequest.headers.UriScheme);
			}
		}
		[CodeDescription("Get the process ID of the application which made this request, or 0 if it cannot be determined.")]
		public int LocalProcessID
		{
			get
			{
				return this._LocalProcessID;
			}
		}
		[CodeDescription("Gets a path-less filename suitable for saving the Response entity. Uses Content-Disposition if available.")]
		public string SuggestedFilename
		{
			get
			{
				if (!Utilities.HasHeaders(this.oResponse))
				{
					return this.Int32_0.ToString() + ".txt";
				}
				if (Utilities.IsNullOrEmpty(this.responseBodyBytes))
				{
					string format = "{0}_Status{1}.txt";
					return string.Format(format, this.Int32_0.ToString(), this.responseCode.ToString());
				}
				string tokenValue = this.oResponse.headers.GetTokenValue("Content-Disposition", "filename");
				if (tokenValue != null)
				{
					return Session._MakeSafeFilename(tokenValue);
				}
				string text = Utilities.TrimBeforeLast(Utilities.TrimAfter(this.String_0, '?'), '/');
				if (text.Length > 0 && text.Length < 64 && text.Contains(".") && text.LastIndexOf('.') == text.IndexOf('.'))
				{
					string text2 = Session._MakeSafeFilename(text);
					string text3 = string.Empty;
					if (this.String_0.Contains("?") || text2.Length < 1 || text2.EndsWith(".php") || text2.EndsWith(".aspx") || text2.EndsWith(".asp") || text2.EndsWith(".asmx") || text2.EndsWith(".cgi"))
					{
						text3 = Utilities.FileExtensionForMIMEType(this.oResponse.MIMEType);
						if (text2.OICEndsWith(text3))
						{
							text3 = string.Empty;
						}
					}
					string format2 = FiddlerApplication.Prefs.GetBoolPref("fiddler.session.prependIDtosuggestedfilename", false) ? "{0}_{1}{2}" : "{1}{2}";
					return string.Format(format2, this.Int32_0.ToString(), text2, text3);
				}
				StringBuilder stringBuilder = new StringBuilder(32);
				stringBuilder.Append(this.Int32_0);
				stringBuilder.Append("_");
				string mIMEType = this.oResponse.MIMEType;
				stringBuilder.Append(Utilities.FileExtensionForMIMEType(mIMEType));
				return stringBuilder.ToString();
			}
		}
		[CodeDescription("Set to true in OnBeforeRequest if this request should bypass the gateway")]
		public bool bypassGateway
		{
			get
			{
				return this._bypassGateway;
			}
			set
			{
				this._bypassGateway = value;
			}
		}
		[CodeDescription("Returns the port used by the client to communicate to Fiddler.")]
		public int clientPort
		{
			get
			{
				return this.m_clientPort;
			}
		}
		[CodeDescription("Enumerated state of the current session.")]
		public SessionStates state
		{
			get
			{
				return this.m_state;
			}
			set
			{
				SessionStates state = this.m_state;
				this.m_state = value;
				if (!this.isFlagSet(SessionFlags.Ignored))
				{
					EventHandler<StateChangeEventArgs> onStateChanged = this.OnStateChanged;
					if (onStateChanged != null)
					{
						StateChangeEventArgs e = new StateChangeEventArgs(state, value);
						onStateChanged(this, e);
						if (this.m_state >= SessionStates.Done)
						{
							this.OnStateChanged = null;
						}
					}
				}
			}
		}
		[CodeDescription("Returns the path and query part of the URL. (For a CONNECT request, returns the host:port to be connected.)")]
		public string PathAndQuery
		{
			get
			{
				if (this.oRequest.headers == null)
				{
					return string.Empty;
				}
				return this.oRequest.headers.RequestPath;
			}
			set
			{
				this.oRequest.headers.RequestPath = value;
			}
		}
		[CodeDescription("Retrieves the complete URI, including protocol/scheme, in the form http://www.host.com/filepath?query.")]
		public string fullUrl
		{
			get
			{
				if (!Utilities.HasHeaders(this.oRequest))
				{
					return string.Empty;
				}
				return string.Format("{0}://{1}", this.oRequest.headers.UriScheme, this.String_0);
			}
			set
			{
				if (string.IsNullOrEmpty(value))
				{
					throw new ArgumentException("Must specify a complete URI");
				}
				string text = Utilities.TrimAfter(value, "://").ToLowerInvariant();
				string string_ = Utilities.TrimBefore(value, "://");
				if (text != "http" && text != "https" && text != "ftp")
				{
					throw new ArgumentException("URI scheme must be http, https, or ftp");
				}
				this.oRequest.headers.UriScheme = text;
				this.String_0 = string_;
			}
		}
		[CodeDescription("Gets or sets the URL (without protocol) being requested from the server, in the form www.host.com/filepath?query.")]
		public string String_0
		{
			get
			{
				if (this.HTTPMethodIs("CONNECT"))
				{
					return this.PathAndQuery;
				}
				return this.host + this.PathAndQuery;
			}
			set
			{
				if (value.OICStartsWithAny(new string[]
				{
					"http://",
					"https://",
					"ftp://"
				}))
				{
					throw new ArgumentException("If you wish to specify a protocol, use the fullUrl property instead. Input was: " + value);
				}
				if (this.HTTPMethodIs("CONNECT"))
				{
					this.PathAndQuery = value;
					this.host = value;
					return;
				}
				int num = value.IndexOfAny(new char[]
				{
					'/',
					'?'
				});
				if (num > -1)
				{
					this.host = value.Substring(0, num);
					this.PathAndQuery = value.Substring(num);
					return;
				}
				this.host = value;
				this.PathAndQuery = "/";
			}
		}
		[CodeDescription("Gets/Sets the host to which this request is targeted. MAY include IPv6 literal brackets. MAY include a trailing port#.")]
		public string host
		{
			get
			{
				if (this.oRequest == null)
				{
					return string.Empty;
				}
				return this.oRequest.host;
			}
			set
			{
				if (this.oRequest != null)
				{
					this.oRequest.host = value;
				}
			}
		}
		[CodeDescription("Gets/Sets the hostname to which this request is targeted; does NOT include any port# but will include IPv6-literal brackets for IPv6 literals.")]
		public string hostname
		{
			get
			{
				string host = this.oRequest.host;
				if (host.Length < 1)
				{
					return string.Empty;
				}
				int num = host.LastIndexOf(':');
				if (num > -1 && num > host.LastIndexOf(']'))
				{
					return host.Substring(0, num);
				}
				return this.oRequest.host;
			}
			set
			{
				int num = value.LastIndexOf(':');
				if (num > -1 && num > value.LastIndexOf(']'))
				{
					throw new ArgumentException("Do not specify a port when setting hostname; use host property instead.");
				}
				string text = this.HTTPMethodIs("CONNECT") ? this.PathAndQuery : this.host;
				num = text.LastIndexOf(':');
				if (num > -1 && num > text.LastIndexOf(']'))
				{
					this.host = value + text.Substring(num);
					return;
				}
				this.host = value;
			}
		}
		[CodeDescription("Returns the server port to which this request is targeted.")]
		public int port
		{
			get
			{
				string sHostPort = this.HTTPMethodIs("CONNECT") ? this.oRequest.headers.RequestPath : this.oRequest.host;
				int result = this.isHTTPS ? 443 : (this.isFTP ? 21 : 80);
				string text;
				Utilities.CrackHostAndPort(sHostPort, out text, ref result);
				return result;
			}
			set
			{
				if (value < 0 || value > 65535)
				{
					throw new ArgumentException("A valid target port value (0-65535) must be specified.");
				}
				this.host = string.Format("{0}:{1}", this.hostname, value);
			}
		}
		[CodeDescription("Returns the sequential number of this request.")]
		public int Int32_0
		{
			get
			{
				return this.m_requestID;
			}
		}
		[CodeDescription("Returns the Address used by the client to communicate to Fiddler.")]
		public string clientIP
		{
			get
			{
				if (this.m_clientIP != null)
				{
					return this.m_clientIP;
				}
				return "0.0.0.0";
			}
		}
		[CodeDescription("Gets or Sets the HTTP Status code of the server's response")]
		public int responseCode
		{
			get
			{
				if (Utilities.HasHeaders(this.oResponse))
				{
					return this.oResponse.headers.HTTPResponseCode;
				}
				return 0;
			}
			set
			{
				if (Utilities.HasHeaders(this.oResponse))
				{
					this.oResponse.headers.SetStatus(value, "Fiddled");
				}
			}
		}
		public bool bHasWebSocketMessages
		{
			get
			{
				if (this.isAnyFlagSet(SessionFlags.IsWebSocketTunnel) && !this.HTTPMethodIs("CONNECT"))
				{
					WebSocket webSocket = this.__oTunnel as WebSocket;
					return webSocket != null && webSocket.MessageCount > 0;
				}
				return false;
			}
		}
		[CodeDescription("Returns TRUE if this session state>ReadingResponse and oResponse not null.")]
		public bool bHasResponse
		{
			get
			{
				return this.state > SessionStates.ReadingResponse && this.oResponse != null && this.oResponse.headers != null && null != this.responseBodyBytes;
			}
		}
		[CodeDescription("Indexer property into SESSION flags, REQUEST headers, and RESPONSE headers. e.g. oSession[\"Request\", \"Host\"] returns string value for the Request host header. If null, returns String.Empty")]
		public string this[string sCollection, string sName]
		{
			get
			{
				if ("SESSION".OICEquals(sCollection))
				{
					string text = this.oFlags[sName];
					return text ?? string.Empty;
				}
				if ("REQUEST".OICEquals(sCollection))
				{
					if (!Utilities.HasHeaders(this.oRequest))
					{
						return string.Empty;
					}
					return this.oRequest[sName];
				}
				else
				{
					if (!"RESPONSE".OICEquals(sCollection))
					{
						return "undefined";
					}
					if (!Utilities.HasHeaders(this.oResponse))
					{
						return string.Empty;
					}
					return this.oResponse[sName];
				}
			}
		}
		[CodeDescription("Indexer property into session flags collection. oSession[\"Flagname\"] returns string value (or null if missing!).")]
		public string this[string sFlag]
		{
			get
			{
				return this.oFlags[sFlag];
			}
			set
			{
				if (value == null)
				{
					this.oFlags.Remove(sFlag);
					return;
				}
				this.oFlags[sFlag] = value;
			}
		}
		public void UNSTABLE_SetBitFlag(SessionFlags FlagsToSet, bool bool_0)
		{
			this.SetBitFlag(FlagsToSet, bool_0);
		}
		internal void SetBitFlag(SessionFlags FlagsToSet, bool bool_0)
		{
			if (bool_0)
			{
				this.BitFlags = (this._bitFlags | FlagsToSet);
				return;
			}
			this.BitFlags = (this._bitFlags & ~FlagsToSet);
		}
		public bool isFlagSet(SessionFlags FlagsToTest)
		{
			return FlagsToTest == (this._bitFlags & FlagsToTest);
		}
		public bool isAnyFlagSet(SessionFlags FlagsToTest)
		{
			return SessionFlags.None != (this._bitFlags & FlagsToTest);
		}
		[CodeDescription("Returns TRUE if the Session's HTTP Method is available and matches the target method.")]
		public bool HTTPMethodIs(string sTestFor)
		{
			return Utilities.HasHeaders(this.oRequest) && string.Equals(this.oRequest.headers.HTTPMethod, sTestFor, StringComparison.OrdinalIgnoreCase);
		}
		[CodeDescription("Returns TRUE if the Session's target hostname (no port) matches sTestHost (case-insensitively).")]
		public bool HostnameIs(string sTestHost)
		{
			if (this.oRequest == null)
			{
				return false;
			}
			int num = this.oRequest.host.LastIndexOf(':');
			if (num > -1 && num > this.oRequest.host.LastIndexOf(']'))
			{
				return 0 == string.Compare(this.oRequest.host, 0, sTestHost, 0, num, StringComparison.OrdinalIgnoreCase);
			}
			return string.Equals(this.oRequest.host, sTestHost, StringComparison.OrdinalIgnoreCase);
		}
		private static string _MakeSafeFilename(string sFilename)
		{
			char[] invalidFileNameChars = Path.GetInvalidFileNameChars();
			if (sFilename.IndexOfAny(invalidFileNameChars) < 0)
			{
				return Utilities.TrimTo(sFilename, 160);
			}
			StringBuilder stringBuilder = new StringBuilder(sFilename);
			for (int i = 0; i < stringBuilder.Length; i++)
			{
				if (Array.IndexOf<char>(invalidFileNameChars, sFilename[i]) > -1)
				{
					stringBuilder[i] = '-';
				}
			}
			return Utilities.TrimTo(stringBuilder.ToString(), 160);
		}
		private void FireContinueTransaction(Session oOrig, Session oNew, ContinueTransactionReason oReason)
		{
			EventHandler<ContinueTransactionEventArgs> onContinueTransaction = this.OnContinueTransaction;
			if (onContinueTransaction != null)
			{
				ContinueTransactionEventArgs e = new ContinueTransactionEventArgs(oOrig, oNew, oReason);
				onContinueTransaction(this, e);
			}
		}
		public string ToHTMLFragment(bool HeadersOnly)
		{
			string text2;
			if (!HeadersOnly)
			{
				string text = this.oRequest.headers.ToString(true, true);
				if (this.requestBodyBytes != null)
				{
					text += Encoding.UTF8.GetString(this.requestBodyBytes);
				}
				text2 = "<SPAN CLASS='REQUEST'>" + Utilities.HtmlEncode(text).Replace("\r\n", "<BR>") + "</SPAN><BR>";
				if (this.oResponse != null && this.oResponse.headers != null)
				{
					string text3 = this.oResponse.headers.ToString(true, true);
					if (this.responseBodyBytes != null)
					{
						Encoding responseBodyEncoding = Utilities.getResponseBodyEncoding(this);
						text3 += responseBodyEncoding.GetString(this.responseBodyBytes);
					}
					text2 = text2 + "<SPAN CLASS='RESPONSE'>" + Utilities.HtmlEncode(text3).Replace("\r\n", "<BR>") + "</SPAN>";
				}
			}
			else
			{
				text2 = "<SPAN CLASS='REQUEST'>" + Utilities.HtmlEncode(this.oRequest.headers.ToString()).Replace("\r\n", "<BR>") + "</SPAN><BR>";
				if (this.oResponse != null && this.oResponse.headers != null)
				{
					text2 = text2 + "<SPAN CLASS='RESPONSE'>" + Utilities.HtmlEncode(this.oResponse.headers.ToString()).Replace("\r\n", "<BR>") + "</SPAN>";
				}
			}
			return text2;
		}
		public string ToString(bool HeadersOnly)
		{
			string text;
			if (!HeadersOnly)
			{
				text = this.oRequest.headers.ToString(true, true);
				if (this.requestBodyBytes != null)
				{
					text += Encoding.UTF8.GetString(this.requestBodyBytes);
				}
				if (this.oResponse != null && this.oResponse.headers != null)
				{
					text = text + "\r\n" + this.oResponse.headers.ToString(true, true);
					if (this.responseBodyBytes != null)
					{
						Encoding responseBodyEncoding = Utilities.getResponseBodyEncoding(this);
						text += responseBodyEncoding.GetString(this.responseBodyBytes);
					}
				}
			}
			else
			{
				text = this.oRequest.headers.ToString();
				if (this.oResponse != null && this.oResponse.headers != null)
				{
					text = text + "\r\n" + this.oResponse.headers.ToString();
				}
			}
			return text;
		}
		public override string ToString()
		{
			return this.ToString(false);
		}
		private void ThreadPause()
		{
			if (this.oSyncEvent == null)
			{
				this.oSyncEvent = new AutoResetEvent(false);
			}
			this.oSyncEvent.WaitOne();
			this.oSyncEvent.Close();
			this.oSyncEvent = null;
		}
		public void ThreadResume()
		{
			if (this.oSyncEvent == null)
			{
				return;
			}
			this.oSyncEvent.Set();
		}
		[CodeDescription("Sets the SessionFlags.Ignore bit for this Session")]
		public void Ignore()
		{
			this.SetBitFlag(SessionFlags.Ignored, true);
			this.oFlags["log-drop-response-body"] = "IgnoreFlag";
			this.oFlags["log-drop-request-body"] = "IgnoreFlag";
			this.bBufferResponse = false;
		}
		internal static void CreateAndExecute(object oParams)
		{
			try
			{
				ProxyExecuteParams proxyExecuteParams = (ProxyExecuteParams)oParams;
				Socket oSocket = proxyExecuteParams.oSocket;
				ClientPipe clientPipe = new ClientPipe(oSocket, proxyExecuteParams.dtConnectionAccepted);
				Session session = new Session(clientPipe, null);
				FiddlerApplication.DoAfterSocketAccept(session, oSocket);
				if (proxyExecuteParams.oServerCert == null || session.AcceptHTTPSRequest(proxyExecuteParams.oServerCert))
				{
					session.Execute(null);
				}
			}
			catch (Exception eX)
			{
				FiddlerApplication.ReportException(eX);
			}
		}
		private bool AcceptHTTPSRequest(X509Certificate2 oCert)
		{
			try
			{
				if (CONFIG.bUseSNIForCN)
				{
					byte[] buffer = new byte[1024];
					int count = this.oRequest.pipeClient.GetRawSocket().Receive(buffer, SocketFlags.Peek);
					HTTPSClientHello hTTPSClientHello = new HTTPSClientHello();
					if (hTTPSClientHello.LoadFromStream(new MemoryStream(buffer, 0, count, false)))
					{
						this.oFlags["https-Client-SessionID"] = hTTPSClientHello.SessionID;
						if (!string.IsNullOrEmpty(hTTPSClientHello.ServerNameIndicator))
						{
							FiddlerApplication.Log.LogFormat("Secure Endpoint request with SNI of '{0}'", new object[]
							{
								hTTPSClientHello.ServerNameIndicator
							});
							this.oFlags["https-Client-SNIHostname"] = hTTPSClientHello.ServerNameIndicator;
							oCert = CertMaker.FindCert(hTTPSClientHello.ServerNameIndicator);
						}
					}
				}
				if (!this.oRequest.pipeClient.SecureClientPipeDirect(oCert))
				{
					FiddlerApplication.Log.LogString("Failed to secure client connection when acting as Secure Endpoint.");
					return false;
				}
			}
			catch (Exception ex)
			{
				FiddlerApplication.Log.LogFormat("Failed to secure client connection when acting as Secure Endpoint: {0}", new object[]
				{
					ex.ToString()
				});
			}
			return true;
		}
		public bool COMETPeek()
		{
			if (this.state != SessionStates.ReadingResponse)
			{
				return false;
			}
			this.responseBodyBytes = this.oResponse._PeekAtBody();
			return true;
		}
		public void PoisonServerPipe()
		{
			if (this.oResponse != null)
			{
				this.oResponse._PoisonPipe();
			}
		}
		public void PoisonClientPipe()
		{
			this._bAllowClientPipeReuse = false;
		}
		private void CloseSessionPipes(bool bNullThemToo)
		{
			if (CONFIG.bDebugSpew)
			{
				FiddlerApplication.DebugSpew(string.Format("CloseSessionPipes() for Session #{0}", this.Int32_0));
			}
			if (this.oRequest != null && this.oRequest.pipeClient != null)
			{
				if (CONFIG.bDebugSpew)
				{
					FiddlerApplication.DebugSpew("Closing client pipe...", new object[]
					{
						this.Int32_0
					});
				}
				this.oRequest.pipeClient.End();
				if (bNullThemToo)
				{
					this.oRequest.pipeClient = null;
				}
			}
			if (this.oResponse != null && this.oResponse.pipeServer != null)
			{
				FiddlerApplication.DebugSpew("Closing server pipe...", new object[]
				{
					this.Int32_0
				});
				this.oResponse.pipeServer.End();
				if (bNullThemToo)
				{
					this.oResponse.pipeServer = null;
				}
			}
		}
		public void Abort()
		{
			try
			{
				if (this.isAnyFlagSet(SessionFlags.IsBlindTunnel | SessionFlags.IsDecryptingTunnel | SessionFlags.IsWebSocketTunnel))
				{
					if (this.__oTunnel != null)
					{
						this.__oTunnel.CloseTunnel();
						this.oFlags["x-Fiddler-Aborted"] = "true";
						this.state = SessionStates.Aborted;
					}
				}
				else
				{
					if (this.m_state < SessionStates.Done)
					{
						this.CloseSessionPipes(true);
						this.oFlags["x-Fiddler-Aborted"] = "true";
						this.state = SessionStates.Aborted;
						this.ThreadResume();
					}
				}
			}
			catch (Exception)
			{
			}
		}
		[CodeDescription("Save HTTP response body to Fiddler Captures folder.")]
		public bool SaveResponseBody()
		{
			string path = CONFIG.GetPath("Captures");
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(this.SuggestedFilename);
			while (File.Exists(path + stringBuilder.ToString()))
			{
				stringBuilder.Insert(0, this.Int32_0.ToString() + "_");
			}
			stringBuilder.Insert(0, path);
			return this.SaveResponseBody(stringBuilder.ToString());
		}
		[CodeDescription("Save HTTP response body to specified location.")]
		public bool SaveResponseBody(string sFilename)
		{
			bool result;
			try
			{
				Utilities.WriteArrayToFile(sFilename, this.responseBodyBytes);
				result = true;
			}
			catch (Exception ex)
			{
				FiddlerApplication.DoNotifyUser(ex.Message + "\n\n" + sFilename, "Save Failed");
				result = false;
			}
			return result;
		}
		[CodeDescription("Save HTTP request body to specified location.")]
		public bool SaveRequestBody(string sFilename)
		{
			bool result;
			try
			{
				Utilities.WriteArrayToFile(sFilename, this.requestBodyBytes);
				result = true;
			}
			catch (Exception ex)
			{
				FiddlerApplication.DoNotifyUser(ex.Message + "\n\n" + sFilename, "Save Failed");
				result = false;
			}
			return result;
		}
		public void SaveSession(string sFilename, bool bHeadersOnly)
		{
			Utilities.EnsureOverwritable(sFilename);
			FileStream fileStream = new FileStream(sFilename, FileMode.Create, FileAccess.Write);
			this.WriteToStream(fileStream, bHeadersOnly);
			fileStream.Close();
		}
		public void SaveRequest(string sFilename, bool bHeadersOnly)
		{
			this.SaveRequest(sFilename, bHeadersOnly, false);
		}
		public void SaveRequest(string sFilename, bool bHeadersOnly, bool bIncludeSchemeAndHostInPath)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(sFilename));
			FileStream fileStream = new FileStream(sFilename, FileMode.Create, FileAccess.Write);
			if (this.oRequest.headers != null)
			{
				byte[] array = this.oRequest.headers.ToByteArray(true, true, bIncludeSchemeAndHostInPath, this.oFlags["X-OverrideHost"]);
				fileStream.Write(array, 0, array.Length);
				if (!bHeadersOnly && this.requestBodyBytes != null)
				{
					fileStream.Write(this.requestBodyBytes, 0, this.requestBodyBytes.Length);
				}
			}
			fileStream.Close();
		}
		public bool LoadMetadata(Stream strmMetadata)
		{
			string a = XmlConvert.ToString(true);
			SessionFlags sessionFlags = SessionFlags.None;
			string text = null;
			bool result;
			try
			{
				XmlTextReader xmlTextReader = new XmlTextReader(strmMetadata);
				xmlTextReader.WhitespaceHandling = WhitespaceHandling.None;
				while (xmlTextReader.Read())
				{
					XmlNodeType nodeType = xmlTextReader.NodeType;
					string name;
					if (nodeType == XmlNodeType.Element && (name = xmlTextReader.Name) != null)
					{
						if (!(name == "Session"))
						{
							if (!(name == "SessionFlag"))
							{
								if (!(name == "SessionTimers"))
								{
									if (!(name == "TunnelInfo"))
									{
										if (name == "PipeInfo")
										{
											this.bBufferResponse = (a != xmlTextReader.GetAttribute("Streamed"));
											if (!this.bBufferResponse)
											{
												sessionFlags |= SessionFlags.ResponseStreamed;
											}
											if (a == xmlTextReader.GetAttribute("CltReuse"))
											{
												sessionFlags |= SessionFlags.ClientPipeReused;
											}
											if (a == xmlTextReader.GetAttribute("Reused"))
											{
												sessionFlags |= SessionFlags.ServerPipeReused;
											}
											if (this.oResponse != null)
											{
												this.oResponse._bWasForwarded = (a == xmlTextReader.GetAttribute("Forwarded"));
												if (this.oResponse._bWasForwarded)
												{
													sessionFlags |= SessionFlags.SentToGateway;
												}
											}
										}
									}
									else
									{
										long lngEgress = 0L;
										long lngIngress = 0L;
										if (long.TryParse(xmlTextReader.GetAttribute("BytesEgress"), out lngEgress) && long.TryParse(xmlTextReader.GetAttribute("BytesIngress"), out lngIngress))
										{
											this.__oTunnel = new MockTunnel(lngEgress, lngIngress);
										}
									}
								}
								else
								{
									this.Timers.ClientConnected = XmlConvert.ToDateTime(xmlTextReader.GetAttribute("ClientConnected"), XmlDateTimeSerializationMode.RoundtripKind);
									string attribute = xmlTextReader.GetAttribute("ClientBeginRequest");
									if (attribute != null)
									{
										this.Timers.ClientBeginRequest = XmlConvert.ToDateTime(attribute, XmlDateTimeSerializationMode.RoundtripKind);
									}
									attribute = xmlTextReader.GetAttribute("GotRequestHeaders");
									if (attribute != null)
									{
										this.Timers.FiddlerGotRequestHeaders = XmlConvert.ToDateTime(attribute, XmlDateTimeSerializationMode.RoundtripKind);
									}
									this.Timers.ClientDoneRequest = XmlConvert.ToDateTime(xmlTextReader.GetAttribute("ClientDoneRequest"), XmlDateTimeSerializationMode.RoundtripKind);
									attribute = xmlTextReader.GetAttribute("GatewayTime");
									if (attribute != null)
									{
										this.Timers.GatewayDeterminationTime = XmlConvert.ToInt32(attribute);
									}
									attribute = xmlTextReader.GetAttribute("DNSTime");
									if (attribute != null)
									{
										this.Timers.DNSTime = XmlConvert.ToInt32(attribute);
									}
									attribute = xmlTextReader.GetAttribute("TCPConnectTime");
									if (attribute != null)
									{
										this.Timers.TCPConnectTime = XmlConvert.ToInt32(attribute);
									}
									attribute = xmlTextReader.GetAttribute("HTTPSHandshakeTime");
									if (attribute != null)
									{
										this.Timers.HTTPSHandshakeTime = XmlConvert.ToInt32(attribute);
									}
									attribute = xmlTextReader.GetAttribute("ServerConnected");
									if (attribute != null)
									{
										this.Timers.ServerConnected = XmlConvert.ToDateTime(attribute, XmlDateTimeSerializationMode.RoundtripKind);
									}
									attribute = xmlTextReader.GetAttribute("FiddlerBeginRequest");
									if (attribute != null)
									{
										this.Timers.FiddlerBeginRequest = XmlConvert.ToDateTime(attribute, XmlDateTimeSerializationMode.RoundtripKind);
									}
									this.Timers.ServerGotRequest = XmlConvert.ToDateTime(xmlTextReader.GetAttribute("ServerGotRequest"), XmlDateTimeSerializationMode.RoundtripKind);
									attribute = xmlTextReader.GetAttribute("ServerBeginResponse");
									if (attribute != null)
									{
										this.Timers.ServerBeginResponse = XmlConvert.ToDateTime(attribute, XmlDateTimeSerializationMode.RoundtripKind);
									}
									attribute = xmlTextReader.GetAttribute("GotResponseHeaders");
									if (attribute != null)
									{
										this.Timers.FiddlerGotResponseHeaders = XmlConvert.ToDateTime(attribute, XmlDateTimeSerializationMode.RoundtripKind);
									}
									this.Timers.ServerDoneResponse = XmlConvert.ToDateTime(xmlTextReader.GetAttribute("ServerDoneResponse"), XmlDateTimeSerializationMode.RoundtripKind);
									this.Timers.ClientBeginResponse = XmlConvert.ToDateTime(xmlTextReader.GetAttribute("ClientBeginResponse"), XmlDateTimeSerializationMode.RoundtripKind);
									this.Timers.ClientDoneResponse = XmlConvert.ToDateTime(xmlTextReader.GetAttribute("ClientDoneResponse"), XmlDateTimeSerializationMode.RoundtripKind);
								}
							}
							else
							{
								this.oFlags.Add(xmlTextReader.GetAttribute("N"), xmlTextReader.GetAttribute("V"));
							}
						}
						else
						{
							if (xmlTextReader.GetAttribute("Aborted") != null)
							{
								this.m_state = SessionStates.Aborted;
							}
							if (xmlTextReader.GetAttribute("BitFlags") != null)
							{
								this.BitFlags = (SessionFlags)uint.Parse(xmlTextReader.GetAttribute("BitFlags"), NumberStyles.HexNumber);
							}
							if (xmlTextReader.GetAttribute("SID") != null)
							{
								text = xmlTextReader.GetAttribute("SID");
							}
						}
					}
				}
				if (this.BitFlags == SessionFlags.None)
				{
					this.BitFlags = sessionFlags;
				}
				if (this.Timers.ClientBeginRequest.Ticks < 1L)
				{
					this.Timers.ClientBeginRequest = this.Timers.ClientConnected;
				}
				if (this.Timers.FiddlerBeginRequest.Ticks < 1L)
				{
					this.Timers.FiddlerBeginRequest = this.Timers.ServerGotRequest;
				}
				if (this.Timers.FiddlerGotRequestHeaders.Ticks < 1L)
				{
					this.Timers.FiddlerGotRequestHeaders = this.Timers.ClientBeginRequest;
				}
				if (this.Timers.FiddlerGotResponseHeaders.Ticks < 1L)
				{
					this.Timers.FiddlerGotResponseHeaders = this.Timers.ServerBeginResponse;
				}
				if (this.m_clientPort == 0 && this.oFlags.ContainsKey("X-ClientPort"))
				{
					int.TryParse(this.oFlags["X-ClientPort"], out this.m_clientPort);
				}
				if (text != null)
				{
					if (this.oFlags.ContainsKey("ui-comments"))
					{
						this.oFlags["x-OriginalSessionID"] = text;
					}
					else
					{
						this.oFlags["ui-comments"] = string.Format("[#{0}] {1}", text, this.oFlags["ui-comments"]);
					}
				}
				xmlTextReader.Close();
				result = true;
			}
			catch (Exception eX)
			{
				FiddlerApplication.ReportException(eX);
				result = false;
			}
			return result;
		}
		public bool SaveMetadata(string sFilename)
		{
			bool result;
			try
			{
				FileStream fileStream = new FileStream(sFilename, FileMode.Create, FileAccess.Write);
				this.WriteMetadataToStream(fileStream);
				fileStream.Close();
				result = true;
			}
			catch (Exception eX)
			{
				FiddlerApplication.ReportException(eX);
				result = false;
			}
			return result;
		}
		public void SaveResponse(string sFilename, bool bHeadersOnly)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(sFilename));
			FileStream fileStream = new FileStream(sFilename, FileMode.Create, FileAccess.Write);
			if (this.oResponse.headers != null)
			{
				byte[] array = this.oResponse.headers.ToByteArray(true, true);
				fileStream.Write(array, 0, array.Length);
				if (!bHeadersOnly && this.responseBodyBytes != null)
				{
					fileStream.Write(this.responseBodyBytes, 0, this.responseBodyBytes.Length);
				}
			}
			fileStream.Close();
		}
		public void WriteMetadataToStream(Stream strmMetadata)
		{
			XmlTextWriter xmlTextWriter = new XmlTextWriter(strmMetadata, Encoding.UTF8);
			xmlTextWriter.Formatting = Formatting.Indented;
			xmlTextWriter.WriteStartDocument();
			xmlTextWriter.WriteStartElement("Session");
			xmlTextWriter.WriteAttributeString("SID", this.Int32_0.ToString());
			xmlTextWriter.WriteAttributeString("BitFlags", ((uint)this.BitFlags).ToString("x"));
			if (this.m_state == SessionStates.Aborted)
			{
				xmlTextWriter.WriteAttributeString("Aborted", XmlConvert.ToString(true));
			}
			xmlTextWriter.WriteStartElement("SessionTimers");
			xmlTextWriter.WriteAttributeString("ClientConnected", XmlConvert.ToString(this.Timers.ClientConnected, XmlDateTimeSerializationMode.RoundtripKind));
			xmlTextWriter.WriteAttributeString("ClientBeginRequest", XmlConvert.ToString(this.Timers.ClientBeginRequest, XmlDateTimeSerializationMode.RoundtripKind));
			xmlTextWriter.WriteAttributeString("GotRequestHeaders", XmlConvert.ToString(this.Timers.FiddlerGotRequestHeaders, XmlDateTimeSerializationMode.RoundtripKind));
			xmlTextWriter.WriteAttributeString("ClientDoneRequest", XmlConvert.ToString(this.Timers.ClientDoneRequest, XmlDateTimeSerializationMode.RoundtripKind));
			xmlTextWriter.WriteAttributeString("GatewayTime", XmlConvert.ToString(this.Timers.GatewayDeterminationTime));
			xmlTextWriter.WriteAttributeString("DNSTime", XmlConvert.ToString(this.Timers.DNSTime));
			xmlTextWriter.WriteAttributeString("TCPConnectTime", XmlConvert.ToString(this.Timers.TCPConnectTime));
			xmlTextWriter.WriteAttributeString("HTTPSHandshakeTime", XmlConvert.ToString(this.Timers.HTTPSHandshakeTime));
			xmlTextWriter.WriteAttributeString("ServerConnected", XmlConvert.ToString(this.Timers.ServerConnected, XmlDateTimeSerializationMode.RoundtripKind));
			xmlTextWriter.WriteAttributeString("FiddlerBeginRequest", XmlConvert.ToString(this.Timers.FiddlerBeginRequest, XmlDateTimeSerializationMode.RoundtripKind));
			xmlTextWriter.WriteAttributeString("ServerGotRequest", XmlConvert.ToString(this.Timers.ServerGotRequest, XmlDateTimeSerializationMode.RoundtripKind));
			xmlTextWriter.WriteAttributeString("ServerBeginResponse", XmlConvert.ToString(this.Timers.ServerBeginResponse, XmlDateTimeSerializationMode.RoundtripKind));
			xmlTextWriter.WriteAttributeString("GotResponseHeaders", XmlConvert.ToString(this.Timers.FiddlerGotResponseHeaders, XmlDateTimeSerializationMode.RoundtripKind));
			xmlTextWriter.WriteAttributeString("ServerDoneResponse", XmlConvert.ToString(this.Timers.ServerDoneResponse, XmlDateTimeSerializationMode.RoundtripKind));
			xmlTextWriter.WriteAttributeString("ClientBeginResponse", XmlConvert.ToString(this.Timers.ClientBeginResponse, XmlDateTimeSerializationMode.RoundtripKind));
			xmlTextWriter.WriteAttributeString("ClientDoneResponse", XmlConvert.ToString(this.Timers.ClientDoneResponse, XmlDateTimeSerializationMode.RoundtripKind));
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteStartElement("PipeInfo");
			if (!this.bBufferResponse)
			{
				xmlTextWriter.WriteAttributeString("Streamed", XmlConvert.ToString(true));
			}
			if (this.oRequest != null && this.oRequest.bClientSocketReused)
			{
				xmlTextWriter.WriteAttributeString("CltReuse", XmlConvert.ToString(true));
			}
			if (this.oResponse != null)
			{
				if (this.oResponse.bServerSocketReused)
				{
					xmlTextWriter.WriteAttributeString("Reused", XmlConvert.ToString(true));
				}
				if (this.oResponse.bWasForwarded)
				{
					xmlTextWriter.WriteAttributeString("Forwarded", XmlConvert.ToString(true));
				}
			}
			xmlTextWriter.WriteEndElement();
			if (this.isTunnel && this.__oTunnel != null)
			{
				xmlTextWriter.WriteStartElement("TunnelInfo");
				xmlTextWriter.WriteAttributeString("BytesEgress", XmlConvert.ToString(this.__oTunnel.EgressByteCount));
				xmlTextWriter.WriteAttributeString("BytesIngress", XmlConvert.ToString(this.__oTunnel.IngressByteCount));
				xmlTextWriter.WriteEndElement();
			}
			xmlTextWriter.WriteStartElement("SessionFlags");
			foreach (string text in this.oFlags.Keys)
			{
				xmlTextWriter.WriteStartElement("SessionFlag");
				xmlTextWriter.WriteAttributeString("N", text);
				xmlTextWriter.WriteAttributeString("V", this.oFlags[text]);
				xmlTextWriter.WriteEndElement();
			}
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndElement();
			xmlTextWriter.WriteEndDocument();
			xmlTextWriter.Flush();
		}
		public bool WriteRequestToStream(bool bHeadersOnly, bool bIncludeProtocolAndHostWithPath, Stream oFS)
		{
			return this.WriteRequestToStream(bHeadersOnly, bIncludeProtocolAndHostWithPath, false, oFS);
		}
		public bool WriteRequestToStream(bool bHeadersOnly, bool bIncludeProtocolAndHostWithPath, bool bEncodeIfBinary, Stream oFS)
		{
			if (!Utilities.HasHeaders(this.oRequest))
			{
				return false;
			}
			bool flag = bEncodeIfBinary && !bHeadersOnly && this.requestBodyBytes != null && Utilities.arrayContainsNonText(this.requestBodyBytes);
			HTTPRequestHeaders hTTPRequestHeaders = this.oRequest.headers;
			if (flag)
			{
				hTTPRequestHeaders = (HTTPRequestHeaders)hTTPRequestHeaders.Clone();
				hTTPRequestHeaders["Fiddler-Encoding"] = "base64";
			}
			byte[] array = hTTPRequestHeaders.ToByteArray(true, true, bIncludeProtocolAndHostWithPath, this.oFlags["X-OverrideHost"]);
			oFS.Write(array, 0, array.Length);
			if (flag)
			{
				byte[] bytes = Encoding.ASCII.GetBytes(Convert.ToBase64String(this.requestBodyBytes));
				oFS.Write(bytes, 0, bytes.Length);
				return true;
			}
			if (!bHeadersOnly && this.requestBodyBytes != null)
			{
				oFS.Write(this.requestBodyBytes, 0, this.requestBodyBytes.Length);
			}
			return true;
		}
		public bool WriteResponseToStream(Stream oFS, bool bHeadersOnly)
		{
			if (!Utilities.HasHeaders(this.oResponse))
			{
				return false;
			}
			byte[] array = this.oResponse.headers.ToByteArray(true, true);
			oFS.Write(array, 0, array.Length);
			if (!bHeadersOnly && this.responseBodyBytes != null)
			{
				oFS.Write(this.responseBodyBytes, 0, this.responseBodyBytes.Length);
			}
			return true;
		}
		internal bool WriteWebSocketMessagesToStream(Stream oFS)
		{
			WebSocket webSocket = this.__oTunnel as WebSocket;
			return webSocket != null && webSocket.WriteWebSocketMessageListToStream(oFS);
		}
		[CodeDescription("Write the session (or session headers) to the specified stream")]
		public bool WriteToStream(Stream oFS, bool bHeadersOnly)
		{
			bool result;
			try
			{
				this.WriteRequestToStream(bHeadersOnly, true, oFS);
				oFS.WriteByte(13);
				oFS.WriteByte(10);
				this.WriteResponseToStream(oFS, bHeadersOnly);
				result = true;
			}
			catch (Exception)
			{
				result = false;
			}
			return result;
		}
		[CodeDescription("Replace HTTP request headers and body using the specified file.")]
		public bool LoadRequestBodyFromFile(string sFilename)
		{
			if (!Utilities.HasHeaders(this.oRequest))
			{
				return false;
			}
			sFilename = Utilities.EnsurePathIsAbsolute(CONFIG.GetPath("Requests"), sFilename);
			return this.oRequest.ReadRequestBodyFromFile(sFilename);
		}
		private bool LoadResponse(Stream strmResponse, string sResponseFile, string sOptionalContentTypeHint)
		{
			bool flag = string.IsNullOrEmpty(sResponseFile);
			this.oResponse = new ServerChatter(this, "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");
			this.responseBodyBytes = Utilities.emptyByteArray;
			this.bBufferResponse = true;
			this.BitFlags |= SessionFlags.ResponseGeneratedByFiddler;
			this.oFlags["x-Fiddler-Generated"] = (flag ? "LoadResponseFromStream" : "LoadResponseFromFile");
			bool result;
			if (flag)
			{
				result = this.oResponse.ReadResponseFromStream(strmResponse, sOptionalContentTypeHint);
			}
			else
			{
				result = this.oResponse.ReadResponseFromFile(sResponseFile, sOptionalContentTypeHint);
			}
			if (this.HTTPMethodIs("HEAD"))
			{
				this.responseBodyBytes = Utilities.emptyByteArray;
			}
			if (this.m_state < SessionStates.AutoTamperResponseBefore)
			{
				this.state = SessionStates.AutoTamperResponseBefore;
			}
			return result;
		}
		public bool LoadResponseFromStream(Stream strmResponse, string sOptionalContentTypeHint)
		{
			return this.LoadResponse(strmResponse, null, sOptionalContentTypeHint);
		}
		[CodeDescription("Replace HTTP response headers and body using the specified file.")]
		public bool LoadResponseFromFile(string sFilename)
		{
			sFilename = Utilities.GetFirstLocalResponse(sFilename);
			string sOptionalContentTypeHint = Utilities.ContentTypeForFilename(sFilename);
			return this.LoadResponse(null, sFilename, sOptionalContentTypeHint);
		}
		[CodeDescription("Return a string generated from the request body, decoding it and converting from a codepage if needed. Possibly expensive due to decompression and will throw on malformed content.")]
		public string GetRequestBodyAsString()
		{
			if (this._HasRequestBody() && Utilities.HasHeaders(this.oRequest))
			{
				byte[] array = Utilities.Dupe(this.requestBodyBytes);
				Utilities.utilDecodeHTTPBody(this.oRequest.headers, ref array);
				Encoding entityBodyEncoding = Utilities.getEntityBodyEncoding(this.oRequest.headers, array);
				return Utilities.GetStringFromArrayRemovingBOM(array, entityBodyEncoding);
			}
			return string.Empty;
		}
		[CodeDescription("Return a string generated from the response body, decoding it and converting from a codepage if needed. Possibly expensive due to decompression and will throw on malformed content.")]
		public string GetResponseBodyAsString()
		{
			if (this._HasResponseBody() && Utilities.HasHeaders(this.oResponse))
			{
				byte[] array = Utilities.Dupe(this.responseBodyBytes);
				Utilities.utilDecodeHTTPBody(this.oResponse.headers, ref array);
				Encoding entityBodyEncoding = Utilities.getEntityBodyEncoding(this.oResponse.headers, array);
				return Utilities.GetStringFromArrayRemovingBOM(array, entityBodyEncoding);
			}
			return string.Empty;
		}
		[CodeDescription("Returns the Encoding of the requestBodyBytes")]
		public Encoding GetRequestBodyEncoding()
		{
			return Utilities.getEntityBodyEncoding(this.oRequest.headers, this.requestBodyBytes);
		}
		[CodeDescription("Returns the Encoding of the responseBodyBytes")]
		public Encoding GetResponseBodyEncoding()
		{
			return Utilities.getResponseBodyEncoding(this);
		}
		[CodeDescription("Returns true if request URI contains the specified string. Case-insensitive.")]
		public bool uriContains(string sLookfor)
		{
			return this.fullUrl.OICContains(sLookfor);
		}
		[CodeDescription("Removes chunking and HTTP Compression from the response. Adds or updates Content-Length header.")]
		public bool utilDecodeResponse()
		{
			return this.utilDecodeResponse(false);
		}
		public bool utilDecodeResponse(bool bSilent)
		{
			if (!Utilities.HasHeaders(this.oResponse) || (!this.oResponse.headers.Exists("Transfer-Encoding") && !this.oResponse.headers.Exists("Content-Encoding")))
			{
				return false;
			}
			if (Utilities.isUnsupportedEncoding(this.oResponse.headers["Transfer-Encoding"], this.oResponse.headers["Content-Encoding"]))
			{
				return false;
			}
			bool result;
			try
			{
				Utilities.utilDecodeHTTPBody(this.oResponse.headers, ref this.responseBodyBytes);
				this.oResponse.headers.Remove("Transfer-Encoding");
				this.oResponse.headers.Remove("Content-Encoding");
				this.oResponse.headers["Content-Length"] = ((this.responseBodyBytes == null) ? "0" : this.responseBodyBytes.LongLength.ToString());
				return true;
			}
			catch (Exception eX)
			{
				if (CONFIG.bDebugSpew)
				{
					FiddlerApplication.DebugSpew("utilDecodeResponse failed. The HTTP response body was malformed. " + Utilities.DescribeException(eX));
				}
				if (!bSilent)
				{
					FiddlerApplication.ReportException(eX, "utilDecodeResponse failed for Session #" + this.Int32_0.ToString(), "The HTTP response body was malformed.");
				}
				this.oFlags["x-UtilDecodeResponse"] = Utilities.DescribeException(eX);
				this.oFlags["ui-backcolor"] = "LightYellow";
				result = false;
			}
			return result;
		}
		[CodeDescription("Removes chunking and HTTP Compression from the Request. Adds or updates Content-Length header.")]
		public bool utilDecodeRequest()
		{
			return this.utilDecodeRequest(false);
		}
		public bool utilDecodeRequest(bool bSilent)
		{
			if (!Utilities.HasHeaders(this.oRequest) || (!this.oRequest.headers.Exists("Transfer-Encoding") && !this.oRequest.headers.Exists("Content-Encoding")))
			{
				return false;
			}
			if (Utilities.isUnsupportedEncoding(this.oRequest.headers["Transfer-Encoding"], this.oRequest.headers["Content-Encoding"]))
			{
				return false;
			}
			bool result;
			try
			{
				Utilities.utilDecodeHTTPBody(this.oRequest.headers, ref this.requestBodyBytes);
				this.oRequest.headers.Remove("Transfer-Encoding");
				this.oRequest.headers.Remove("Content-Encoding");
				this.oRequest.headers["Content-Length"] = ((this.requestBodyBytes == null) ? "0" : this.requestBodyBytes.LongLength.ToString());
				return true;
			}
			catch (Exception eX)
			{
				if (!bSilent)
				{
					FiddlerApplication.ReportException(eX, "utilDecodeRequest failed for Session #" + this.Int32_0.ToString(), "The HTTP request body was malformed.");
				}
				this.oFlags["x-UtilDecodeRequest"] = Utilities.DescribeException(eX);
				this.oFlags["ui-backcolor"] = "LightYellow";
				result = false;
			}
			return result;
		}
		[CodeDescription("Use GZIP to compress the request body. Throws exceptions to caller.")]
		public bool utilGZIPRequest()
		{
			if (!this._mayCompressRequest())
			{
				return false;
			}
			this.requestBodyBytes = Utilities.GzipCompress(this.requestBodyBytes);
			this.oRequest.headers["Content-Encoding"] = "gzip";
			this.oRequest.headers["Content-Length"] = ((this.requestBodyBytes == null) ? "0" : this.requestBodyBytes.LongLength.ToString());
			return true;
		}
		[CodeDescription("Use GZIP to compress the response body. Throws exceptions to caller.")]
		public bool utilGZIPResponse()
		{
			if (!this._mayCompressResponse())
			{
				return false;
			}
			this.responseBodyBytes = Utilities.GzipCompress(this.responseBodyBytes);
			this.oResponse.headers["Content-Encoding"] = "gzip";
			this.oResponse.headers["Content-Length"] = ((this.responseBodyBytes == null) ? "0" : this.responseBodyBytes.LongLength.ToString());
			return true;
		}
		[CodeDescription("Use DEFLATE to compress the response body. Throws exceptions to caller.")]
		public bool utilDeflateResponse()
		{
			if (!this._mayCompressResponse())
			{
				return false;
			}
			this.responseBodyBytes = Utilities.DeflaterCompress(this.responseBodyBytes);
			this.oResponse.headers["Content-Encoding"] = "deflate";
			this.oResponse.headers["Content-Length"] = ((this.responseBodyBytes == null) ? "0" : this.responseBodyBytes.LongLength.ToString());
			return true;
		}
		[CodeDescription("Use BZIP2 to compress the response body. Throws exceptions to caller.")]
		public bool utilBZIP2Response()
		{
			if (!this._mayCompressResponse())
			{
				return false;
			}
			this.responseBodyBytes = Utilities.bzip2Compress(this.responseBodyBytes);
			this.oResponse.headers["Content-Encoding"] = "bzip2";
			this.oResponse.headers["Content-Length"] = ((this.responseBodyBytes == null) ? "0" : this.responseBodyBytes.LongLength.ToString());
			return true;
		}
		private bool _mayCompressRequest()
		{
			return this._HasRequestBody() && !this.oRequest.headers.Exists("Content-Encoding") && !this.oRequest.headers.Exists("Transfer-Encoding");
		}
		private bool _mayCompressResponse()
		{
			return this._HasResponseBody() && !this.oResponse.headers.Exists("Content-Encoding") && !this.oResponse.headers.Exists("Transfer-Encoding");
		}
		[CodeDescription("Apply Transfer-Encoding: chunked to the response, if possible.")]
		public bool utilChunkResponse(int iSuggestedChunkCount)
		{
			if (Utilities.HasHeaders(this.oRequest) && "HTTP/1.1".OICEquals(this.oRequest.headers.HTTPVersion) && !this.HTTPMethodIs("HEAD") && !this.HTTPMethodIs("CONNECT") && Utilities.HasHeaders(this.oResponse) && Utilities.HTTPStatusAllowsBody(this.oResponse.headers.HTTPResponseCode) && (this.responseBodyBytes == null || this.responseBodyBytes.LongLength <= 2147483647L) && !this.oResponse.headers.Exists("Transfer-Encoding"))
			{
				this.responseBodyBytes = Utilities.doChunk(this.responseBodyBytes, iSuggestedChunkCount);
				this.oResponse.headers.Remove("Content-Length");
				this.oResponse.headers["Transfer-Encoding"] = "chunked";
				return true;
			}
			return false;
		}
		[CodeDescription("Perform a case-sensitive string replacement on the request body (not URL!). Updates Content-Length header. Returns TRUE if replacements occur.")]
		public bool utilReplaceInRequest(string sSearchFor, string sReplaceWith)
		{
			if (!this._HasRequestBody() || !Utilities.HasHeaders(this.oRequest))
			{
				return false;
			}
			string requestBodyAsString = this.GetRequestBodyAsString();
			string text = requestBodyAsString.Replace(sSearchFor, sReplaceWith);
			if (requestBodyAsString != text)
			{
				this.utilSetRequestBody(text);
				return true;
			}
			return false;
		}
		[CodeDescription("Call inside OnBeforeRequest to create a Response object and bypass the server.")]
		public void utilCreateResponseAndBypassServer()
		{
			if (this.state > SessionStates.SendingRequest)
			{
				throw new InvalidOperationException("Too late, we're already talking to the server.");
			}
			this.oResponse = new ServerChatter(this, "HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n");
			this.responseBodyBytes = Utilities.emptyByteArray;
			this.oFlags["x-Fiddler-Generated"] = "utilCreateResponseAndBypassServer";
			this.BitFlags |= SessionFlags.ResponseGeneratedByFiddler;
			this.bBufferResponse = true;
			this.state = SessionStates.AutoTamperResponseBefore;
		}
		[CodeDescription("Perform a regex-based replacement on the response body. Specify RegEx Options via leading Inline Flags, e.g. (?im) for case-Insensitive Multi-line. Updates Content-Length header. Note, you should call utilDecodeResponse first!  Returns TRUE if replacements occur.")]
		public bool utilReplaceRegexInResponse(string sSearchForRegEx, string sReplaceWithExpression)
		{
			if (!this._HasResponseBody())
			{
				return false;
			}
			Encoding responseBodyEncoding = Utilities.getResponseBodyEncoding(this);
			string @string = responseBodyEncoding.GetString(this.responseBodyBytes);
			string text = Regex.Replace(@string, sSearchForRegEx, sReplaceWithExpression, RegexOptions.ExplicitCapture | RegexOptions.Singleline);
			if (@string != text)
			{
				this.responseBodyBytes = responseBodyEncoding.GetBytes(text);
				this.oResponse["Content-Length"] = this.responseBodyBytes.LongLength.ToString();
				return true;
			}
			return false;
		}
		[CodeDescription("Perform a case-sensitive string replacement on the response body. Updates Content-Length header. Note, you should call utilDecodeResponse first!  Returns TRUE if replacements occur.")]
		public bool utilReplaceInResponse(string sSearchFor, string sReplaceWith)
		{
			return this._innerReplaceInResponse(sSearchFor, sReplaceWith, true, true);
		}
		[CodeDescription("Perform a single case-sensitive string replacement on the response body. Updates Content-Length header. Note, you should call utilDecodeResponse first! Returns TRUE if replacements occur.")]
		public bool utilReplaceOnceInResponse(string sSearchFor, string sReplaceWith, bool bCaseSensitive)
		{
			return this._innerReplaceInResponse(sSearchFor, sReplaceWith, false, bCaseSensitive);
		}
		private bool _innerReplaceInResponse(string sSearchFor, string sReplaceWith, bool bReplaceAll, bool bCaseSensitive)
		{
			if (!this._HasResponseBody())
			{
				return false;
			}
			Encoding responseBodyEncoding = Utilities.getResponseBodyEncoding(this);
			string @string = responseBodyEncoding.GetString(this.responseBodyBytes);
			string text;
			if (bReplaceAll)
			{
				text = @string.Replace(sSearchFor, sReplaceWith);
			}
			else
			{
				int num = @string.IndexOf(sSearchFor, bCaseSensitive ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase);
				if (num == 0)
				{
					text = sReplaceWith + @string.Substring(sSearchFor.Length);
				}
				else
				{
					if (num <= 0)
					{
						return false;
					}
					text = @string.Substring(0, num) + sReplaceWith + @string.Substring(num + sSearchFor.Length);
				}
			}
			if (@string != text)
			{
				this.responseBodyBytes = responseBodyEncoding.GetBytes(text);
				this.oResponse["Content-Length"] = this.responseBodyBytes.LongLength.ToString();
				return true;
			}
			return false;
		}
		[CodeDescription("Replaces the request body with sString as UTF8. Sets Content-Length header & removes Transfer-Encoding/Content-Encoding")]
		public void utilSetRequestBody(string sString)
		{
			if (sString == null)
			{
				sString = string.Empty;
			}
			this.oRequest.headers.Remove("Transfer-Encoding");
			this.oRequest.headers.Remove("Content-Encoding");
			this.requestBodyBytes = Encoding.UTF8.GetBytes(sString);
			this.oRequest["Content-Length"] = this.requestBodyBytes.LongLength.ToString();
		}
		[CodeDescription("Replaces the response body with sString. Sets Content-Length header & removes Transfer-Encoding/Content-Encoding")]
		public void utilSetResponseBody(string sString)
		{
			if (sString == null)
			{
				sString = string.Empty;
			}
			this.oResponse.headers.Remove("Transfer-Encoding");
			this.oResponse.headers.Remove("Content-Encoding");
			Encoding responseBodyEncoding = Utilities.getResponseBodyEncoding(this);
			this.responseBodyBytes = responseBodyEncoding.GetBytes(sString);
			this.oResponse["Content-Length"] = this.responseBodyBytes.LongLength.ToString();
		}
		[CodeDescription("Prepend a string to the response body. Updates Content-Length header. Note, you should call utilDecodeResponse first!")]
		public void utilPrependToResponseBody(string sString)
		{
			if (this.responseBodyBytes == null)
			{
				this.responseBodyBytes = Utilities.emptyByteArray;
			}
			Encoding responseBodyEncoding = Utilities.getResponseBodyEncoding(this);
			this.responseBodyBytes = Utilities.JoinByteArrays(responseBodyEncoding.GetBytes(sString), this.responseBodyBytes);
			this.oResponse.headers["Content-Length"] = this.responseBodyBytes.LongLength.ToString();
		}
		[CodeDescription("Find a string in the request body. Return its index or -1.")]
		public int utilFindInRequest(string sSearchFor, bool bCaseSensitive)
		{
			if (!this._HasRequestBody())
			{
				return -1;
			}
			string @string = Utilities.getEntityBodyEncoding(this.oRequest.headers, this.requestBodyBytes).GetString(this.requestBodyBytes);
			return @string.IndexOf(sSearchFor, bCaseSensitive ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase);
		}
		private bool _HasRequestBody()
		{
			return !Utilities.IsNullOrEmpty(this.requestBodyBytes);
		}
		private bool _HasResponseBody()
		{
			return !Utilities.IsNullOrEmpty(this.responseBodyBytes);
		}
		[CodeDescription("Find a string in the response body. Return its index or -1. Note, you should call utilDecodeResponse first!")]
		public int utilFindInResponse(string sSearchFor, bool bCaseSensitive)
		{
			if (!this._HasResponseBody())
			{
				return -1;
			}
			string @string = Utilities.getResponseBodyEncoding(this).GetString(this.responseBodyBytes);
			return @string.IndexOf(sSearchFor, bCaseSensitive ? StringComparison.InvariantCulture : StringComparison.InvariantCultureIgnoreCase);
		}
		[CodeDescription("Reset the SessionID counter to 0. This method can lead to confusing UI, so use sparingly.")]
		internal static void ResetSessionCounter()
		{
			Interlocked.Exchange(ref Session.cRequests, 0);
		}
		public Session(byte[] arrRequest, byte[] arrResponse) : this(arrRequest, arrResponse, SessionFlags.None)
		{
		}
		public Session(SessionData oSD) : this(oSD.arrRequest, oSD.arrResponse, SessionFlags.None)
		{
			this.LoadMetadata(new MemoryStream(oSD.arrMetadata));
			if (oSD.arrWebSocketMessages != null && oSD.arrWebSocketMessages.Length > 0)
			{
				WebSocket.LoadWebSocketMessagesFromStream(this, new MemoryStream(oSD.arrWebSocketMessages));
			}
		}
		public Session(byte[] arrRequest, byte[] arrResponse, SessionFlags oSF)
		{
			this.bBufferResponse = FiddlerApplication.Prefs.GetBoolPref("fiddler.ui.rules.bufferresponses", false);
			this.Timers = new SessionTimers();
			this._bAllowClientPipeReuse = true;
			this.oFlags = new StringDictionary();
			//base..ctor();
			if (Utilities.IsNullOrEmpty(arrRequest))
			{
				throw new ArgumentException("Missing request data for session");
			}
			if (Utilities.IsNullOrEmpty(arrResponse))
			{
				arrResponse = Encoding.ASCII.GetBytes("HTTP/1.1 0 FIDDLER GENERATED - RESPONSE DATA WAS MISSING\r\n\r\n");
			}
			this.state = SessionStates.Done;
			this.m_requestID = Interlocked.Increment(ref Session.cRequests);
			this.BitFlags = oSF;
			int count;
			int num;
			HTTPHeaderParseWarnings hTTPHeaderParseWarnings;
			if (!Parser.FindEntityBodyOffsetFromArray(arrRequest, out count, out num, out hTTPHeaderParseWarnings))
			{
				throw new InvalidDataException("Request corrupt, unable to find end of headers.");
			}
			int count2;
			int num2;
			if (!Parser.FindEntityBodyOffsetFromArray(arrResponse, out count2, out num2, out hTTPHeaderParseWarnings))
			{
				throw new InvalidDataException("Response corrupt, unable to find end of headers.");
			}
			this.requestBodyBytes = new byte[arrRequest.Length - num];
			this.responseBodyBytes = new byte[arrResponse.Length - num2];
			Buffer.BlockCopy(arrRequest, num, this.requestBodyBytes, 0, this.requestBodyBytes.Length);
			Buffer.BlockCopy(arrResponse, num2, this.responseBodyBytes, 0, this.responseBodyBytes.Length);
			string sData = CONFIG.oHeaderEncoding.GetString(arrRequest, 0, count) + "\r\n\r\n";
			string sHeaders = CONFIG.oHeaderEncoding.GetString(arrResponse, 0, count2) + "\r\n\r\n";
			this.oRequest = new ClientChatter(this, sData);
			this.oResponse = new ServerChatter(this, sHeaders);
		}
		internal Session(ClientPipe clientPipe, ServerPipe serverPipe)
		{
			this.bBufferResponse = FiddlerApplication.Prefs.GetBoolPref("fiddler.ui.rules.bufferresponses", false);
			this.Timers = new SessionTimers();
			this._bAllowClientPipeReuse = true;
			this.oFlags = new StringDictionary();
			//base..ctor();
			if (CONFIG.bDebugSpew)
			{
				this.OnStateChanged += delegate(object sender, StateChangeEventArgs e)
				{
					FiddlerApplication.DebugSpew(string.Format("onstatechange>#{0} moving from state '{1}' to '{2}' {3}", new object[]
					{
						this.Int32_0.ToString(),
						e.oldState,
						e.newState,
						Environment.StackTrace
					}));
				};
			}
			if (clientPipe != null)
			{
				this.Timers.ClientConnected = clientPipe.dtAccepted;
				this.m_clientIP = ((clientPipe.Address == null) ? null : clientPipe.Address.ToString());
				this.m_clientPort = clientPipe.Port;
				this.oFlags["x-clientIP"] = this.m_clientIP;
				this.oFlags["x-clientport"] = this.m_clientPort.ToString();
				if (clientPipe.LocalProcessID != 0)
				{
					this._LocalProcessID = clientPipe.LocalProcessID;
					this.oFlags["x-ProcessInfo"] = string.Format("{0}:{1}", clientPipe.LocalProcessName, this._LocalProcessID);
				}
			}
			else
			{
				this.Timers.ClientConnected = DateTime.Now;
			}
			this.oResponse = new ServerChatter(this);
			this.oRequest = new ClientChatter(this);
			this.oRequest.pipeClient = clientPipe;
			this.oResponse.pipeServer = serverPipe;
		}
		public Session(HTTPRequestHeaders oRequestHeaders, byte[] arrRequestBody)
		{
			this.bBufferResponse = FiddlerApplication.Prefs.GetBoolPref("fiddler.ui.rules.bufferresponses", false);
			this.Timers = new SessionTimers();
			this._bAllowClientPipeReuse = true;
			this.oFlags = new StringDictionary();
			//base..ctor();
			if (oRequestHeaders == null)
			{
				throw new ArgumentNullException("oRequestHeaders", "oRequestHeaders must not be null when creating a new Session.");
			}
			if (arrRequestBody == null)
			{
				arrRequestBody = Utilities.emptyByteArray;
			}
			if (CONFIG.bDebugSpew)
			{
				this.OnStateChanged += delegate(object sender, StateChangeEventArgs e)
				{
					FiddlerApplication.DebugSpew(string.Format("onstatechange>#{0} moving from state '{1}' to '{2}' {3}", new object[]
					{
						this.Int32_0.ToString(),
						e.oldState,
						e.newState,
						Environment.StackTrace
					}));
				};
			}
			this.Timers.ClientConnected = (this.Timers.ClientBeginRequest = (this.Timers.FiddlerGotRequestHeaders = DateTime.Now));
			this.m_clientIP = null;
			this.m_clientPort = 0;
			this.oFlags["x-clientIP"] = this.m_clientIP;
			this.oFlags["x-clientport"] = this.m_clientPort.ToString();
			this.oResponse = new ServerChatter(this);
			this.oRequest = new ClientChatter(this);
			this.oRequest.pipeClient = null;
			this.oResponse.pipeServer = null;
			this.oRequest.headers = oRequestHeaders;
			this.requestBodyBytes = arrRequestBody;
			this.m_state = SessionStates.AutoTamperRequestBefore;
		}
		internal void ExecuteUponAsyncRequest()
		{
			ThreadPool.UnsafeQueueUserWorkItem(new WaitCallback(this.Execute), null);
		}
		internal void Execute(object objThreadState)
		{
			try
			{
				this.InnerExecute();
				if (this.nextSession != null)
				{
					this.nextSession.ExecuteUponAsyncRequest();
					this.nextSession = null;
				}
			}
			catch (Exception eX)
			{
				FiddlerApplication.ReportException(eX, "Uncaught Exception in Session #" + this.Int32_0.ToString());
			}
		}
		private void InnerExecute()
		{
			if (this.oRequest == null || this.oResponse == null)
			{
				return;
			}
			if (!this._executeObtainRequest())
			{
				return;
			}
			if (this.HTTPMethodIs("CONNECT"))
			{
				this.isTunnel = true;
				if (this.oFlags.ContainsKey("x-replywithtunnel"))
				{
					this._ReturnSelfGeneratedCONNECTTunnel(this.hostname);
					return;
				}
			}
			if (this.m_state >= SessionStates.ReadingResponse)
			{
				if (this.isAnyFlagSet(SessionFlags.ResponseGeneratedByFiddler))
				{
					FiddlerApplication.DoResponseHeadersAvailable(this);
				}
			}
			else
			{
				if (this.oFlags.ContainsKey("x-replywithfile"))
				{
					this.oResponse = new ServerChatter(this, "HTTP/1.1 200 OK\r\nServer: Fiddler\r\n\r\n");
					if (this.LoadResponseFromFile(this.oFlags["x-replywithfile"]) && this.isAnyFlagSet(SessionFlags.ResponseGeneratedByFiddler))
					{
						FiddlerApplication.DoResponseHeadersAvailable(this);
					}
					this.oFlags["x-repliedwithfile"] = this.oFlags["x-replywithfile"];
					this.oFlags.Remove("x-replywithfile");
				}
				else
				{
					if (this.port < 0 || this.port > 65535)
					{
						FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInRequest, true, false, "HTTP Request specified an invalid port number.");
					}
					if (this._isDirectRequestToFiddler())
					{
						if (this.oRequest.headers.RequestPath.OICEndsWith(".pac"))
						{
							if (this.oRequest.headers.RequestPath.OICEndsWith("/proxy.pac"))
							{
								this._returnPACFileResponse();
								return;
							}
							if (this.oRequest.headers.RequestPath.OICEndsWith("/UpstreamProxy.pac"))
							{
								this._returnUpstreamPACFileResponse();
								return;
							}
						}
						if (this.oRequest.headers.RequestPath.OICEndsWith("/fiddlerroot.cer"))
						{
							Session._returnRootCert(this);
							return;
						}
						if (CONFIG.iReverseProxyForPort == 0)
						{
							this._returnEchoServiceResponse();
							return;
						}
						this.oFlags.Add("X-ReverseProxy", "1");
						this.host = string.Format("{0}:{1}", CONFIG.sReverseProxyHostname, CONFIG.iReverseProxyForPort);
					}

					while (true)
					{
						this.state = SessionStates.SendingRequest;
						if (!this.oResponse.ResendRequest())
						{
							break;
						}
						if (this.isAnyFlagSet(SessionFlags.RequestStreamed))
						{
							GenericTunnel.CreateTunnel(this, false);
						}
						this.Timers.ServerGotRequest = DateTime.Now;
						this.state = SessionStates.ReadingResponse;
						if (this.HTTPMethodIs("CONNECT") && !this.oResponse._bWasForwarded)
						{
							goto IL_2C0;
						}
						if (this.oResponse.ReadResponse())
						{
							goto IL_40D;
						}
						if (!this.oResponse.bServerSocketReused || this.state == SessionStates.Aborted)
						{
							goto IL_384;
						}
						FiddlerApplication.DebugSpew("[" + this.Int32_0.ToString() + "] ServerSocket Reuse failed. Restarting fresh.");
						this.oResponse.Initialize(true);
					}
					this.CloseSessionPipes(true);
					this.state = SessionStates.Aborted;
					return;
					IL_2C0:
					this.SetBitFlag(SessionFlags.ResponseGeneratedByFiddler, true);
					this.oResponse.headers = new HTTPResponseHeaders();
					this.oResponse.headers.HTTPVersion = this.oRequest.headers.HTTPVersion;
					this.oResponse.headers.SetStatus(200, "Connection Established");
					this.oResponse.headers.Add("FiddlerGateway", "Direct");
					this.oResponse.headers.Add("StartTime", DateTime.Now.ToString("HH:mm:ss.fff"));
					this.oResponse.headers.Add("Connection", "close");
					this.responseBodyBytes = Utilities.emptyByteArray;
					goto IL_56A;
					IL_384:
					FiddlerApplication.DebugSpew("Failed to read server response. Aborting.");
					if (this.state != SessionStates.Aborted)
					{
						string text = string.Empty;
						if (!Utilities.IsNullOrEmpty(this.responseBodyBytes))
						{
							text = Encoding.UTF8.GetString(this.responseBodyBytes);
						}
						text = string.Format("Server returned {0} bytes.{1}", this.oResponse.m_responseTotalDataCount, text);
						this.oRequest.FailSession(504, "Fiddler - Receive Failure", string.Format("[Fiddler] ReadResponse() failed: The server did not return a response for this request.{0}", text));
					}
					this.CloseSessionPipes(true);
					this.state = SessionStates.Aborted;
					return;
					IL_40D:
					if (200 == this.responseCode && this.isAnyFlagSet(SessionFlags.RequestStreamed))
					{
						this.responseBodyBytes = this.oResponse.TakeEntity();
						try
						{
							this.oRequest.pipeClient.Send(this.oResponse.headers.ToByteArray(true, true));
							this.oRequest.pipeClient.Send(this.responseBodyBytes);
							(this.__oTunnel as GenericTunnel).BeginResponseStreaming();
						}
						catch (Exception eX)
						{
							FiddlerApplication.Log.LogFormat("Failed to create RPC Tunnel {0}", new object[]
							{
								Utilities.DescribeException(eX)
							});
						}
						return;
					}
					if (this.isAnyFlagSet(SessionFlags.ResponseBodyDropped))
					{
						this.responseBodyBytes = Utilities.emptyByteArray;
						this.oResponse.FreeResponseDataBuffer();
					}
					else
					{
						this.responseBodyBytes = this.oResponse.TakeEntity();
						long num;
						if (this.oResponse.headers.Exists("Content-Length") && !this.HTTPMethodIs("HEAD") && long.TryParse(this.oResponse.headers["Content-Length"], NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out num) && num != this.responseBodyBytes.LongLength)
						{
							FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInResponse, true, true, string.Format("Content-Length mismatch: Response Header indicated {0:N0} bytes, but server sent {1:N0} bytes.", num, this.responseBodyBytes.LongLength));
						}
					}
				}
			}
			IL_56A:
			this.oFlags["x-ResponseBodyTransferLength"] = ((this.responseBodyBytes == null) ? "0" : this.responseBodyBytes.LongLength.ToString());
			this.state = SessionStates.AutoTamperResponseBefore;
			FiddlerApplication.DoBeforeResponse(this);
			if (this._handledAsAutomaticRedirect())
			{
				return;
			}
			if (this._handledAsAutomaticAuth())
			{
				return;
			}
			bool flag = false;
			if (this.m_state >= SessionStates.Done || this.isFlagSet(SessionFlags.ResponseStreamed))
			{
				this.FinishUISession(this.isFlagSet(SessionFlags.ResponseStreamed));
				if (this.isFlagSet(SessionFlags.ResponseStreamed) && this.oFlags.ContainsKey("log-drop-response-body"))
				{
					this.SetBitFlag(SessionFlags.ResponseBodyDropped, true);
					this.responseBodyBytes = Utilities.emptyByteArray;
				}
				flag = true;
			}
			if (flag)
			{
				if (this.m_state < SessionStates.Done)
				{
					this.m_state = SessionStates.Done;
				}
				FiddlerApplication.DoAfterSessionComplete(this);
			}
			else
			{
				if (this.oFlags.ContainsKey("x-replywithfile"))
				{
					this.LoadResponseFromFile(this.oFlags["x-replywithfile"]);
					this.oFlags["x-replacedwithfile"] = this.oFlags["x-replywithfile"];
					this.oFlags.Remove("x-replywithfile");
				}
				this.state = SessionStates.AutoTamperResponseAfter;
			}
			bool flag2 = false;
			if (this._isResponseMultiStageAuthChallenge())
			{
				flag2 = this._isNTLMType2();
			}
			if (this.m_state >= SessionStates.Done)
			{
				this.FinishUISession();
				flag = true;
			}
			if (!flag)
			{
				this.ReturnResponse(flag2);
			}
			if (flag && this.oRequest.pipeClient != null)
			{
				if (flag2 || this._MayReuseMyClientPipe())
				{
					this._createNextSession(flag2);
				}
				else
				{
					this.oRequest.pipeClient.End();
				}
				this.oRequest.pipeClient = null;
			}
			this.oResponse.releaseServerPipe();
		}
		private bool _handledAsAutomaticAuth()
		{
			if (this._isResponseAuthChallenge() && this.oFlags.ContainsKey("x-AutoAuth") && !this.oFlags.ContainsKey("x-AutoAuth-Failed"))
			{
				bool result;
				try
				{
					result = this._PerformInnerAuth();
				}
				catch (TypeLoadException arg)
				{
					FiddlerApplication.Log.LogFormat("!Warning: Automatic authentication failed. You should installl the latest .NET Framework 2.0/3.5 Service Pack from WindowsUpdate.\n" + arg, new object[0]);
					result = false;
				}
				return result;
			}
			this.__WebRequestForAuth = null;
			return false;
		}
		private bool _PerformInnerAuth()
		{
			bool flag = 407 == this.oResponse.headers.HTTPResponseCode;
			bool result;
			try
			{
				Uri uri = new Uri(this.fullUrl);
				if (CONFIG.bDebugSpew)
				{
					FiddlerApplication.DebugSpew("Performing automatic authentication to {0} in response to {1}", new object[]
					{
						uri,
						this.oResponse.headers.HTTPResponseCode
					});
				}
				if (this.__WebRequestForAuth == null)
				{
					this.__WebRequestForAuth = WebRequest.Create(uri);
				}
				Type type = this.__WebRequestForAuth.GetType();
				type.InvokeMember("Async", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.SetProperty, null, this.__WebRequestForAuth, new object[]
				{
					false
				});
				object obj = type.InvokeMember("ServerAuthenticationState", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetProperty, null, this.__WebRequestForAuth, new object[0]);
				if (obj == null)
				{
					throw new ApplicationException("Auth state is null");
				}
				Type type2 = obj.GetType();
				type2.InvokeMember("ChallengedUri", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.SetField, null, obj, new object[]
				{
					uri
				});
				string text = this.oFlags["X-AutoAuth-SPN"];
				if (text == null && !flag)
				{
					text = Session._GetSPNForUri(uri);
				}
				if (text != null)
				{
					if (CONFIG.bDebugSpew)
					{
						FiddlerApplication.DebugSpew("Authenticating to '{0}' using SPN='{1}' for authentication challenge", new object[]
						{
							uri,
							text
						});
					}
					type2.InvokeMember("ChallengedSpn", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.SetField, null, obj, new object[]
					{
						text
					});
				}
				try
				{
					if (this.oResponse.pipeServer.bIsSecured)
					{
						TransportContext transportContext = this.oResponse.pipeServer._GetTransportContext();
						if (transportContext != null)
						{
							type2.InvokeMember("_TransportContext", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.SetField, null, obj, new object[]
							{
								transportContext
							});
						}
					}
				}
				catch (Exception ex)
				{
					FiddlerApplication.Log.LogFormat("Cannot get TransportContext. You may need to upgrade to a later .NET Framework. {0}", new object[]
					{
						ex.Message
					});
				}
				string challenge = flag ? this.oResponse["Proxy-Authenticate"] : this.oResponse["WWW-Authenticate"];
				ICredentials credentials;
				if (this.oFlags["x-AutoAuth"].Contains(":"))
				{
					string text2 = Utilities.TrimAfter(this.oFlags["x-AutoAuth"], ':');
					if (text2.Contains("\\"))
					{
						string domain = Utilities.TrimAfter(text2, '\\');
						text2 = Utilities.TrimBefore(text2, '\\');
						credentials = new NetworkCredential(text2, Utilities.TrimBefore(this.oFlags["x-AutoAuth"], ':'), domain);
					}
					else
					{
						credentials = new NetworkCredential(text2, Utilities.TrimBefore(this.oFlags["x-AutoAuth"], ':'));
					}
				}
				else
				{
					credentials = CredentialCache.DefaultCredentials;
				}
				this.__WebRequestForAuth.Method = this.RequestMethod;
				Authorization authorization = AuthenticationManager.Authenticate(challenge, this.__WebRequestForAuth, credentials);
				if (authorization == null)
				{
					throw new Exception("AuthenticationManager.Authenticate returned null.");
				}
				string message = authorization.Message;
				this.nextSession = new Session(this.oRequest.pipeClient, this.oResponse.pipeServer);
				this.FireContinueTransaction(this, this.nextSession, ContinueTransactionReason.Authenticate);
				if (!authorization.Complete)
				{
					this.nextSession.__WebRequestForAuth = this.__WebRequestForAuth;
				}
				this.__WebRequestForAuth = null;
				this.nextSession.requestBodyBytes = this.requestBodyBytes;
				this.nextSession.oRequest.headers = (HTTPRequestHeaders)this.oRequest.headers.Clone();
				this.nextSession.oRequest.headers[flag ? "Proxy-Authorization" : "Authorization"] = message;
				this.nextSession.SetBitFlag(SessionFlags.RequestGeneratedByFiddler, true);
				this.nextSession.oFlags["x-From-Builder"] = "Stage2";
				int num;
				if (int.TryParse(this.oFlags["x-AutoAuth-Retries"], out num))
				{
					num--;
					if (num > 0)
					{
						this.nextSession.oFlags["x-AutoAuth"] = this.oFlags["x-AutoAuth"];
						this.nextSession.oFlags["x-AutoAuth-Retries"] = num.ToString();
					}
					else
					{
						this.nextSession.oFlags["x-AutoAuth-Failed"] = "true";
					}
				}
				else
				{
					this.nextSession.oFlags["x-AutoAuth-Retries"] = "5";
					this.nextSession.oFlags["x-AutoAuth"] = this.oFlags["x-AutoAuth"];
				}
				if (this.oFlags.ContainsKey("x-Builder-Inspect"))
				{
					this.nextSession.oFlags["x-Builder-Inspect"] = this.oFlags["x-Builder-Inspect"];
				}
				if (this.oFlags.ContainsKey("x-Builder-MaxRedir"))
				{
					this.nextSession.oFlags["x-Builder-MaxRedir"] = this.oFlags["x-Builder-MaxRedir"];
				}
				this.state = SessionStates.Done;
				this.nextSession.state = SessionStates.AutoTamperRequestBefore;
				this.FinishUISession(!this.bBufferResponse);
				result = true;
			}
			catch (Exception ex2)
			{
				FiddlerApplication.Log.LogFormat("Automatic authentication of Session #{0} was unsuccessful. {1}\n{2}", new object[]
				{
					this.Int32_0,
					Utilities.DescribeException(ex2),
					ex2.StackTrace
				});
				this.__WebRequestForAuth = null;
				result = false;
			}
			return result;
		}
		private static string _GetSPNForUri(Uri uriTarget)
		{
			int int32Pref = FiddlerApplication.Prefs.GetInt32Pref("fiddler.auth.SPNMode", 3);
			string text;
			switch (int32Pref)
			{
			case 0:
				return null;
			case 1:
				text = uriTarget.DnsSafeHost;
				goto IL_77;
			}
			text = uriTarget.DnsSafeHost;
			if (int32Pref == 3 || (uriTarget.HostNameType != UriHostNameType.IPv6 && uriTarget.HostNameType != UriHostNameType.IPv4 && text.IndexOf('.') == -1))
			{
				string canonicalName = DNSResolver.GetCanonicalName(uriTarget.DnsSafeHost);
				if (!string.IsNullOrEmpty(canonicalName))
				{
					text = canonicalName;
				}
			}
			IL_77:
			text = "HTTP/" + text;
			if (uriTarget.Port != 80 && uriTarget.Port != 443 && FiddlerApplication.Prefs.GetBoolPref("fiddler.auth.SPNIncludesPort", false))
			{
				text = text + ":" + uriTarget.Port.ToString();
			}
			return text;
		}
		public string GetRedirectTargetURL()
		{
			if (Utilities.IsRedirectStatus(this.responseCode) && Utilities.HasHeaders(this.oResponse))
			{
				return Session.GetRedirectTargetURL(this.fullUrl, this.oResponse["Location"]);
			}
			return null;
		}
		public static string GetRedirectTargetURL(string sBase, string sLocation)
		{
			int num = sLocation.IndexOf(":");
			string result;
			if (num >= 0)
			{
				if (sLocation.IndexOfAny(new char[]
				{
					'/',
					'?',
					'#'
				}) >= num)
				{
					result = sLocation;
					return result;
				}
			}
			try
			{
				Uri baseUri = new Uri(sBase);
				Uri uri = new Uri(baseUri, sLocation);
				result = uri.ToString();
			}
			catch (UriFormatException)
			{
				result = null;
			}
			return result;
		}
		private static bool isRedirectableURI(string sBase, string sLocation, out string sTarget)
		{
			sTarget = Session.GetRedirectTargetURL(sBase, sLocation);
			return sTarget != null && sTarget.OICStartsWithAny(new string[]
			{
				"http://",
				"https://",
				"ftp://"
			});
		}
		private bool _handledAsAutomaticRedirect()
		{
			if (this.oResponse.headers.HTTPResponseCode < 300 || this.oResponse.headers.HTTPResponseCode > 308 || !this.oFlags.ContainsKey("x-Builder-MaxRedir") || !this.oResponse.headers.Exists("Location"))
			{
				return false;
			}
			string text;
			if (!Session.isRedirectableURI(this.fullUrl, this.oResponse["Location"], out text))
			{
				return false;
			}
			this.nextSession = new Session(this.oRequest.pipeClient, null);
			this.FireContinueTransaction(this, this.nextSession, ContinueTransactionReason.Redirect);
			this.nextSession.oRequest.headers = (HTTPRequestHeaders)this.oRequest.headers.Clone();
			text = Utilities.TrimAfter(text, '#');
			try
			{
				this.nextSession.fullUrl = new Uri(text).AbsoluteUri;
			}
			catch (UriFormatException arg)
			{
				FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInResponse, false, false, string.Format("Redirect's Location header was malformed.\nLocation: {0}\n\n{1}", text, arg));
				this.nextSession.fullUrl = text;
			}
			if (this.oResponse.headers.HTTPResponseCode != 307)
			{
				if (this.oResponse.headers.HTTPResponseCode != 308)
				{
					if (!this.nextSession.HTTPMethodIs("HEAD"))
					{
						this.nextSession.RequestMethod = "GET";
					}
					this.nextSession.oRequest.headers.Remove("Content-Length");
					this.nextSession.oRequest.headers.Remove("Transfer-Encoding");
					this.nextSession.requestBodyBytes = Utilities.emptyByteArray;
					goto IL_1C8;
				}
			}
			this.nextSession.requestBodyBytes = Utilities.Dupe(this.requestBodyBytes);
			IL_1C8:
			this.nextSession.SetBitFlag(SessionFlags.RequestGeneratedByFiddler, true);
			this.nextSession["x-From-Builder"] = "Redir";
			if (this.oFlags.ContainsKey("x-AutoAuth"))
			{
				this.nextSession.oFlags["x-AutoAuth"] = this.oFlags["x-AutoAuth"];
			}
			if (this.oFlags.ContainsKey("x-Builder-Inspect"))
			{
				this.nextSession.oFlags["x-Builder-Inspect"] = this.oFlags["x-Builder-Inspect"];
			}
			int num;
			if (int.TryParse(this.oFlags["x-Builder-MaxRedir"], out num))
			{
				num--;
				if (num > 0)
				{
					this.nextSession.oFlags["x-Builder-MaxRedir"] = num.ToString();
				}
			}
			this.nextSession.state = SessionStates.AutoTamperRequestBefore;
			this.state = SessionStates.Done;
			this.FinishUISession(!this.bBufferResponse);
			return true;
		}
		private void ExecuteHTTPLintOnRequest()
		{
			if (this.fullUrl.Length > 2083)
			{
				FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInRequest, false, false, string.Format("[HTTPLint Warning] Request URL was {0} characters. WinINET-based clients encounter problems when dealing with URLs longer than 2083 characters.", this.fullUrl.Length));
			}
			if (this.oRequest.headers == null)
			{
				return;
			}
			if (this.oRequest.headers.ByteCount() > 16000)
			{
				FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInRequest, false, false, string.Format("[HTTPLint Warning] Request headers were {0} bytes long. Many servers will reject requests this large.", this.oRequest.headers.ByteCount()));
			}
		}
		private void ExecuteHTTPLintOnResponse()
		{
			if (this.responseBodyBytes != null && this.oResponse.headers != null)
			{
				if (this.oResponse.headers.Exists("Content-Encoding"))
				{
					if (this.oResponse.headers.ExistsAndContains("Content-Encoding", ","))
					{
						FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInResponse, false, false, string.Format("[HTTPLint Warning] Response appears to specify multiple encodings: '{0}'. This will prevent decoding in Internet Explorer.", this.oResponse.headers["Content-Encoding"]));
					}
					if (this.oResponse.headers.ExistsAndContains("Content-Encoding", "gzip") && this.oRequest != null && this.oRequest.headers != null && !this.oRequest.headers.ExistsAndContains("Accept-Encoding", "gzip"))
					{
						FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInResponse, false, false, "[HTTPLint] Illegal response. Response specified Content-Encoding: gzip, but request did not specify GZIP in Accept-Encoding.");
					}
					if (this.oResponse.headers.ExistsAndContains("Content-Encoding", "deflate") && this.oRequest != null && this.oRequest.headers != null && !this.oRequest.headers.ExistsAndContains("Accept-Encoding", "deflate"))
					{
						FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInResponse, false, false, "[HTTPLint] Illegal response. Response specified Content-Encoding: Deflate, but request did not specify Deflate in Accept-Encoding.");
					}
					if (this.oResponse.headers.ExistsAndContains("Content-Encoding", "chunked"))
					{
						FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInResponse, false, false, "[HTTPLint] Response specified Content-Encoding: chunked, but Chunked is a Transfer-Encoding.");
					}
				}
				if (this.oResponse.headers.ExistsAndContains("Transfer-Encoding", "chunked"))
				{
					if (Utilities.HasHeaders(this.oRequest) && "HTTP/1.0".OICEquals(this.oRequest.headers.HTTPVersion))
					{
						FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInResponse, false, false, "[HTTPLint] Invalid response. Responses to HTTP/1.0 clients MUST NOT specify a Transfer-Encoding.");
					}
					if (this.oResponse.headers.Exists("Content-Length"))
					{
						FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInResponse, false, false, "[HTTPLint] Invalid response headers. Messages MUST NOT include both a Content-Length header field and a non-identity transfer-coding.");
					}
					long num = 0L;
					long num2 = (long)this.responseBodyBytes.Length;
					if (!Utilities.IsChunkedBodyComplete(this, this.responseBodyBytes, 0L, (long)this.responseBodyBytes.Length, out num, out num2))
					{
						FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInResponse, false, false, "[HTTPLint] The HTTP Chunked response body was incomplete; most likely lacking the final 0-size chunk.");
					}
				}
				List<HTTPHeaderItem> list = this.oResponse.headers.FindAll("ETAG");
				if (list.Count > 1)
				{
					FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInResponse, false, false, string.Format("[HTTPLint] Response contained {0} ETag headers", list.Count));
				}
				if (list.Count > 0)
				{
					string value = list[0].Value;
					if (!value.EndsWith("\"") || (!value.StartsWith("\"") && !value.StartsWith("W/\"")))
					{
						FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInResponse, false, false, string.Format("[HTTPLint] ETag values must be a quoted string. Response ETag: {0}", value));
					}
				}
				if (!this.oResponse.headers.Exists("Date") && this.responseCode != 100 && this.responseCode != 101 && this.responseCode < 500 && !this.HTTPMethodIs("CONNECT"))
				{
					FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInResponse, false, false, "[HTTPLint] With rare exceptions, servers MUST include a DATE response header. RFC2616 Section 14.18");
				}
				if (this.responseCode != 304 && this.responseCode > 299 && this.responseCode < 399)
				{
					if (this.oResponse.headers.Exists("Location"))
					{
						if (this.oResponse["Location"].StartsWith("/"))
						{
							FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInResponse, false, false, string.Format("[HTTPLint] HTTP Location header must specify a fully-qualified URL. Location: {0}", this.oResponse["Location"]));
						}
					}
					else
					{
						FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInResponse, false, false, "[HTTPLint] HTTP/3xx redirect response headers lacked a Location header.");
					}
				}
				if (this.oResponse.headers.ExistsAndContains("Content-Type", "utf8"))
				{
					FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInResponse, false, false, "[HTTPLint] Content-Type header specified UTF-8 incorrectly. CharSet=UTF-8 is valid, CharSet=UTF8 is not.");
				}
				List<HTTPHeaderItem> list2 = this.oResponse.headers.FindAll("Set-Cookie");
				if (list2.Count > 0)
				{
					if (this.hostname.Contains("_"))
					{
						FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInResponse, false, false, "[HTTPLint Warning] Response sets a cookie, and server's hostname contains '_'. Internet Explorer does not permit cookies to be set on hostnames containing underscores. See http://support.microsoft.com/kb/316112");
					}
					foreach (HTTPHeaderItem current in list2)
					{
						string text = Utilities.GetCommaTokenValue(current.Value, "domain");
						if (!string.IsNullOrEmpty(text))
						{
							if (text.StartsWith("."))
							{
								text = text.Substring(1);
							}
							if (!this.hostname.EndsWith(text))
							{
								FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInResponse, false, false, string.Format("[HTTPLint] Illegal DOMAIN in Set-Cookie. Cookie from {0} specified 'domain={1}'", this.hostname, text));
							}
						}
						string text2 = Utilities.TrimAfter(current.Value, ';');
						string text3 = text2;
						for (int i = 0; i < text3.Length; i++)
						{
							char c = text3[i];
							if (c == ',')
							{
								FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInResponse, false, false, string.Format("[HTTPLint] Illegal comma in cookie. Set-Cookie: {0}.", text2));
							}
							else
							{
								if (c >= '\u0080')
								{
									FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInResponse, false, false, string.Format("[HTTPLint] Non-ASCII character found in Set-Cookie: {0}. Some browsers (Safari) may corrupt this cookie.", text2));
								}
							}
						}
					}
				}
				return;
			}
		}
		internal void _AssignID()
		{
			this.m_requestID = Interlocked.Increment(ref Session.cRequests);
		}
		private bool _executeObtainRequest()
		{
			if (this.state > SessionStates.ReadingRequest)
			{
				this.Timers.ClientBeginRequest = (this.Timers.FiddlerGotRequestHeaders = (this.Timers.ClientDoneRequest = DateTime.Now));
				this._AssignID();
			}
			else
			{
				this.state = SessionStates.ReadingRequest;
				if (!this.oRequest.ReadRequest())
				{
					if (this.oResponse != null)
					{
						this.oResponse._detachServerPipe();
					}
					if (this.oRequest.headers == null)
					{
						this.oFlags["ui-hide"] = "stealth-NewOrReusedClosedWithoutRequest";
					}
					this.CloseSessionPipes(true);
					this.state = SessionStates.Aborted;
					return false;
				}
				this.Timers.ClientDoneRequest = DateTime.Now;
				if (CONFIG.bDebugSpew)
				{
					FiddlerApplication.DebugSpew(string.Format("Session ID #{0} for request read from {1}.", this.m_requestID, this.oRequest.pipeClient));
				}
				bool result;
				try
				{
					this.requestBodyBytes = this.oRequest.TakeEntity();
					goto IL_11C;
				}
				catch (Exception eX)
				{
					FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInRequest, true, false, "Failed to obtain request body. " + Utilities.DescribeException(eX));
					this.CloseSessionPipes(true);
					this.state = SessionStates.Aborted;
					result = false;
				}
				return result;
			}
			IL_11C:
			this._replaceVirtualHostnames();
			if (Utilities.IsNullOrEmpty(this.requestBodyBytes) && Utilities.HTTPMethodRequiresBody(this.RequestMethod))
			{
				FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInRequest, true, false, "This HTTP method requires a request body.");
			}
			string text = this.oFlags["X-Original-Host"];
			if (text != null)
			{
				if (string.Empty == text)
				{
					FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInRequest, false, false, "HTTP/1.1 Request was missing the required HOST header.");
				}
				else
				{
					if (!FiddlerApplication.Prefs.GetBoolPref("fiddler.network.SetHostHeaderFromURL", true))
					{
						this.oFlags["X-OverrideHost"] = this.oFlags["X-URI-Host"];
					}
					FiddlerApplication.HandleHTTPError(this, SessionFlags.ProtocolViolationInRequest, false, false, string.Format("The Request's Host header did not match the URL's host component.\n\nURL Host:\t{0}\nHeader Host:\t{1}", this.oFlags["X-URI-Host"], this.oFlags["X-Original-Host"]));
				}
			}
			if (this.isHTTPS)
			{
				this.SetBitFlag(SessionFlags.IsHTTPS, true);
				this.SetBitFlag(SessionFlags.IsFTP, false);
			}
			else
			{
				if (this.isFTP)
				{
					this.SetBitFlag(SessionFlags.IsFTP, true);
					this.SetBitFlag(SessionFlags.IsHTTPS, false);
				}
			}
			this.state = SessionStates.AutoTamperRequestBefore;
			FiddlerApplication.DoBeforeRequest(this);
			if (this.m_state >= SessionStates.Done)
			{
				this.FinishUISession();
				return false;
			}
			if (this.m_state < SessionStates.AutoTamperRequestAfter)
			{
				this.state = SessionStates.AutoTamperRequestAfter;
			}
			return this.m_state < SessionStates.Done;
		}
		private bool _isResponseMultiStageAuthChallenge()
		{
			return (401 == this.oResponse.headers.HTTPResponseCode && this.oResponse.headers["WWW-Authenticate"].OICStartsWith("N")) || (407 == this.oResponse.headers.HTTPResponseCode && this.oResponse.headers["Proxy-Authenticate"].OICStartsWith("N"));
		}
		private bool _isResponseAuthChallenge()
		{
			if (401 == this.oResponse.headers.HTTPResponseCode)
			{
				return this.oResponse.headers.ExistsAndContains("WWW-Authenticate", "NTLM") || this.oResponse.headers.ExistsAndContains("WWW-Authenticate", "Negotiate") || this.oResponse.headers.ExistsAndContains("WWW-Authenticate", "Digest");
			}
			return 407 == this.oResponse.headers.HTTPResponseCode && (this.oResponse.headers.ExistsAndContains("Proxy-Authenticate", "NTLM") || this.oResponse.headers.ExistsAndContains("Proxy-Authenticate", "Negotiate") || this.oResponse.headers.ExistsAndContains("Proxy-Authenticate", "Digest"));
		}
		private void _replaceVirtualHostnames()
		{
			if (!this.hostname.OICEndsWith(".fiddler"))
			{
				return;
			}
			string text = this.hostname.ToLowerInvariant();
			string a;
			if ((a = text) != null)
			{
				if (!(a == "ipv4.fiddler"))
				{
					if (!(a == "localhost.fiddler"))
					{
						if (!(a == "ipv6.fiddler"))
						{
							return;
						}
						this.hostname = "[::1]";
					}
					else
					{
						this.hostname = "localhost";
					}
				}
				else
				{
					this.hostname = "127.0.0.1";
				}
				this.oFlags["x-UsedVirtualHost"] = text;
				this.bypassGateway = true;
				if (this.HTTPMethodIs("CONNECT"))
				{
					this.oFlags["x-OverrideCertCN"] = text;
				}
				return;
			}
		}
		private bool _isDirectRequestToFiddler()
		{
			if (this.port != CONFIG.ListenPort)
			{
				return false;
			}
			if (string.Equals(CONFIG.sFiddlerListenHostPort, this.host, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			string text = this.hostname.ToLowerInvariant();
			if (text == "localhost" || text == "localhost." || text == CONFIG.sAlternateHostname)
			{
				return true;
			}
			if (text.StartsWith("[") && text.EndsWith("]"))
			{
				text = text.Substring(1, text.Length - 2);
			}
			IPAddress iPAddress = Utilities.IPFromString(text);
			if (iPAddress != null)
			{
				try
				{
					if (IPAddress.IsLoopback(iPAddress))
					{
						bool result = true;
						return result;
					}
					IPAddress[] hostAddresses = Dns.GetHostAddresses(string.Empty);
					IPAddress[] array = hostAddresses;
					for (int i = 0; i < array.Length; i++)
					{
						IPAddress iPAddress2 = array[i];
						if (iPAddress2.Equals(iPAddress))
						{
							bool result = true;
							return result;
						}
					}
				}
				catch (Exception)
				{
				}
				return false;
			}
			return text.StartsWith(CONFIG.sMachineName) && (text.Length == CONFIG.sMachineName.Length || text == CONFIG.sMachineName + "." + CONFIG.sMachineDomain);
		}
		private void _returnEchoServiceResponse()
		{
			if (!FiddlerApplication.Prefs.GetBoolPref("fiddler.echoservice.enabled", true))
			{
				if (this.oRequest != null && this.oRequest.pipeClient != null)
				{
					this.oRequest.pipeClient.EndWithRST();
				}
				this.state = SessionStates.Aborted;
				return;
			}
			if (this.HTTPMethodIs("CONNECT"))
			{
				this.oRequest.FailSession(405, "Method Not Allowed", "This endpoint does not support HTTP CONNECTs. Try GET or POST instead.");
				return;
			}
			int num = 200;
			Action<Session> delLastChance = null;
			if (this.PathAndQuery.Length == 4 && Regex.IsMatch(this.PathAndQuery, "/\\d{3}"))
			{
				num = int.Parse(this.PathAndQuery.Substring(1));
				if (Utilities.IsRedirectStatus(num))
				{
					delLastChance = delegate(Session session_0)
					{
						session_0.oResponse["Location"] = "/200";
					};
				}
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendFormat("<!doctype html>\n<html><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=UTF-8\"><title>", new object[0]);
			if (num != 200)
			{
				stringBuilder.AppendFormat("[{0}] - ", num);
			}
			stringBuilder.Append("Fiddler Echo Service</title></head><body style=\"font-family: arial,sans-serif;\"><h1>Fiddler Echo Service</h1><br /><pre>");
			stringBuilder.Append(Utilities.HtmlEncode(this.oRequest.headers.ToString(true, true)));
			if (this.requestBodyBytes != null && this.requestBodyBytes.LongLength > 0L)
			{
				stringBuilder.Append(Utilities.HtmlEncode(Encoding.UTF8.GetString(this.requestBodyBytes)));
			}
			stringBuilder.Append("</pre>");
			stringBuilder.AppendFormat("This page returned a <b>HTTP/{0}</b> response <br />", num);
			if (this.oFlags.ContainsKey("X-ProcessInfo"))
			{
				stringBuilder.AppendFormat("Originating Process Information: <code>{0}</code><br />", this.oFlags["X-ProcessInfo"]);
			}
			stringBuilder.Append("<hr /><ul><li>To configure Fiddler as a reverse proxy instead of seeing this page, see <a href='" + CONFIG.GetUrl("REDIR") + "REVERSEPROXY'>Reverse Proxy Setup</a><li>You can download the <a href=\"FiddlerRoot.cer\">FiddlerRoot certificate</a></ul></body></html>");
			this.oRequest.BuildAndReturnResponse(num, "Fiddler Generated", stringBuilder.ToString(), delLastChance);
			this.state = SessionStates.Aborted;
		}
		private void _returnPACFileResponse()
		{
			this.utilCreateResponseAndBypassServer();
			this.oResponse.headers["Content-Type"] = "application/x-ns-proxy-autoconfig";
			this.oResponse.headers["Cache-Control"] = "max-age=60";
			this.oResponse.headers["Connection"] = "close";
			this.utilSetResponseBody(FiddlerApplication.oProxy._GetPACScriptText(FiddlerApplication.oProxy.IsAttached));
			this.state = SessionStates.Aborted;
			FiddlerApplication.DoResponseHeadersAvailable(this);
			this.ReturnResponse(false);
		}
		private void _returnUpstreamPACFileResponse()
		{
			this.utilCreateResponseAndBypassServer();
			this.oResponse.headers["Content-Type"] = "application/x-ns-proxy-autoconfig";
			this.oResponse.headers["Connection"] = "close";
			this.oResponse.headers["Cache-Control"] = "max-age=300";
			string text = FiddlerApplication.oProxy._GetUpstreamPACScriptText();
			if (string.IsNullOrEmpty(text))
			{
				this.responseCode = 404;
			}
			this.utilSetResponseBody(text);
			this.state = SessionStates.Aborted;
			this.ReturnResponse(false);
		}
		private static void _returnRootCert(Session oS)
		{
			oS.utilCreateResponseAndBypassServer();
			oS.oResponse.headers["Connection"] = "close";
			oS.oResponse.headers["Cache-Control"] = "max-age=0";
			byte[] rootCertBytes = CertMaker.getRootCertBytes();
			if (rootCertBytes != null)
			{
				oS.oResponse.headers["Content-Type"] = "application/x-x509-ca-cert";
				oS.responseBodyBytes = rootCertBytes;
				oS.oResponse.headers["Content-Length"] = oS.responseBodyBytes.Length.ToString();
			}
			else
			{
				oS.responseCode = 404;
				oS.oResponse.headers["Content-Type"] = "text/html; charset=UTF-8";
				oS.utilSetResponseBody("No root certificate was found. Have you enabled HTTPS traffic decryption in Fiddler yet?".PadRight(512, ' '));
			}
			FiddlerApplication.DoResponseHeadersAvailable(oS);
			oS.ReturnResponse(false);
		}
		private void _ReturnSelfGeneratedCONNECTTunnel(string sHostname)
		{
			this.SetBitFlag(SessionFlags.ResponseGeneratedByFiddler | SessionFlags.IsDecryptingTunnel, true);
			this.oResponse.headers = new HTTPResponseHeaders();
			this.oResponse.headers.SetStatus(200, "DecryptEndpoint Created");
			this.oResponse.headers.Add("Timestamp", DateTime.Now.ToString("HH:mm:ss.fff"));
			this.oResponse.headers.Add("FiddlerGateway", "AutoResponder");
			this.oResponse.headers.Add("Connection", "close");
			this.responseBodyBytes = Encoding.UTF8.GetBytes("This is a Fiddler-generated response to the client's request for a CONNECT tunnel.\n\n");
			this.oFlags["ui-backcolor"] = "Lavender";
			FiddlerApplication.DoBeforeResponse(this);
			this.state = SessionStates.Done;
			FiddlerApplication.DoAfterSessionComplete(this);
			if (CONFIG.bUseSNIForCN && !this.oFlags.ContainsKey("x-OverrideCertCN"))
			{
				string text = this.oFlags["https-Client-SNIHostname"];
				if (!string.IsNullOrEmpty(text) && text != sHostname)
				{
					this.oFlags["x-OverrideCertCN"] = this.oFlags["https-Client-SNIHostname"];
				}
			}
			string sHostname2 = this.oFlags["x-OverrideCertCN"] ?? sHostname;
			if (this.oRequest.pipeClient != null && this.oRequest.pipeClient.SecureClientPipe(sHostname2, this.oResponse.headers))
			{
				Session session = new Session(this.oRequest.pipeClient, null);
				this.oRequest.pipeClient = null;
				session.oFlags["x-serversocket"] = "AUTO-RESPONDER-GENERATED";
				session.Execute(null);
				return;
			}
			this.CloseSessionPipes(false);
		}
		private bool _isNTLMType2()
		{
			if (!this.oFlags.ContainsKey("x-SuppressProxySupportHeader"))
			{
				this.oResponse.headers["Proxy-Support"] = "Session-Based-Authentication";
			}
			if (407 == this.oResponse.headers.HTTPResponseCode)
			{
				if (this.oRequest.headers["Proxy-Authorization"].Length < 1)
				{
					return false;
				}
				if (!this.oResponse.headers.Exists("Proxy-Authenticate") || this.oResponse.headers["Proxy-Authenticate"].Length < 6)
				{
					return false;
				}
			}
			else
			{
				if (string.IsNullOrEmpty(this.oRequest.headers["Authorization"]))
				{
					return false;
				}
				if (!this.oResponse.headers.Exists("WWW-Authenticate") || this.oResponse.headers["WWW-Authenticate"].Length < 6)
				{
					return false;
				}
			}
			return true;
		}
		private bool _MayReuseMyClientPipe()
		{
			return CONFIG.bReuseClientSockets && this._bAllowClientPipeReuse && !this.oResponse.headers.ExistsAndEquals("Connection", "close") && !this.oRequest.headers.ExistsAndEquals("Connection", "close") && !this.oResponse.headers.ExistsAndEquals("Proxy-Connection", "close") && !this.oRequest.headers.ExistsAndEquals("Proxy-Connection", "close") && (this.oResponse.headers.HTTPVersion == "HTTP/1.1" || this.oResponse.headers.ExistsAndContains("Connection", "Keep-Alive"));
		}
		internal bool ReturnResponse(bool bForceClientServerPipeAffinity)
		{
			this.state = SessionStates.SendingResponse;
			bool flag = false;
			this.Timers.ClientBeginResponse = (this.Timers.ClientDoneResponse = DateTime.Now);
			bool result;
			try
			{
				if (this.oRequest.pipeClient != null && this.oRequest.pipeClient.Connected)
				{
					if (this.oFlags.ContainsKey("response-trickle-delay"))
					{
						int transmitDelay = int.Parse(this.oFlags["response-trickle-delay"]);
						this.oRequest.pipeClient.TransmitDelay = transmitDelay;
					}
					this.oRequest.pipeClient.Send(this.oResponse.headers.ToByteArray(true, true));
					this.oRequest.pipeClient.Send(this.responseBodyBytes);
					this.Timers.ClientDoneResponse = DateTime.Now;
					if (this.responseCode == 101 && this.oRequest != null && this.oResponse != null && this.oRequest.headers != null && this.oRequest.headers.ExistsAndContains("Upgrade", "WebSocket") && this.oResponse.headers != null && this.oResponse.headers.ExistsAndContains("Upgrade", "WebSocket"))
					{
						FiddlerApplication.Log.LogFormat("Upgrading Session #{0} to websocket", new object[]
						{
							this.Int32_0
						});
						WebSocket.CreateTunnel(this);
						this.state = SessionStates.Done;
						this.FinishUISession(false);
						result = true;
						return result;
					}
					if (this.responseCode == 200 && this.HTTPMethodIs("CONNECT") && this.oRequest.pipeClient != null)
					{
						bForceClientServerPipeAffinity = true;
						Socket rawSocket = this.oRequest.pipeClient.GetRawSocket();
						if (rawSocket != null)
						{
							byte[] array = new byte[1024];
							int num = rawSocket.Receive(array, SocketFlags.Peek);
							if (num == 0)
							{
								this.oFlags["x-CONNECT-Peek"] = "After the client received notice of the established CONNECT, it failed to send any data.";
								this.requestBodyBytes = Encoding.UTF8.GetBytes("After the client received notice of the established CONNECT, it failed to send any data.\n");
								if (this.isFlagSet(SessionFlags.SentToGateway))
								{
									this.PoisonServerPipe();
								}
								this.PoisonClientPipe();
								this.oRequest.pipeClient.End();
								flag = true;
								goto IL_445;
							}
							if (CONFIG.bDebugSpew)
							{
								FiddlerApplication.Log.LogFormat("Peeking at the first bytes from CONNECT'd client session {0} yielded:\n{1}", new object[]
								{
									this.Int32_0,
									Utilities.ByteArrayToHexView(array, 32, num)
								});
							}
							if (array[0] == 22 || array[0] == 128)
							{
								try
								{
									HTTPSClientHello hTTPSClientHello = new HTTPSClientHello();
									if (hTTPSClientHello.LoadFromStream(new MemoryStream(array, 0, num, false)))
									{
										this.requestBodyBytes = Encoding.UTF8.GetBytes(hTTPSClientHello.ToString() + "\n");
										this["https-Client-SessionID"] = hTTPSClientHello.SessionID;
										if (!string.IsNullOrEmpty(hTTPSClientHello.ServerNameIndicator))
										{
											this["https-Client-SNIHostname"] = hTTPSClientHello.ServerNameIndicator;
										}
									}
								}
								catch (Exception)
								{
								}
								CONNECTTunnel.CreateTunnel(this);
								flag = true;
								goto IL_445;
							}
							if (num <= 4 || array[0] != 71 || array[1] != 69 || array[2] != 84 || array[3] != 32)
							{
								this.oFlags["x-CONNECT-Peek"] = BitConverter.ToString(array, 0, Math.Min(num, 16));
								this.oFlags["x-no-decrypt"] = "PeekYieldedUnknownProtocol";
								CONNECTTunnel.CreateTunnel(this);
								flag = true;
								goto IL_445;
							}
							this.SetBitFlag(SessionFlags.IsWebSocketTunnel, true);
						}
					}
					if (bForceClientServerPipeAffinity || this._MayReuseMyClientPipe())
					{
						FiddlerApplication.DebugSpew("Creating next session with pipes from {0}.", new object[]
						{
							this.Int32_0
						});
						this._createNextSession(bForceClientServerPipeAffinity);
						flag = true;
					}
					else
					{
						if (CONFIG.bDebugSpew)
						{
							FiddlerApplication.DebugSpew(string.Format("fiddler.network.clientpipereuse> Closing client socket since bReuseClientSocket was false after returning [{0}]", this.String_0));
						}
						this.oRequest.pipeClient.End();
						flag = true;
					}
				}
				else
				{
					flag = true;
				}
				goto IL_445;
			}
			catch (Exception ex)
			{
				if (CONFIG.bDebugSpew)
				{
					FiddlerApplication.DebugSpew(string.Format("Write to client failed for Session #{0}; exception was {1}", this.Int32_0, ex.ToString()));
				}
				this.state = SessionStates.Aborted;
				goto IL_445;
			}
			return result;
			IL_445:
			this.oRequest.pipeClient = null;
			if (flag)
			{
				this.state = SessionStates.Done;
				try
				{
					this.FinishUISession(false);
				}
				catch (Exception)
				{
				}
			}
			FiddlerApplication.DoAfterSessionComplete(this);
			if (this.oFlags.ContainsKey("log-drop-response-body"))
			{
				this.oFlags["x-ResponseBodyFinalLength"] = ((this.responseBodyBytes != null) ? this.responseBodyBytes.LongLength.ToString() : "0");
				this.SetBitFlag(SessionFlags.ResponseBodyDropped, true);
				this.responseBodyBytes = Utilities.emptyByteArray;
			}
			return flag;
		}
		private void _createNextSession(bool bForceClientServerPipeAffinity)
		{
			if (this.oResponse != null && this.oResponse.pipeServer != null && (bForceClientServerPipeAffinity || this.oResponse.pipeServer.ReusePolicy == PipeReusePolicy.MarriedToClientPipe || this.oFlags.ContainsKey("X-ClientServerPipeAffinity")))
			{
				this.nextSession = new Session(this.oRequest.pipeClient, this.oResponse.pipeServer);
				this.oResponse.pipeServer = null;
				return;
			}
			this.nextSession = new Session(this.oRequest.pipeClient, null);
		}
		internal void FinishUISession()
		{
			this.FinishUISession(false);
		}
		internal void FinishUISession(bool bSynchronous)
		{
		}
	}
}
