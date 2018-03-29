using System;
using System.Security.Cryptography.X509Certificates;
namespace Fiddler
{
	public interface ICertificateProvider3 : ICertificateProvider2
	{
		bool CacheCertificateForHost(string sHost, X509Certificate2 oCert);
	}
}
