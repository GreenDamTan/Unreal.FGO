using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Windows.Forms;
namespace Fiddler
{
	public class FiddlerApplication
	{
		internal static DataPoints Counters;
		public static bool isClosing;
		public static X509Certificate oDefaultClientCertificate;
		public static LocalCertificateSelectionCallback ClientCertificateProvider;
		internal static Logger _Log;
		internal static readonly PeriodicWorker Janitor;
		internal static PreferenceBag _Prefs;
		[CodeDescription("Fiddler's core proxy engine.")]
		public static Proxy oProxy;
		public static FiddlerTranscoders oTranscoders;
		private static List<string> slLeakedFiles;
		[CodeDescription("This event fires when the user instructs Fiddler to clear the cache or cookies.")]
		public static event EventHandler<CacheClearEventArgs> OnClearCache;
		public static event EventHandler<RawReadEventArgs> OnReadResponseBuffer;
		public static event EventHandler<RawReadEventArgs> OnReadRequestBuffer;
		public static event SessionStateHandler BeforeRequest;
		public static event SessionStateHandler BeforeResponse;
		public static event SessionStateHandler RequestHeadersAvailable;
		public static event SessionStateHandler ResponseHeadersAvailable;
		public static event SessionStateHandler BeforeReturningError;
		public static event EventHandler<WebSocketMessageEventArgs> OnWebSocketMessage;
		public static event SessionStateHandler AfterSessionComplete;
		[CodeDescription("This event fires when a user notification would be shown. See CONFIG.QuietMode property.")]
		public static event EventHandler<NotificationEventArgs> OnNotification;
		[CodeDescription("This event fires a HTTPS certificate is validated.")]
		public static event EventHandler<ValidateServerCertificateEventArgs> OnValidateServerCertificate;
		[CodeDescription("Sync this event to be notified when FiddlerCore has attached as the system proxy.")]
		public static event SimpleEventHandler FiddlerAttach;
		[CodeDescription("Sync this event to be notified when FiddlerCore has detached as the system proxy.")]
		public static event SimpleEventHandler FiddlerDetach;
		public static event EventHandler<ConnectionEventArgs> AfterSocketConnect;
		public static event EventHandler<ConnectionEventArgs> AfterSocketAccept;
		public static ISAZProvider oSAZProvider
		{
			get;
			set;
		}
		[CodeDescription("Fiddler's logging subsystem; displayed on the LOG tab by default.")]
		public static Logger Log
		{
			get
			{
				return FiddlerApplication._Log;
			}
		}
		[CodeDescription("Fiddler's Preferences collection. http://fiddler.wikidot.com/prefs")]
		public static IFiddlerPreferences Prefs
		{
			get
			{
				return FiddlerApplication._Prefs;
			}
		}
		public static string GetVersionString()
		{
			FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location);
			string empty = string.Empty;
			string text = "FiddlerCore";
			return string.Format("{0}/{1}.{2}.{3}.{4}{5}", new object[]
			{
				text,
				versionInfo.FileMajorPart,
				versionInfo.FileMinorPart,
				versionInfo.FileBuildPart,
				versionInfo.FilePrivatePart,
				empty
			});
		}
		public static string GetDetailedInfo()
		{
			StringBuilder stringBuilder = new StringBuilder(512);
			stringBuilder.AppendFormat("\nRunning {0}on: {1}:{2}\n", string.Empty, CONFIG.sMachineName, FiddlerApplication.oProxy.ListenPort.ToString());
			if (CONFIG.bHookAllConnections)
			{
				stringBuilder.AppendLine("Listening to: All Adapters");
			}
			else
			{
				stringBuilder.AppendFormat("Listening to: {0}\n", CONFIG.sHookConnectionNamed ?? "Default LAN");
			}
			if (CONFIG.iReverseProxyForPort > 0)
			{
				stringBuilder.AppendFormat("Acting as reverse proxy for port #{0}\n", CONFIG.iReverseProxyForPort);
			}
			stringBuilder.Append(FiddlerApplication.oProxy.GetGatewayInformation());
			string empty = string.Empty;
			return string.Format("Fiddler Web Debugger ({0})\n{8}\n{1}-bit {2}, VM: {3:N2}mb, WS: {4:N2}mb\n{5} {6}\n\n{7}\n", new object[]
			{
				CONFIG.bIsBeta ? string.Format("v{0} beta", Application.ProductVersion) : string.Format("v{0}", Application.ProductVersion),
				(8 == IntPtr.Size) ? "64" : "32",
				Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE"),
				Process.GetCurrentProcess().PagedMemorySize64 / 1048576L,
				Process.GetCurrentProcess().WorkingSet64 / 1048576L,
				".NET " + Environment.Version,
				Utilities.GetOSVerString(),
				stringBuilder.ToString(),
				empty
			});
		}
		public static bool IsStarted()
		{
			return null != FiddlerApplication.oProxy;
		}
		public static bool IsSystemProxy()
		{
			return FiddlerApplication.oProxy != null && FiddlerApplication.oProxy.IsAttached;
		}
		public static void Startup(int iListenPort, FiddlerCoreStartupFlags oFlags)
		{
			if (FiddlerApplication.oProxy != null)
			{
				throw new InvalidOperationException("Calling startup twice without calling shutdown is not permitted.");
			}
			if (iListenPort >= 0 && iListenPort <= 65535)
			{
				CONFIG.ListenPort = iListenPort;
				CONFIG.bAllowRemoteConnections = (FiddlerCoreStartupFlags.None < (oFlags & FiddlerCoreStartupFlags.AllowRemoteClients));
				CONFIG.bMITM_HTTPS = (FiddlerCoreStartupFlags.None < (oFlags & FiddlerCoreStartupFlags.DecryptSSL));
				CONFIG.bCaptureCONNECT = true;
				CONFIG.bCaptureFTP = (FiddlerCoreStartupFlags.None < (oFlags & FiddlerCoreStartupFlags.CaptureFTP));
				CONFIG.bForwardToGateway = (FiddlerCoreStartupFlags.None < (oFlags & FiddlerCoreStartupFlags.ChainToUpstreamGateway));
				CONFIG.bHookAllConnections = (FiddlerCoreStartupFlags.None < (oFlags & FiddlerCoreStartupFlags.MonitorAllConnections));
				if (FiddlerCoreStartupFlags.None < (oFlags & FiddlerCoreStartupFlags.CaptureLocalhostTraffic))
				{
					CONFIG.sHostsThatBypassFiddler = CONFIG.sHostsThatBypassFiddler;
				}
				FiddlerApplication.oProxy = new Proxy(true);
				if (FiddlerApplication.oProxy.Start(CONFIG.ListenPort, CONFIG.bAllowRemoteConnections))
				{
					if (iListenPort == 0)
					{
						CONFIG.ListenPort = FiddlerApplication.oProxy.ListenPort;
					}
					if (FiddlerCoreStartupFlags.None < (oFlags & FiddlerCoreStartupFlags.RegisterAsSystemProxy))
					{
						FiddlerApplication.oProxy.Attach(true);
						return;
					}
					if (FiddlerCoreStartupFlags.None < (oFlags & FiddlerCoreStartupFlags.ChainToUpstreamGateway))
					{
						FiddlerApplication.oProxy.CollectConnectoidAndGatewayInfo();
					}
				}
				return;
			}
			throw new ArgumentOutOfRangeException("bListenPort", "Port must be between 0 and 65535.");
		}
		public static void Startup(int iListenPort, bool bRegisterAsSystemProxy, bool bDecryptSSL)
		{
			FiddlerCoreStartupFlags fiddlerCoreStartupFlags = FiddlerCoreStartupFlags.Default;
			if (bRegisterAsSystemProxy)
			{
				fiddlerCoreStartupFlags |= FiddlerCoreStartupFlags.RegisterAsSystemProxy;
			}
			else
			{
				fiddlerCoreStartupFlags &= ~FiddlerCoreStartupFlags.RegisterAsSystemProxy;
			}
			if (bDecryptSSL)
			{
				fiddlerCoreStartupFlags |= FiddlerCoreStartupFlags.DecryptSSL;
			}
			else
			{
				fiddlerCoreStartupFlags &= ~FiddlerCoreStartupFlags.DecryptSSL;
			}
			FiddlerApplication.Startup(iListenPort, fiddlerCoreStartupFlags);
		}
		public static void Startup(int iListenPort, bool bRegisterAsSystemProxy, bool bDecryptSSL, bool bAllowRemote)
		{
			FiddlerCoreStartupFlags fiddlerCoreStartupFlags = FiddlerCoreStartupFlags.Default;
			if (bRegisterAsSystemProxy)
			{
				fiddlerCoreStartupFlags |= FiddlerCoreStartupFlags.RegisterAsSystemProxy;
			}
			else
			{
				fiddlerCoreStartupFlags &= ~FiddlerCoreStartupFlags.RegisterAsSystemProxy;
			}
			if (bDecryptSSL)
			{
				fiddlerCoreStartupFlags |= FiddlerCoreStartupFlags.DecryptSSL;
			}
			else
			{
				fiddlerCoreStartupFlags &= ~FiddlerCoreStartupFlags.DecryptSSL;
			}
			if (bAllowRemote)
			{
				fiddlerCoreStartupFlags |= FiddlerCoreStartupFlags.AllowRemoteClients;
			}
			else
			{
				fiddlerCoreStartupFlags &= ~FiddlerCoreStartupFlags.AllowRemoteClients;
			}
			FiddlerApplication.Startup(iListenPort, fiddlerCoreStartupFlags);
		}
		public static Proxy CreateProxyEndpoint(int iPort, bool bAllowRemote, string sHTTPSHostname)
		{
			Proxy proxy = new Proxy(false);
			if (!string.IsNullOrEmpty(sHTTPSHostname))
			{
				proxy.ActAsHTTPSEndpointForHostname(sHTTPSHostname);
			}
			if (proxy.Start(iPort, bAllowRemote))
			{
				return proxy;
			}
			proxy.Dispose();
			return null;
		}
		public static Proxy CreateProxyEndpoint(int iPort, bool bAllowRemote, X509Certificate2 certHTTPS)
		{
			Proxy proxy = new Proxy(false);
			if (certHTTPS != null)
			{
				proxy.AssignEndpointCertificate(certHTTPS);
			}
			if (proxy.Start(iPort, bAllowRemote))
			{
				return proxy;
			}
			proxy.Dispose();
			return null;
		}
		public static void Shutdown()
		{
			if (FiddlerApplication.oProxy != null)
			{
				FiddlerApplication.oProxy.Detach();
				FiddlerApplication.oProxy.Dispose();
				FiddlerApplication.oProxy = null;
			}
		}
		internal static bool DoReadResponseBuffer(Session oS, byte[] arrBytes, int cBytes)
		{
			if (FiddlerApplication.OnReadResponseBuffer == null)
			{
				return true;
			}
			if (oS.isFlagSet(SessionFlags.Ignored))
			{
				return true;
			}
			RawReadEventArgs rawReadEventArgs = new RawReadEventArgs(oS, arrBytes, cBytes);
			FiddlerApplication.OnReadResponseBuffer(oS, rawReadEventArgs);
			return !rawReadEventArgs.AbortReading;
		}
		internal static bool DoReadRequestBuffer(Session oS, byte[] arrBytes, int cBytes)
		{
			if (FiddlerApplication.OnReadRequestBuffer == null)
			{
				return true;
			}
			if (oS.isFlagSet(SessionFlags.Ignored))
			{
				return true;
			}
			RawReadEventArgs rawReadEventArgs = new RawReadEventArgs(oS, arrBytes, cBytes);
			FiddlerApplication.OnReadRequestBuffer(oS, rawReadEventArgs);
			return !rawReadEventArgs.AbortReading;
		}
		internal static bool DoClearCache(bool bClearFiles, bool bClearCookies)
		{
			EventHandler<CacheClearEventArgs> onClearCache = FiddlerApplication.OnClearCache;
			if (onClearCache == null)
			{
				return true;
			}
			CacheClearEventArgs cacheClearEventArgs = new CacheClearEventArgs(bClearFiles, bClearCookies);
			onClearCache(null, cacheClearEventArgs);
			return !cacheClearEventArgs.Cancel;
		}
		internal static void CheckOverrideCertificatePolicy(Session oS, string sExpectedCN, X509Certificate ServerCertificate, X509Chain ServerCertificateChain, SslPolicyErrors sslPolicyErrors, ref CertificateValidity oValidity)
		{
			EventHandler<ValidateServerCertificateEventArgs> onValidateServerCertificate = FiddlerApplication.OnValidateServerCertificate;
			if (onValidateServerCertificate == null)
			{
				return;
			}
			ValidateServerCertificateEventArgs validateServerCertificateEventArgs = new ValidateServerCertificateEventArgs(oS, sExpectedCN, ServerCertificate, ServerCertificateChain, sslPolicyErrors);
			onValidateServerCertificate(oS, validateServerCertificateEventArgs);
			oValidity = validateServerCertificateEventArgs.ValidityState;
		}
		internal static void DoBeforeRequest(Session oSession)
		{
			if (oSession.isFlagSet(SessionFlags.Ignored))
			{
				return;
			}
			if (FiddlerApplication.BeforeRequest != null)
			{
				FiddlerApplication.BeforeRequest(oSession);
			}
		}
		internal static void DoBeforeResponse(Session oSession)
		{
			if (oSession.isFlagSet(SessionFlags.Ignored))
			{
				return;
			}
			if (FiddlerApplication.BeforeResponse != null)
			{
				FiddlerApplication.BeforeResponse(oSession);
			}
		}
		internal static void DoResponseHeadersAvailable(Session oSession)
		{
			if (oSession.isFlagSet(SessionFlags.Ignored))
			{
				return;
			}
			if (FiddlerApplication.ResponseHeadersAvailable != null)
			{
				FiddlerApplication.ResponseHeadersAvailable(oSession);
			}
		}
		internal static void DoRequestHeadersAvailable(Session oSession)
		{
			if (oSession.isFlagSet(SessionFlags.Ignored))
			{
				return;
			}
			if (FiddlerApplication.RequestHeadersAvailable != null)
			{
				FiddlerApplication.RequestHeadersAvailable(oSession);
			}
		}
		internal static void DoOnWebSocketMessage(Session oS, WebSocketMessage oWSM)
		{
			if (oS.isFlagSet(SessionFlags.Ignored))
			{
				return;
			}
			EventHandler<WebSocketMessageEventArgs> onWebSocketMessage = FiddlerApplication.OnWebSocketMessage;
			if (onWebSocketMessage != null)
			{
				onWebSocketMessage(oS, new WebSocketMessageEventArgs(oWSM));
			}
		}
		internal static void DoBeforeReturningError(Session oSession)
		{
			if (oSession.isFlagSet(SessionFlags.Ignored))
			{
				return;
			}
			if (FiddlerApplication.BeforeReturningError != null)
			{
				FiddlerApplication.BeforeReturningError(oSession);
			}
		}
		internal static void DoAfterSessionComplete(Session oSession)
		{
			if (oSession.isFlagSet(SessionFlags.Ignored))
			{
				return;
			}
			if (FiddlerApplication.AfterSessionComplete != null)
			{
				FiddlerApplication.AfterSessionComplete(oSession);
			}
		}
		internal static void OnFiddlerAttach()
		{
			if (FiddlerApplication.FiddlerAttach != null)
			{
				FiddlerApplication.FiddlerAttach();
			}
		}
		internal static void OnFiddlerDetach()
		{
			if (FiddlerApplication.FiddlerDetach != null)
			{
				FiddlerApplication.FiddlerDetach();
			}
		}
		public static bool DoExport(string sExportFormat, Session[] oSessions, Dictionary<string, object> dictOptions, EventHandler<ProgressCallbackEventArgs> ehPCEA)
		{
			if (string.IsNullOrEmpty(sExportFormat))
			{
				return false;
			}
			TranscoderTuple exporter = FiddlerApplication.oTranscoders.GetExporter(sExportFormat);
			if (exporter == null)
			{
				FiddlerApplication.Log.LogFormat("No exporter for the format '{0}' was available.", new object[]
				{
					sExportFormat
				});
				return false;
			}
			bool result = false;
			try
			{
				ISessionExporter sessionExporter = (ISessionExporter)Activator.CreateInstance(exporter.typeFormatter);
				if (ehPCEA == null)
				{
					ehPCEA = delegate(object sender, ProgressCallbackEventArgs e)
					{
						string text = (e.PercentComplete > 0) ? ("Export is " + e.PercentComplete + "% complete; ") : string.Empty;
						FiddlerApplication.Log.LogFormat("{0}{1}", new object[]
						{
							text,
							e.ProgressText
						});
					};
				}
				result = sessionExporter.ExportSessions(sExportFormat, oSessions, dictOptions, ehPCEA);
				sessionExporter.Dispose();
			}
			catch (Exception eX)
			{
				FiddlerApplication.LogAddonException(eX, "Exporter for " + sExportFormat + " failed.");
				result = false;
			}
			return result;
		}
		public static Session[] DoImport(string sImportFormat, bool bAddToSessionList, Dictionary<string, object> dictOptions, EventHandler<ProgressCallbackEventArgs> ehPCEA)
		{
			if (string.IsNullOrEmpty(sImportFormat))
			{
				return null;
			}
			TranscoderTuple importer = FiddlerApplication.oTranscoders.GetImporter(sImportFormat);
			if (importer == null)
			{
				return null;
			}
			Session[] array;
			try
			{
				ISessionImporter sessionImporter = (ISessionImporter)Activator.CreateInstance(importer.typeFormatter);
				if (ehPCEA == null)
				{
					ehPCEA = delegate(object sender, ProgressCallbackEventArgs e)
					{
						string text = (e.PercentComplete > 0) ? ("Import is " + e.PercentComplete + "% complete; ") : string.Empty;
						FiddlerApplication.Log.LogFormat("{0}{1}", new object[]
						{
							text,
							e.ProgressText
						});
						Application.DoEvents();
					};
				}
				array = sessionImporter.ImportSessions(sImportFormat, dictOptions, ehPCEA);
				sessionImporter.Dispose();
				if (array == null)
				{
					return null;
				}
			}
			catch (Exception eX)
			{
				FiddlerApplication.LogAddonException(eX, "Importer for " + sImportFormat + " failed.");
				array = null;
			}
			return array;
		}
		[CodeDescription("Reset the SessionID counter to 0. This method can lead to confusing UI, so call sparingly.")]
		public static void ResetSessionCounter()
		{
			Session.ResetSessionCounter();
		}
		internal static void DoNotifyUser(string sMessage, string sTitle)
		{
			FiddlerApplication.DoNotifyUser(sMessage, sTitle, MessageBoxIcon.None);
		}
		internal static void DoNotifyUser(string sMessage, string sTitle, MessageBoxIcon oIcon)
		{
			if (FiddlerApplication.OnNotification != null)
			{
				NotificationEventArgs e = new NotificationEventArgs(string.Format("{0} - {1}", sTitle, sMessage));
				FiddlerApplication.OnNotification(null, e);
			}
			if (!CONFIG.QuietMode)
			{
				MessageBox.Show(sMessage, sTitle, MessageBoxButtons.OK, oIcon);
			}
		}
		internal static void ReportException(Exception eX)
		{
			FiddlerApplication.ReportException(eX, "Sorry, you may have found a bug...", null);
		}
		public static void ReportException(Exception eX, string sTitle)
		{
			FiddlerApplication.ReportException(eX, sTitle, null);
		}
		public static void ReportException(Exception eX, string sTitle, string sCallerMessage)
		{
			Trace.WriteLine(string.Concat(new object[]
			{
				"*************************\n",
				eX.Message,
				"\n",
				eX.StackTrace,
				"\n",
				eX.InnerException
			}));
			if (eX is ThreadAbortException && FiddlerApplication.isClosing)
			{
				return;
			}
			if (eX is ConfigurationErrorsException)
			{
				FiddlerApplication.DoNotifyUser(string.Concat(new object[]
				{
					"Your Microsoft .NET Configuration file is corrupt and contains invalid data. You can often correct this error by installing updates from WindowsUpdate and/or reinstalling the .NET Framework.\r\n",
					eX.Message,
					"\n\nType: ",
					eX.GetType().ToString(),
					"\nSource: ",
					eX.Source,
					"\n",
					eX.StackTrace,
					"\n\n",
					eX.InnerException,
					"\nFiddler v",
					Application.ProductVersion,
					(8 == IntPtr.Size) ? " (x64 " : " (x86 ",
					Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE"),
					") [.NET ",
					Environment.Version,
					" on ",
					Environment.OSVersion.VersionString,
					"] "
				}), sTitle, MessageBoxIcon.Hand);
				return;
			}
			string text;
			if (eX is OutOfMemoryException)
			{
				sTitle = "Out of Memory Error";
				text = "An out-of-memory exception was encountered. To help avoid out-of-memory conditions, please see: " + CONFIG.GetUrl("REDIR") + "FIDDLEROOM";
			}
			else
			{
				if (string.IsNullOrEmpty(sCallerMessage))
				{
					text = "Fiddler has encountered an unexpected problem. If you believe this is a bug in Fiddler, please copy this message by hitting CTRL+C, and submit a bug report using the Help | Send Feedback menu.";
				}
				else
				{
					text = sCallerMessage;
				}
			}
			FiddlerApplication.DoNotifyUser(string.Concat(new object[]
			{
				text,
				"\n\n",
				eX.Message,
				"\n\nType: ",
				eX.GetType().ToString(),
				"\nSource: ",
				eX.Source,
				"\n",
				eX.StackTrace,
				"\n\n",
				eX.InnerException,
				"\nFiddler v",
				Application.ProductVersion,
				(8 == IntPtr.Size) ? " (x64 " : " (x86 ",
				Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE"),
				") [.NET ",
				Environment.Version,
				" on ",
				Environment.OSVersion.VersionString,
				"] "
			}), sTitle, MessageBoxIcon.Hand);
		}
		internal static void HandleHTTPError(Session oSession, SessionFlags flagViolation, bool bPoisonClientConnection, bool bPoisonServerConnection, string sMessage)
		{
			if (bPoisonClientConnection)
			{
				oSession.PoisonClientPipe();
			}
			if (bPoisonServerConnection)
			{
				oSession.PoisonServerPipe();
			}
			oSession.SetBitFlag(flagViolation, true);
			oSession["ui-backcolor"] = "LightYellow";
			FiddlerApplication.Log.LogFormat("{0} - [#{1}] {2}", new object[]
			{
				"Fiddler.Network.ProtocolViolation",
				oSession.Int32_0.ToString(),
				sMessage
			});
			sMessage = "[ProtocolViolation] " + sMessage;
			if (oSession["x-HTTPProtocol-Violation"] == null || !oSession["x-HTTPProtocol-Violation"].Contains(sMessage))
			{
				oSession["x-HTTPProtocol-Violation"] = oSession["x-HTTPProtocol-Violation"] + sMessage;
			}
		}
		internal static void DebugSpew(string sMessage)
		{
			if (CONFIG.bDebugSpew)
			{
				Trace.WriteLine(sMessage);
			}
		}
		internal static void DebugSpew(string sMessage, params object[] args)
		{
			if (CONFIG.bDebugSpew)
			{
				Trace.WriteLine(string.Format(sMessage, args));
			}
		}
		private FiddlerApplication()
		{
		}
		static FiddlerApplication()
		{
			FiddlerApplication.Counters = new DataPoints();
			FiddlerApplication._Log = new Logger(false);
			FiddlerApplication.Janitor = new PeriodicWorker();
			FiddlerApplication._Prefs = null;
			FiddlerApplication.oTranscoders = new FiddlerTranscoders();
			FiddlerApplication.slLeakedFiles = new List<string>();
			FiddlerApplication._SetXceedLicenseKeys();
			FiddlerApplication._Prefs = new PreferenceBag(null);
		}
		internal static void _SetXceedLicenseKeys()
		{
		}
		internal static void LogAddonException(Exception eX, string sTitle)
		{
			if (FiddlerApplication.Prefs.GetBoolPref("fiddler.debug.extensions.showerrors", false) || FiddlerApplication.Prefs.GetBoolPref("fiddler.debug.extensions.verbose", false))
			{
				FiddlerApplication.Log.LogFormat("!Exception from Extension: {0}", new object[]
				{
					Utilities.DescribeException(eX)
				});
				FiddlerApplication.ReportException(eX, sTitle, "Fiddler has encountered an unexpected problem with an extension.");
			}
		}
		public static void LogLeakedFile(string sTempFile)
		{
			List<string> obj;
			Monitor.Enter(obj = FiddlerApplication.slLeakedFiles);
			try
			{
				FiddlerApplication.slLeakedFiles.Add(sTempFile);
			}
			finally
			{
				Monitor.Exit(obj);
			}
		}
		internal static void WipeLeakedFiles()
		{
			try
			{
				if (FiddlerApplication.slLeakedFiles.Count >= 1)
				{
					List<string> obj;
					Monitor.Enter(obj = FiddlerApplication.slLeakedFiles);
					try
					{
						foreach (string current in FiddlerApplication.slLeakedFiles)
						{
							try
							{
								File.Delete(current);
							}
							catch (Exception)
							{
							}
						}
						FiddlerApplication.slLeakedFiles.Clear();
					}
					finally
					{
						Monitor.Exit(obj);
					}
				}
			}
			catch (Exception ex)
			{
				Trace.WriteLine(string.Format("Fiddler.WipeLeakedFiles failed! {0}", ex.Message));
			}
		}
		internal static void DoAfterSocketConnect(Session oSession, Socket sockServer)
		{
			EventHandler<ConnectionEventArgs> afterSocketConnect = FiddlerApplication.AfterSocketConnect;
			if (afterSocketConnect == null)
			{
				return;
			}
			ConnectionEventArgs e = new ConnectionEventArgs(oSession, sockServer);
			afterSocketConnect(oSession, e);
		}
		internal static void DoAfterSocketAccept(Session oSession, Socket sockClient)
		{
			EventHandler<ConnectionEventArgs> afterSocketAccept = FiddlerApplication.AfterSocketAccept;
			if (afterSocketAccept == null)
			{
				return;
			}
			ConnectionEventArgs e = new ConnectionEventArgs(oSession, sockClient);
			afterSocketAccept(oSession, e);
		}
	}
}
