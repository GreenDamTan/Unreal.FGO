using System;
using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Authentication;
using System.Text;
using System.Windows.Forms;

namespace Fiddler
{
	public static class CONFIG
	{
		internal const int I_MAX_CONNECTION_QUEUE = 50;
		internal static string sDefaultBrowserExe;
		internal static string sDefaultBrowserParams;
		internal static bool bRunningOnCLRv4;
		private static bool bQuietMode;
		private static ProcessFilterCategories _pfcDecyptFilter;
		private static string sLVColInfo;
		internal static bool bIsViewOnly;
		internal static bool bUseXceedDecompressForGZIP;
		internal static bool bUseXceedDecompressForDeflate;
		public static bool bMapSocketToProcess;
		public static bool bMITM_HTTPS;
		public static bool bUseSNIForCN;
		private static bool bIgnoreServerCertErrors;
		public static bool bStreamAudioVideo;
		internal static bool bCheckCompressionIntegrity;
		internal static bool bShowDefaultClientCertificateNeededPrompt;
		internal static string sFiddlerListenHostPort;
		internal static string sMakeCertParamsRoot;
		internal static string sMakeCertParamsEE;
		internal static string sMakeCertRootCN;
		internal static string sMakeCertSubjectO;
		private static string sRootUrl;
		private static string sSecureRootUrl;
		internal static string sRootKey;
		private static string sUserPath;
		private static string sScriptPath;
		public static bool bUseAESForSAZ;
		public static SslProtocols oAcceptedClientHTTPSProtocols;
		public static SslProtocols oAcceptedServerHTTPSProtocols;
		public static Version FiddlerVersionInfo;
		internal static bool bIsBeta;
		[Obsolete]
		public static bool bForwardToGateway;
		private static GatewayType _UpstreamGateway;
		public static bool bDebugSpew;
		public static Encoding oHeaderEncoding;
		public static Encoding oBodyEncoding;
		public static bool bReuseServerSockets;
		public static bool bReuseClientSockets;
		public static bool bCaptureCONNECT;
		public static bool bCaptureFTP;
		public static bool bUseEventLogForExceptions;
		public static bool bAutoProxyLogon;
		public static bool bEnableIPv6;
		public static string sHookConnectionNamed;
		internal static bool bHookAllConnections;
		internal static bool bHookWithPAC;
		private static string m_sHostsThatBypassFiddler;
		private static string m_JSEditor;
		private static bool m_bAllowRemoteConnections;
		public static string sGatewayUsername;
		public static string sGatewayPassword;
		private static bool m_bCheckForISA;
		private static int m_ListenPort;
		internal static bool bUsingPortOverride;
		private static bool m_bForceExclusivePort;
		public static bool bDebugCertificateGeneration;
		private static int _iReverseProxyForPort;
		public static string sAlternateHostname;
		internal static string sReverseProxyHostname;
		internal static string sMachineName;
		internal static string sMachineDomain;
		internal static HostList oHLSkipDecryption;
		private static bool fNeedToMaximizeOnload;
		public static ProcessFilterCategories DecryptWhichProcesses
		{
			get
			{
				return CONFIG._pfcDecyptFilter;
			}
			set
			{
				CONFIG._pfcDecyptFilter = value;
			}
		}
		public static GatewayType UpstreamGateway
		{
			get
			{
				return CONFIG._UpstreamGateway;
			}
			internal set
			{
				if (value < GatewayType.None || value > GatewayType.WPAD)
				{
					value = GatewayType.System;
				}
				CONFIG._UpstreamGateway = value;
				CONFIG.bForwardToGateway = (value != GatewayType.None);
			}
		}
		public static int iReverseProxyForPort
		{
			get
			{
				return CONFIG._iReverseProxyForPort;
			}
			set
			{
				if (value > -1 && value <= 65535 && value != CONFIG.m_ListenPort)
				{
					CONFIG._iReverseProxyForPort = value;
					return;
				}
				FiddlerApplication.Log.LogFormat("!Invalid configuration. ReverseProxyForPort may not be set to {0}", new object[]
				{
					value
				});
			}
		}
		public static string sHostsThatBypassFiddler
		{
			get
			{
				return CONFIG.m_sHostsThatBypassFiddler;
			}
			set
			{
				string text = value;
				if (text == null)
				{
					text = string.Empty;
				}
				if (!text.OICContains("<-loopback>") && !text.OICContains("<loopback>"))
				{
					text = "<-loopback>;" + text;
				}
				CONFIG.m_sHostsThatBypassFiddler = text;
			}
		}
		public static bool ForceExclusivePort
		{
			get
			{
				return CONFIG.m_bForceExclusivePort;
			}
			internal set
			{
				CONFIG.m_bForceExclusivePort = value;
			}
		}
		public static bool IgnoreServerCertErrors
		{
			get
			{
				return CONFIG.bIgnoreServerCertErrors;
			}
			set
			{
				CONFIG.bIgnoreServerCertErrors = value;
			}
		}
		public static bool QuietMode
		{
			get
			{
				return CONFIG.bQuietMode;
			}
			set
			{
				CONFIG.bQuietMode = value;
			}
		}
		public static int ListenPort
		{
			get
			{
				return CONFIG.m_ListenPort;
			}
			internal set
			{
				if (value >= 0 && value < 65536)
				{
					CONFIG.m_ListenPort = value;
					CONFIG.sFiddlerListenHostPort = Utilities.TrimAfter(CONFIG.sFiddlerListenHostPort, ':') + ":" + CONFIG.m_ListenPort.ToString();
				}
			}
		}
		[CodeDescription("Return path to user's FiddlerScript editor.")]
		public static string JSEditor
		{
			get
			{
				if (string.IsNullOrEmpty(CONFIG.m_JSEditor))
				{
					CONFIG.m_JSEditor = CONFIG.GetPath("TextEditor");
				}
				return CONFIG.m_JSEditor;
			}
			set
			{
				CONFIG.m_JSEditor = value;
			}
		}
		[CodeDescription("Returns true if Fiddler is configured to accept remote clients.")]
		public static bool bAllowRemoteConnections
		{
			get
			{
				return CONFIG.m_bAllowRemoteConnections;
			}
			internal set
			{
				CONFIG.m_bAllowRemoteConnections = value;
			}
		}
		public static void SetNoDecryptList(string sNewList)
		{
			if (string.IsNullOrEmpty(sNewList))
			{
				CONFIG.oHLSkipDecryption = null;
				return;
			}
			CONFIG.oHLSkipDecryption = new HostList();
			CONFIG.oHLSkipDecryption.AssignFromString(sNewList);
		}
		[CodeDescription("Return a special Url.")]
		public static string GetUrl(string sWhatUrl)
		{
			switch (sWhatUrl)
			{
			case "AutoResponderHelp":
				return CONFIG.sRootUrl + "help/AutoResponder.asp";
			case "ChangeList":
				return CONFIG.sSecureRootUrl + "fiddler2/version.asp?ver=";
			case "FiltersHelp":
				return CONFIG.sRootUrl + "help/Filters.asp";
			case "HelpContents":
				return CONFIG.sRootUrl + "help/?ver=";
			case "REDIR":
				return "http://fiddler2.com/r/?";
			case "VerCheck":
				return "http://fiddler2.com/UpdateCheck.aspx?isBeta=";
			case "InstallLatest":
				if (!CONFIG.bIsBeta)
				{
					return CONFIG.sSecureRootUrl + "r/?GetFiddler2";
				}
				return CONFIG.sSecureRootUrl + "r/?GetFiddler2Beta";
			case "ShopAmazon":
				return "http://fiddler2.com/r/?shop";
			}
			return CONFIG.sRootUrl;
		}
		public static string GetRegPath(string sWhatPath)
		{
			switch (sWhatPath)
			{
			case "Root":
				return CONFIG.sRootKey;
			case "LMIsBeta":
				return CONFIG.sRootKey;
			case "MenuExt":
				return CONFIG.sRootKey + "MenuExt\\";
			case "UI":
				return CONFIG.sRootKey + "UI\\";
			case "Dynamic":
				return CONFIG.sRootKey + "Dynamic\\";
			case "Prefs":
				return CONFIG.sRootKey + "Prefs\\";
			}
			return CONFIG.sRootKey;
		}
		[CodeDescription("Return a filesystem path.")]
		public static string GetPath(string sWhatPath)
		{
			switch (sWhatPath)
			{
			case "App":
				return Path.GetDirectoryName(Application.ExecutablePath) + Path.DirectorySeparatorChar;
			case "AutoFiddlers_Machine":
				return string.Concat(new object[]
				{
					Path.GetDirectoryName(Application.ExecutablePath),
					Path.DirectorySeparatorChar,
					"Scripts",
					Path.DirectorySeparatorChar
				});
			case "AutoFiddlers_User":
				return CONFIG.sUserPath + "Scripts" + Path.DirectorySeparatorChar;
			case "AutoResponderDefaultRules":
				return CONFIG.sUserPath + "AutoResponder.xml";
			case "Captures":
				return FiddlerApplication.Prefs.GetStringPref("fiddler.config.path.captures", CONFIG.sUserPath + "Captures" + Path.DirectorySeparatorChar);
			case "CustomRules":
				return CONFIG.sScriptPath;
			case "DefaultClientCertificate":
				return FiddlerApplication.Prefs.GetStringPref("fiddler.config.path.defaultclientcert", CONFIG.sUserPath + "ClientCertificate.cer");
			case "FiddlerRootCert":
				return CONFIG.sUserPath + "DO_NOT_TRUST_FiddlerRoot.cer";
			case "Filters":
				return CONFIG.sUserPath + "Filters" + Path.DirectorySeparatorChar;
			case "Inspectors":
				return string.Concat(new object[]
				{
					Path.GetDirectoryName(Application.ExecutablePath),
					Path.DirectorySeparatorChar,
					"Inspectors",
					Path.DirectorySeparatorChar
				});
			case "Inspectors_User":
				return CONFIG.sUserPath + "Inspectors" + Path.DirectorySeparatorChar;
			case "PerUser-ISA-Config":
			{
				string text = "C:\\";
				try
				{
					text = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
				}
				catch (Exception)
				{
				}
				return text + "\\microsoft\\firewall client 2004\\management.ini";
			}
			case "PerMachine-ISA-Config":
			{
				string text = "C:\\";
				try
				{
					text = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
				}
				catch (Exception)
				{
				}
				return text + "\\microsoft\\firewall client 2004\\management.ini";
			}
			case "MakeCert":
			{
				string text = FiddlerApplication.Prefs.GetStringPref("fiddler.config.path.makecert", Path.GetDirectoryName(Application.ExecutablePath) + Path.DirectorySeparatorChar + "MakeCert.exe");
				if (!File.Exists(text))
				{
					text = "MakeCert.exe";
				}
				return text;
			}
			case "MyDocs":
			{
				string text = "C:\\";
				try
				{
					text = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
				}
				catch (Exception ex)
				{
					FiddlerApplication.DoNotifyUser("Initialization Error", "Failed to retrieve path to your My Documents folder.\nThis generally means you have a relative environment variable.\nDefaulting to C:\\\n\n" + ex.Message);
				}
				return text;
			}
			case "Pac":
				return FiddlerApplication.Prefs.GetStringPref("fiddler.config.path.pac", string.Concat(new object[]
				{
					CONFIG.sUserPath,
					"Scripts",
					Path.DirectorySeparatorChar,
					"BrowserPAC.js"
				}));
			case "Requests":
				return FiddlerApplication.Prefs.GetStringPref("fiddler.config.path.requests", string.Concat(new object[]
				{
					CONFIG.sUserPath,
					"Captures",
					Path.DirectorySeparatorChar,
					"Requests",
					Path.DirectorySeparatorChar
				}));
			case "Responses":
				return FiddlerApplication.Prefs.GetStringPref("fiddler.config.path.responses", string.Concat(new object[]
				{
					CONFIG.sUserPath,
					"Captures",
					Path.DirectorySeparatorChar,
					"Responses",
					Path.DirectorySeparatorChar
				}));
			case "Root":
				return CONFIG.sUserPath;
			case "SafeTemp":
			{
				string text = "C:\\";
				try
				{
					text = Environment.GetFolderPath(Environment.SpecialFolder.InternetCache);
				}
				catch (Exception ex2)
				{
					FiddlerApplication.DoNotifyUser("Failed to retrieve path to your Internet Cache folder.\nThis generally means you have a relative environment variable.\nDefaulting to C:\\\n\n" + ex2.Message, "GetPath(SafeTemp) Failed");
				}
				return text;
			}
			case "SampleRules":
				return string.Concat(new object[]
				{
					Path.GetDirectoryName(Application.ExecutablePath),
					Path.DirectorySeparatorChar,
					"Scripts",
					Path.DirectorySeparatorChar,
					"SampleRules.js"
				});
			case "Scripts":
				return CONFIG.sUserPath + "Scripts" + Path.DirectorySeparatorChar;
			case "TemplateResponses":
				return FiddlerApplication.Prefs.GetStringPref("fiddler.config.path.templateresponses", string.Concat(new object[]
				{
					Path.GetDirectoryName(Application.ExecutablePath),
					Path.DirectorySeparatorChar,
					"ResponseTemplates",
					Path.DirectorySeparatorChar
				}));
			case "Tools":
				return FiddlerApplication.Prefs.GetStringPref("fiddler.config.path.Tools", string.Concat(new object[]
				{
					Path.GetDirectoryName(Application.ExecutablePath),
					Path.DirectorySeparatorChar,
					"Tools",
					Path.DirectorySeparatorChar
				}));
			case "Transcoders_Machine":
				return string.Concat(new object[]
				{
					Path.GetDirectoryName(Application.ExecutablePath),
					Path.DirectorySeparatorChar,
					"ImportExport",
					Path.DirectorySeparatorChar
				});
			case "Transcoders_User":
				return CONFIG.sUserPath + "ImportExport" + Path.DirectorySeparatorChar;
			}
			return "C:\\";
		}
		internal static void EnsureFoldersExist()
		{
			try
			{
				if (!Directory.Exists(CONFIG.GetPath("Captures")))
				{
					Directory.CreateDirectory(CONFIG.GetPath("Captures"));
				}
				if (!Directory.Exists(CONFIG.GetPath("Requests")))
				{
					Directory.CreateDirectory(CONFIG.GetPath("Requests"));
				}
				if (!Directory.Exists(CONFIG.GetPath("Responses")))
				{
					Directory.CreateDirectory(CONFIG.GetPath("Responses"));
				}
				if (!Directory.Exists(CONFIG.GetPath("Scripts")))
				{
					Directory.CreateDirectory(CONFIG.GetPath("Scripts"));
				}
			}
			catch (Exception ex)
			{
				FiddlerApplication.DoNotifyUser(ex.ToString(), "Folder Creation Failed");
			}
			try
			{
				if (!FiddlerApplication.Prefs.GetBoolPref("fiddler.script.delaycreate", true) && !File.Exists(CONFIG.GetPath("CustomRules")) && File.Exists(CONFIG.GetPath("SampleRules")))
				{
					File.Copy(CONFIG.GetPath("SampleRules"), CONFIG.GetPath("CustomRules"));
				}
			}
			catch (Exception ex2)
			{
				FiddlerApplication.DoNotifyUser(ex2.ToString(), "Initial file copies failed");
			}
		}
		static CONFIG()
		{
			CONFIG.sDefaultBrowserExe = "iexplore.exe";
			CONFIG.sDefaultBrowserParams = string.Empty;
			CONFIG.bRunningOnCLRv4 = (Environment.Version.Major > 3);
			CONFIG.bQuietMode = !Environment.UserInteractive;
			CONFIG._pfcDecyptFilter = ProcessFilterCategories.All;
			CONFIG.sLVColInfo = null;
			CONFIG.bIsViewOnly = false;
			CONFIG.bUseXceedDecompressForGZIP = false;
			CONFIG.bUseXceedDecompressForDeflate = false;
			CONFIG.bMapSocketToProcess = true;
			CONFIG.bMITM_HTTPS = false;
			CONFIG.bUseSNIForCN = false;
			CONFIG.bIgnoreServerCertErrors = false;
			CONFIG.bStreamAudioVideo = false;
			CONFIG.bCheckCompressionIntegrity = false;
			CONFIG.bShowDefaultClientCertificateNeededPrompt = true;
			CONFIG.sFiddlerListenHostPort = "127.0.0.1:8888";
			CONFIG.sMakeCertParamsRoot = "-r -ss my -n \"CN={0}{1}\" -sky signature -eku 1.3.6.1.5.5.7.3.1 -h 1 -cy authority -a sha1 -m 132 -b {3}{4}";
			CONFIG.sMakeCertParamsEE = "-pe -ss my -n \"CN={0}{1}\" -sky exchange -in {2} -is my -eku 1.3.6.1.5.5.7.3.1 -cy end -a sha1 -m 132 -b {3}{4}";
			CONFIG.sMakeCertRootCN = "DO_NOT_TRUST_FiddlerRoot";
			CONFIG.sMakeCertSubjectO = ", O=DO_NOT_TRUST, OU=Created by http://www.fiddler2.com";
			CONFIG.sRootUrl = "http://fiddler2.com/fiddlercore/";
			CONFIG.sSecureRootUrl = "https://fiddler2.com/";
			CONFIG.sRootKey = "SOFTWARE\\Microsoft\\FiddlerCore\\";
			CONFIG.sUserPath = string.Concat(new object[]
			{
				CONFIG.GetPath("MyDocs"),
				Path.DirectorySeparatorChar,
				"FiddlerCore",
				Path.DirectorySeparatorChar
			});
			CONFIG.sScriptPath = string.Concat(new object[]
			{
				CONFIG.sUserPath,
				"Scripts",
				Path.DirectorySeparatorChar,
				"CustomRules.js"
			});
			CONFIG.bUseAESForSAZ = true;
			CONFIG.oAcceptedClientHTTPSProtocols = (SslProtocols)4092;
			CONFIG.oAcceptedServerHTTPSProtocols = SslProtocols.Default;
			CONFIG.FiddlerVersionInfo = Assembly.GetExecutingAssembly().GetName().Version;
			CONFIG.bIsBeta = false;
			CONFIG.bForwardToGateway = true;
			CONFIG._UpstreamGateway = GatewayType.System;
			CONFIG.bDebugSpew = false;
			CONFIG.oHeaderEncoding = Encoding.UTF8;
			CONFIG.oBodyEncoding = Encoding.UTF8;
			CONFIG.bReuseServerSockets = true;
			CONFIG.bReuseClientSockets = true;
			CONFIG.bCaptureCONNECT = true;
			CONFIG.bCaptureFTP = false;
			CONFIG.bUseEventLogForExceptions = false;
			CONFIG.bAutoProxyLogon = false;
			CONFIG.bEnableIPv6 = (Environment.OSVersion.Version.Major > 5);
			CONFIG.sHookConnectionNamed = "DefaultLAN";
			CONFIG.bHookAllConnections = true;
			CONFIG.bHookWithPAC = false;
			CONFIG.m_bCheckForISA = true;
			CONFIG.m_ListenPort = 8888;
			CONFIG.bUsingPortOverride = false;
			CONFIG.bDebugCertificateGeneration = true;
			CONFIG.sAlternateHostname = "?";
			CONFIG.sReverseProxyHostname = "localhost";
			CONFIG.sMachineName = string.Empty;
			CONFIG.sMachineDomain = string.Empty;
			CONFIG.oHLSkipDecryption = null;
			try
			{
				IPGlobalProperties iPGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
				CONFIG.sMachineDomain = iPGlobalProperties.DomainName.ToLowerInvariant();
				CONFIG.sMachineName = iPGlobalProperties.HostName.ToLowerInvariant();
			}
			catch (Exception)
			{
			}
			CONFIG.bQuietMode = true;
			CONFIG.bDebugSpew = false;
			CONFIG.m_ListenPort = 8866;
			if (Environment.OSVersion.Version.Major < 6 && Environment.OSVersion.Version.Minor < 1)
			{
				CONFIG.bMapSocketToProcess = false;
			}
		}
	}
}
