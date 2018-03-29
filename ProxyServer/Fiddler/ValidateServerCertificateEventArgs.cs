using System;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
namespace Fiddler
{
	public class ValidateServerCertificateEventArgs : EventArgs
	{
		private readonly X509Certificate _oServerCertificate;
		private readonly string _sExpectedCN;
		private readonly Session _oSession;
		private readonly X509Chain _ServerCertificateChain;
		private readonly SslPolicyErrors _sslPolicyErrors;
		public int TargetPort
		{
			get
			{
				return this._oSession.port;
			}
		}
		public string ExpectedCN
		{
			get
			{
				return this._sExpectedCN;
			}
		}
		public Session Session
		{
			get
			{
				return this._oSession;
			}
		}
		public X509Chain ServerCertificateChain
		{
			get
			{
				return this._ServerCertificateChain;
			}
		}
		public SslPolicyErrors CertificatePolicyErrors
		{
			get
			{
				return this._sslPolicyErrors;
			}
		}
		public CertificateValidity ValidityState
		{
			get;
			set;
		}
		public X509Certificate ServerCertificate
		{
			get
			{
				return this._oServerCertificate;
			}
		}
		internal ValidateServerCertificateEventArgs(Session inSession, string inExpectedCN, X509Certificate inServerCertificate, X509Chain inServerCertificateChain, SslPolicyErrors inSslPolicyErrors)
		{
			this._oSession = inSession;
			this._sExpectedCN = inExpectedCN;
			this._oServerCertificate = inServerCertificate;
			this._ServerCertificateChain = inServerCertificateChain;
			this._sslPolicyErrors = inSslPolicyErrors;
		}
	}
}
