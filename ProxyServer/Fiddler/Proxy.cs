using Microsoft.Win32;
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Windows.Forms;
namespace Fiddler
{
	public class Proxy : IDisposable
	{
		internal static string sUpstreamPACScript;
		internal RegistryWatcher oRegistryWatcher;
		private string _sHTTPSHostname;
		private X509Certificate2 _oHTTPSCertificate;
		internal WinINETConnectoids oAllConnectoids;
		private WinINETProxyInfo piSystemGateway;
		private WinHTTPAutoProxy oAutoProxy;
		private IPEndPoint _ipepFtpGateway;
		private IPEndPoint _ipepHttpGateway;
		private IPEndPoint _ipepHttpsGateway;
		internal IPEndPoint _DefaultEgressEndPoint;
		private PreferenceBag.PrefWatcher? watcherPrefNotify = null;
		internal static PipePool htServerPipePool = new PipePool();
		private Socket oAcceptor;
		private bool _bIsAttached;
		private bool _bDetaching;
		private ProxyBypassList oBypassList;
		public event EventHandler DetachedUnexpectedly;
		public int ListenPort
		{
			get
			{
				if (this.oAcceptor != null)
				{
					return (this.oAcceptor.LocalEndPoint as IPEndPoint).Port;
				}
				return 0;
			}
		}
		public bool IsAttached
		{
			get
			{
				return this._bIsAttached;
			}
			set
			{
				if (value)
				{
					this.Attach();
					return;
				}
				this.Detach();
			}
		}
		public override string ToString()
		{
			return string.Format("Proxy instance is listening for requests on Port #{0}. HTTPS SubjectCN: {1}\n\n{2}", this.ListenPort, this._sHTTPSHostname ?? "<None>", Proxy.htServerPipePool.InspectPool());
		}
		protected virtual void OnDetachedUnexpectedly()
		{
			EventHandler detachedUnexpectedly = this.DetachedUnexpectedly;
			if (detachedUnexpectedly != null)
			{
				detachedUnexpectedly(this, null);
			}
		}
		internal Proxy(bool bIsPrimary)
		{
			if (bIsPrimary)
			{
				try
				{
					NetworkChange.NetworkAvailabilityChanged += new NetworkAvailabilityChangedEventHandler(this.NetworkChange_NetworkAvailabilityChanged);
					NetworkChange.NetworkAddressChanged += new NetworkAddressChangedEventHandler(this.NetworkChange_NetworkAddressChanged);
				}
				catch (Exception)
				{
				}
				try
				{
					this.watcherPrefNotify = new PreferenceBag.PrefWatcher?(FiddlerApplication.Prefs.AddWatcher("fiddler.network", new EventHandler<PrefChangeEventArgs>(this.onNetworkPrefsChange)));
					this.SetDefaultEgressEndPoint(FiddlerApplication.Prefs["fiddler.network.egress.ip"]);
					CONFIG.SetNoDecryptList(FiddlerApplication.Prefs["fiddler.network.https.NoDecryptionHosts"]);
					CONFIG.sFiddlerListenHostPort = string.Format("{0}:{1}", FiddlerApplication.Prefs.GetStringPref("fiddler.network.proxy.RegistrationHostName", "127.0.0.1").ToLower(), CONFIG.ListenPort);
					ClientChatter._cbClientReadBuffer = FiddlerApplication.Prefs.GetInt32Pref("fiddler.network.sockets.ClientReadBufferSize", 8192);
					ServerChatter._cbServerReadBuffer = FiddlerApplication.Prefs.GetInt32Pref("fiddler.network.sockets.ServerReadBufferSize", 32768);
				}
				catch (Exception)
				{
				}
			}
		}
		private void SetDefaultEgressEndPoint(string sEgressIP)
		{
			if (string.IsNullOrEmpty(sEgressIP))
			{
				this._DefaultEgressEndPoint = null;
				return;
			}
			IPAddress address;
			if (IPAddress.TryParse(sEgressIP, out address))
			{
				this._DefaultEgressEndPoint = new IPEndPoint(address, 0);
				return;
			}
			this._DefaultEgressEndPoint = null;
		}
		private void onNetworkPrefsChange(object sender, PrefChangeEventArgs e)
		{
			if (e.PrefName.OICStartsWith("fiddler.network.timeouts."))
			{
				if (e.PrefName.OICEquals("fiddler.network.timeouts.serverpipe.send.initial"))
				{
					ServerPipe._timeoutSendInitial = FiddlerApplication.Prefs.GetInt32Pref("fiddler.network.timeouts.serverpipe.send.initial", -1);
					return;
				}
				if (e.PrefName.OICEquals("fiddler.network.timeouts.serverpipe.send.reuse"))
				{
					ServerPipe._timeoutSendReused = FiddlerApplication.Prefs.GetInt32Pref("fiddler.network.timeouts.serverpipe.send.reuse", -1);
					return;
				}
				if (e.PrefName.OICEquals("fiddler.network.timeouts.serverpipe.receive.initial"))
				{
					ServerPipe._timeoutReceiveInitial = FiddlerApplication.Prefs.GetInt32Pref("fiddler.network.timeouts.serverpipe.receive.initial", -1);
					return;
				}
				if (e.PrefName.OICEquals("fiddler.network.timeouts.serverpipe.receive.reuse"))
				{
					ServerPipe._timeoutReceiveReused = FiddlerApplication.Prefs.GetInt32Pref("fiddler.network.timeouts.serverpipe.receive.reuse", -1);
					return;
				}
				if (e.PrefName.OICEquals("fiddler.network.timeouts.serverpipe.reuse"))
				{
					PipePool.MSEC_PIPE_POOLED_LIFETIME = (uint)FiddlerApplication.Prefs.GetInt32Pref("fiddler.network.timeouts.serverpipe.reuse", 120000);
					return;
				}
				if (e.PrefName.OICEquals("fiddler.network.timeouts.clientpipe.receive.initial"))
				{
					ClientPipe._timeoutReceiveInitial = FiddlerApplication.Prefs.GetInt32Pref("fiddler.network.timeouts.clientpipe.receive.initial", 60000);
					return;
				}
				if (e.PrefName.OICEquals("fiddler.network.timeouts.clientpipe.receive.reuse"))
				{
					ClientPipe._timeoutReceiveReused = FiddlerApplication.Prefs.GetInt32Pref("fiddler.network.timeouts.clientpipe.receive.reuse", 30000);
					return;
				}
				if (e.PrefName.OICEquals("fiddler.network.timeouts.dnscache"))
				{
					DNSResolver.MSEC_DNS_CACHE_LIFETIME = (ulong)((long)FiddlerApplication.Prefs.GetInt32Pref("fiddler.network.timeouts.dnscache", 150000));
				}
				return;
			}
			else
			{
				if (e.PrefName.OICEquals("fiddler.network.sockets.ClientReadBufferSize"))
				{
					ClientChatter._cbClientReadBuffer = FiddlerApplication.Prefs.GetInt32Pref("fiddler.network.sockets.ClientReadBufferSize", 8192);
					return;
				}
				if (e.PrefName.OICEquals("fiddler.network.sockets.ServerReadBufferSize"))
				{
					ServerChatter._cbServerReadBuffer = FiddlerApplication.Prefs.GetInt32Pref("fiddler.network.sockets.ServerReadBufferSize", 32768);
					return;
				}
				if (e.PrefName.OICEquals("fiddler.network.egress.ip"))
				{
					this.SetDefaultEgressEndPoint(e.ValueString);
					return;
				}
				if (e.PrefName.OICEquals("fiddler.network.https.NoDecryptionHosts"))
				{
					CONFIG.SetNoDecryptList(e.ValueString);
					return;
				}
				if (e.PrefName.OICEquals("fiddler.network.proxy.RegistrationHostName"))
				{
					CONFIG.sFiddlerListenHostPort = string.Format("{0}:{1}", FiddlerApplication.Prefs.GetStringPref("fiddler.network.proxy.RegistrationHostName", "127.0.0.1").ToLower(), CONFIG.ListenPort);
				}
				return;
			}
		}
		private void NetworkChange_NetworkAddressChanged(object sender, EventArgs e)
		{
			try
			{
				DNSResolver.ClearCache();
				FiddlerApplication.Log.LogString("NetworkAddressChanged.");
				if (this.oAutoProxy != null)
				{
					this.oAutoProxy.iAutoProxySuccessCount = 0;
				}
				if (this.piSystemGateway != null && this.piSystemGateway.bUseManualProxies)
				{
					this._DetermineGatewayIPEndPoints();
				}
			}
			catch (Exception eX)
			{
				FiddlerApplication.ReportException(eX);
			}
		}
		private void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
		{
			try
			{
				this.PurgeServerPipePool();
				FiddlerApplication.Log.LogFormat("fiddler.network.availability.change> Network Available: {0}", new object[]
				{
					e.IsAvailable
				});
			}
			catch (Exception)
			{
			}
		}
		[CodeDescription("Send a custom request through the proxy, blocking until it completes (or aborts).")]
		public Session SendRequestAndWait(HTTPRequestHeaders oHeaders, byte[] arrRequestBodyBytes, StringDictionary oNewFlags, EventHandler<StateChangeEventArgs> onStateChange)
		{
			ManualResetEvent oMRE = new ManualResetEvent(false);
			Session session = this.SendRequest(oHeaders, arrRequestBodyBytes, oNewFlags, onStateChange);
			session.OnStateChanged += delegate(object sender, StateChangeEventArgs e)
			{
				if (e.newState >= SessionStates.Done)
				{
					FiddlerApplication.DebugSpew("SendRequestAndWait Session #{0} reached state {1}", new object[]
					{
						(sender as Session).Int32_0,
						e.newState
					});
					oMRE.Set();
				}
			};
			oMRE.WaitOne();
			return session;
		}
		[CodeDescription("Send a custom request through the proxy. Hook the OnStateChanged event of the returned Session to monitor progress")]
		public Session SendRequest(HTTPRequestHeaders oHeaders, byte[] arrRequestBodyBytes, StringDictionary oNewFlags)
		{
			return this.SendRequest(oHeaders, arrRequestBodyBytes, oNewFlags, null);
		}
		[CodeDescription("Send a custom request through the proxy. Hook the OnStateChanged event of the returned Session to monitor progress")]
		public Session SendRequest(HTTPRequestHeaders oHeaders, byte[] arrRequestBodyBytes, StringDictionary oNewFlags, EventHandler<StateChangeEventArgs> onStateChange)
		{
			if (oHeaders.ExistsAndContains("Fiddler-Encoding", "base64"))
			{
				oHeaders.Remove("Fiddler-Encoding");
				if (!Utilities.IsNullOrEmpty(arrRequestBodyBytes))
				{
					arrRequestBodyBytes = Convert.FromBase64String(Encoding.ASCII.GetString(arrRequestBodyBytes));
					if (oNewFlags == null)
					{
						oNewFlags = new StringDictionary();
					}
					oNewFlags["x-Builder-FixContentLength"] = "CFE-required";
				}
			}
			if (oHeaders.Exists("Fiddler-Host"))
			{
				if (oNewFlags == null)
				{
					oNewFlags = new StringDictionary();
				}
				oNewFlags["x-OverrideHost"] = oHeaders["Fiddler-Host"];
				oNewFlags["X-IgnoreCertCNMismatch"] = "Overrode HOST";
				oHeaders.Remove("Fiddler-Host");
			}
			if (oNewFlags != null && oNewFlags.ContainsKey("x-Builder-FixContentLength"))
			{
				if (arrRequestBodyBytes != null && !oHeaders.ExistsAndContains("Transfer-Encoding", "chunked"))
				{
					if (!Utilities.HTTPMethodAllowsBody(oHeaders.HTTPMethod) && arrRequestBodyBytes.Length == 0)
					{
						oHeaders.Remove("Content-Length");
					}
					else
					{
						oHeaders["Content-Length"] = arrRequestBodyBytes.LongLength.ToString();
					}
				}
				else
				{
					oHeaders.Remove("Content-Length");
				}
			}
			Session session = new Session((HTTPRequestHeaders)oHeaders.Clone(), arrRequestBodyBytes);
			session.SetBitFlag(SessionFlags.RequestGeneratedByFiddler, true);
			if (onStateChange != null)
			{
				session.OnStateChanged += onStateChange;
			}
			if (oNewFlags != null && oNewFlags.Count > 0)
			{
				foreach (DictionaryEntry dictionaryEntry in oNewFlags)
				{
					session.oFlags[(string)dictionaryEntry.Key] = oNewFlags[(string)dictionaryEntry.Key];
				}
			}
			session.ExecuteUponAsyncRequest();
			return session;
		}
		public Session SendRequest(string sRequest, StringDictionary oNewFlags)
		{
			byte[] bytes = CONFIG.oHeaderEncoding.GetBytes(sRequest);
			int count;
			int num;
			HTTPHeaderParseWarnings hTTPHeaderParseWarnings;
			if (!Parser.FindEntityBodyOffsetFromArray(bytes, out count, out num, out hTTPHeaderParseWarnings))
			{
				throw new ArgumentException("sRequest did not represent a valid HTTP request", "sRequest");
			}
			string sHeaders = CONFIG.oHeaderEncoding.GetString(bytes, 0, count) + "\r\n\r\n";
			HTTPRequestHeaders hTTPRequestHeaders = new HTTPRequestHeaders();
			if (!hTTPRequestHeaders.AssignFromString(sHeaders))
			{
				throw new ArgumentException("sRequest did not contain valid HTTP headers", "sRequest");
			}
			byte[] array;
			if (1 > bytes.Length - num)
			{
				array = Utilities.emptyByteArray;
			}
			else
			{
				array = new byte[bytes.Length - num];
				Buffer.BlockCopy(bytes, num, array, 0, array.Length);
			}
			return this.SendRequest(hTTPRequestHeaders, array, oNewFlags);
		}
		[Obsolete("This overload of InjectCustomRequest is obsolete. Use a different version.", true)]
		public void InjectCustomRequest(HTTPRequestHeaders oHeaders, byte[] arrRequestBodyBytes, bool bRunRequestRules, bool bViewResult)
		{
			StringDictionary stringDictionary = new StringDictionary();
			stringDictionary["x-From-Builder"] = "true";
			if (bViewResult)
			{
				stringDictionary["x-Builder-Inspect"] = "1";
			}
			this.InjectCustomRequest(oHeaders, arrRequestBodyBytes, stringDictionary);
		}
		public void InjectCustomRequest(HTTPRequestHeaders oHeaders, byte[] arrRequestBodyBytes, StringDictionary oNewFlags)
		{
			this.SendRequest(oHeaders, arrRequestBodyBytes, oNewFlags);
		}
		public void InjectCustomRequest(string sRequest, StringDictionary oNewFlags)
		{
			this.SendRequest(sRequest, oNewFlags);
		}
		public void InjectCustomRequest(string sRequest)
		{
			if (this.oAcceptor == null)
			{
				this.InjectCustomRequest(sRequest, null);
				return;
			}
			Socket socket = new Socket(IPAddress.Loopback.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			socket.Connect(new IPEndPoint(IPAddress.Loopback, CONFIG.ListenPort));
			socket.Send(Encoding.UTF8.GetBytes(sRequest));
			socket.Shutdown(SocketShutdown.Both);
			socket.Close();
		}
		public IPEndPoint FindGatewayForOrigin(string sURIScheme, string sHostAndPort)
		{
			if (string.IsNullOrEmpty(sURIScheme))
			{
				return null;
			}
			if (string.IsNullOrEmpty(sHostAndPort))
			{
				return null;
			}
			if (CONFIG.UpstreamGateway == GatewayType.None)
			{
				return null;
			}
			if (Utilities.isLocalhost(sHostAndPort))
			{
				return null;
			}
			if (sURIScheme.OICEquals("http"))
			{
				if (sHostAndPort.EndsWith(":80", StringComparison.Ordinal))
				{
					sHostAndPort = sHostAndPort.Substring(0, sHostAndPort.Length - 3);
				}
			}
			else
			{
				if (sURIScheme.OICEquals("https"))
				{
					if (sHostAndPort.EndsWith(":443", StringComparison.Ordinal))
					{
						sHostAndPort = sHostAndPort.Substring(0, sHostAndPort.Length - 4);
					}
				}
				else
				{
					if (sURIScheme.OICEquals("ftp") && sHostAndPort.EndsWith(":21", StringComparison.Ordinal))
					{
						sHostAndPort = sHostAndPort.Substring(0, sHostAndPort.Length - 3);
					}
				}
			}
			if (this.oAutoProxy != null && this.oAutoProxy.iAutoProxySuccessCount > -1)
			{
				IPEndPoint result;
				if (this.oAutoProxy.GetAutoProxyForUrl(sURIScheme + "://" + sHostAndPort, out result))
				{
					this.oAutoProxy.iAutoProxySuccessCount = 1;
					return result;
				}
				if (this.oAutoProxy.iAutoProxySuccessCount == 0 && !FiddlerApplication.Prefs.GetBoolPref("fiddler.network.gateway.UseFailedAutoProxy", false))
				{
					FiddlerApplication.Log.LogString("AutoProxy failed. Disabling for this network.");
					this.oAutoProxy.iAutoProxySuccessCount = -1;
				}
			}
			if (this.oBypassList != null && this.oBypassList.IsBypass(sURIScheme, sHostAndPort))
			{
				return null;
			}
			if (sURIScheme.OICEquals("http"))
			{
				return this._ipepHttpGateway;
			}
			if (sURIScheme.OICEquals("https"))
			{
				return this._ipepHttpsGateway;
			}
			if (sURIScheme.OICEquals("ftp"))
			{
				return this._ipepFtpGateway;
			}
			return null;
		}
		private void AcceptConnection(IAsyncResult iasyncResult_0)
		{
			try
			{
				ProxyExecuteParams state = new ProxyExecuteParams(this.oAcceptor.EndAccept(iasyncResult_0), this._oHTTPSCertificate);
				ThreadPool.UnsafeQueueUserWorkItem(new WaitCallback(Session.CreateAndExecute), state);
			}
			catch (ObjectDisposedException)
			{
				return;
			}
			catch (Exception)
			{
			}
			try
			{
				this.oAcceptor.BeginAccept(new AsyncCallback(this.AcceptConnection), null);
			}
			catch (Exception)
			{
			}
		}
		public bool Attach()
		{
			return this.Attach(false);
		}
		private void ProxyRegistryKeysChanged(object sender, EventArgs e)
		{
			if (!this._bIsAttached || this._bDetaching)
			{
				return;
			}
			if (!FiddlerApplication.Prefs.GetBoolPref("fiddler.proxy.WatchRegistry", true))
			{
				return;
			}
			ScheduledTasks.ScheduleWork("VerifyAttached", 50u, new SimpleEventHandler(this.VerifyAttached));
		}
		internal void VerifyAttached()
		{
			FiddlerApplication.Log.LogString("WinINET Registry change detected. Verifying proxy keys are intact...");
			bool flag = true;
			try
			{
				if (this.oAllConnectoids != null)
				{
					flag = !this.oAllConnectoids.MarkUnhookedConnections(CONFIG.sFiddlerListenHostPort);
				}
			}
			catch (Exception)
			{
			}
			if (flag)
			{
				RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings", false);
				if (registryKey != null)
				{
					if (1 != Utilities.GetRegistryInt(registryKey, "ProxyEnable", 0))
					{
						flag = false;
					}
					string text = registryKey.GetValue("ProxyServer") as string;
					if (string.IsNullOrEmpty(text))
					{
						flag = false;
					}
					else
					{
						if (!text.OICEquals(CONFIG.sFiddlerListenHostPort) && !text.OICContains("http=" + CONFIG.sFiddlerListenHostPort))
						{
							flag = false;
						}
					}
					registryKey.Close();
					if (!flag && this.oAllConnectoids != null)
					{
						this.oAllConnectoids.MarkDefaultLANAsUnhooked();
					}
				}
			}
			if (!flag)
			{
				this.OnDetachedUnexpectedly();
			}
		}
		internal bool Attach(bool bCollectGWInfo)
		{
			if (this._bIsAttached)
			{
				return true;
			}
			if (CONFIG.bIsViewOnly)
			{
				return false;
			}
			if (bCollectGWInfo)
			{
				this.CollectConnectoidAndGatewayInfo();
			}
			WinINETProxyInfo winINETProxyInfo = new WinINETProxyInfo();
			winINETProxyInfo.bUseManualProxies = true;
			winINETProxyInfo.bAllowDirect = true;
			winINETProxyInfo.sHttpProxy = CONFIG.sFiddlerListenHostPort;
			if (CONFIG.bCaptureCONNECT)
			{
				winINETProxyInfo.sHttpsProxy = CONFIG.sFiddlerListenHostPort;
			}
			else
			{
				if (this.piSystemGateway != null && this.piSystemGateway.bUseManualProxies)
				{
					winINETProxyInfo.sHttpsProxy = this.piSystemGateway.sHttpsProxy;
				}
			}
			if (CONFIG.bCaptureFTP)
			{
				winINETProxyInfo.sFtpProxy = CONFIG.sFiddlerListenHostPort;
			}
			else
			{
				if (this.piSystemGateway != null && this.piSystemGateway.bUseManualProxies)
				{
					winINETProxyInfo.sFtpProxy = this.piSystemGateway.sFtpProxy;
				}
			}
			if (this.piSystemGateway != null && this.piSystemGateway.bUseManualProxies)
			{
				winINETProxyInfo.sSocksProxy = this.piSystemGateway.sSocksProxy;
			}
			winINETProxyInfo.sHostsThatBypass = CONFIG.sHostsThatBypassFiddler;
			if (CONFIG.bHookWithPAC)
			{
				if (FiddlerApplication.Prefs.GetBoolPref("fiddler.proxy.pacfile.usefileprotocol", true))
				{
					winINETProxyInfo.sPACScriptLocation = "file://" + CONFIG.GetPath("Pac");
				}
				else
				{
					winINETProxyInfo.sPACScriptLocation = "http://" + CONFIG.sFiddlerListenHostPort + "/proxy.pac";
				}
			}
			if (this.oAllConnectoids == null)
			{
				this.CollectConnectoidAndGatewayInfo();
			}
			if (this.oAllConnectoids.HookConnections(winINETProxyInfo))
			{
				this._bIsAttached = true;
				FiddlerApplication.OnFiddlerAttach();
				this.WriteAutoProxyPACFile(true);
				if (this.oRegistryWatcher == null && FiddlerApplication.Prefs.GetBoolPref("fiddler.proxy.WatchRegistry", true))
				{
					this.oRegistryWatcher = RegistryWatcher.WatchKey(RegistryHive.CurrentUser, "Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings", new EventHandler(this.ProxyRegistryKeysChanged));
				}
				Proxy._setDynamicRegistryKey(true);
				return true;
			}
			FiddlerApplication.DoNotifyUser("Failed to register Fiddler as the system proxy.", "Error");
			Proxy._setDynamicRegistryKey(false);
			return false;
		}
		internal void CollectConnectoidAndGatewayInfo()
		{
			try
			{
				this.oAllConnectoids = new WinINETConnectoids();
				this.RefreshUpstreamGatewayInformation();
			}
			catch (Exception eX)
			{
				FiddlerApplication.ReportException(eX, "System Error");
			}
		}
		private static IPEndPoint GetFirstRespondingEndpoint(string sHostPortList)
		{
			if (Utilities.IsNullOrWhiteSpace(sHostPortList))
			{
				return null;
			}
			sHostPortList = Utilities.TrimAfter(sHostPortList, ';');
			IPEndPoint iPEndPoint = null;
			int port = 80;
			string text;
			Utilities.CrackHostAndPort(sHostPortList, out text, ref port);
			IPAddress[] iPAddressList;
			IPEndPoint result;
			try
			{
				iPAddressList = DNSResolver.GetIPAddressList(text, true, null);
			}
			catch
			{
				FiddlerApplication.Log.LogFormat("fiddler.network.gateway> Unable to resolve upstream proxy '{0}'... ignoring.", new object[]
				{
					text
				});
				result = null;
				return result;
			}
			try
			{
				IPAddress[] array = iPAddressList;
				for (int i = 0; i < array.Length; i++)
				{
					IPAddress iPAddress = array[i];
					try
					{
						using (Socket socket = new Socket(iPAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
						{
							socket.NoDelay = true;
							if (FiddlerApplication.oProxy._DefaultEgressEndPoint != null)
							{
								socket.Bind(FiddlerApplication.oProxy._DefaultEgressEndPoint);
							}
							socket.Connect(iPAddress, port);
							iPEndPoint = new IPEndPoint(iPAddress, port);
						}
						break;
					}
					catch (Exception ex)
					{
						if (!FiddlerApplication.Prefs.GetBoolPref("fiddler.network.dns.fallback", true))
						{
							break;
						}
						FiddlerApplication.Log.LogFormat("fiddler.network.gateway.connect>Connection to {0} failed. {1}. Will try DNS Failover if available.", new object[]
						{
							iPAddress.ToString(),
							ex.Message
						});
					}
				}
				result = iPEndPoint;
			}
			catch (Exception)
			{
				result = null;
			}
			return result;
		}
		private void _DetermineGatewayIPEndPoints()
		{
			this._ipepHttpGateway = Proxy.GetFirstRespondingEndpoint(this.piSystemGateway.sHttpProxy);
			if (this.piSystemGateway.sHttpsProxy == this.piSystemGateway.sHttpProxy)
			{
				this._ipepHttpsGateway = this._ipepHttpGateway;
			}
			else
			{
				this._ipepHttpsGateway = Proxy.GetFirstRespondingEndpoint(this.piSystemGateway.sHttpsProxy);
			}
			if (this.piSystemGateway.sFtpProxy == this.piSystemGateway.sHttpProxy)
			{
				this._ipepFtpGateway = this._ipepHttpGateway;
				return;
			}
			this._ipepFtpGateway = Proxy.GetFirstRespondingEndpoint(this.piSystemGateway.sFtpProxy);
		}
		private static void _setDynamicRegistryKey(bool bAttached)
		{
			if (CONFIG.bIsViewOnly)
			{
				return;
			}
			try
			{
				RegistryKey registryKey = Registry.CurrentUser.CreateSubKey(CONFIG.GetRegPath("Dynamic"));
				if (registryKey != null)
				{
					registryKey.SetValue("Attached", bAttached ? 1 : 0, RegistryValueKind.DWord);
					registryKey.Close();
				}
			}
			catch (Exception ex)
			{
				FiddlerApplication.Log.LogFormat("fiddler.network.FiddlerHook> Unable to set Dynamic registry key; registry permissions likely corrupt. Exception: {0}", new object[]
				{
					ex.Message
				});
			}
		}
		public bool Detach()
		{
			return this.Detach(false);
		}
		internal bool Detach(bool bDontCheckIfAttached)
		{
			if (!bDontCheckIfAttached && !this._bIsAttached)
			{
				return true;
			}
			if (CONFIG.bIsViewOnly)
			{
				return true;
			}
			bool result;
			try
			{
				this._bDetaching = true;
				Proxy._setDynamicRegistryKey(false);
				if (this.oAllConnectoids.UnhookAllConnections())
				{
					this._bIsAttached = false;
					FiddlerApplication.OnFiddlerDetach();
					this.WriteAutoProxyPACFile(false);
					return true;
				}
				result = false;
			}
			finally
			{
				this._bDetaching = false;
			}
			return result;
		}
		internal string _GetUpstreamPACScriptText()
		{
			return Proxy.sUpstreamPACScript;
		}
		internal string _GetPACScriptText(bool bUseFiddler)
		{
			string str;
			if (bUseFiddler)
			{
				str = FiddlerApplication.Prefs.GetStringPref("fiddler.proxy.pacfile.text", "return 'PROXY " + CONFIG.sFiddlerListenHostPort + "';");
			}
			else
			{
				str = "return 'DIRECT';";
			}
			return "//Autogenerated file; do not edit. Rewritten on attach and detach of Fiddler.\r\n//This Automatic Proxy Configuration script can be used by non-WinINET browsers.\r\nfunction FindProxyForURL(url, host){\r\n  " + str + "\r\n}";
		}
		private void WriteAutoProxyPACFile(bool bUseFiddler)
		{
		}
		internal void Stop()
		{
			if (this.oAcceptor == null)
			{
				return;
			}
			try
			{
				this.oAcceptor.LingerState = new LingerOption(true, 0);
				this.oAcceptor.Close();
			}
			catch (Exception ex)
			{
				Trace.WriteLine("oProxy.Dispose threw an exception: " + ex.Message);
			}
		}
		internal bool Start(int iPort, bool bAllowRemote)
		{
			if (CONFIG.bIsViewOnly && DialogResult.No == MessageBox.Show("This instance is running in Fiddler's Viewer Mode. Do you want to start the listener anyway?", "Warning: Viewer Mode", MessageBoxButtons.YesNo))
			{
				return false;
			}
			bool flag = false;
			bool result;
			try
			{
				flag = (bAllowRemote && CONFIG.bEnableIPv6 && Socket.OSSupportsIPv6);
			}
			catch (Exception ex)
			{
				if (ex is ConfigurationErrorsException)
				{
					FiddlerApplication.DoNotifyUser(string.Concat(new object[]
					{
						"A Microsoft .NET configuration file (listed below) is corrupt and contains invalid data. You can often correct this error by installing updates from WindowsUpdate and/or reinstalling the .NET Framework.\n\n",
						ex.Message,
						"\nSource: ",
						ex.Source,
						"\n",
						ex.StackTrace,
						"\n\n",
						ex.InnerException,
						"\nFiddler v",
						Application.ProductVersion,
						(8 == IntPtr.Size) ? " (x64) " : " (x86) ",
						" [.NET ",
						Environment.Version,
						" on ",
						Environment.OSVersion.VersionString,
						"] "
					}), ".NET Configuration Error", MessageBoxIcon.Hand);
					this.oAcceptor = null;
					result = false;
					return result;
				}
			}
			try
			{
				if (flag)
				{
					this.oAcceptor = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
					if (Environment.OSVersion.Version.Major > 5)
					{
						this.oAcceptor.SetSocketOption(SocketOptionLevel.IPv6, (SocketOptionName)27, 0);
					}
				}
				else
				{
					this.oAcceptor = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
				}
				if (CONFIG.ForceExclusivePort)
				{
					this.oAcceptor.ExclusiveAddressUse = true;
				}
				if (bAllowRemote)
				{
					if (flag)
					{
						this.oAcceptor.Bind(new IPEndPoint(IPAddress.IPv6Any, iPort));
					}
					else
					{
						this.oAcceptor.Bind(new IPEndPoint(IPAddress.Any, iPort));
					}
				}
				else
				{
					this.oAcceptor.Bind(new IPEndPoint(IPAddress.Loopback, iPort));
				}
				this.oAcceptor.Listen(50);
			}
			catch (SocketException ex2)
			{
				string text = string.Empty;
				string sTitle = "Fiddler Cannot Listen";
				int errorCode = ex2.ErrorCode;
				if (errorCode != 10013)
				{
					switch (errorCode)
					{
					case 10047:
					case 10049:
						if (flag)
						{
							text = "\nThis often means that you've enabled IPv6 support inside Tools > Fiddler Options, but your computer has IPv6 disabled.";
							goto IL_248;
						}
						goto IL_248;
					case 10048:
						break;
					default:
						goto IL_248;
					}
				}
				text = string.Format("\nThis is usually due to another service running on this port. Use NETSTAT -AB at a command prompt to identify it.\n{0}", (iPort == CONFIG.ListenPort) ? "If you don't want to stop using the other program, simply change the port used by Fiddler.\nClick Tools > Fiddler Options > Connections, select a new port, and restart Fiddler." : string.Empty);
				sTitle = "Port in Use";
				IL_248:
				this.oAcceptor = null;
				FiddlerApplication.DoNotifyUser(string.Format("Unable to bind to port [{0}]. ErrorCode: {1}.\n{2}\n\n{3}\n\n{4}", new object[]
				{
					iPort,
					ex2.ErrorCode,
					text,
					ex2.ToString(),
					string.Concat(new object[]
					{
						"Fiddler v",
						Application.ProductVersion,
						" [.NET ",
						Environment.Version,
						" on ",
						Environment.OSVersion.VersionString,
						"]"
					})
				}), sTitle, MessageBoxIcon.Hand);
				result = false;
				return result;
			}
			catch (UnauthorizedAccessException eX)
			{
				this.oAcceptor = null;
				FiddlerApplication.ReportException(eX, "Cannot Create Listener", "Fiddler could not start its Listener socket. This problem will occur if you attempt to\nrun Fiddler2 using the limited 'Guest' account. (Fiddler v4 does not have this limitation.)");
				result = false;
				return result;
			}
			catch (Exception eX2)
			{
				this.oAcceptor = null;
				FiddlerApplication.ReportException(eX2);
				result = false;
				return result;
			}
			try
			{
				this.oAcceptor.BeginAccept(new AsyncCallback(this.AcceptConnection), null);
				return true;
			}
			catch (Exception ex3)
			{
				this.oAcceptor = null;
				FiddlerApplication.Log.LogFormat("Fiddler BeginAccept() Exception: {0}", new object[]
				{
					ex3.Message
				});
				result = false;
			}
			return result;
		}
		public void Dispose()
		{
			if (this.watcherPrefNotify.HasValue)
			{
				FiddlerApplication.Prefs.RemoveWatcher(this.watcherPrefNotify.Value);
			}
			if (this.oRegistryWatcher != null)
			{
				this.oRegistryWatcher.Dispose();
			}
			this.Stop();
			if (this.oAutoProxy != null)
			{
				this.oAutoProxy.Dispose();
				this.oAutoProxy = null;
			}
		}
		public void PurgeServerPipePool()
		{
			Proxy.htServerPipePool.Clear();
		}
		public void AssignEndpointCertificate(X509Certificate2 certHTTPS)
		{
			this._oHTTPSCertificate = certHTTPS;
			if (certHTTPS != null)
			{
				this._sHTTPSHostname = certHTTPS.Subject;
				return;
			}
			this._sHTTPSHostname = null;
		}
		internal void RefreshUpstreamGatewayInformation()
		{
			this._ipepFtpGateway = (this._ipepHttpGateway = (this._ipepHttpsGateway = null));
			this.piSystemGateway = null;
			this.oBypassList = null;
			if (this.oAutoProxy != null)
			{
				this.oAutoProxy.Dispose();
				this.oAutoProxy = null;
			}
			switch (CONFIG.UpstreamGateway)
			{
			case GatewayType.None:
				FiddlerApplication.Log.LogString("Setting upstream gateway to none");
				return;
			case GatewayType.Manual:
			{
				WinINETProxyInfo oPI = WinINETProxyInfo.CreateFromStrings(FiddlerApplication.Prefs.GetStringPref("fiddler.network.gateway.proxies", string.Empty), FiddlerApplication.Prefs.GetStringPref("fiddler.network.gateway.exceptions", string.Empty));
				this.AssignGateway(oPI);
				return;
			}
			case GatewayType.System:
				this.AssignGateway(this.oAllConnectoids.GetDefaultConnectionGatewayInfo());
				return;
			case GatewayType.WPAD:
				FiddlerApplication.Log.LogString("Setting upstream gateway to WPAD");
				this.oAutoProxy = new WinHTTPAutoProxy(true, null);
				return;
			default:
				return;
			}
		}
		private void AssignGateway(WinINETProxyInfo oPI)
		{
			this.piSystemGateway = oPI;
			if (this.piSystemGateway.bAutoDetect || this.piSystemGateway.sPACScriptLocation != null)
			{
				this.oAutoProxy = new WinHTTPAutoProxy(this.piSystemGateway.bAutoDetect, this.piSystemGateway.sPACScriptLocation);
			}
			if (this.piSystemGateway.bUseManualProxies)
			{
				this._DetermineGatewayIPEndPoints();
				if (!string.IsNullOrEmpty(this.piSystemGateway.sHostsThatBypass))
				{
					this.oBypassList = new ProxyBypassList(this.piSystemGateway.sHostsThatBypass);
					if (!this.oBypassList.HasEntries)
					{
						this.oBypassList = null;
					}
				}
			}
		}
		internal bool ActAsHTTPSEndpointForHostname(string sHTTPSHostname)
		{
			try
			{
				if (string.IsNullOrEmpty(sHTTPSHostname))
				{
					throw new ArgumentException();
				}
				this._oHTTPSCertificate = CertMaker.FindCert(sHTTPSHostname);
				this._sHTTPSHostname = this._oHTTPSCertificate.Subject;
				return true;
			}
			catch (Exception)
			{
				this._oHTTPSCertificate = null;
				this._sHTTPSHostname = null;
			}
			return false;
		}
		internal string GetGatewayInformation()
		{
			if (FiddlerApplication.oProxy.oAutoProxy != null)
			{
				return string.Format("Gateway: Auto-Config\n{0}", FiddlerApplication.oProxy.oAutoProxy.ToString());
			}
			IPEndPoint iPEndPoint = this.FindGatewayForOrigin("http", "fiddler2.com");
			if (iPEndPoint != null)
			{
				return string.Format("Gateway: {0}:{1}\n", iPEndPoint.Address.ToString(), iPEndPoint.Port.ToString());
			}
			return string.Format("Gateway: No Gateway\n", new object[0]);
		}
		internal void ShowGatewayInformation()
		{
			if (this.piSystemGateway == null && this.oAutoProxy == null)
			{
				MessageBox.Show("No upstream gateway proxy is configured.", "No Upstream Gateway");
				return;
			}
			if (this.piSystemGateway != null && this.piSystemGateway.bUseManualProxies)
			{
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.Append(this.piSystemGateway.ToString());
				if (!string.IsNullOrEmpty(this.piSystemGateway.sHttpProxy) && this._ipepHttpGateway == null)
				{
					stringBuilder.AppendLine("\nWARNING: HTTP Gateway specified was unreachable and is being ignored.");
				}
				if (!string.IsNullOrEmpty(this.piSystemGateway.sHttpsProxy) && this._ipepHttpsGateway == null)
				{
					stringBuilder.AppendLine("\nWARNING: HTTPS Gateway specified was unreachable and is being ignored.");
				}
				if (!string.IsNullOrEmpty(this.piSystemGateway.sFtpProxy) && this._ipepFtpGateway == null)
				{
					stringBuilder.AppendLine("\nWARNING: FTP Gateway specified was unreachable and is being ignored.");
				}
				MessageBox.Show(stringBuilder.ToString(), "Manually-Configured Gateway");
				return;
			}
			if (this.oAutoProxy != null)
			{
				MessageBox.Show(this.oAutoProxy.ToString().Trim(), "Auto-Configured Gateway");
				return;
			}
			MessageBox.Show("No upstream gateway proxy is configured.", "No Upstream Gateway");
		}
	}
}
