using System;
using System.Globalization;
using System.IO;
using System.Text;
namespace Fiddler
{
	public class ClientChatter
	{
		internal static int _cbClientReadBuffer = 8192;
		public ClientPipe pipeClient;
		private HTTPRequestHeaders m_headers;
		private Session m_session;
		private string m_sHostFromURI;
		private PipeReadBuffer m_requestData;
		private int iEntityBodyOffset;
		private int iBodySeekProgress;
		internal long _PeekUploadProgress
		{
			get
			{
				if (this.m_requestData == null)
				{
					return -1L;
				}
				long length = this.m_requestData.Length;
				if (length > (long)this.iEntityBodyOffset)
				{
					return length - (long)this.iEntityBodyOffset;
				}
				return length;
			}
		}
		public HTTPRequestHeaders headers
		{
			get
			{
				return this.m_headers;
			}
			set
			{
				this.m_headers = value;
			}
		}
		public bool bClientSocketReused
		{
			get
			{
				return this.m_session.isFlagSet(SessionFlags.ClientPipeReused);
			}
		}
		public string host
		{
			get
			{
				if (this.m_headers != null)
				{
					return this.m_headers["Host"];
				}
				return string.Empty;
			}
			internal set
			{
				if (value == null)
				{
					value = string.Empty;
				}
				if (this.m_headers != null)
				{
					if (value.EndsWith(":80") && "HTTP".OICEquals(this.m_headers.UriScheme))
					{
						value = value.Substring(0, value.Length - 3);
					}
					this.m_headers["Host"] = value;
					if ("CONNECT".OICEquals(this.m_headers.HTTPMethod))
					{
						this.m_headers.RequestPath = value;
					}
				}
			}
		}
		public string this[string sHeader]
		{
			get
			{
				if (this.m_headers != null)
				{
					return this.m_headers[sHeader];
				}
				return string.Empty;
			}
			set
			{
				if (this.m_headers == null)
				{
					throw new InvalidDataException("Request Headers object does not exist");
				}
				this.m_headers[sHeader] = value;
			}
		}
		internal ClientChatter(Session oSession)
		{
			this.m_session = oSession;
		}
		internal ClientChatter(Session oSession, string sData)
		{
			this.m_session = oSession;
			this.headers = Parser.ParseRequest(sData);
			if (this.headers != null && "CONNECT" == this.m_headers.HTTPMethod)
			{
				this.m_session.isTunnel = true;
			}
		}
		internal bool ReadRequestBodyFromFile(string sFilename)
		{
			if (File.Exists(sFilename))
			{
				this.m_session.requestBodyBytes = File.ReadAllBytes(sFilename);
				this.m_headers["Content-Length"] = this.m_session.requestBodyBytes.Length.ToString();
				return true;
			}
			this.m_session.requestBodyBytes = Encoding.UTF8.GetBytes("File not found: " + sFilename);
			this.m_headers["Content-Length"] = this.m_session.requestBodyBytes.Length.ToString();
			return false;
		}
		private long _calculateExpectedEntityTransferSize()
		{
			long num = 0L;
			if (this.m_headers.ExistsAndEquals("Transfer-encoding", "chunked"))
			{
				if ((long)this.iEntityBodyOffset >= this.m_requestData.Length)
				{
					throw new InvalidDataException("Bad request: Chunked Body was missing entirely.");
				}
				long num2;
				long num3;
				if (!Utilities.IsChunkedBodyComplete(this.m_session, this.m_requestData, (long)this.iEntityBodyOffset, out num2, out num3))
				{
					throw new InvalidDataException("Bad request: Chunked Body was incomplete.");
				}
				if (num3 < (long)this.iEntityBodyOffset)
				{
					throw new InvalidDataException("Bad request: Chunked Body was malformed. Entity ends before it starts!");
				}
				return num3 - (long)this.iEntityBodyOffset;
			}
			else
			{
				if (long.TryParse(this.m_headers["Content-Length"], NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out num) && num > -1L)
				{
					return num;
				}
				return num;
			}
		}
		private void _freeRequestData()
		{
			if (this.m_requestData != null)
			{
				this.m_requestData.Dispose();
				this.m_requestData = null;
			}
		}
		internal byte[] TakeEntity()
		{
			if (this.iEntityBodyOffset < 0)
			{
				throw new InvalidDataException("Request Entity Body Offset must not be negative");
			}
			long num = this.m_requestData.Length - (long)this.iEntityBodyOffset;
			long num2 = this._calculateExpectedEntityTransferSize();
			byte[] array;
			if (num != num2)
			{
				if (num > num2)
				{
					FiddlerApplication.Log.LogFormat("HTTP Pipelining Client detected; excess data on client socket for session #{0}.", new object[]
					{
						this.m_session.Int32_0
					});
					byte[] emptyByteArray;
					try
					{
						array = new byte[num - num2];
						this.m_requestData.Position = (long)this.iEntityBodyOffset + num2;
						this.m_requestData.Read(array, 0, array.Length);
						goto IL_CD;
					}
					catch (OutOfMemoryException eX)
					{
						FiddlerApplication.ReportException(eX, "HTTP Request Pipeline Too Large");
						array = Encoding.ASCII.GetBytes("Fiddler: Out of memory");
						this.m_session.PoisonClientPipe();
						emptyByteArray = Utilities.emptyByteArray;
					}
					return emptyByteArray;
					IL_CD:
					this.pipeClient.putBackSomeBytes(array);
					num = num2;
				}
				else
				{
					if (!this.m_session.isFlagSet(SessionFlags.RequestStreamed))
					{
						FiddlerApplication.HandleHTTPError(this.m_session, SessionFlags.ProtocolViolationInRequest, true, true, string.Format("Content-Length mismatch: Request Header indicated {0:N0} bytes, but client sent {1:N0} bytes.", num2, num));
					}
				}
			}
			try
			{
				array = new byte[num];
				this.m_requestData.Position = (long)this.iEntityBodyOffset;
				this.m_requestData.Read(array, 0, array.Length);
			}
			catch (OutOfMemoryException eX2)
			{
				FiddlerApplication.ReportException(eX2, "HTTP Request Too Large");
				array = Encoding.ASCII.GetBytes("Fiddler: Out of memory");
				this.m_session.PoisonClientPipe();
			}
			this._freeRequestData();
			return array;
		}
		public void FailSession(int iError, string sErrorStatusText, string sErrorBody)
		{
			this.BuildAndReturnResponse(iError, sErrorStatusText, sErrorBody, null);
		}
		internal void BuildAndReturnResponse(int iStatus, string sStatusText, string sBodyText, Action<Session> delLastChance)
		{
			this.m_session.SetBitFlag(SessionFlags.ResponseGeneratedByFiddler, true);
			if (iStatus >= 400 && sBodyText.Length < 512)
			{
				sBodyText = sBodyText.PadRight(512, ' ');
			}
			this.m_session.responseBodyBytes = Encoding.UTF8.GetBytes(sBodyText);
			this.m_session.oResponse.headers = new HTTPResponseHeaders(CONFIG.oHeaderEncoding);
			this.m_session.oResponse.headers.SetStatus(iStatus, sStatusText);
			this.m_session.oResponse.headers.Add("Date", DateTime.Now.ToUniversalTime().ToString("r"));
			this.m_session.oResponse.headers.Add("Content-Type", "text/html; charset=UTF-8");
			this.m_session.oResponse.headers.Add("Connection", "close");
			this.m_session.oResponse.headers.Add("Timestamp", DateTime.Now.ToString("HH:mm:ss.fff"));
			this.m_session.state = SessionStates.Aborted;
			if (delLastChance != null)
			{
				delLastChance(this.m_session);
			}
			FiddlerApplication.DoBeforeReturningError(this.m_session);
			this.m_session.ReturnResponse(false);
		}
		private bool ParseRequestForHeaders()
		{
			if (this.m_requestData == null || this.iEntityBodyOffset < 4)
			{
				return false;
			}
			this.m_headers = new HTTPRequestHeaders(CONFIG.oHeaderEncoding);
			byte[] buffer = this.m_requestData.GetBuffer();
			int num;
			int num2;
			int num3;
			string text;
			Parser.CrackRequestLine(buffer, out num, out num2, out num3, out text);
			if (num >= 1 && num2 >= 1)
			{
				if (!string.IsNullOrEmpty(text))
				{
					FiddlerApplication.HandleHTTPError(this.m_session, SessionFlags.ProtocolViolationInRequest, true, false, text);
				}
				string @string = Encoding.ASCII.GetString(buffer, 0, num - 1);
				this.m_headers.HTTPMethod = @string.ToUpperInvariant();
				if (@string != this.m_headers.HTTPMethod)
				{
					FiddlerApplication.HandleHTTPError(this.m_session, SessionFlags.ProtocolViolationInRequest, false, false, string.Format("Per RFC2616, HTTP Methods are case-sensitive. Client sent '{0}', expected '{1}'.", @string, this.m_headers.HTTPMethod));
				}
				this.m_headers.HTTPVersion = Encoding.ASCII.GetString(buffer, num + num2 + 1, num3 - num2 - num - 2).Trim().ToUpperInvariant();
				int num4 = 0;
				if (buffer[num] != 47)
				{
					if (num2 > 7 && buffer[num + 4] == 58 && buffer[num + 5] == 47 && buffer[num + 6] == 47)
					{
						this.m_headers.UriScheme = Encoding.ASCII.GetString(buffer, num, 4);
						num4 = num + 6;
						num += 7;
						num2 -= 7;
					}
					else
					{
						if (num2 > 8 && buffer[num + 5] == 58 && buffer[num + 6] == 47 && buffer[num + 7] == 47)
						{
							this.m_headers.UriScheme = Encoding.ASCII.GetString(buffer, num, 5);
							num4 = num + 7;
							num += 8;
							num2 -= 8;
						}
						else
						{
							if (num2 > 6 && buffer[num + 3] == 58 && buffer[num + 4] == 47 && buffer[num + 5] == 47)
							{
								this.m_headers.UriScheme = Encoding.ASCII.GetString(buffer, num, 3);
								num4 = num + 5;
								num += 6;
								num2 -= 6;
							}
						}
					}
				}
				if (num4 == 0)
				{
					if (this.pipeClient != null && this.pipeClient.bIsSecured)
					{
						this.m_headers.UriScheme = "https";
					}
					else
					{
						this.m_headers.UriScheme = "http";
					}
				}
				if (num4 > 0)
				{
					if (num2 == 0)
					{
						FiddlerApplication.HandleHTTPError(this.m_session, SessionFlags.ProtocolViolationInRequest, true, false, "Incorrectly formed Request-Line. Request-URI component was missing.\r\n\r\n" + Encoding.ASCII.GetString(buffer, 0, num3));
						return false;
					}
					while (num2 > 0 && buffer[num] != 47)
					{
						if (buffer[num] == 63)
						{
							break;
						}
						num++;
						num2--;
					}
					int num5 = num4 + 1;
					int num6 = num - num5;
					if (num6 > 0)
					{
						this.m_sHostFromURI = CONFIG.oHeaderEncoding.GetString(buffer, num5, num6);
						if (this.m_headers.UriScheme == "ftp" && this.m_sHostFromURI.Contains("@"))
						{
							int num7 = this.m_sHostFromURI.LastIndexOf("@") + 1;
							this.m_headers._uriUserInfo = this.m_sHostFromURI.Substring(0, num7);
							this.m_sHostFromURI = this.m_sHostFromURI.Substring(num7);
						}
					}
				}
				byte[] array = new byte[num2];
				Buffer.BlockCopy(buffer, num, array, 0, num2);
				this.m_headers.RawPath = array;
				if (string.IsNullOrEmpty(this.m_headers.RequestPath))
				{
					FiddlerApplication.HandleHTTPError(this.m_session, SessionFlags.ProtocolViolationInRequest, false, false, "Incorrectly formed Request-Line. abs_path was empty (e.g. missing /). RFC2616 Section 5.1.2");
				}
				string text2 = CONFIG.oHeaderEncoding.GetString(buffer, num3, this.iEntityBodyOffset - num3).Trim();
				if (text2.Length >= 1)
				{
					string[] sHeaderLines = text2.Replace("\r\n", "\n").Split(new char[]
					{
						'\n'
					});
					string empty = string.Empty;
					if (!Parser.ParseNVPHeaders(this.m_headers, sHeaderLines, 0, ref empty))
					{
						FiddlerApplication.HandleHTTPError(this.m_session, SessionFlags.ProtocolViolationInRequest, true, false, "Incorrectly formed request headers.\n" + empty);
					}
				}
				if (this.m_headers.Exists("Content-Length") && this.m_headers.ExistsAndContains("Transfer-Encoding", "chunked"))
				{
					FiddlerApplication.HandleHTTPError(this.m_session, SessionFlags.ProtocolViolationInRequest, false, false, "Content-Length request header MUST NOT be present when Transfer-Encoding is used (RFC2616 Section 4.4)");
				}
				return true;
			}
			FiddlerApplication.HandleHTTPError(this.m_session, SessionFlags.ProtocolViolationInRequest, true, false, "Incorrectly formed Request-Line");
			return false;
		}
		private bool isRequestComplete()
		{
			if (this.m_headers == null)
			{
				if (!this.HeadersAvailable())
				{
					return false;
				}
				if (!this.ParseRequestForHeaders())
				{
					string str;
					if (this.m_requestData != null)
					{
						str = Utilities.ByteArrayToHexView(this.m_requestData.GetBuffer(), 24, (int)Math.Min(this.m_requestData.Length, 2048L));
					}
					else
					{
						str = "{Fiddler:no data}";
					}
					if (this.m_headers == null)
					{
						this.m_headers = new HTTPRequestHeaders();
						this.m_headers.HTTPMethod = "BAD";
						this.m_headers["Host"] = "BAD-REQUEST";
						this.m_headers.RequestPath = "/BAD_REQUEST";
					}
					this.FailSession(400, "Fiddler - Bad Request", "[Fiddler] Request Header parsing failed. Request was:\n" + str);
					return true;
				}
				this.m_session.Timers.FiddlerGotRequestHeaders = DateTime.Now;
				this.m_session._AssignID();
				FiddlerApplication.DoRequestHeadersAvailable(this.m_session);
			}
			if (this.m_headers.ExistsAndEquals("Transfer-encoding", "chunked"))
			{
				long num;
				long num2;
				return Utilities.IsChunkedBodyComplete(this.m_session, this.m_requestData, (long)this.iEntityBodyOffset, out num, out num2);
			}
			if (Utilities.isRPCOverHTTPSMethod(this.m_headers.HTTPMethod) && !this.m_headers.ExistsAndEquals("Content-Length", "0"))
			{
				this.m_session.SetBitFlag(SessionFlags.RequestStreamed, true);
				return true;
			}
			if (this.m_headers.Exists("Content-Length"))
			{
				long num3 = 0L;
				bool result;
				try
				{
					if (long.TryParse(this.m_headers["Content-Length"], NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out num3) && num3 >= 0L)
					{
						result = (this.m_requestData.Length >= (long)this.iEntityBodyOffset + num3);
					}
					else
					{
						FiddlerApplication.HandleHTTPError(this.m_session, SessionFlags.ProtocolViolationInRequest, true, true, "Request content length was invalid.\nContent-Length: " + this.m_headers["Content-Length"]);
						this.FailSession(400, "Fiddler - Bad Request", "[Fiddler] Request Content-Length header parsing failed.\nContent-Length: " + this.m_headers["Content-Length"]);
						result = true;
					}
				}
				catch
				{
					this.FailSession(400, "Fiddler - Bad Request", "[Fiddler] Unknown error: Check content length header?");
					result = false;
				}
				return result;
			}
			return true;
		}
		private bool HeadersAvailable()
		{
			if (this.m_requestData.Length < 16L)
			{
				return false;
			}
			byte[] buffer = this.m_requestData.GetBuffer();
			long length = this.m_requestData.Length;
			HTTPHeaderParseWarnings hTTPHeaderParseWarnings;
			if (Parser.FindEndOfHeaders(buffer, ref this.iBodySeekProgress, length, out hTTPHeaderParseWarnings))
			{
				this.iEntityBodyOffset = this.iBodySeekProgress + 1;
				return true;
			}
			return false;
		}
		internal bool ReadRequest()
		{
			if (this.m_requestData != null)
			{
				FiddlerApplication.ReportException(new InvalidOperationException("ReadRequest called when requestData buffer already existed."));
				return false;
			}
			if (this.pipeClient == null)
			{
				FiddlerApplication.ReportException(new InvalidOperationException("ReadRequest called after pipeClient was null'd."));
				return false;
			}
			this.m_requestData = new PipeReadBuffer(true);
			this.m_session.SetBitFlag(SessionFlags.ClientPipeReused, this.pipeClient.iUseCount > 0u);
			this.pipeClient.IncrementUse(0);
			this.pipeClient.setReceiveTimeout();
			int num = 0;
			bool flag = false;
			bool flag2 = false;
			byte[] array = new byte[ClientChatter._cbClientReadBuffer];
			while (true)
			{
				try
				{
					num = this.pipeClient.Receive(array);
					goto IL_1F1;
				}
				catch (Exception ex)
				{
					if (CONFIG.bDebugSpew)
					{
						FiddlerApplication.DebugSpew(string.Format("ReadRequest {0} threw {1}", (this.pipeClient == null) ? "Null pipeClient" : this.pipeClient.ToString(), ex.Message));
					}
					flag = true;
					goto IL_1F1;
				}
				goto IL_D2;
				IL_1DE:
				if (flag2 || flag)
				{
					goto IL_22F;
				}
				if (this.isRequestComplete())
				{
					goto Block_17;
				}
				continue;
				IL_D2:
				flag2 = true;
				FiddlerApplication.DoReadRequestBuffer(this.m_session, array, 0);
				if (CONFIG.bDebugSpew)
				{
					FiddlerApplication.DebugSpew(string.Format("ReadRequest {0} returned {1}", (this.pipeClient == null) ? "Null pipeClient" : this.pipeClient.ToString(), num));
					goto IL_1DE;
				}
				goto IL_1DE;
				IL_1F1:
				if (num <= 0)
				{
					goto IL_D2;
				}
				if (CONFIG.bDebugSpew)
				{
					FiddlerApplication.DebugSpew(string.Format("READ FROM {0}:\n{1}", this.pipeClient, Utilities.ByteArrayToHexView(array, 32, num)));
				}
				if (!FiddlerApplication.DoReadRequestBuffer(this.m_session, array, num))
				{
					flag = true;
				}
				if (0L == this.m_requestData.Length)
				{
					this.m_session.Timers.ClientBeginRequest = DateTime.Now;
					if (1u == this.pipeClient.iUseCount && num > 2)
					{
						if (array[0] == 5)
						{
							break;
						}
						if (array[0] == 5)
						{
							break;
						}
					}
					int i;
					for (i = 0; i < num; i++)
					{
						if (13 != array[i] && 10 != array[i])
						{
							break;
						}
					}
					this.m_requestData.Write(array, i, num - i);
					goto IL_1DE;
				}
				this.m_requestData.Write(array, 0, num);
				goto IL_1DE;
			}
			goto IL_1FD;
			Block_17:
			goto IL_22F;
			IL_1FD:
			FiddlerApplication.Log.LogFormat("It looks like someone is trying to send SOCKS traffic to us.\r\n{0}", new object[]
			{
				Utilities.ByteArrayToHexView(array, 16, Math.Min(num, 256))
			});
			return false;
			IL_22F:
			array = null;
			if (!flag)
			{
				if (this.m_requestData.Length != 0L)
				{
					if (this.m_headers == null || this.m_session.state >= SessionStates.Done)
					{
						this._freeRequestData();
						return false;
					}
					if ("CONNECT" == this.m_headers.HTTPMethod)
					{
						this.m_session.isTunnel = true;
						this.m_sHostFromURI = this.m_session.PathAndQuery;
					}
					if (this.m_sHostFromURI != null)
					{
						if (this.m_headers.Exists("Host"))
						{
							if (!Utilities.areOriginsEquivalent(this.m_sHostFromURI, this.m_headers["Host"], this.m_session.isHTTPS ? 443 : (this.m_session.isFTP ? 21 : 80)) && (!this.m_session.isTunnel || !Utilities.areOriginsEquivalent(this.m_sHostFromURI, this.m_headers["Host"], 443)))
							{
								this.m_session.oFlags["X-Original-Host"] = this.m_headers["Host"];
								this.m_session.oFlags["X-URI-Host"] = this.m_sHostFromURI;
								if (FiddlerApplication.Prefs.GetBoolPref("fiddler.network.SetHostHeaderFromURL", true))
								{
									this.m_headers["Host"] = this.m_sHostFromURI;
								}
							}
						}
						else
						{
							if ("HTTP/1.1".OICEquals(this.m_headers.HTTPVersion))
							{
								this.m_session.oFlags["X-Original-Host"] = string.Empty;
							}
							this.m_headers["Host"] = this.m_sHostFromURI;
						}
						this.m_sHostFromURI = null;
					}
					if (!this.m_headers.Exists("Host"))
					{
						this._freeRequestData();
						return false;
					}
					return true;
				}
			}
			this._freeRequestData();
			if (this.pipeClient == null)
			{
				return false;
			}
			if (this.pipeClient.iUseCount < 2u || (this.pipeClient.bIsSecured && this.pipeClient.iUseCount < 3u))
			{
				FiddlerApplication.Log.LogFormat("[Fiddler] No {0} request was received from ({1}) new client socket, port {2}.", new object[]
				{
					this.pipeClient.bIsSecured ? "HTTPS" : "HTTP",
					this.m_session.oFlags["X-ProcessInfo"],
					this.m_session.oFlags["X-CLIENTPORT"]
				});
			}
			return false;
		}
	}
}
