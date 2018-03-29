using System;
namespace Fiddler
{
	public interface ICertificateProvider2 : ICertificateProvider
	{
		bool ClearCertificateCache(bool bClearRoot);
	}
}
