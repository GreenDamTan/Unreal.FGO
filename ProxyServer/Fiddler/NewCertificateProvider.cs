namespace Fiddler
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;

    internal class NewCertificateProvider : ICertificateProvider, ICertificateProvider2, ICertificateProvider3
    {
        private bool useWildcards;
        private Dictionary<string, X509Certificate2> certServerCache = new Dictionary<string, X509Certificate2>();
        private ICertificateEngine engin;
        private ReaderWriterLockSlim readerWriterLockSlim_0 = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        private readonly string[] domainSuffix = new string[] { ".com", ".org", ".edu", ".gov", ".net" };
        private X509Certificate2 certRoot;

        internal NewCertificateProvider()
        {
            bool flag = false;
            if (FiddlerApplication.Prefs.GetBoolPref("fiddler.certmaker.PreferCertEnroll", true) && IsWin7())
            {
                flag = true;
                this.engin = CertEnrollEngine.CreateEngine(this);
            }
            if (this.engin == null)
            {
                this.engin = MakeCertEngine.smethod_0();
            }
            if ((this.engin == null) && !flag)
            {
                this.engin = CertEnrollEngine.CreateEngine(this);
            }
            if (this.engin == null)
            {
                FiddlerApplication.Log.LogFormat("!Fiddler.CertMaker> Critical failure: No Certificate Creation engine could be created. Disabling HTTPS Decryption.", new object[0]);
                CONFIG.bMITM_HTTPS = false;
            }
            else
            {
                this.useWildcards = FiddlerApplication.Prefs.GetBoolPref("fiddler.certmaker.UseWildcards", true);
                if (this.engin.GetType() == typeof(MakeCertEngine))
                {
                    this.useWildcards = false;
                }
                if (CONFIG.bDebugCertificateGeneration)
                {
                    FiddlerApplication.Log.LogFormat("/Fiddler.CertMaker> Using {0} for certificate generation; UseWildcards={1}.", new object[] { this.engin.GetType().ToString(), this.useWildcards });
                }
            }
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

        public bool ClearCertificateCache()
        {
            return this.ClearCertificateCache(true);
        }

        public bool ClearCertificateCache(bool bClearRoot)
        {
            bool flag = true;
            try
            {
                X509Certificate2Collection certificates;
                this.GetWriterLock();
                this.certServerCache.Clear();
                this.certRoot = null;
                string str = string.Format("CN={0}{1}", CONFIG.sMakeCertRootCN, CONFIG.sMakeCertSubjectO);
                if (bClearRoot)
                {
                    certificates = smethod_1(StoreName.Root, StoreLocation.CurrentUser, str);
                    if (certificates.Count > 0)
                    {
                        X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                        store.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadWrite);
                        try
                        {
                            store.RemoveRange(certificates);
                        }
                        catch
                        {
                            flag = false;
                        }
                        store.Close();
                    }
                }
                certificates = smethod_2(StoreName.My, str);
                if (certificates.Count <= 0)
                {
                    return flag;
                }
                if (!bClearRoot)
                {
                    X509Certificate2 rootCertificate = this.GetRootCertificate();
                    if (rootCertificate != null)
                    {
                        certificates.Remove(rootCertificate);
                        if (certificates.Count < 1)
                        {
                            return true;
                        }
                    }
                }
                X509Store store2 = new X509Store(StoreName.My, StoreLocation.CurrentUser);
                store2.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadWrite);
                try
                {
                    store2.RemoveRange(certificates);
                }
                catch
                {
                    flag = false;
                }
                store2.Close();
            }
            finally
            {
                this.FreeWriterLock();
            }
            return flag;
        }

        public bool CreateRootCertificate()
        {
            return (null != this.method_2(CONFIG.sMakeCertRootCN, true));
        }

        private void FreeReaderLock()
        {
            this.readerWriterLockSlim_0.ExitReadLock();
        }

        private void FreeWriterLock()
        {
            this.readerWriterLockSlim_0.ExitWriteLock();
        }

        public X509Certificate2 GetCertificateForHost(string sHostname)
        {
            X509Certificate2 certificate;
            bool flag;
            if ((this.useWildcards && sHostname.OICEndsWithAny(this.domainSuffix)) && (Utilities.IndexOfNth(sHostname, 2, '.') > 0))
            {
                sHostname = "*." + Utilities.TrimBefore(sHostname, ".");
            }
            try
            {
                this.GetReaderLock();
                if (this.certServerCache.TryGetValue(sHostname, out certificate))
                {
                    return certificate;
                }
            }
            finally
            {
                this.FreeReaderLock();
            }
            certificate = this.method_1(sHostname, out flag);
            if ((certificate != null) && !flag)
            {
                this.CacheCertificateForHost(sHostname, certificate);
            }
            return certificate;
        }

        public string GetConfigurationString()
        {
            if (this.engin == null)
            {
                return "No Engine Loaded.";
            }
            StringBuilder builder = new StringBuilder();
            string str = Utilities.TrimBefore(this.engin.GetType().ToString(), "+");
            builder.AppendFormat("Certificate Engine:\t{0}\n", str);
            if (str == "CertEnrollEngine")
            {
                builder.AppendFormat("HashAlg-Root:\t{0}\n", FiddlerApplication.Prefs.GetStringPref("fiddler.certmaker.ce.Root.SigAlg", "SHA256"));
                builder.AppendFormat("HashAlg-EE:\t{0}\n", FiddlerApplication.Prefs.GetStringPref("fiddler.certmaker.ce.EE.SigAlg", "SHA256"));
                builder.AppendFormat("KeyLen-Root:\t{0}bits\n", FiddlerApplication.Prefs.GetInt32Pref("fiddler.certmaker.ce.Root.KeyLength", 0x800));
                builder.AppendFormat("KeyLen-EE:\t{0}bits\n", FiddlerApplication.Prefs.GetInt32Pref("fiddler.certmaker.ce.EE.KeyLength", 0x800));
                builder.AppendFormat("ValidFrom:\t{0} days ago\n", FiddlerApplication.Prefs.GetInt32Pref("fiddler.certmaker.GraceDays", 0x16e));
                builder.AppendFormat("ValidFor:\t\t{0} days\n", FiddlerApplication.Prefs.GetInt32Pref("fiddler.certmaker.ValidDays", 0x721));
            }
            else
            {
                builder.AppendFormat("ValidFrom:\t{0} days ago\n", FiddlerApplication.Prefs.GetInt32Pref("fiddler.certmaker.GraceDays", 0x16e));
                builder.AppendFormat("HashAlg-Root:\t{0}\n", FiddlerApplication.Prefs.GetStringPref("fiddler.certmaker.Root.SigAlg", (Environment.OSVersion.Version.Major > 5) ? "SHA256" : "SHA1"));
                builder.AppendFormat("HashAlg-EE:\t{0}\n", FiddlerApplication.Prefs.GetStringPref("fiddler.certmaker.EE.SigAlg", (Environment.OSVersion.Version.Major > 5) ? "SHA256" : "SHA1"));
                builder.AppendFormat("ExtraParams-Root:\t{0}\n", FiddlerApplication.Prefs.GetStringPref("fiddler.certmaker.EE.extraparams", string.Empty));
                builder.AppendFormat("ExtraParams-EE:\t{0}\n", FiddlerApplication.Prefs.GetStringPref("fiddler.certmaker.root.extraparams", string.Empty));
            }
            return builder.ToString();
        }

        private void GetReaderLock()
        {
            this.readerWriterLockSlim_0.EnterReadLock();
        }

        public X509Certificate2 GetRootCertificate()
        {
            if (this.certRoot != null)
            {
                return this.certRoot;
            }
            X509Certificate2 certificate = LoadCertificateFromWindowsStore(CONFIG.sMakeCertRootCN);
            if (CONFIG.bDebugCertificateGeneration)
            {
                if (certificate != null)
                {
                    smethod_3(certificate);
                }
                else
                {
                    FiddlerApplication.Log.LogString("DefaultCertMaker: GetRootCertificate() did not find the root in the Windows TrustStore.");
                }
            }
            this.certRoot = certificate;
            return certificate;
        }

        private void GetWriterLock()
        {
            this.readerWriterLockSlim_0.EnterWriteLock();
        }

        internal string GetEngineName()
        {
            if (this.engin.GetType() == typeof(CertEnrollEngine))
            {
                return "CertEnroll engine";
            }
            if (this.engin.GetType() == typeof(MakeCertEngine))
            {
                return "MakeCert engine";
            }
            return "Unknown engine";
        }

        internal X509Certificate2 method_1(string string_1, out bool bool_1)
        {
            bool_1 = false;
            X509Certificate2 certificate = LoadCertificateFromWindowsStore(string_1);
            if (certificate == null)
            {
                bool_1 = true;
                certificate = this.method_2(string_1, false);
                if (certificate == null)
                {
                    FiddlerApplication.Log.LogFormat("!Fiddler.CertMaker> Tried to create cert for '{0}', but can't find it from thread {1}!", new object[] { string_1, Thread.CurrentThread.ManagedThreadId });
                }
            }
            return certificate;
        }

        private X509Certificate2 method_2(string string_1, bool bool_1)
        {
            if (string_1.IndexOfAny(new char[] { '"', '\r', '\n', '\0' }) != -1)
            {
                return null;
            }
            if (!bool_1 && (this.GetRootCertificate() == null))
            {
                try
                {
                    this.GetWriterLock();
                    if ((this.GetRootCertificate() == null) && !this.CreateRootCertificate())
                    {
                        FiddlerApplication.DoNotifyUser("Creation of the root certificate was not successful.", "Certificate Error");
                        return null;
                    }
                }
                finally
                {
                    this.FreeWriterLock();
                }
            }
            X509Certificate2 certificate = null;
            try
            {
                X509Certificate2 certificate2;
                this.GetWriterLock();
                if (!this.certServerCache.TryGetValue(string_1, out certificate2))
                {
                    certificate2 = LoadCertificateFromWindowsStore(string_1);
                }
                if (certificate2 != null)
                {
                    if (CONFIG.bDebugCertificateGeneration)
                    {
                        FiddlerApplication.Log.LogFormat("/Fiddler.CertMaker>{1} A racing thread already successfully CreatedCert({0})", new object[] { string_1, Thread.CurrentThread.ManagedThreadId });
                    }
                    return certificate2;
                }
                certificate = this.engin.GetRootCertificate(string_1, bool_1);
                if (certificate != null)
                {
                    if (bool_1)
                    {
                        this.certRoot = certificate;
                    }
                    else
                    {
                        this.certServerCache[string_1] = certificate;
                    }
                }
            }
            finally
            {
                this.FreeWriterLock();
            }
            if ((certificate == null) && !bool_1)
            {
                FiddlerApplication.Log.LogFormat("!Fiddler.CertMaker> Failed to create certificate for '{0}'.", new object[] { string_1 });
                smethod_3(this.GetRootCertificate());
            }
            return certificate;
        }

        public bool rootCertIsTrusted(out bool bUserTrusted, out bool bMachineTrusted)
        {
            bUserTrusted = 0 < smethod_1(StoreName.Root, StoreLocation.CurrentUser, string.Format("CN={0}{1}", CONFIG.sMakeCertRootCN, CONFIG.sMakeCertSubjectO)).Count;
            bMachineTrusted = 0 < smethod_1(StoreName.Root, StoreLocation.LocalMachine, string.Format("CN={0}{1}", CONFIG.sMakeCertRootCN, CONFIG.sMakeCertSubjectO)).Count;
            if (!bUserTrusted)
            {
                return bMachineTrusted;
            }
            return true;
        }

        private static bool IsWin7()
        {
            return ((Environment.OSVersion.Version.Major > 6) || ((Environment.OSVersion.Version.Major == 6) && (Environment.OSVersion.Version.Minor > 0)));
        }

        private static X509Certificate2Collection smethod_1(StoreName storeName_0, StoreLocation storeLocation_0, string string_1)
        {
            X509Certificate2Collection certificates2;
            X509Store store = new X509Store(storeName_0, storeLocation_0);
            try
            {
                store.Open(OpenFlags.OpenExistingOnly);
                certificates2 = store.Certificates.Find(X509FindType.FindBySubjectDistinguishedName, string_1, false);
            }
            finally
            {
                store.Close();
            }
            return certificates2;
        }

        private static X509Certificate2Collection smethod_2(StoreName storeName_0, string string_1)
        {
            X509Certificate2Collection certificates2;
            X509Store store = new X509Store(storeName_0, StoreLocation.CurrentUser);
            try
            {
                store.Open(OpenFlags.OpenExistingOnly);
                certificates2 = store.Certificates.Find(X509FindType.FindByIssuerDistinguishedName, string_1, false);
            }
            finally
            {
                store.Close();
            }
            return certificates2;
        }

        private static void smethod_3(X509Certificate2 x509Certificate2_1)
        {
            try
            {
                if (x509Certificate2_1 != null)
                {
                    if (!x509Certificate2_1.HasPrivateKey)
                    {
                        FiddlerApplication.Log.LogString("/Fiddler.CertMaker> Root Certificate located but HasPrivateKey==false!");
                    }
                    FiddlerApplication.Log.LogFormat("/Fiddler.CertMaker> Root Certificate located; private key in container '{0}'", new object[] { (x509Certificate2_1.PrivateKey as RSACryptoServiceProvider).CspKeyContainerInfo.UniqueKeyContainerName });
                }
                else
                {
                    FiddlerApplication.Log.LogString("/Fiddler.CertMaker> Unable to log Root Certificate private key storage as the certificate was unexpectedly null");
                }
            }
            catch (Exception exception)
            {
                FiddlerApplication.Log.LogFormat("/Fiddler.CertMaker> Failed to identify private key location for Root Certificate. Exception: {0}", new object[] { Utilities.GetExceptionInfo(exception) });
            }
        }

        internal static X509Certificate2 LoadCertificateFromWindowsStore(string sHostname)
        {
            X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            try
            {
                store.Open(OpenFlags.ReadOnly);
                string inStr = string.Format("CN={0}{1}", sHostname, CONFIG.sMakeCertSubjectO);
                X509Certificate2Enumerator enumerator = store.Certificates.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    X509Certificate2 current = enumerator.Current;
                    if (inStr.OICEquals(current.Subject))
                    {
                        return current;
                    }
                }
            }
            finally
            {
                store.Close();
            }
            return null;
        }

        public bool TrustRootCertificate()
        {
            X509Certificate2 rootCertificate = this.GetRootCertificate();
            if (rootCertificate == null)
            {
                FiddlerApplication.Log.LogString("!Fiddler.CertMaker> The Root certificate could not be found.");
                return false;
            }
            try
            {
                X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
                store.Open(OpenFlags.ReadWrite);
                try
                {
                    store.Add(rootCertificate);
                }
                finally
                {
                    store.Close();
                }
                return true;
            }
            catch (Exception exception)
            {
                FiddlerApplication.Log.LogFormat("!Fiddler.CertMaker> Unable to auto-trust root: {0}", new object[] { exception });
                return false;
            }
        }

        private class CertEnrollEngine : NewCertificateProvider.ICertificateEngine
        {
            private ICertificateProvider3 icertificateProvider3_0;
            private object object_0;
            private string keyProviderName = "Microsoft Enhanced Cryptographic Provider v1.0";
            private System.Type type_0;
            private System.Type type_1;
            private System.Type type_10;
            private System.Type type_11;
            private System.Type type_12;
            private System.Type type_13;
            private System.Type type_2;
            private System.Type type_3;
            private System.Type type_4;
            private System.Type type_5;
            private System.Type type_6;
            private System.Type type_7;
            private System.Type type_8;
            private System.Type type_9;

            private CertEnrollEngine(ICertificateProvider3 ParentProvider)
            {
                this.icertificateProvider3_0 = ParentProvider;
                this.keyProviderName = FiddlerApplication.Prefs.GetStringPref("fiddler.certmaker.KeyProviderName", this.keyProviderName);
                this.type_0 = System.Type.GetTypeFromProgID("X509Enrollment.CX500DistinguishedName", true);
                this.type_1 = System.Type.GetTypeFromProgID("X509Enrollment.CX509PrivateKey", true);
                this.type_2 = System.Type.GetTypeFromProgID("X509Enrollment.CObjectId", true);
                this.type_3 = System.Type.GetTypeFromProgID("X509Enrollment.CObjectIds.1", true);
                this.type_5 = System.Type.GetTypeFromProgID("X509Enrollment.CX509ExtensionEnhancedKeyUsage");
                this.type_4 = System.Type.GetTypeFromProgID("X509Enrollment.CX509ExtensionKeyUsage");
                this.type_6 = System.Type.GetTypeFromProgID("X509Enrollment.CX509CertificateRequestCertificate");
                this.type_7 = System.Type.GetTypeFromProgID("X509Enrollment.CX509Extensions");
                this.type_8 = System.Type.GetTypeFromProgID("X509Enrollment.CX509ExtensionBasicConstraints");
                this.type_9 = System.Type.GetTypeFromProgID("X509Enrollment.CSignerCertificate");
                this.type_10 = System.Type.GetTypeFromProgID("X509Enrollment.CX509Enrollment");
                this.type_11 = System.Type.GetTypeFromProgID("X509Enrollment.CAlternativeName");
                this.type_12 = System.Type.GetTypeFromProgID("X509Enrollment.CAlternativeNames");
                this.type_13 = System.Type.GetTypeFromProgID("X509Enrollment.CX509ExtensionAlternativeNames");
            }

            public X509Certificate2 GetRootCertificate(string string_1, bool bool_0)
            {
                return this.method_0(string_1, bool_0, true);
            }

            private X509Certificate2 method_0(string string_1, bool bool_0, bool bool_1)
            {
                Class36 class3 = new Class36
                {
                    string_0 = string_1,
                    bool_0 = bool_0,
                    class35_0 = this
                };
                if (bool_1 && (Thread.CurrentThread.GetApartmentState() != ApartmentState.MTA))
                {
                    Class37 class2 = new Class37
                    {
                        class36_0 = class3
                    };
                    if (CONFIG.bDebugCertificateGeneration)
                    {
                        FiddlerApplication.Log.LogFormat("/Fiddler.CertMaker> Caller was in ApartmentState: {0}; hopping to Threadpool", new object[] { Thread.CurrentThread.GetApartmentState().ToString() });
                    }
                    class2.x509Certificate2_0 = null;
                    class2.manualResetEvent_0 = new ManualResetEvent(false);
                    ThreadPool.QueueUserWorkItem(new WaitCallback(class2.method_0));
                    class2.manualResetEvent_0.WaitOne();
                    class2.manualResetEvent_0.Close();
                    return class2.x509Certificate2_0;
                }
                string str = string.Format("CN={0}{1}", class3.string_0, CONFIG.sMakeCertSubjectO);
                if (CONFIG.bDebugCertificateGeneration)
                {
                    FiddlerApplication.Log.LogFormat("/Fiddler.CertMaker> Invoking CertEnroll for Subject: {0}; Thread's ApartmentState: {1}", new object[] { str, Thread.CurrentThread.GetApartmentState().ToString() });
                }
                string stringPref = FiddlerApplication.Prefs.GetStringPref(class3.bool_0 ? "fiddler.certmaker.ce.Root.SigAlg" : "fiddler.certmaker.ce.EE.SigAlg", "SHA256");
                int num = -FiddlerApplication.Prefs.GetInt32Pref("fiddler.certmaker.GraceDays", 0x16e);
                int num2 = FiddlerApplication.Prefs.GetInt32Pref("fiddler.certmaker.ValidDays", 0x721);
                X509Certificate2 certificate = null;
                try
                {
                    if (class3.bool_0)
                    {
                        int num3 = FiddlerApplication.Prefs.GetInt32Pref("fiddler.certmaker.ce.Root.KeyLength", 0x800);
                        certificate = this.method_1(true, class3.string_0, str, num3, stringPref, DateTime.Now.AddDays((double)num), DateTime.Now.AddDays((double)num2), null);
                    }
                    else
                    {
                        int num4 = FiddlerApplication.Prefs.GetInt32Pref("fiddler.certmaker.ce.EE.KeyLength", 0x800);
                        certificate = this.method_1(false, class3.string_0, str, num4, stringPref, DateTime.Now.AddDays((double)num), DateTime.Now.AddDays((double)num2), this.icertificateProvider3_0.GetRootCertificate());
                    }
                    if (CONFIG.bDebugCertificateGeneration)
                    {
                        FiddlerApplication.Log.LogFormat("/Fiddler.CertMaker> Finished CertEnroll for '{0}'. Returning {1}", new object[] { str, (certificate != null) ? "cert" : "null" });
                    }
                    return certificate;
                }
                catch (Exception exception)
                {
                    FiddlerApplication.Log.LogFormat("!ERROR: Failed to generate Certificate using CertEnroll. {0}", new object[] { Utilities.GetExceptionInfo(exception) });
                }
                return null;
            }

            private X509Certificate2 method_1(bool bool_0, string string_1, string string_2, int int_0, string string_3, DateTime dateTime_0, DateTime dateTime_1, X509Certificate2 x509Certificate2_0)
            {
                X509Certificate2 certificate2;
                if (bool_0 != (null == x509Certificate2_0))
                {
                    throw new ArgumentException("You must specify a Signing Certificate if and only if you are not creating a root.", "oSigningCertificate");
                }
                object target = Activator.CreateInstance(this.type_0);
                object[] args = new object[] { string_2, 0 };
                this.type_0.InvokeMember("Encode", BindingFlags.InvokeMethod, null, target, args);
                object obj3 = Activator.CreateInstance(this.type_0);
                if (!bool_0)
                {
                    args[0] = x509Certificate2_0.Subject;
                }
                this.type_0.InvokeMember("Encode", BindingFlags.InvokeMethod, null, obj3, args);
                object obj4 = null;
                if (!bool_0)
                {
                    obj4 = this.object_0;
                }
                if (obj4 == null)
                {
                    obj4 = Activator.CreateInstance(this.type_1);
                    args = new object[] { this.keyProviderName };
                    this.type_1.InvokeMember("ProviderName", BindingFlags.PutDispProperty, null, obj4, args);
                    args[0] = 2;
                    this.type_1.InvokeMember("ExportPolicy", BindingFlags.PutDispProperty, null, obj4, args);
                    args = new object[] { bool_0 ? 2 : 1 };
                    this.type_1.InvokeMember("KeySpec", BindingFlags.PutDispProperty, null, obj4, args);
                    if (!bool_0)
                    {
                        args = new object[] { 0xb0 };
                        this.type_1.InvokeMember("KeyUsage", BindingFlags.PutDispProperty, null, obj4, args);
                    }
                    args[0] = int_0;
                    this.type_1.InvokeMember("Length", BindingFlags.PutDispProperty, null, obj4, args);
                    this.type_1.InvokeMember("Create", BindingFlags.InvokeMethod, null, obj4, null);
                    if (!bool_0)
                    {
                        this.object_0 = obj4;
                    }
                }
                else if (CONFIG.bDebugCertificateGeneration)
                {
                    FiddlerApplication.Log.LogFormat("/Fiddler.CertMaker> Reusing PrivateKey for '{0}'", new object[] { string_1 });
                }
                args = new object[1];
                object obj5 = Activator.CreateInstance(this.type_2);
                args[0] = "1.3.6.1.5.5.7.3.1";
                this.type_2.InvokeMember("InitializeFromValue", BindingFlags.InvokeMethod, null, obj5, args);
                object obj6 = Activator.CreateInstance(this.type_3);
                args[0] = obj5;
                this.type_3.InvokeMember("Add", BindingFlags.InvokeMethod, null, obj6, args);
                object obj7 = Activator.CreateInstance(this.type_5);
                args[0] = obj6;
                this.type_5.InvokeMember("InitializeEncode", BindingFlags.InvokeMethod, null, obj7, args);
                object obj8 = Activator.CreateInstance(this.type_6);
                args = new object[] { 1, obj4, string.Empty };
                this.type_6.InvokeMember("InitializeFromPrivateKey", BindingFlags.InvokeMethod, null, obj8, args);
                args = new object[] { target };
                this.type_6.InvokeMember("Subject", BindingFlags.PutDispProperty, null, obj8, args);
                args[0] = obj3;
                this.type_6.InvokeMember("Issuer", BindingFlags.PutDispProperty, null, obj8, args);
                args[0] = dateTime_0;
                this.type_6.InvokeMember("NotBefore", BindingFlags.PutDispProperty, null, obj8, args);
                args[0] = dateTime_1;
                this.type_6.InvokeMember("NotAfter", BindingFlags.PutDispProperty, null, obj8, args);
                object obj9 = Activator.CreateInstance(this.type_4);
                args[0] = 0xb0;
                this.type_4.InvokeMember("InitializeEncode", BindingFlags.InvokeMethod, null, obj9, args);
                object obj10 = this.type_6.InvokeMember("X509Extensions", BindingFlags.GetProperty, null, obj8, null);
                args = new object[1];
                if (!bool_0)
                {
                    args[0] = obj9;
                    this.type_7.InvokeMember("Add", BindingFlags.InvokeMethod, null, obj10, args);
                }
                args[0] = obj7;
                this.type_7.InvokeMember("Add", BindingFlags.InvokeMethod, null, obj10, args);
                if (!bool_0 && FiddlerApplication.Prefs.GetBoolPref("fiddler.certmaker.AddSubjectAltName", true))
                {
                    object obj11 = Activator.CreateInstance(this.type_11);
                    IPAddress address = Utilities.IPFromString(string_1);
                    if (address == null)
                    {
                        args = new object[] { 3, string_1 };
                        this.type_11.InvokeMember("InitializeFromString", BindingFlags.InvokeMethod, null, obj11, args);
                    }
                    else
                    {
                        args = new object[] { 8, 1, Convert.ToBase64String(address.GetAddressBytes()) };
                        this.type_11.InvokeMember("InitializeFromRawData", BindingFlags.InvokeMethod, null, obj11, args);
                    }
                    object obj12 = Activator.CreateInstance(this.type_12);
                    args = new object[] { obj11 };
                    this.type_12.InvokeMember("Add", BindingFlags.InvokeMethod, null, obj12, args);
                    Marshal.ReleaseComObject(obj11);
                    if ((address != null) && (AddressFamily.InterNetworkV6 == address.AddressFamily))
                    {
                        obj11 = Activator.CreateInstance(this.type_11);
                        args = new object[] { 3, "[" + string_1 + "]" };
                        this.type_11.InvokeMember("InitializeFromString", BindingFlags.InvokeMethod, null, obj11, args);
                        args = new object[] { obj11 };
                        this.type_12.InvokeMember("Add", BindingFlags.InvokeMethod, null, obj12, args);
                        Marshal.ReleaseComObject(obj11);
                    }
                    object obj13 = Activator.CreateInstance(this.type_13);
                    args = new object[] { obj12 };
                    this.type_13.InvokeMember("InitializeEncode", BindingFlags.InvokeMethod, null, obj13, args);
                    args = new object[] { obj13 };
                    this.type_7.InvokeMember("Add", BindingFlags.InvokeMethod, null, obj10, args);
                }
                if (bool_0)
                {
                    object obj14 = Activator.CreateInstance(this.type_8);
                    args = new object[] { "true", "0" };
                    this.type_8.InvokeMember("InitializeEncode", BindingFlags.InvokeMethod, null, obj14, args);
                    args = new object[] { obj14 };
                    this.type_7.InvokeMember("Add", BindingFlags.InvokeMethod, null, obj10, args);
                }
                else
                {
                    object obj15 = Activator.CreateInstance(this.type_9);
                    args = new object[] { 0, 0, 12, x509Certificate2_0.Thumbprint };
                    this.type_9.InvokeMember("Initialize", BindingFlags.InvokeMethod, null, obj15, args);
                    args = new object[] { obj15 };
                    this.type_6.InvokeMember("SignerCertificate", BindingFlags.PutDispProperty, null, obj8, args);
                }
                object obj16 = Activator.CreateInstance(this.type_2);
                args = new object[] { 1, 0, 0, string_3 };
                this.type_2.InvokeMember("InitializeFromAlgorithmName", BindingFlags.InvokeMethod, null, obj16, args);
                args = new object[] { obj16 };
                this.type_6.InvokeMember("HashAlgorithm", BindingFlags.PutDispProperty, null, obj8, args);
                this.type_6.InvokeMember("Encode", BindingFlags.InvokeMethod, null, obj8, null);
                object obj17 = Activator.CreateInstance(this.type_10);
                args[0] = obj8;
                this.type_10.InvokeMember("InitializeFromRequest", BindingFlags.InvokeMethod, null, obj17, args);
                if (bool_0)
                {
                    args[0] = "DO_NOT_TRUST_FiddlerRoot-CE";
                    this.type_10.InvokeMember("CertificateFriendlyName", BindingFlags.PutDispProperty, null, obj17, args);
                }
                args[0] = 0;
                object obj18 = this.type_10.InvokeMember("CreateRequest", BindingFlags.InvokeMethod, null, obj17, args);
                args = new object[] { 2, obj18, 0, string.Empty };
                this.type_10.InvokeMember("InstallResponse", BindingFlags.InvokeMethod, null, obj17, args);
                args = new object[] { null, 0, 1 };
                string s = string.Empty;
                try
                {
                    s = (string)this.type_10.InvokeMember("CreatePFX", BindingFlags.InvokeMethod, null, obj17, args);
                    return new X509Certificate2(Convert.FromBase64String(s), string.Empty, X509KeyStorageFlags.Exportable);
                }
                catch (Exception exception)
                {
                    FiddlerApplication.Log.LogFormat("!Failed to CreatePFX: {0}", new object[] { Utilities.GetExceptionInfo(exception) });
                    certificate2 = null;
                }
                return certificate2;
            }

            internal static NewCertificateProvider.ICertificateEngine CreateEngine(ICertificateProvider3 certificateProvider)
            {
                try
                {
                    return new NewCertificateProvider.CertEnrollEngine(certificateProvider);
                }
                catch (Exception exception)
                {
                    FiddlerApplication.Log.LogFormat("Failed to initialize CertEnrollEngine: {0}", new object[] { Utilities.GetExceptionInfo(exception) });
                }
                return null;
            }

            [CompilerGenerated]
            private sealed class Class36
            {
                public bool bool_0;
                public NewCertificateProvider.CertEnrollEngine class35_0;
                public string string_0;
            }

            [CompilerGenerated]
            private sealed class Class37
            {
                public NewCertificateProvider.CertEnrollEngine.Class36 class36_0;
                public ManualResetEvent manualResetEvent_0;
                public X509Certificate2 x509Certificate2_0;

                public void method_0(object object_0)
                {
                    this.x509Certificate2_0 = this.class36_0.class35_0.method_0(this.class36_0.string_0, this.class36_0.bool_0, false);
                    this.manualResetEvent_0.Set();
                }
            }
        }

        private class MakeCertEngine : NewCertificateProvider.ICertificateEngine
        {
            private string string_0;
            private string string_1 = "sha1";

            private MakeCertEngine()
            {
                if (Environment.OSVersion.Version.Major > 5)
                {
                    this.string_1 = "sha256";
                }
                this.string_0 = CONFIG.GetPath("MakeCert");
                if (!System.IO.File.Exists(this.string_0))
                {
                    FiddlerApplication.Log.LogFormat("Cannot locate:\n\t\"{0}\"\n\nPlease move makecert.exe to the Fiddler installation directory.", new object[] { this.string_0 });
                    throw new FileNotFoundException("Cannot locate: \"" + this.string_0 + "\". Please move makecert.exe to the Fiddler installation directory.");
                }
            }

            public X509Certificate2 GetRootCertificate(string string_2, bool bool_0)
            {
                X509Certificate2 certificate = null;
                int num;
                string str2;
                string stringPref = FiddlerApplication.Prefs.GetStringPref("fiddler.certmaker.DateFormatString", "MM/dd/yyyy");
                int num2 = -FiddlerApplication.Prefs.GetInt32Pref("fiddler.certmaker.GraceDays", 0x16e);
                DateTime time = DateTime.Now.AddDays((double)num2);
                if (bool_0)
                {
                    str2 = string.Format(CONFIG.sMakeCertParamsRoot, new object[] { string_2, CONFIG.sMakeCertSubjectO, CONFIG.sMakeCertRootCN, FiddlerApplication.Prefs.GetStringPref("fiddler.certmaker.Root.SigAlg", this.string_1), time.ToString(stringPref, CultureInfo.InvariantCulture), FiddlerApplication.Prefs.GetStringPref("fiddler.certmaker.Root.extraparams", string.Empty) });
                }
                else
                {
                    str2 = string.Format(CONFIG.sMakeCertParamsEE, new object[] { string_2, CONFIG.sMakeCertSubjectO, CONFIG.sMakeCertRootCN, FiddlerApplication.Prefs.GetStringPref("fiddler.certmaker.EE.SigAlg", this.string_1), time.ToString(stringPref, CultureInfo.InvariantCulture), FiddlerApplication.Prefs.GetStringPref("fiddler.certmaker.EE.extraparams", string.Empty) });
                }
                if (CONFIG.bDebugCertificateGeneration)
                {
                    FiddlerApplication.Log.LogFormat("/Fiddler.CertMaker> Invoking makecert.exe with arguments: {0}", new object[] { str2 });
                }
                string str = Utilities.GetExecutableOutput(this.string_0, str2, out num);
                if (CONFIG.bDebugCertificateGeneration)
                {
                    FiddlerApplication.Log.LogFormat("/Fiddler.CertMaker>{3}-CreateCert({0}) => ({1}){2}", new object[] { string_2, num, (num == 0) ? "." : ("\r\n" + str), Thread.CurrentThread.ManagedThreadId });
                }
                if (num == 0)
                {
                    int num3 = 6;
                    do
                    {
                        certificate = NewCertificateProvider.LoadCertificateFromWindowsStore(string_2);
                        Thread.Sleep((int)(50 * (6 - num3)));
                        if (CONFIG.bDebugCertificateGeneration && (certificate == null))
                        {
                            FiddlerApplication.Log.LogFormat("!WARNING: Couldn't find certificate for {0} on try #{1}", new object[] { string_2, 6 - num3 });
                        }
                        num3--;
                    }
                    while ((certificate == null) && (num3 >= 0));
                }
                if (certificate == null)
                {
                    string sMessage = string.Format("Creation of the interception certificate failed.\n\nmakecert.exe returned {0}.\n\n{1}", num, str);
                    FiddlerApplication.Log.LogFormat("Fiddler.CertMaker> [{0} {1}] Returned Error: {2} ", new object[] { this.string_0, str2, sMessage });
                    if (CONFIG.bDebugCertificateGeneration)
                    {
                        FiddlerApplication.DoNotifyUser(sMessage, "Unable to Generate Certificate");
                    }
                }
                return certificate;
            }

            internal static NewCertificateProvider.ICertificateEngine smethod_0()
            {
                try
                {
                    return new NewCertificateProvider.MakeCertEngine();
                }
                catch (Exception exception)
                {
                    FiddlerApplication.Log.LogFormat("!Failed to initialize MakeCertEngine: {0}", new object[] { Utilities.GetExceptionInfo(exception) });
                }
                return null;
            }
        }

        private interface ICertificateEngine
        {
            X509Certificate2 GetRootCertificate(string domain, bool bool_0);
        }
    }
}

