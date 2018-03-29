using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
namespace Fiddler
{
	public class CertMaker
	{
		public static ICertificateProvider oCertProvider;
		public static string GetCertProviderInfo()
		{
            if (oCertProvider is NewCertificateProvider)
            {
                return ((NewCertificateProvider)oCertProvider).GetEngineName();
            }

            if (CertMaker.oCertProvider == null)
			{
				return string.Empty;
			}

			if (CertMaker.oCertProvider.GetType().Name == "Fiddler.DefaultCertificateProvider")
			{
				return "Fiddler.DefaultCertificateProvider";
			}
			return string.Format("{0} from {1}", CertMaker.oCertProvider, CertMaker.oCertProvider.GetType().Assembly.Location);
		}
		public static void EnsureReady()
		{
			if (CertMaker.oCertProvider != null)
			{
				return;
			}
			CertMaker.oCertProvider = (CertMaker.LoadOverrideCertProvider() ?? new NewCertificateProvider());
		}
		private static ICertificateProvider LoadOverrideCertProvider()
		{
			string stringPref = FiddlerApplication.Prefs.GetStringPref("fiddler.certmaker.assembly", CONFIG.GetPath("App") + "CertMaker.dll");
			if (!File.Exists(stringPref))
			{
				return null;
			}
			Assembly assembly;
			try
			{
				assembly = Assembly.LoadFrom(stringPref);
				if (!Utilities.FiddlerMeetsVersionRequirement(assembly, "Certificate Makers"))
				{
					FiddlerApplication.Log.LogFormat("Assembly {0} did not specify a RequiredVersionAttribute. Aborting load of Certificate Generation module.", new object[]
					{
						assembly.CodeBase
					});
					ICertificateProvider result = null;
					return result;
				}
			}
			catch (Exception ex)
			{
				FiddlerApplication.Log.LogFormat("Failed to load CertMaker from {0} due to {1}.", new object[]
				{
					stringPref,
					ex.Message
				});
				ICertificateProvider result = null;
				return result;
			}
			Type[] exportedTypes = assembly.GetExportedTypes();
			for (int i = 0; i < exportedTypes.Length; i++)
			{
				Type type = exportedTypes[i];
				if (!type.IsAbstract && type.IsPublic && type.IsClass && typeof(ICertificateProvider).IsAssignableFrom(type))
				{
					try
					{
						ICertificateProvider result = (ICertificateProvider)Activator.CreateInstance(type);
						return result;
					}
					catch (Exception ex2)
					{
						FiddlerApplication.DoNotifyUser(string.Format("[Fiddler] Failure loading {0} CertMaker from {1}: {2}\n\n{3}\n\n{4}", new object[]
						{
							type.Name,
							assembly.CodeBase,
							ex2.Message,
							ex2.StackTrace,
							ex2.InnerException
						}), "Load Error");
					}
				}
			}
			FiddlerApplication.Log.LogFormat("Assembly {0} did not contain a recognized ICertificateProvider.", new object[]
			{
				assembly.CodeBase
			});
			return null;
		}
		public static bool removeFiddlerGeneratedCerts()
		{
			return CertMaker.removeFiddlerGeneratedCerts(true);
		}
		public static bool removeFiddlerGeneratedCerts(bool bRemoveRoot)
		{
			CertMaker.EnsureReady();
			if (CertMaker.oCertProvider is ICertificateProvider2)
			{
				return (CertMaker.oCertProvider as ICertificateProvider2).ClearCertificateCache(bRemoveRoot);
			}
			return CertMaker.oCertProvider.ClearCertificateCache();
		}
		public static X509Certificate2 GetRootCertificate()
		{
			CertMaker.EnsureReady();
			return CertMaker.oCertProvider.GetRootCertificate();
		}
		internal static byte[] getRootCertBytes()
		{
			X509Certificate2 rootCertificate = CertMaker.GetRootCertificate();
			if (rootCertificate == null)
			{
				return null;
			}
			return rootCertificate.Export(X509ContentType.Cert);
		}
		internal static bool exportRootToDesktop()
		{
			try
			{
				byte[] rootCertBytes = CertMaker.getRootCertBytes();
				if (rootCertBytes != null)
				{
					File.WriteAllBytes(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + Path.DirectorySeparatorChar + "FiddlerRoot.cer", rootCertBytes);
					bool result = true;
					return result;
				}
				FiddlerApplication.DoNotifyUser("The root certificate could not be located.", "Export Failed");
			}
			catch (Exception eX)
			{
				FiddlerApplication.ReportException(eX, "Certificate Export Failed");
				bool result = false;
				return result;
			}
			return false;
		}
		public static X509Certificate2 FindCert(string sHostname)
		{
			CertMaker.EnsureReady();
			return CertMaker.oCertProvider.GetCertificateForHost(sHostname);
		}
		public static bool StoreCert(string sHost, X509Certificate2 oCert)
		{
			CertMaker.EnsureReady();
			ICertificateProvider3 certificateProvider = CertMaker.oCertProvider as ICertificateProvider3;
			if (certificateProvider == null)
			{
				return false;
			}
			if (!oCert.HasPrivateKey)
			{
				throw new ArgumentException("The provided certificate MUST have a private key.", "oCert");
			}
			return certificateProvider.CacheCertificateForHost(sHost, oCert);
		}
		public static void StoreCert(string sHost, string sPFXFilename, string sPFXPassword)
		{
			X509Certificate2 oCert = new X509Certificate2(sPFXFilename, sPFXPassword);
			if (!CertMaker.StoreCert(sHost, oCert))
			{
				throw new InvalidOperationException("The current ICertificateProvider does not support storing custom certificates.");
			}
		}
		public static bool rootCertExists()
		{
			bool result;
			try
			{
				X509Certificate2 rootCertificate = CertMaker.GetRootCertificate();
				result = (null != rootCertificate);
			}
			catch (Exception)
			{
				result = false;
			}
			return result;
		}
		public static bool rootCertIsTrusted()
		{
			CertMaker.EnsureReady();
			bool flag;
			bool flag2;
			return CertMaker.oCertProvider.rootCertIsTrusted(out flag, out flag2);
		}
		public static bool rootCertIsMachineTrusted()
		{
			CertMaker.EnsureReady();
			bool flag;
			bool result;
			CertMaker.oCertProvider.rootCertIsTrusted(out flag, out result);
			return result;
		}
		public static bool createRootCert()
		{
			CertMaker.EnsureReady();
			return CertMaker.oCertProvider.CreateRootCertificate();
		}
		public static bool trustRootCert()
		{
			CertMaker.EnsureReady();
			return CertMaker.oCertProvider.TrustRootCertificate();
		}
		public static void DoDispose()
		{
			if (FiddlerApplication.isClosing && CertMaker.oCertProvider == null)
			{
				return;
			}
			CertMaker.EnsureReady();
			if (FiddlerApplication.Prefs.GetBoolPref("fiddler.CertMaker.CleanupServerCertsOnExit", false))
			{
				CertMaker.removeFiddlerGeneratedCerts(false);
			}
			IDisposable disposable = CertMaker.oCertProvider as IDisposable;
			if (disposable != null)
			{
				disposable.Dispose();
			}
			CertMaker.oCertProvider = null;
		}
	}
}
