using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Threading;
namespace Fiddler
{
    public class ServerChatter
    {
        internal static int _cbServerReadBuffer = 32768;
        public ServerPipe pipeServer;
        private Session m_session;
        private HTTPResponseHeaders m_inHeaders;
        internal bool _bWasForwarded;
        private PipeReadBuffer m_responseData;
        internal long m_responseTotalDataCount;
        private int iEntityBodyOffset;
        private int _iBodySeekProgress;
        private bool _bLeakedHeaders;
        private long _lngLeakedOffset;
        private long _lngLastChunkInfoOffset = -1L;
        public string MIMEType
        {
            get
            {
                if (this.headers == null)
                {
                    return string.Empty;
                }
                string text = this.headers["Content-Type"];
                if (text.Length > 0)
                {
                    text = Utilities.TrimAfter(text, ';').Trim();
                }
                return text;
            }
        }
        internal long _PeekDownloadProgress
        {
            get
            {
                if (this.m_responseData != null)
                {
                    return this.m_responseTotalDataCount;
                }
                return -1L;
            }
        }
        public int iTTFB
        {
            get
            {
                int num = (int)(this.m_session.Timers.ServerBeginResponse - this.m_session.Timers.FiddlerBeginRequest).TotalMilliseconds;
                if (num <= 0)
                {
                    return 0;
                }
                return num;
            }
        }
        public int iTTLB
        {
            get
            {
                int num = (int)(this.m_session.Timers.ServerDoneResponse - this.m_session.Timers.FiddlerBeginRequest).TotalMilliseconds;
                if (num <= 0)
                {
                    return 0;
                }
                return num;
            }
        }
        public bool bWasForwarded
        {
            get
            {
                return this._bWasForwarded;
            }
        }
        public bool bServerSocketReused
        {
            get
            {
                return this.m_session.isFlagSet(SessionFlags.ServerPipeReused);
            }
        }
        public HTTPResponseHeaders headers
        {
            get
            {
                return this.m_inHeaders;
            }
            set
            {
                if (value != null)
                {
                    this.m_inHeaders = value;
                }
            }
        }
        public string this[string sHeader]
        {
            get
            {
                if (this.m_inHeaders != null)
                {
                    return this.m_inHeaders[sHeader];
                }
                return string.Empty;
            }
            set
            {
                if (this.m_inHeaders == null)
                {
                    throw new InvalidDataException("Response Headers object does not exist");
                }
                this.m_inHeaders[sHeader] = value;
            }
        }
        internal byte[] _PeekAtBody()
        {
            if (this.iEntityBodyOffset < 1 || this.m_responseData == null || this.m_responseData.Length < 1L)
            {
                return Utilities.emptyByteArray;
            }
            int num = (int)this.m_responseData.Length - this.iEntityBodyOffset;
            if (num < 1)
            {
                return Utilities.emptyByteArray;
            }
            byte[] array = new byte[num];
            Buffer.BlockCopy(this.m_responseData.GetBuffer(), this.iEntityBodyOffset, array, 0, num);
            return array;
        }
        internal ServerChatter(Session oSession)
        {
            this.m_session = oSession;
            this.m_responseData = new PipeReadBuffer(false);
        }
        internal ServerChatter(Session oSession, string sHeaders)
        {
            this.m_session = oSession;
            this.m_inHeaders = Parser.ParseResponse(sHeaders);
        }
        internal void Initialize(bool bAllocatePipeReadBuffer)
        {
            if (bAllocatePipeReadBuffer)
            {
                this.m_responseData = new PipeReadBuffer(false);
            }
            else
            {
                this.m_responseData = null;
            }
            this.iEntityBodyOffset = 0;
            this._iBodySeekProgress = 0;
            this._lngLeakedOffset = 0L;
            this._lngLastChunkInfoOffset = -1L;
            this.m_inHeaders = null;
            this._bLeakedHeaders = false;
            this.pipeServer = null;
            this._bWasForwarded = false;
            this.m_session.SetBitFlag(SessionFlags.ServerPipeReused, false);
        }
        internal byte[] TakeEntity()
        {
            byte[] array;
            try
            {
                array = new byte[this.m_responseData.Length - (long)this.iEntityBodyOffset];
                this.m_responseData.Position = (long)this.iEntityBodyOffset;
                this.m_responseData.Read(array, 0, array.Length);
            }
            catch (OutOfMemoryException eX)
            {
                FiddlerApplication.ReportException(eX, "HTTP Response Too Large");
                array = Encoding.ASCII.GetBytes("Fiddler: Out of memory");
                this.m_session.PoisonServerPipe();
            }
            this.FreeResponseDataBuffer();
            return array;
        }
        internal void FreeResponseDataBuffer()
        {
            this.m_responseData.Dispose();
            this.m_responseData = null;
        }
        private bool HeadersAvailable()
        {
            if (this.iEntityBodyOffset > 0)
            {
                return true;
            }
            if (this.m_responseData == null)
            {
                return false;
            }
            byte[] buffer = this.m_responseData.GetBuffer();
            HTTPHeaderParseWarnings hTTPHeaderParseWarnings;
            if (Parser.FindEndOfHeaders(buffer, ref this._iBodySeekProgress, this.m_responseData.Length, out hTTPHeaderParseWarnings))
            {
                this.iEntityBodyOffset = this._iBodySeekProgress + 1;
                if (hTTPHeaderParseWarnings == HTTPHeaderParseWarnings.EndedWithLFLF)
                {
                    FiddlerApplication.HandleHTTPError(this.m_session, SessionFlags.ProtocolViolationInResponse, false, true, "The Server did not return properly formatted HTTP Headers. HTTP headers\nshould be terminated with CRLFCRLF. These were terminated with LFLF.");
                }
                if (hTTPHeaderParseWarnings == HTTPHeaderParseWarnings.EndedWithLFCRLF)
                {
                    FiddlerApplication.HandleHTTPError(this.m_session, SessionFlags.ProtocolViolationInResponse, false, true, "The Server did not return properly formatted HTTP Headers. HTTP headers\nshould be terminated with CRLFCRLF. These were terminated with LFCRLF.");
                }
                return true;
            }
            return false;
        }
        private bool ParseResponseForHeaders()
        {
            if (this.m_responseData == null || this.iEntityBodyOffset < 4)
            {
                return false;
            }
            this.m_inHeaders = new HTTPResponseHeaders(CONFIG.oHeaderEncoding);
            byte[] buffer = this.m_responseData.GetBuffer();
            string text = CONFIG.oHeaderEncoding.GetString(buffer, 0, this.iEntityBodyOffset).Trim();
            if (text == null || text.Length < 1)
            {
                this.m_inHeaders = null;
                return false;
            }
            string[] array = text.Replace("\r\n", "\n").Split(new char[]
            {
                '\n'
            });
            if (array.Length < 1)
            {
                return false;
            }
            int num = array[0].IndexOf(' ');
            if (num <= 0)
            {
                FiddlerApplication.HandleHTTPError(this.m_session, SessionFlags.ProtocolViolationInResponse, false, true, "Cannot parse HTTP response; Status line contains no spaces. Data:\n\n\t" + array[0]);
                return false;
            }
            this.m_inHeaders.HTTPVersion = array[0].Substring(0, num).ToUpperInvariant();
            array[0] = array[0].Substring(num + 1).Trim();
            if (!this.m_inHeaders.HTTPVersion.OICStartsWith("HTTP/"))
            {
                if (!this.m_inHeaders.HTTPVersion.OICStartsWith("ICY"))
                {
                    FiddlerApplication.HandleHTTPError(this.m_session, SessionFlags.ProtocolViolationInResponse, false, true, "Response does not start with HTTP. Data:\n\n\t" + array[0]);
                    return false;
                }
                this.m_session.bBufferResponse = false;
                this.m_session.oFlags["log-drop-response-body"] = "ICY";
            }
            this.m_inHeaders.HTTPResponseStatus = array[0];
            num = array[0].IndexOf(' ');
            bool flag;
            if (num > 0)
            {
                flag = int.TryParse(array[0].Substring(0, num).Trim(), NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out this.m_inHeaders.HTTPResponseCode);
            }
            else
            {
                string text2 = array[0].Trim();
                if (!(flag = int.TryParse(text2, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out this.m_inHeaders.HTTPResponseCode)))
                {
                    int i = 0;
                    while (i < text2.Length)
                    {
                        if (char.IsDigit(text2[i]))
                        {
                            i++;
                        }
                        else
                        {
                            if (flag = int.TryParse(text2.Substring(0, i), NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out this.m_inHeaders.HTTPResponseCode))
                            {
                                FiddlerApplication.HandleHTTPError(this.m_session, SessionFlags.ProtocolViolationInResponse, false, false, "The response's status line was missing a space between ResponseCode and ResponseStatus. Data:\n\n\t" + text2);
                                break;
                            }
                            break;
                        }
                    }
                }
            }
            if (!flag)
            {
                FiddlerApplication.HandleHTTPError(this.m_session, SessionFlags.ProtocolViolationInResponse, false, true, "The response's status line did not contain a ResponseCode. Data:\n\n\t" + array[0]);
                return false;
            }
            string empty = string.Empty;
            if (!Parser.ParseNVPHeaders(this.m_inHeaders, array, 1, ref empty))
            {
                FiddlerApplication.HandleHTTPError(this.m_session, SessionFlags.ProtocolViolationInResponse, false, true, "Incorrectly formed response headers.\n" + empty);
            }
            if (this.m_inHeaders.Exists("Content-Length") && this.m_inHeaders.ExistsAndContains("Transfer-Encoding", "chunked"))
            {
                FiddlerApplication.HandleHTTPError(this.m_session, SessionFlags.ProtocolViolationInRequest, false, false, "Content-Length response header MUST NOT be present when Transfer-Encoding is used (RFC2616 Section 4.4)");
            }
            return true;
        }
        private bool GetHeaders()
        {
            if (!this.HeadersAvailable())
            {
                return false;
            }
            if (!this.ParseResponseForHeaders())
            {
                this.m_session.SetBitFlag(SessionFlags.ProtocolViolationInResponse, true);
                this._PoisonPipe();
                string arg;
                if (this.m_responseData != null)
                {
                    arg = "<plaintext>\n" + Utilities.ByteArrayToHexView(this.m_responseData.GetBuffer(), 24, (int)Math.Min(this.m_responseData.Length, 2048L));
                }
                else
                {
                    arg = "{Fiddler:no data}";
                }
                this.m_session.oRequest.FailSession(500, "Fiddler - Bad Response", string.Format("[Fiddler] Response Header parsing failed.\n{0}Response Data:\n{1}", this.m_session.isFlagSet(SessionFlags.ServerPipeReused) ? "This can be caused by an illegal HTTP response earlier on this reused server socket-- for instance, a HTTP/304 response which illegally contains a body.\n" : string.Empty, arg));
                return true;
            }
            if (this.m_inHeaders.HTTPResponseCode > 99 && this.m_inHeaders.HTTPResponseCode < 200)
            {
                if (this.m_inHeaders.Exists("Content-Length") && "0" != this.m_inHeaders["Content-Length"].Trim())
                {
                    FiddlerApplication.HandleHTTPError(this.m_session, SessionFlags.ProtocolViolationInResponse, false, true, "HTTP/1xx responses MUST NOT contain a body, but a non-zero content-length was returned.");
                }
                if (this.m_inHeaders.HTTPResponseCode != 101 || !this.m_inHeaders.ExistsAndContains("Upgrade", "WebSocket"))
                {
                    if (FiddlerApplication.Prefs.GetBoolPref("fiddler.network.leakhttp1xx", true) && this.m_session.oRequest.pipeClient != null)
                    {
                        try
                        {
                            this.m_session.oRequest.pipeClient.Send(this.m_inHeaders.ToByteArray(true, true));
                            StringDictionary oFlags;
                            (oFlags = this.m_session.oFlags)["x-fiddler-Stream1xx"] = oFlags["x-fiddler-Stream1xx"] + "Returned a HTTP/" + this.m_inHeaders.HTTPResponseCode.ToString() + " message from the server.";
                            goto IL_279;
                        }
                        catch (Exception ex)
                        {
                            if (FiddlerApplication.Prefs.GetBoolPref("fiddler.network.streaming.abortifclientaborts", false))
                            {
                                throw new Exception("Leaking HTTP/1xx response to client failed", ex);
                            }
                            FiddlerApplication.Log.LogFormat("fiddler.network.streaming> Streaming of HTTP/1xx headers from #{0} to client failed: {1}", new object[]
                            {
                                this.m_session.Int32_0,
                                ex.Message
                            });
                            goto IL_279;
                        }
                    }
                    StringDictionary oFlags2;
                    (oFlags2 = this.m_session.oFlags)["x-fiddler-streaming"] = oFlags2["x-fiddler-streaming"] + "Eating a HTTP/" + this.m_inHeaders.HTTPResponseCode.ToString() + " message from the stream.";
                    IL_279:
                    this._deleteInformationalMessage();
                    return this.GetHeaders();
                }
            }
            return true;
        }
        private bool isResponseBodyComplete()
        {
            if (this.m_session.HTTPMethodIs("HEAD"))
            {
                return true;
            }
            if (this.m_session.HTTPMethodIs("CONNECT") && this.m_inHeaders.HTTPResponseCode == 200)
            {
                return true;
            }
            if (this.m_inHeaders.HTTPResponseCode == 200 && this.m_session.isFlagSet(SessionFlags.RequestStreamed))
            {
                this.m_session.bBufferResponse = true;
                return true;
            }
            if (this.m_inHeaders.HTTPResponseCode != 204 && this.m_inHeaders.HTTPResponseCode != 205 && this.m_inHeaders.HTTPResponseCode != 304 && (this.m_inHeaders.HTTPResponseCode <= 99 || this.m_inHeaders.HTTPResponseCode >= 200))
            {
                if (this.m_inHeaders.ExistsAndEquals("Transfer-Encoding", "chunked"))
                {
                    if (this._lngLastChunkInfoOffset < (long)this.iEntityBodyOffset)
                    {
                        this._lngLastChunkInfoOffset = (long)this.iEntityBodyOffset;
                    }
                    long num;
                    return Utilities.IsChunkedBodyComplete(this.m_session, this.m_responseData, this._lngLastChunkInfoOffset, out this._lngLastChunkInfoOffset, out num);
                }
                if (this.m_inHeaders.Exists("Content-Length"))
                {
                    long num2;
                    if (long.TryParse(this.m_inHeaders["Content-Length"], NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out num2) && num2 >= 0L)
                    {
                        return this.m_responseTotalDataCount >= (long)this.iEntityBodyOffset + num2;
                    }
                    FiddlerApplication.HandleHTTPError(this.m_session, SessionFlags.ProtocolViolationInResponse, true, true, "Content-Length response header is not a valid unsigned integer.\nContent-Length: " + this.m_inHeaders["Content-Length"]);
                    return true;
                }
                else
                {
                    if (!this.m_inHeaders.ExistsAndEquals("Connection", "close") && !this.m_inHeaders.ExistsAndEquals("Proxy-Connection", "close") && (!(this.m_inHeaders.HTTPVersion != "HTTP/1.1") || this.m_inHeaders.ExistsAndContains("Connection", "Keep-Alive")))
                    {
                        FiddlerApplication.HandleHTTPError(this.m_session, SessionFlags.ProtocolViolationInResponse, true, true, "No Connection: close, no Content-Length. No way to tell if the response is complete.");
                        return false;
                    }
                    return false;
                }
            }
            else
            {
                if (this.m_inHeaders.Exists("Content-Length") && "0" != this.m_inHeaders["Content-Length"].Trim())
                {
                    FiddlerApplication.HandleHTTPError(this.m_session, SessionFlags.ProtocolViolationInResponse, false, true, "This type of HTTP response MUST NOT contain a body, but a non-zero content-length was returned.");
                    return true;
                }
                return true;
            }
        }
        private void _deleteInformationalMessage()
        {
            this.m_inHeaders = null;
            byte[] array = new byte[this.m_responseData.Length - (long)this.iEntityBodyOffset];
            this.m_responseData.Position = (long)this.iEntityBodyOffset;
            this.m_responseData.Read(array, 0, array.Length);
            this.m_responseData.Dispose();
            this.m_responseData = new PipeReadBuffer(array.Length);
            this.m_responseData.Write(array, 0, array.Length);
            this.m_responseTotalDataCount = 0L;
            this._iBodySeekProgress = 0;
            this.iEntityBodyOffset = 0;
        }
        internal void releaseServerPipe()
        {
            if (this.pipeServer == null)
            {
                return;
            }
            if (this.headers.ExistsAndEquals("Connection", "close") || this.headers.ExistsAndEquals("Proxy-Connection", "close") || (this.headers.HTTPVersion != "HTTP/1.1" && !this.headers.ExistsAndContains("Connection", "Keep-Alive")) || !this.pipeServer.Connected)
            {
                this.pipeServer.ReusePolicy = PipeReusePolicy.NoReuse;
            }
            this._detachServerPipe();
        }
        internal void _detachServerPipe()
        {
            if (this.pipeServer == null)
            {
                return;
            }
            if (this.pipeServer.ReusePolicy != PipeReusePolicy.NoReuse && this.pipeServer.ReusePolicy != PipeReusePolicy.MarriedToClientPipe && this.pipeServer.isClientCertAttached && !this.pipeServer.isAuthenticated)
            {
                this.pipeServer.MarkAsAuthenticated(this.m_session.LocalProcessID);
            }
            Proxy.htServerPipePool.PoolOrClosePipe(this.pipeServer);
            this.pipeServer = null;
        }
        private static bool SIDsMatch(int iPID, string sIDSession, string sIDPipe)
        {
            return string.Equals(sIDSession, sIDPipe, StringComparison.Ordinal) || (iPID != 0 && string.Equals(string.Format("PID{0}*{1}", iPID, sIDSession), sIDPipe, StringComparison.Ordinal));
        }
        private bool ConnectToHost()
        {
            string text = this.m_session.oFlags["x-overrideHostName"];
            if (text != null)
            {
                this.m_session.oFlags["x-overrideHost"] = string.Format("{0}:{1}", text, this.m_session.port);
            }
            text = this.m_session.oFlags["x-overrideHost"];
            if (text == null)
            {
                if (this.m_session.HTTPMethodIs("CONNECT"))
                {
                    text = this.m_session.PathAndQuery;
                }
                else
                {
                    text = this.m_session.host;
                }
            }
            bool flag = false;
            IPEndPoint[] array = null;
            if (this.m_session.oFlags["x-overrideGateway"] != null)
            {
                if ("DIRECT".OICEquals(this.m_session.oFlags["x-overrideGateway"]))
                {
                    this.m_session.bypassGateway = true;
                }
                else
                {
                    string text2 = this.m_session.oFlags["x-overrideGateway"];
                    if (text2.OICStartsWith("socks="))
                    {
                        flag = true;
                        text2 = text2.Substring(6);
                    }
                    array = Utilities.IPEndPointListFromHostPortString(text2);
                    if (flag && array == null)
                    {
                        this.m_session.oRequest.FailSession(502, "Fiddler - SOCKS Proxy DNS Lookup Failed", string.Format("[Fiddler] SOCKS DNS Lookup for \"{0}\" failed. {1}", Utilities.HtmlEncode(text2), NetworkInterface.GetIsNetworkAvailable() ? string.Empty : "The system reports that no network connection is available. \n"));
                        return false;
                    }
                }
            }
            else
            {
                if (!this.m_session.bypassGateway)
                {
                    int tickCount = Environment.TickCount;
                    string text3 = this.m_session.oRequest.headers.UriScheme;
                    if (text3 == "http" && this.m_session.HTTPMethodIs("CONNECT"))
                    {
                        text3 = "https";
                    }
                    IPEndPoint iPEndPoint = FiddlerApplication.oProxy.FindGatewayForOrigin(text3, text);
                    if (iPEndPoint != null)
                    {
                        array = new IPEndPoint[]
                        {
                            iPEndPoint
                        };
                    }
                    this.m_session.Timers.GatewayDeterminationTime = Environment.TickCount - tickCount;
                }
            }
            if (array != null)
            {
                this._bWasForwarded = true;
            }
            else
            {
                if (this.m_session.isFTP)
                {
                    return true;
                }
            }
            int num = this.m_session.isHTTPS ? 443 : (this.m_session.isFTP ? 21 : 80);
            string text4;
            Utilities.CrackHostAndPort(text, out text4, ref num);
            string text5;
            if (array != null)
            {
                if (!this.m_session.isHTTPS && !flag)
                {
                    text5 = string.Format("GW:{0}->*", array[0]);
                }
                else
                {
                    text5 = string.Format("{0}:{1}->{2}/{3}:{4}", new object[]
                    {
                        flag ? "SOCKS" : "GW",
                        array[0],
                        this.m_session.isHTTPS ? "https" : "http",
                        text4,
                        num
                    });
                }
            }
            else
            {
                text5 = string.Format("DIRECT->{0}{1}:{2}", this.m_session.isHTTPS ? "https/" : "http/", text4, num);
            }
            if (this.pipeServer != null && !this.m_session.oFlags.ContainsKey("X-ServerPipe-Marriage-Trumps-All") && !ServerChatter.SIDsMatch(this.m_session.LocalProcessID, text5, this.pipeServer.sPoolKey))
            {
                FiddlerApplication.Log.LogFormat("Session #{0} detaching server pipe. Had: '{1}' but needs: '{2}'", new object[]
                {
                    this.m_session.Int32_0,
                    this.pipeServer.sPoolKey,
                    text5
                });
                this.m_session.oFlags["X-Divorced-ServerPipe"] = string.Format("Had: '{0}' but needs: '{1}'", this.pipeServer.sPoolKey, text5);
                this._detachServerPipe();
            }
            if (this.pipeServer == null && !this.m_session.oFlags.ContainsKey("X-Bypass-ServerPipe-Reuse-Pool"))
            {
                this.pipeServer = Proxy.htServerPipePool.TakePipe(text5, this.m_session.LocalProcessID, this.m_session.Int32_0);
            }
            if (this.pipeServer != null)
            {
                this.m_session.Timers.ServerConnected = this.pipeServer.dtConnected;
                StringDictionary oFlags;
                (oFlags = this.m_session.oFlags)["x-serversocket"] = oFlags["x-serversocket"] + "REUSE " + this.pipeServer._sPipeName;
                if (this.pipeServer.Address != null && !this.pipeServer.isConnectedToGateway)
                {
                    this.m_session.m_hostIP = this.pipeServer.Address.ToString();
                    this.m_session.oFlags["x-hostIP"] = this.m_session.m_hostIP;
                }
                if (CONFIG.bDebugSpew)
                {
                    FiddlerApplication.DebugSpew(string.Format("Session #{0} ({1} {2}): Reusing {3}\r\n", new object[]
                    {
                        this.m_session.Int32_0,
                        this.m_session.RequestMethod,
                        this.m_session.fullUrl,
                        this.pipeServer.ToString()
                    }));
                }
                return true;
            }
            if (this.m_session.oFlags.ContainsKey("x-serversocket"))
            {
                StringDictionary oFlags2;
                (oFlags2 = this.m_session.oFlags)["x-serversocket"] = oFlags2["x-serversocket"] + "*NEW*";
            }
            IPEndPoint[] arrDest = null;
            bool result;
            if (array == null)
            {
                if (num >= 0 && num <= 65535)
                {
                    try
                    {
                        IPAddress[] iPAddressList = DNSResolver.GetIPAddressList(text4, true, this.m_session.Timers);
                        List<IPEndPoint> list = new List<IPEndPoint>();
                        IPAddress[] array2 = iPAddressList;
                        for (int i = 0; i < array2.Length; i++)
                        {
                            IPAddress address = array2[i];
                            list.Add(new IPEndPoint(address, num));
                        }
                        array = list.ToArray();
                        //goto IL_629;
                    }
                    catch (Exception eX)
                    {
                        this.m_session.oRequest.FailSession(502, "Fiddler - DNS Lookup Failed", string.Format("[Fiddler] DNS Lookup for \"{0}\" failed. {1}{2}", Utilities.HtmlEncode(text4), NetworkInterface.GetIsNetworkAvailable() ? string.Empty : "The system reports that no network connection is available. \n", Utilities.DescribeException(eX)));
                        result = false;
                        return result;
                    }
                }
                if (array == null)
                {
                    this.m_session.oRequest.FailSession(400, "Fiddler - Bad Request", "[Fiddler] HTTP Request specified an invalid port number.");
                    return false;
                }
            }
            arrDest = array;
            try
            {
                Socket socket = ServerChatter.CreateConnectedSocket(arrDest, this.m_session);
                if (flag)
                {
                    socket = this._SOCKSifyConnection(text4, num, socket);
                }
                if (this.m_session.isHTTPS && this._bWasForwarded)
                {
                    if (!ServerChatter.SendHTTPCONNECTToGateway(socket, text4, num, this.m_session.oRequest.headers))
                    {
                        throw new Exception("Upstream Gateway refused requested CONNECT.");
                    }
                    this.m_session.oFlags["x-CreatedTunnel"] = "Fiddler-Created-A-CONNECT-Tunnel";
                }
                this.pipeServer = new ServerPipe(socket, "ServerPipe#" + this.m_session.Int32_0.ToString(), this._bWasForwarded, text5);
                if (flag)
                {
                    this.pipeServer.isConnectedViaSOCKS = true;
                }
                if (this.m_session.isHTTPS && !this.pipeServer.SecureExistingConnection(this.m_session, text4, this.m_session.oFlags["https-Client-Certificate"], ref this.m_session.Timers.HTTPSHandshakeTime))
                {
                    string text6 = "Failed to negotiate HTTPS connection with server.";
                    if (!Utilities.IsNullOrEmpty(this.m_session.responseBodyBytes))
                    {
                        text6 += Encoding.UTF8.GetString(this.m_session.responseBodyBytes);
                    }
                    throw new SecurityException(text6);
                }
                result = true;
            }
            catch (Exception ex)
            {
                string text7 = string.Empty;
                bool flag2 = true;
                if (ex is SecurityException)
                {
                    flag2 = false;
                }
                SocketException ex2 = ex as SocketException;
                if (ex2 != null)
                {
                    if (ex2.ErrorCode != 10013)
                    {
                        if (ex2.ErrorCode != 10050)
                        {
                            text7 = string.Format("<br />ErrorCode: {0}.", ex2.ErrorCode);
                            goto IL_7E8;
                        }
                    }
                    text7 = string.Format("A Firewall may be blocking Fiddler's traffic. <br />ErrorCode: {0}.", ex2.ErrorCode);
                    flag2 = false;
                }
                IL_7E8:
                string sErrorStatusText;
                string arg;
                if (this._bWasForwarded)
                {
                    sErrorStatusText = "Fiddler - Gateway Connection Failed";
                    arg = "[Fiddler] The socket connection to the upstream proxy/gateway failed.";
                    if (flag2)
                    {
                        text7 = string.Format("Closing Fiddler, changing your system proxy settings, and restarting Fiddler may help. {0}", text7);
                    }
                }
                else
                {
                    sErrorStatusText = "Fiddler - Connection Failed";
                    arg = string.Format("[Fiddler] The socket connection to {0} failed.", Utilities.HtmlEncode(text4));
                }
                this.m_session.oRequest.FailSession(502, sErrorStatusText, string.Format("{0} {1} <br />{2}", arg, text7, Utilities.HtmlEncode(Utilities.DescribeException(ex))));
                result = false;
            }
            return result;
        }
        private Socket _SOCKSifyConnection(string sServerHostname, int iServerPort, Socket newSocket)
        {
            this._bWasForwarded = false;
            this.m_session.SetBitFlag(SessionFlags.SentToSOCKSGateway, true);
            FiddlerApplication.DebugSpew("Creating SOCKS connection to {0}.", new object[]
            {
                this.m_session.hostname
            });
            bool flag = false;
            byte[] buffer = ServerChatter._BuildSOCKS4ConnectHandshakeForTarget(sServerHostname, iServerPort);
            newSocket.Send(buffer);
            byte[] array = new byte[64];
            int num = newSocket.Receive(array);
            if (num > 1 && array[0] == 0 && array[1] == 90)
            {
                if (num > 7)
                {
                    string text = string.Format("{0}.{1}.{2}.{3}", new object[]
                    {
                        array[4],
                        array[5],
                        array[6],
                        array[7]
                    });
                    this.m_session.m_hostIP = text;
                    this.m_session.oFlags["x-hostIP"] = text;
                }
                flag = true;
            }
            if (!flag)
            {
                try
                {
                    newSocket.Close();
                }
                catch
                {
                }
                string text2 = string.Empty;
                if (num > 1 && array[0] == 0)
                {
                    int num2 = (int)array[1];
                    text2 = string.Format("Gateway returned error 0x{0:x}", num2);
                    switch (num2)
                    {
                        case 91:
                            text2 += "-'request rejected or failed'";
                            break;
                        case 92:
                            text2 += "-'request failed because client is not running identd (or not reachable from the server)'";
                            break;
                        case 93:
                            text2 += "-'request failed because client's identd could not confirm the user ID string in the request'";
                            break;
                        default:
                            text2 += "-'unknown'";
                            break;
                    }
                }
                else
                {
                    if (num > 0)
                    {
                        text2 = "Gateway returned a malformed response:\n" + Utilities.ByteArrayToHexView(array, 8, num);
                    }
                    else
                    {
                        text2 = "Gateway returned no data.";
                    }
                }
                throw new InvalidDataException(string.Format("SOCKS gateway failed: {0}", text2));
            }
            return newSocket;
        }
        private static byte[] _BuildSOCKS4ConnectHandshakeForTarget(string sTargetHost, int iPort)
        {
            MemoryStream memoryStream = new MemoryStream();
            byte[] bytes = Encoding.ASCII.GetBytes(sTargetHost);
            memoryStream.WriteByte(4);
            memoryStream.WriteByte(1);
            memoryStream.WriteByte((byte)(iPort >> 8));
            memoryStream.WriteByte((byte)(iPort & 255));
            memoryStream.WriteByte(0);
            memoryStream.WriteByte(0);
            memoryStream.WriteByte(0);
            memoryStream.WriteByte(127);
            memoryStream.WriteByte(0);
            memoryStream.Write(bytes, 0, bytes.Length);
            memoryStream.WriteByte(0);
            return memoryStream.ToArray();
        }
        private static bool SendHTTPCONNECTToGateway(Socket oGWSocket, string sHost, int iPort, HTTPRequestHeaders oRH)
        {
            string text = oRH["User-Agent"];
            string text2 = FiddlerApplication.Prefs.GetStringPref("fiddler.composer.HTTPSProxyBasicCreds", null);
            if (!string.IsNullOrEmpty(text2))
            {
                text2 = Convert.ToBase64String(Encoding.UTF8.GetBytes(text2));
            }
            string s = string.Format("CONNECT {0}:{1} HTTP/1.1\r\n{2}{3}Connection: close\r\n\r\n", new object[]
            {
                sHost,
                iPort,
                string.IsNullOrEmpty(text) ? string.Empty : string.Format("User-Agent: {0}\r\n", text),
                string.IsNullOrEmpty(text2) ? string.Empty : string.Format("Proxy-Authorization: Basic {0}\r\n", text2)
            });
            oGWSocket.Send(Encoding.ASCII.GetBytes(s));
            byte[] array = new byte[8192];
            int num = oGWSocket.Receive(array);
            if (num > 12 && Utilities.isHTTP200Array(array))
            {
                return true;
            }
            if (num > 12 && Utilities.isHTTP407Array(array))
            {
                FiddlerApplication.Log.LogFormat("fiddler.network.connect2> Upstream gateway demanded proxy authentication, which is not supported in this scenario. {0}", new object[]
                {
                    Encoding.UTF8.GetString(array, 0, Math.Min(num, 256))
                });
                return false;
            }
            FiddlerApplication.Log.LogFormat("fiddler.network.connect2> Unexpected response from upstream gateway {0}", new object[]
            {
                Encoding.UTF8.GetString(array, 0, Math.Min(num, 256))
            });
            return false;
        }
        private static Socket CreateConnectedSocket(IPEndPoint[] arrDest, Session _oSession)
        {
            Socket socket = null;
            bool flag = false;
            Stopwatch stopwatch = Stopwatch.StartNew();
            Exception ex = null;
            for (int i = 0; i < arrDest.Length; i++)
            {
                IPEndPoint iPEndPoint = arrDest[i];
                try
                {
                    socket = new Socket(iPEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    socket.NoDelay = true;
                    if (FiddlerApplication.oProxy._DefaultEgressEndPoint != null)
                    {
                        socket.Bind(FiddlerApplication.oProxy._DefaultEgressEndPoint);
                    }
                    socket.Connect(iPEndPoint);
                    _oSession.m_hostIP = iPEndPoint.Address.ToString();
                    _oSession.oFlags["x-hostIP"] = _oSession.m_hostIP;
                    FiddlerApplication.DoAfterSocketConnect(_oSession, socket);
                    flag = true;
                    break;
                }
                catch (Exception ex2)
                {
                    ex = ex2;
                    if (!FiddlerApplication.Prefs.GetBoolPref("fiddler.network.dns.fallback", true))
                    {
                        break;
                    }
                    _oSession.oFlags["x-DNS-Failover"] = _oSession.oFlags["x-DNS-Failover"] + "+1";
                }
            }
            _oSession.Timers.ServerConnected = DateTime.Now;
            _oSession.Timers.TCPConnectTime = (int)stopwatch.ElapsedMilliseconds;
            if (!flag)
            {
                throw ex;
            }
            return socket;
        }
        internal bool ResendRequest()
        {
            bool bool_ = this.pipeServer != null;
            if (!this.ConnectToHost())
            {
                FiddlerApplication.DebugSpew("Session #{0} ConnectToHost returned null. Bailing...", new object[]
                {
                    this.m_session.Int32_0
                });
                this.m_session.SetBitFlag(SessionFlags.ServerPipeReused, bool_);
                return false;
            }
            try
            {
                if (this.m_session.isFTP && !this.m_session.isFlagSet(SessionFlags.SentToGateway))
                {
                    bool result = true;
                    return result;
                }
                this.pipeServer.IncrementUse(this.m_session.Int32_0);
                this.pipeServer.setTimeouts();
                this.m_session.Timers.ServerConnected = this.pipeServer.dtConnected;
                this._bWasForwarded = this.pipeServer.isConnectedToGateway;
                this.m_session.SetBitFlag(SessionFlags.ServerPipeReused, this.pipeServer.iUseCount > 1u);
                this.m_session.SetBitFlag(SessionFlags.SentToGateway, this._bWasForwarded);
                if (!this._bWasForwarded && !this.m_session.isHTTPS)
                {
                    this.m_session.oRequest.headers.RenameHeaderItems("Proxy-Connection", "Connection");
                }
                if (!this.pipeServer.isAuthenticated)
                {
                    string text = this.m_session.oRequest.headers["Authorization"];
                    if (text != null && text.OICStartsWith("N"))
                    {
                        this.pipeServer.MarkAsAuthenticated(this.m_session.LocalProcessID);
                    }
                }
                this.m_session.Timers.FiddlerBeginRequest = DateTime.Now;
                if (this.m_session.oFlags.ContainsKey("request-trickle-delay"))
                {
                    int transmitDelay = int.Parse(this.m_session.oFlags["request-trickle-delay"]);
                    this.pipeServer.TransmitDelay = transmitDelay;
                }
                if (this._bWasForwarded || !this.m_session.HTTPMethodIs("CONNECT"))
                {
                    bool includeProtocolInPath = this._bWasForwarded && !this.m_session.isHTTPS;
                    byte[] oBytes = this.m_session.oRequest.headers.ToByteArray(true, true, includeProtocolInPath, this.m_session.oFlags["X-OverrideHost"]);
                    this.pipeServer.Send(oBytes);
                    if (this.m_session.requestBodyBytes != null && this.m_session.requestBodyBytes.Length > 0)
                    {
                        if (this.m_session.oFlags.ContainsKey("request-body-delay"))
                        {
                            int millisecondsTimeout = int.Parse(this.m_session.oFlags["request-body-delay"]);
                            Thread.Sleep(millisecondsTimeout);
                        }
                        this.pipeServer.Send(this.m_session.requestBodyBytes);
                    }
                }
            }
            catch (Exception eX)
            {
                bool result;
                if (this.bServerSocketReused && this.m_session.state != SessionStates.Aborted)
                {
                    this.pipeServer = null;
                    result = this.ResendRequest();
                    return result;
                }
                FiddlerApplication.DebugSpew("ResendRequest() failed: " + Utilities.DescribeException(eX));
                this.m_session.oRequest.FailSession(504, "Fiddler - Send Failure", "[Fiddler] ResendRequest() failed: " + Utilities.DescribeException(eX));
                result = false;
                return result;
            }
            this.m_session.oFlags["x-EgressPort"] = this.pipeServer.LocalPort.ToString();
            if (this.m_session.oFlags.ContainsKey("log-drop-request-body"))
            {
                this.m_session.oFlags["x-RequestBodyLength"] = ((this.m_session.requestBodyBytes != null) ? this.m_session.requestBodyBytes.Length.ToString() : "0");
                this.m_session.requestBodyBytes = Utilities.emptyByteArray;
            }
            return true;
        }
        private void _ReturnFileReadError(string sRemoteError, string sTrustedError)
        {
            this.Initialize(false);
            string text;
            if (this.m_session.LocalProcessID <= 0 && !this.m_session.isFlagSet(SessionFlags.RequestGeneratedByFiddler))
            {
                text = sRemoteError;
            }
            else
            {
                text = sTrustedError;
            }
            text = text.PadRight(512, ' ');
            this.m_session.responseBodyBytes = Encoding.UTF8.GetBytes(text);
            this.m_inHeaders = new HTTPResponseHeaders(CONFIG.oHeaderEncoding);
            this.m_inHeaders.HTTPResponseCode = 404;
            this.m_inHeaders.HTTPResponseStatus = "404 Not Found";
            this.m_inHeaders.Add("Content-Length", this.m_session.responseBodyBytes.Length.ToString());
            this.m_inHeaders.Add("Cache-Control", "max-age=0, must-revalidate");
        }
        internal bool ReadResponseFromArray(byte[] arrResponse, bool bAllowBOM, string sContentTypeHint)
        {
            this.Initialize(true);
            int num = arrResponse.Length;
            int num2 = 0;
            bool flag = false;
            if (bAllowBOM && (flag = (arrResponse.Length > 3 && arrResponse[0] == 239 && arrResponse[1] == 187 && arrResponse[2] == 191)))
            {
                num2 = 3;
                num -= 3;
            }
            bool flag2 = arrResponse.Length > 5 + num2 && arrResponse[num2] == 72 && arrResponse[num2 + 1] == 84 && arrResponse[num2 + 2] == 84 && arrResponse[num2 + 3] == 80 && arrResponse[num2 + 4] == 47;
            if (flag && !flag2)
            {
                num += 3;
                num2 = 0;
            }
            this.m_responseData.Capacity = num;
            this.m_responseData.Write(arrResponse, num2, num);
            if (flag2 && this.HeadersAvailable() && this.ParseResponseForHeaders())
            {
                this.m_session.responseBodyBytes = this.TakeEntity();
            }
            else
            {
                this.Initialize(false);
                this.m_inHeaders = new HTTPResponseHeaders(CONFIG.oHeaderEncoding);
                this.m_inHeaders.HTTPResponseCode = 200;
                this.m_inHeaders.HTTPResponseStatus = "200 OK with automatic headers";
                this.m_inHeaders["Date"] = DateTime.Now.ToUniversalTime().ToString("r");
                this.m_inHeaders["Content-Length"] = arrResponse.LongLength.ToString();
                this.m_inHeaders["Cache-Control"] = "max-age=0, must-revalidate";
                if (sContentTypeHint != null)
                {
                    this.m_inHeaders["Content-Type"] = sContentTypeHint;
                }
                this.m_session.responseBodyBytes = arrResponse;
            }
            return true;
        }
        internal bool ReadResponseFromFile(string sFilename, string sOptionalContentTypeHint)
        {
            if (!File.Exists(sFilename))
            {
                this._ReturnFileReadError("Fiddler - The requested file was not found.", "Fiddler - The file '" + sFilename + "' was not found.");
                return false;
            }
            byte[] arrResponse;
            bool result;
            try
            {
                arrResponse = File.ReadAllBytes(sFilename);
                goto IL_50;
            }
            catch (Exception eX)
            {
                this._ReturnFileReadError("Fiddler - The requested file could not be read.", "Fiddler - The requested file could not be read. " + Utilities.DescribeException(eX));
                result = false;
            }
            return result;
            IL_50:
            return this.ReadResponseFromArray(arrResponse, true, sOptionalContentTypeHint);
        }
        internal bool ReadResponseFromStream(Stream oResponse, string sContentTypeHint)
        {
            MemoryStream memoryStream = new MemoryStream();
            byte[] array = new byte[32768];
            int count;
            while ((count = oResponse.Read(array, 0, array.Length)) > 0)
            {
                memoryStream.Write(array, 0, count);
            }
            byte[] array2 = new byte[memoryStream.Length];
            memoryStream.Position = 0L;
            memoryStream.Read(array2, 0, array2.Length);
            return this.ReadResponseFromArray(array2, false, sContentTypeHint);
        }
        internal bool ReadResponse()
        {
            if (this.pipeServer != null)
            {
                bool flag = false;
                bool flag2 = false;
                bool flag3 = false;
                bool flag4 = false;
                byte[] array = new byte[ServerChatter._cbServerReadBuffer];
                while (true)
                {
                    try
                    {
                        int num = this.pipeServer.Receive(array);
                        if (0L == this.m_session.Timers.ServerBeginResponse.Ticks)
                        {
                            this.m_session.Timers.ServerBeginResponse = DateTime.Now;
                        }
                        if (num <= 0)
                        {
                            flag = true;
                            FiddlerApplication.DoReadResponseBuffer(this.m_session, array, 0);
                            if (CONFIG.bDebugSpew)
                            {
                                FiddlerApplication.DebugSpew(string.Format("READ FROM {0}: returned 0 signaling end-of-stream", this.pipeServer));
                            }
                        }
                        else
                        {
                            if (CONFIG.bDebugSpew)
                            {
                                FiddlerApplication.DebugSpew(string.Format("READ FROM {0}:\n{1}", this.pipeServer, Utilities.ByteArrayToHexView(array, 32, num)));
                            }
                            if (!FiddlerApplication.DoReadResponseBuffer(this.m_session, array, num))
                            {
                                flag2 = true;
                            }
                            this.m_responseData.Write(array, 0, num);
                            this.m_responseTotalDataCount += (long)num;
                            if (this.m_inHeaders == null && this.GetHeaders())
                            {
                                this.m_session.Timers.FiddlerGotResponseHeaders = DateTime.Now;
                                if (this.m_session.state == SessionStates.Aborted && this.m_session.isAnyFlagSet(SessionFlags.ProtocolViolationInResponse))
                                {
                                    bool result = false;
                                    return result;
                                }
                                FiddlerApplication.DoResponseHeadersAvailable(this.m_session);
                                string inStr = this.m_inHeaders["Content-Type"];
                                if (inStr.OICStartsWithAny(new string[]
                                {
                                    "text/event-stream",
                                    "multipart/x-mixed-replace"
                                }) && FiddlerApplication.Prefs.GetBoolPref("fiddler.network.streaming.AutoStreamByMIME", true))
                                {
                                    this.m_session.bBufferResponse = false;
                                }
                                else
                                {
                                    if (CONFIG.bStreamAudioVideo && inStr.OICStartsWithAny(new string[]
                                    {
                                        "video/",
                                        "audio/",
                                        "application/x-mms-framed"
                                    }))
                                    {
                                        this.m_session.bBufferResponse = false;
                                    }
                                }
                                if (!this.m_session.bBufferResponse && this.m_session.HTTPMethodIs("CONNECT"))
                                {
                                    this.m_session.bBufferResponse = true;
                                }
                                if (!this.m_session.bBufferResponse && 101 == this.m_inHeaders.HTTPResponseCode)
                                {
                                    this.m_session.bBufferResponse = true;
                                }
                                if (!this.m_session.bBufferResponse && this.m_session.oRequest.pipeClient == null)
                                {
                                    this.m_session.bBufferResponse = true;
                                }
                                if (!this.m_session.bBufferResponse && (401 == this.m_inHeaders.HTTPResponseCode || 407 == this.m_inHeaders.HTTPResponseCode) && this.m_session.oFlags.ContainsKey("x-AutoAuth"))
                                {
                                    this.m_session.bBufferResponse = true;
                                }
                                //what's this? (-_-!!)
                                //this.m_session.SetBitFlag(SessionFlags.ResponseStreamed, !this.m_session.bBufferResponse);
                                if (!this.m_session.bBufferResponse)
                                {
                                    if (this.m_session.oFlags.ContainsKey("response-trickle-delay"))
                                    {
                                        int transmitDelay = int.Parse(this.m_session.oFlags["response-trickle-delay"]);
                                        this.m_session.oRequest.pipeClient.TransmitDelay = transmitDelay;
                                    }
                                    if (this.m_session.oFlags.ContainsKey("log-drop-response-body") || FiddlerApplication.Prefs.GetBoolPref("fiddler.network.streaming.ForgetStreamedData", false))
                                    {
                                        flag3 = true;
                                    }
                                }
                            }
                            if (this.m_inHeaders != null && this.m_session.isFlagSet(SessionFlags.ResponseStreamed))
                            {
                                if (!flag4 && !this.LeakResponseBytes())
                                {
                                    flag4 = true;
                                }
                                if (flag3)
                                {
                                    this.m_session.SetBitFlag(SessionFlags.ResponseBodyDropped, true);
                                    if (this._lngLastChunkInfoOffset > -1L)
                                    {
                                        this.ReleaseStreamedChunkedData();
                                    }
                                    else
                                    {
                                        if (this.m_inHeaders.ExistsAndContains("Transfer-Encoding", "chunked"))
                                        {
                                            this.ReleaseStreamedChunkedData();
                                        }
                                        else
                                        {
                                            this.ReleaseStreamedData();
                                        }
                                    }
                                }
                            }
                        }
                        goto IL_55E;
                    }
                    catch (SocketException ex)
                    {
                        flag2 = true;
                        if (ex.ErrorCode == 10060)
                        {
                            this.m_session["X-ServerPipeError"] = "Timed out while reading response.";
                        }
                        else
                        {
                            FiddlerApplication.Log.LogFormat("fiddler.network.readresponse.failure> Session #{0} raised exception {1}", new object[]
                            {
                                this.m_session.Int32_0,
                                Utilities.DescribeException(ex)
                            });
                        }
                        goto IL_55E;
                    }
                    catch (Exception ex2)
                    {
                        flag2 = true;
                        if (ex2 is OperationCanceledException)
                        {
                            this.m_session.state = SessionStates.Aborted;
                            FiddlerApplication.Log.LogFormat("fiddler.network.readresponse.failure> Session #{0} was aborted {1}", new object[]
                            {
                                this.m_session.Int32_0,
                                Utilities.DescribeException(ex2)
                            });
                        }
                        else
                        {
                            if (ex2 is OutOfMemoryException)
                            {
                                FiddlerApplication.ReportException(ex2);
                                this.m_session.state = SessionStates.Aborted;
                                FiddlerApplication.Log.LogFormat("fiddler.network.readresponse.failure> Session #{0} Out of Memory", new object[]
                                {
                                    this.m_session.Int32_0
                                });
                            }
                            else
                            {
                                FiddlerApplication.Log.LogFormat("fiddler.network.readresponse.failure> Session #{0} raised exception {1}", new object[]
                                {
                                    this.m_session.Int32_0,
                                    Utilities.DescribeException(ex2)
                                });
                            }
                        }
                        goto IL_55E;
                    }
                    IL_543:
                    if (flag2)
                    {
                        break;
                    }
                    if (this.m_inHeaders != null && this.isResponseBodyComplete())
                    {
                        break;
                    }
                    continue;
                    IL_55E:
                    if (flag)
                    {
                        break;
                    }
                    goto IL_543;
                }
                this.m_session.Timers.ServerDoneResponse = DateTime.Now;
                if (this.m_session.isFlagSet(SessionFlags.ResponseStreamed))
                {
                    this.m_session.Timers.ClientDoneResponse = this.m_session.Timers.ServerDoneResponse;
                }
                if (0L == this.m_responseTotalDataCount && this.m_inHeaders == null)
                {
                    flag2 = true;
                }
                array = null;
                if (flag2)
                {
                    this.m_responseData.Dispose();
                    this.m_responseData = null;
                    return false;
                }
                if (this.m_inHeaders == null)
                {
                    FiddlerApplication.HandleHTTPError(this.m_session, SessionFlags.ProtocolViolationInResponse, true, true, "The Server did not return properly-formatted HTTP Headers. Maybe missing altogether (e.g. HTTP/0.9), maybe only \\r\\r instead of \\r\\n\\r\\n?\n");
                    this.m_session.SetBitFlag(SessionFlags.ResponseStreamed, false);
                    this.m_inHeaders = new HTTPResponseHeaders(CONFIG.oHeaderEncoding);
                    this.m_inHeaders.HTTPVersion = "HTTP/1.0";
                    this.m_inHeaders.HTTPResponseCode = 200;
                    this.m_inHeaders.HTTPResponseStatus = "200 This buggy server did not return headers";
                    this.iEntityBodyOffset = 0;
                    return true;
                }
                return true;
            }
            if (this.m_session.isFTP && !this.m_session.isFlagSet(SessionFlags.SentToGateway))
            {
                FTPGateway.MakeFTPRequest(this.m_session, ref this.m_responseData, out this.m_inHeaders);
                return true;
            }
            return false;
        }
        private void ReleaseStreamedChunkedData()
        {
            if ((long)this.iEntityBodyOffset > this._lngLastChunkInfoOffset)
            {
                this._lngLastChunkInfoOffset = (long)this.iEntityBodyOffset;
            }
            long num;
            Utilities.IsChunkedBodyComplete(this.m_session, this.m_responseData, this._lngLastChunkInfoOffset, out this._lngLastChunkInfoOffset, out num);
            int num2 = (int)(this.m_responseData.Length - this._lngLastChunkInfoOffset);
            PipeReadBuffer pipeReadBuffer = new PipeReadBuffer(num2);
            pipeReadBuffer.Write(this.m_responseData.GetBuffer(), (int)this._lngLastChunkInfoOffset, num2);
            this.m_responseData = pipeReadBuffer;
            this._lngLeakedOffset = (long)num2;
            this._lngLastChunkInfoOffset = 0L;
            this.iEntityBodyOffset = 0;
        }
        private void ReleaseStreamedData()
        {
            this.m_responseData = new PipeReadBuffer(false);
            this._lngLeakedOffset = 0L;
            if (this.iEntityBodyOffset > 0)
            {
                this.m_responseTotalDataCount -= (long)this.iEntityBodyOffset;
                this.iEntityBodyOffset = 0;
            }
        }
        private bool LeakResponseBytes()
        {
            bool result;
            try
            {
                if (this.m_session.oRequest.pipeClient == null)
                {
                    result = false;
                }
                else
                {
                    if (!this._bLeakedHeaders)
                    {
                        if ((401 == this.m_inHeaders.HTTPResponseCode && this.m_inHeaders["WWW-Authenticate"].OICStartsWith("N")) || (407 == this.m_inHeaders.HTTPResponseCode && this.m_inHeaders["Proxy-Authenticate"].OICStartsWith("N")))
                        {
                            this.m_inHeaders["Proxy-Support"] = "Session-Based-Authentication";
                        }
                        this.m_session.Timers.ClientBeginResponse = DateTime.Now;
                        this._bLeakedHeaders = true;
                        this.m_session.oRequest.pipeClient.Send(this.m_inHeaders.ToByteArray(true, true));
                        this._lngLeakedOffset = (long)this.iEntityBodyOffset;
                    }
                    this.m_session.oRequest.pipeClient.Send(this.m_responseData.GetBuffer(), (int)this._lngLeakedOffset, (int)(this.m_responseData.Length - this._lngLeakedOffset));
                    this._lngLeakedOffset = this.m_responseData.Length;
                    result = true;
                }
            }
            catch (Exception ex)
            {
                this.m_session.PoisonClientPipe();
                if (FiddlerApplication.Prefs.GetBoolPref("fiddler.network.streaming.abortifclientaborts", false))
                {
                    throw new OperationCanceledException("Leaking response to client failed", ex);
                }
                FiddlerApplication.Log.LogFormat("fiddler.network.streaming> Streaming of response #{0} to client failed: {1}. Leaking aborted.", new object[]
                {
                    this.m_session.Int32_0,
                    ex.Message
                });
                result = false;
            }
            return result;
        }
        internal void _PoisonPipe()
        {
            if (this.pipeServer != null)
            {
                this.pipeServer.ReusePolicy = PipeReusePolicy.NoReuse;
            }
        }
    }
}
