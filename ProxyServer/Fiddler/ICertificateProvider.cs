using System;
using System.Security.Cryptography.X509Certificates;
namespace Fiddler
{
	public interface ICertificateProvider
	{
		X509Certificate2 GetCertificateForHost(string sHostname);
		X509Certificate2 GetRootCertificate();
		bool CreateRootCertificate();
		bool TrustRootCertificate();
		bool ClearCertificateCache();
		bool rootCertIsTrusted(out bool bUserTrusted, out bool bMachineTrusted);
	}
}
