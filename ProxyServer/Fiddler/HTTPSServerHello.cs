using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
namespace Fiddler
{
	internal class HTTPSServerHello
	{
		private int _HandshakeVersion;
		private int _MessageLen;
		private int _MajorVersion;
		private int _MinorVersion;
		private byte[] _Random;
		private byte[] _SessionID;
		private uint _iCipherSuite;
		private int _iCompression;
		private List<string> _Extensions;
		public bool bALPNToSPDY
		{
			get;
			set;
		}
		private string CompressionSuite
		{
			get
			{
				if (this._iCompression < HTTPSClientHello.HTTPSCompressionSuites.Length)
				{
					return HTTPSClientHello.HTTPSCompressionSuites[this._iCompression];
				}
				return "Unrecognized compression format";
			}
		}
		internal string CipherSuite
		{
			get
			{
				if ((ulong)this._iCipherSuite < (ulong)((long)HTTPSClientHello.SSL3CipherSuites.Length))
				{
					return HTTPSClientHello.SSL3CipherSuites[(int)((UIntPtr)this._iCipherSuite)];
				}
				if (HTTPSClientHello.dictTLSCipherSuites.ContainsKey(this._iCipherSuite))
				{
					return HTTPSClientHello.dictTLSCipherSuites[this._iCipherSuite];
				}
				return "Unrecognized cipher - See http://www.iana.org/assignments/tls-parameters/";
			}
		}
		public string SessionID
		{
			get
			{
				if (this._SessionID == null)
				{
					return string.Empty;
				}
				return Utilities.ByteArrayToString(this._SessionID);
			}
		}
		public bool bNPNToSPDY
		{
			get;
			set;
		}
		public override string ToString()
		{
			StringBuilder stringBuilder = new StringBuilder(512);
			if (this._HandshakeVersion == 2)
			{
				stringBuilder.Append("A SSLv2-compatible ServerHello handshake was found. In v2, the ~client~ selects the active cipher after the ServerHello, when sending the Client-Master-Key message. Fiddler only parses the handshake.\n\n");
			}
			else
			{
				stringBuilder.Append("A SSLv3-compatible ServerHello handshake was found. Fiddler extracted the parameters below.\n\n");
			}
			stringBuilder.AppendFormat("Version: {0}\n", HTTPSUtilities.HTTPSVersionToString(this._MajorVersion, this._MinorVersion));
			stringBuilder.AppendFormat("SessionID:\t{0}\n", Utilities.ByteArrayToString(this._SessionID));
			if (this._HandshakeVersion == 3)
			{
				stringBuilder.AppendFormat("Random:\t\t{0}\n", Utilities.ByteArrayToString(this._Random));
				stringBuilder.AppendFormat("Cipher:\t\t{0} [0x{1:X4}]\n", this.CipherSuite, this._iCipherSuite);
			}
			stringBuilder.AppendFormat("CompressionSuite:\t{0} [0x{1:X2}]\n", this.CompressionSuite, this._iCompression);
			stringBuilder.AppendFormat("Extensions:\n\t{0}\n", HTTPSServerHello.ExtensionListToString(this._Extensions));
			return stringBuilder.ToString();
		}
		private static string ExtensionListToString(List<string> slExts)
		{
			if (slExts != null && slExts.Count >= 1)
			{
				return string.Join("\n\t", slExts.ToArray());
			}
			return "\tnone";
		}
		private void ParseServerHelloExtension(int iExtType, byte[] arrData)
		{
			if (this._Extensions == null)
			{
				this._Extensions = new List<string>();
			}
			if (iExtType <= 35)
			{
				switch (iExtType)
				{
				case 0:
					this._Extensions.Add(string.Format("\tserver_name\t{0}", Utilities.ByteArrayToString(arrData)));
					return;
				case 1:
					this._Extensions.Add(string.Format("\tmax_fragment_length\t{0}", Utilities.ByteArrayToString(arrData)));
					return;
				case 2:
					this._Extensions.Add(string.Format("\tclient_certificate_url\t{0}", Utilities.ByteArrayToString(arrData)));
					return;
				case 3:
					this._Extensions.Add(string.Format("\ttrusted_ca_keys\t{0}", Utilities.ByteArrayToString(arrData)));
					return;
				case 4:
					this._Extensions.Add(string.Format("\ttruncated_hmac\t{0}", Utilities.ByteArrayToString(arrData)));
					return;
				case 5:
					this._Extensions.Add(string.Format("\tstatus_request\t{0}", Utilities.ByteArrayToString(arrData)));
					return;
				case 6:
					this._Extensions.Add(string.Format("\tuser_mapping\t{0}", Utilities.ByteArrayToString(arrData)));
					return;
				case 7:
				case 8:
				case 15:
					break;
				case 9:
					this._Extensions.Add(string.Format("\tcert_type\t{0}", Utilities.ByteArrayToString(arrData)));
					return;
				case 10:
					this._Extensions.Add(string.Format("\telliptic_curves\t{0}", HTTPSUtilities.GetECCCurvesAsString(arrData)));
					return;
				case 11:
					this._Extensions.Add(string.Format("\tec_point_formats\t{0}", HTTPSUtilities.GetECCPointFormatsAsString(arrData)));
					return;
				case 12:
					this._Extensions.Add(string.Format("\tsrp_rfc_5054\t{0}", Utilities.ByteArrayToString(arrData)));
					return;
				case 13:
					this._Extensions.Add(string.Format("\tsignature_algorithms\t{0}", Utilities.ByteArrayToString(arrData)));
					return;
				case 14:
					this._Extensions.Add(string.Format("\tuse_srtp\t{0}", Utilities.ByteArrayToString(arrData)));
					return;
				case 16:
				{
					string protocolListAsString = HTTPSUtilities.GetProtocolListAsString(arrData);
					this._Extensions.Add(string.Format("\tALPN\t\t{0}", protocolListAsString));
					if (protocolListAsString.Contains("spdy/"))
					{
						this.bALPNToSPDY = true;
						return;
					}
					return;
				}
				default:
					if (iExtType == 35)
					{
						this._Extensions.Add(string.Format("\tSessionTicket TLS\t{0}", Utilities.ByteArrayToString(arrData)));
						return;
					}
					break;
				}
			}
			else
			{
				if (iExtType != 13172)
				{
					if (iExtType == 30031)
					{
						this._Extensions.Add(string.Format("\tchannel_id(GoogleDraft)\t{0}", Utilities.ByteArrayToString(arrData)));
						return;
					}
					if (iExtType == 65281)
					{
						this._Extensions.Add(string.Format("\trenegotiation_info\t{0}", Utilities.ByteArrayToString(arrData)));
						return;
					}
				}
				else
				{
					string extensionString = HTTPSUtilities.GetExtensionString(arrData);
					this._Extensions.Add(string.Format("\tNextProtocolNegotiation\t{0}", extensionString));
					if (extensionString.Contains("spdy/"))
					{
						this.bNPNToSPDY = true;
						return;
					}
					return;
				}
			}
			this._Extensions.Add(string.Format("\t0x{0:x4}\t\t{1}", iExtType, Utilities.ByteArrayToString(arrData)));
		}
		private void ParseServerHelloExtensions(byte[] arrExtensionsData)
		{
			int i = 0;
			try
			{
				while (i < arrExtensionsData.Length)
				{
					int iExtType = ((int)arrExtensionsData[i] << 8) + (int)arrExtensionsData[i + 1];
					int num = ((int)arrExtensionsData[i + 2] << 8) + (int)arrExtensionsData[i + 3];
					byte[] array = new byte[num];
					Buffer.BlockCopy(arrExtensionsData, i + 4, array, 0, array.Length);
					try
					{
						this.ParseServerHelloExtension(iExtType, array);
					}
					catch (Exception eX)
					{
						FiddlerApplication.Log.LogFormat("Error parsing server TLS extension. {0}", new object[]
						{
							Utilities.DescribeException(eX)
						});
					}
					i += 4 + num;
				}
			}
			catch (Exception eX2)
			{
				FiddlerApplication.Log.LogFormat("Error parsing server TLS extensions. {0}", new object[]
				{
					Utilities.DescribeException(eX2)
				});
			}
		}
		internal bool LoadFromStream(Stream oNS)
		{
			int num = oNS.ReadByte();
			if (num == 22)
			{
				this._HandshakeVersion = 3;
				this._MajorVersion = oNS.ReadByte();
				this._MinorVersion = oNS.ReadByte();
				int num2 = oNS.ReadByte() << 8;
				num2 += oNS.ReadByte();
				oNS.ReadByte();
				byte[] array = new byte[3];
				int num3 = oNS.Read(array, 0, array.Length);
				this._MessageLen = ((int)array[0] << 16) + ((int)array[1] << 8) + (int)array[2];
				this._MajorVersion = oNS.ReadByte();
				this._MinorVersion = oNS.ReadByte();
				this._Random = new byte[32];
				num3 = oNS.Read(this._Random, 0, 32);
				int num4 = oNS.ReadByte();
				this._SessionID = new byte[num4];
				num3 = oNS.Read(this._SessionID, 0, this._SessionID.Length);
				this._iCipherSuite = (uint)((oNS.ReadByte() << 8) + oNS.ReadByte());
				this._iCompression = oNS.ReadByte();
				if (this._MajorVersion < 3 || (this._MajorVersion == 3 && this._MinorVersion < 1))
				{
					return true;
				}
				array = new byte[2];
				num3 = oNS.Read(array, 0, array.Length);
				if (num3 < 2)
				{
					return true;
				}
				int num5 = ((int)array[0] << 8) + (int)array[1];
				if (num5 < 1)
				{
					return true;
				}
				array = new byte[num5];
				num3 = oNS.Read(array, 0, array.Length);
				if (num3 == array.Length)
				{
					this.ParseServerHelloExtensions(array);
				}
				return true;
			}
			else
			{
				if (num == 21)
				{
					byte[] array2 = new byte[7];
					oNS.Read(array2, 0, 7);
					FiddlerApplication.Log.LogFormat("Got an alert from the server!\n{0}", new object[]
					{
						Utilities.ByteArrayToHexView(array2, 8)
					});
					return false;
				}
				this._HandshakeVersion = 2;
				oNS.ReadByte();
				if (128 != (num & 128))
				{
					oNS.ReadByte();
				}
				num = oNS.ReadByte();
				if (num != 4)
				{
					return false;
				}
				this._SessionID = new byte[1];
				oNS.Read(this._SessionID, 0, 1);
				oNS.ReadByte();
				this._MinorVersion = oNS.ReadByte();
				this._MajorVersion = oNS.ReadByte();
				return true;
			}
		}
	}
}
