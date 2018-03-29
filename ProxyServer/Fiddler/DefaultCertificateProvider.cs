using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
namespace Fiddler
{
	internal class DefaultCertificateProvider : ICertificateProvider3
	{
		private string _sMakeCertLocation;
		private Dictionary<string, X509Certificate2> certServerCache = new Dictionary<string, X509Certificate2>();
		private X509Certificate2 certRoot;
		private ReaderWriterLock _oRWLock = new ReaderWriterLock();
		private void GetReaderLock()
		{
			this._oRWLock.AcquireReaderLock(-1);
		}
		private void FreeReaderLock()
		{
			this._oRWLock.ReleaseReaderLock();
		}
		private void GetWriterLock()
		{
			this._oRWLock.AcquireWriterLock(-1);
		}
		private void FreeWriterLock()
		{
			this._oRWLock.ReleaseWriterLock();
		}
		internal DefaultCertificateProvider()
		{
			this._sMakeCertLocation = CONFIG.GetPath("MakeCert");
		}
		private static X509Certificate2Collection FindCertsBySubject(StoreName storeName, StoreLocation storeLocation, string sFullSubject)
		{
			X509Store x509Store = new X509Store(storeName, storeLocation);
			x509Store.Open(OpenFlags.OpenExistingOnly);
			X509Certificate2Collection result = x509Store.Certificates.Find(X509FindType.FindBySubjectDistinguishedName, sFullSubject, false);
			x509Store.Close();
			return result;
		}
		private static X509Certificate2Collection FindCertsByIssuer(StoreName storeName, string sFullIssuerSubject)
		{
			X509Store x509Store = new X509Store(storeName, StoreLocation.CurrentUser);
			x509Store.Open(OpenFlags.OpenExistingOnly);
			X509Certificate2Collection result = x509Store.Certificates.Find(X509FindType.FindByIssuerDistinguishedName, sFullIssuerSubject, false);
			x509Store.Close();
			return result;
		}
		public bool ClearCertificateCache(bool bRemoveRoot)
		{
			bool result = true;
			try
			{
				this.GetWriterLock();
				this.certServerCache.Clear();
				this.certRoot = null;
				string text = string.Format("CN={0}{1}", CONFIG.sMakeCertRootCN, CONFIG.sMakeCertSubjectO);
				X509Certificate2Collection x509Certificate2Collection;
				if (bRemoveRoot)
				{
					x509Certificate2Collection = DefaultCertificateProvider.FindCertsBySubject(StoreName.Root, StoreLocation.CurrentUser, text);
					if (x509Certificate2Collection.Count > 0)
					{
						X509Store x509Store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
						x509Store.Open(OpenFlags.ReadWrite | OpenFlags.OpenExistingOnly);
						try
						{
							x509Store.RemoveRange(x509Certificate2Collection);
						}
						catch
						{
							result = false;
						}
						x509Store.Close();
					}
				}
				x509Certificate2Collection = DefaultCertificateProvider.FindCertsByIssuer(StoreName.My, text);
				if (x509Certificate2Collection.Count > 0)
				{
					if (!bRemoveRoot)
					{
						X509Certificate2 rootCertificate = this.GetRootCertificate();
						if (rootCertificate != null)
						{
							x509Certificate2Collection.Remove(rootCertificate);
							if (x509Certificate2Collection.Count < 1)
							{
								return true;
							}
						}
					}
					X509Store x509Store2 = new X509Store(StoreName.My, StoreLocation.CurrentUser);
					x509Store2.Open(OpenFlags.ReadWrite | OpenFlags.OpenExistingOnly);
					try
					{
						x509Store2.RemoveRange(x509Certificate2Collection);
					}
					catch
					{
						result = false;
					}
					x509Store2.Close();
				}
			}
			finally
			{
				this.FreeWriterLock();
			}
			return result;
		}
		public bool ClearCertificateCache()
		{
			return ((ICertificateProvider2)this).ClearCertificateCache(true);
		}
		public bool rootCertIsTrusted(out bool bUserTrusted, out bool bMachineTrusted)
		{
			bUserTrusted = (0 < DefaultCertificateProvider.FindCertsBySubject(StoreName.Root, StoreLocation.CurrentUser, string.Format("CN={0}{1}", CONFIG.sMakeCertRootCN, CONFIG.sMakeCertSubjectO)).Count);
			bMachineTrusted = (0 < DefaultCertificateProvider.FindCertsBySubject(StoreName.Root, StoreLocation.LocalMachine, string.Format("CN={0}{1}", CONFIG.sMakeCertRootCN, CONFIG.sMakeCertSubjectO)).Count);
			return bUserTrusted || bMachineTrusted;
		}
		public bool TrustRootCertificate()
		{
			X509Certificate2 rootCertificate = this.GetRootCertificate();
			if (rootCertificate == null)
			{
				FiddlerApplication.Log.LogString("!Fiddler.CertMaker> The Root certificate could not be found.");
				return false;
			}
			bool result;
			try
			{
				X509Store x509Store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
				x509Store.Open(OpenFlags.ReadWrite);
				try
				{
					x509Store.Add(rootCertificate);
				}
				finally
				{
					x509Store.Close();
				}
				result = true;
			}
			catch (Exception ex)
			{
				FiddlerApplication.Log.LogFormat("!Fiddler.CertMaker> Unable to auto-trust root: {0}", new object[]
				{
					ex
				});
				result = false;
			}
			return result;
		}
		public bool CreateRootCertificate()
		{
			return null != this.CreateCert(CONFIG.sMakeCertRootCN, true);
		}
		public X509Certificate2 GetRootCertificate()
		{
			if (this.certRoot != null)
			{
				return this.certRoot;
			}
			X509Certificate2 result = DefaultCertificateProvider.LoadCertificateFromWindowsStore(CONFIG.sMakeCertRootCN);
			this.certRoot = result;
			return result;
		}
		public X509Certificate2 GetCertificateForHost(string sHostname)
		{
			try
			{
				this.GetReaderLock();
				if (this.certServerCache.ContainsKey(sHostname))
				{
					return this.certServerCache[sHostname];
				}
			}
			finally
			{
				this.FreeReaderLock();
			}
			bool flag;
			X509Certificate2 x509Certificate = this.LoadOrCreateCertificate(sHostname, out flag);
			if (x509Certificate != null && !flag)
			{
				this.CacheCertificateForHost(sHostname, x509Certificate);
			}
			return x509Certificate;
		}
		internal X509Certificate2 LoadOrCreateCertificate(string sHostname, out bool bAttemptedCreation)
		{
			bAttemptedCreation = false;
			X509Certificate2 x509Certificate = DefaultCertificateProvider.LoadCertificateFromWindowsStore(sHostname);
			if (x509Certificate != null)
			{
				return x509Certificate;
			}
			bAttemptedCreation = true;
			x509Certificate = this.CreateCert(sHostname, false);
			if (x509Certificate == null)
			{
				FiddlerApplication.Log.LogFormat("!Fiddler.CertMaker> Tried to create cert for {0}, but can't find it from thread {1}!", new object[]
				{
					sHostname,
					Thread.CurrentThread.ManagedThreadId
				});
			}
			return x509Certificate;
		}
		internal static X509Certificate2 LoadCertificateFromWindowsStore(string sHostname)
		{
			X509Store x509Store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
			x509Store.Open(OpenFlags.ReadOnly);
			string inStr = string.Format("CN={0}{1}", sHostname, CONFIG.sMakeCertSubjectO);
			X509Certificate2Enumerator enumerator = x509Store.Certificates.GetEnumerator();
			while (enumerator.MoveNext())
			{
				X509Certificate2 current = enumerator.Current;
				if (inStr.OICEquals(current.Subject))
				{
					x509Store.Close();
					return current;
				}
			}
			x509Store.Close();
			return null;
		}
		public bool CacheCertificateForHost(string sHost, X509Certificate2 oCert)
		{
			try
			{
				this.GetWriterLock();
				this.certServerCache[sHost] = oCert;
			}
			finally
			{
				this.FreeWriterLock();
			}
			return true;
		}
		private X509Certificate2 CreateCert(string sHostname, bool isRoot)
		{
			if (sHostname.IndexOfAny(new char[]
			{
				'"',
				'\r',
				'\n',
				'\0'
			}) != -1)
			{
				return null;
			}
			if (!isRoot && this.GetRootCertificate() == null)
			{
				try
				{
					this.GetWriterLock();
					if (this.GetRootCertificate() == null && !this.CreateRootCertificate())
					{
						FiddlerApplication.DoNotifyUser("Creation of the root certificate was not successful.", "Certificate Error");
						X509Certificate2 result = null;
						return result;
					}
				}
				finally
				{
					this.FreeWriterLock();
				}
			}
			if (!File.Exists(this._sMakeCertLocation))
			{
				FiddlerApplication.DoNotifyUser("Cannot locate:\n\t\"" + this._sMakeCertLocation + "\"\n\nPlease move makecert.exe to the Fiddler installation directory.", "MakeCert.exe not found");
				throw new FileNotFoundException("Cannot locate: " + this._sMakeCertLocation + ". Please move makecert.exe to the Fiddler installation directory.");
			}
			X509Certificate2 x509Certificate = null;
			string stringPref = FiddlerApplication.Prefs.GetStringPref("fiddler.certmaker.DateFormatString", "MM/dd/yyyy");
			int num = -FiddlerApplication.Prefs.GetInt32Pref("fiddler.certmaker.GraceDays", 366);
			DateTime dateTime = DateTime.Now.AddDays((double)num);
			string text;
			if (isRoot)
			{
				text = string.Format(CONFIG.sMakeCertParamsRoot, new object[]
				{
					sHostname,
					CONFIG.sMakeCertSubjectO,
					CONFIG.sMakeCertRootCN,
					dateTime.ToString(stringPref, CultureInfo.InvariantCulture),
					FiddlerApplication.Prefs.GetStringPref("fiddler.certmaker.Root.extraparams", string.Empty)
				});
			}
			else
			{
				text = string.Format(CONFIG.sMakeCertParamsEE, new object[]
				{
					sHostname,
					CONFIG.sMakeCertSubjectO,
					CONFIG.sMakeCertRootCN,
					dateTime.ToString(stringPref, CultureInfo.InvariantCulture),
					FiddlerApplication.Prefs.GetStringPref("fiddler.certmaker.EE.extraparams", string.Empty)
				});
			}
			int num2;
			string executableOutput;
			try
			{
				this.GetWriterLock();
				X509Certificate2 x509Certificate2;
				if (!this.certServerCache.TryGetValue(sHostname, out x509Certificate2))
				{
					x509Certificate2 = DefaultCertificateProvider.LoadCertificateFromWindowsStore(sHostname);
				}
				if (x509Certificate2 != null)
				{
					if (CONFIG.bDebugCertificateGeneration)
					{
						FiddlerApplication.Log.LogFormat("/Fiddler.CertMaker>{1} A racing thread already successfully CreatedCert({0})", new object[]
						{
							sHostname,
							Thread.CurrentThread.ManagedThreadId
						});
					}
					X509Certificate2 result = x509Certificate2;
					return result;
				}
				if (CONFIG.bDebugCertificateGeneration)
				{
					FiddlerApplication.Log.LogFormat("/Fiddler.CertMaker> Invoking makecert.exe with arguments: {0}", new object[]
					{
						text
					});
				}
				executableOutput = Utilities.GetExecutableOutput(this._sMakeCertLocation, text, out num2);
				if (CONFIG.bDebugCertificateGeneration)
				{
					FiddlerApplication.Log.LogFormat("/Fiddler.CertMaker>{3}-CreateCert({0}) => ({1}){2}", new object[]
					{
						sHostname,
						num2,
						(num2 == 0) ? "." : ("\r\n" + executableOutput),
						Thread.CurrentThread.ManagedThreadId
					});
				}
				if (num2 == 0)
				{
					int num3 = 6;
					do
					{
						x509Certificate = DefaultCertificateProvider.LoadCertificateFromWindowsStore(sHostname);
						Thread.Sleep(50 * (6 - num3));
						if (CONFIG.bDebugCertificateGeneration && x509Certificate == null)
						{
							FiddlerApplication.Log.LogFormat("!WARNING: Couldn't find certificate for {0} on try #{1}", new object[]
							{
								sHostname,
								6 - num3
							});
						}
						num3--;
						if (x509Certificate != null)
						{
							break;
						}
					}
					while (num3 >= 0);
				}
				if (x509Certificate != null)
				{
					if (isRoot)
					{
						this.certRoot = x509Certificate;
					}
					else
					{
						this.certServerCache[sHostname] = x509Certificate;
					}
				}
			}
			finally
			{
				this.FreeWriterLock();
			}
			if (x509Certificate == null)
			{
				string text2 = string.Format("Creation of the interception certificate failed.\n\nmakecert.exe returned {0}.\n\n{1}", num2, executableOutput);
				FiddlerApplication.Log.LogFormat("Fiddler.CertMaker> [{0} {1}] Returned Error: {2} ", new object[]
				{
					this._sMakeCertLocation,
					text,
					text2
				});
				if (CONFIG.bDebugCertificateGeneration)
				{
					FiddlerApplication.DoNotifyUser(text2, "Unable to Generate Certificate");
				}
			}
			return x509Certificate;
		}
	}
}
