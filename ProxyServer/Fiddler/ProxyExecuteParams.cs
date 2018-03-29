using System;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
namespace Fiddler
{
	internal class ProxyExecuteParams
	{
		public Socket oSocket;
		public X509Certificate2 oServerCert;
		public DateTime dtConnectionAccepted;
		public ProxyExecuteParams(Socket oS, X509Certificate2 oC)
		{
			this.dtConnectionAccepted = DateTime.Now;
			this.oSocket = oS;
			this.oServerCert = oC;
		}
	}
}
