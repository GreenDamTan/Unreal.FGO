using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
namespace Fiddler
{
	internal class HTTPSClientHello
	{
		private int _HandshakeVersion;
		private int _MessageLen;
		private int _MajorVersion;
		private int _MinorVersion;
		private byte[] _Random;
		private byte[] _SessionID;
		private uint[] _CipherSuites;
		private byte[] _CompressionSuites;
		private string _ServerNameIndicator;
		private List<string> _Extensions;
		internal static readonly string[] HTTPSCompressionSuites = new string[]
		{
			"NO_COMPRESSION",
			"DEFLATE"
		};
		internal static readonly string[] SSL3CipherSuites = new string[]
		{
			"SSL_NULL_WITH_NULL_NULL",
			"SSL_RSA_WITH_NULL_MD5",
			"SSL_RSA_WITH_NULL_SHA",
			"SSL_RSA_EXPORT_WITH_RC4_40_MD5",
			"SSL_RSA_WITH_RC4_128_MD5",
			"SSL_RSA_WITH_RC4_128_SHA",
			"SSL_RSA_EXPORT_WITH_RC2_40_MD5",
			"SSL_RSA_WITH_IDEA_SHA",
			"SSL_RSA_EXPORT_WITH_DES40_SHA",
			"SSL_RSA_WITH_DES_SHA",
			"SSL_RSA_WITH_3DES_EDE_SHA",
			"SSL_DH_DSS_EXPORT_WITH_DES40_SHA",
			"SSL_DH_DSS_WITH_DES_SHA",
			"SSL_DH_DSS_WITH_3DES_EDE_SHA",
			"SSL_DH_RSA_EXPORT_WITH_DES40_SHA",
			"SSL_DH_RSA_WITH_DES_SHA",
			"SSL_DH_RSA_WITH_3DES_EDE_SHA",
			"SSL_DHE_DSS_EXPORT_WITH_DES40_SHA",
			"SSL_DHE_DSS_WITH_DES_SHA",
			"SSL_DHE_DSS_WITH_3DES_EDE_SHA",
			"SSL_DHE_RSA_EXPORT_WITH_DES40_SHA",
			"SSL_DHE_RSA_WITH_DES_SHA",
			"SSL_DHE_RSA_WITH_3DES_EDE_SHA",
			"SSL_DH_anon_EXPORT_WITH_RC4_40_MD5",
			"SSL_DH_anon_WITH_RC4_128_MD5",
			"SSL_DH_anon_EXPORT_WITH_DES40_SHA",
			"SSL_DH_anon_WITH_DES_SHA",
			"SSL_DH_anon_WITH_3DES_EDE_SHA",
			"SSL_FORTEZZA_KEA_WITH_NULL_SHA",
			"SSL_FORTEZZA_KEA_WITH_FORTEZZA_SHA",
			"SSL_FORTEZZA_KEA_WITH_RC4_128_SHA"
		};
		internal static readonly Dictionary<uint, string> dictTLSCipherSuites = new Dictionary<uint, string>
		{

			{
				30u,
				"TLS_KRB5_WITH_DES_SHA"
			},

			{
				31u,
				"TLS_KRB5_WITH_3DES_EDE_SHA"
			},

			{
				32u,
				"TLS_KRB5_WITH_RC4_128_SHA"
			},

			{
				33u,
				"TLS_KRB5_WITH_IDEA_SHA"
			},

			{
				34u,
				"TLS_KRB5_WITH_DES_MD5"
			},

			{
				35u,
				"TLS_KRB5_WITH_3DES_EDE_MD5"
			},

			{
				36u,
				"TLS_KRB5_WITH_RC4_128_MD5"
			},

			{
				37u,
				"TLS_RSA_EXPORT1024_WITH_RC4_56_SHA"
			},

			{
				38u,
				"TLS_KRB5_EXPORT_WITH_DES_40_SHA"
			},

			{
				39u,
				"TLS_KRB5_EXPORT_WITH_RC2_40_SHA"
			},

			{
				40u,
				"TLS_KRB5_EXPORT_WITH_RC4_40_SHA"
			},

			{
				41u,
				"TLS_KRB5_EXPORT_WITH_DES_40_MD5"
			},

			{
				42u,
				"TLS_KRB5_EXPORT_WITH_RC2_40_MD5"
			},

			{
				43u,
				"TLS_KRB5_EXPORT_WITH_RC4_40_MD5"
			},

			{
				47u,
				"TLS_RSA_AES_128_SHA"
			},

			{
				48u,
				"TLS_DH_DSS_WITH_AES_128_SHA"
			},

			{
				49u,
				"TLS_DH_RSA_WITH_AES_128_SHA"
			},

			{
				50u,
				"TLS_DHE_DSS_WITH_AES_128_SHA"
			},

			{
				51u,
				"TLS_DHE_RSA_WITH_AES_128_SHA"
			},

			{
				52u,
				"TLS_DH_ANON_WITH_AES_128_SHA"
			},

			{
				53u,
				"TLS_RSA_AES_256_SHA"
			},

			{
				54u,
				"TLS_DH_DSS_WITH_AES_256_SHA"
			},

			{
				55u,
				"TLS_DH_RSA_WITH_AES_256_SHA"
			},

			{
				56u,
				"TLS_DHE_DSS_WITH_AES_256_SHA"
			},

			{
				57u,
				"TLS_DHE_RSA_WITH_AES_256_SHA"
			},

			{
				58u,
				"TLS_DH_ANON_WITH_AES_256_SHA"
			},

			{
				62u,
				"TLS_DH_DSS_WITH_AES_128_CBC_SHA256"
			},

			{
				63u,
				"TLS_DH_RSA_WITH_AES_128_CBC_SHA256"
			},

			{
				65u,
				"TLS_RSA_WITH_CAMELLIA_128_CBC_SHA"
			},

			{
				68u,
				"TLS_DHE_DSS_WITH_CAMELLIA_128_CBC_SHA"
			},

			{
				69u,
				"TLS_DHE_RSA_WITH_CAMELLIA_128_CBC_SHA"
			},

			{
				71u,
				"TLS_ECDH_ECDSA_WITH_NULL_SHA"
			},

			{
				72u,
				"TLS_ECDH_ECDSA_WITH_RC4_128_SHA"
			},

			{
				73u,
				"TLS_ECDH_ECDSA_WITH_DES_SHA"
			},

			{
				74u,
				"TLS_ECDH_ECDSA_WITH_3DES_EDE_SHA"
			},

			{
				75u,
				"TLS_ECDH_ECDSA_WITH_AES_128_SHA"
			},

			{
				76u,
				"TLS_ECDH_ECDSA_WITH_AES_256_SHA"
			},

			{
				77u,
				"TLS_ECDH_RSA_WITH_NULL_SHA"
			},

			{
				78u,
				"TLS_ECDH_RSA_WITH_RC4_128_SHA"
			},

			{
				79u,
				"TLS_ECDH_RSA_WITH_DES_SHA"
			},

			{
				80u,
				"TLS_ECDH_RSA_WITH_3DES_EDE_SHA"
			},

			{
				81u,
				"TLS_ECDH_RSA_WITH_AES_128_SHA"
			},

			{
				82u,
				"TLS_ECDH_RSA_WITH_AES_256_SHA"
			},

			{
				98u,
				"TLS_RSA_EXPORT1024_WITH_DES_SHA"
			},

			{
				99u,
				"TLS_DHE_DSS_EXPORT1024_WITH_DES_SHA"
			},

			{
				100u,
				"TLS_RSA_EXPORT1024_WITH_RC4_56_SHA"
			},

			{
				101u,
				"TLS_DHE_DSS_EXPORT1024_WITH_RC4_56_SHA"
			},

			{
				102u,
				"TLS_DHE_DSS_WITH_RC4_128_SHA"
			},

			{
				103u,
				"TLS_DHE_RSA_WITH_AES_128_CBC_SHA256"
			},

			{
				104u,
				"TLS_DH_DSS_WITH_AES_256_CBC_SHA256"
			},

			{
				105u,
				"TLS_DH_RSA_WITH_AES_256_CBC_SHA256"
			},

			{
				107u,
				"TLS_DHE_RSA_WITH_AES_256_CBC_SHA256"
			},

			{
				119u,
				"TLS_ECDHE_ECDSA_WITH_AES_128_SHA"
			},

			{
				120u,
				"TLS_ECDHE_RSA_WITH_AES_128_SHA"
			},

			{
				132u,
				"TLS_RSA_WITH_CAMELLIA_256_CBC_SHA"
			},

			{
				135u,
				"TLS_DHE_DSS_WITH_CAMELLIA_256_CBC_SHA"
			},

			{
				136u,
				"TLS_DHE_RSA_WITH_CAMELLIA_256_CBC_SHA"
			},

			{
				150u,
				"TLS_RSA_WITH_SEED_CBC_SHA"
			},

			{
				255u,
				"TLS_EMPTY_RENEGOTIATION_INFO_SCSV"
			},

			{
				49154u,
				"TLS_ECDH_ECDSA_WITH_RC4_128_SHA"
			},

			{
				49155u,
				"TLS_ECDH_ECDSA_WITH_3DES_EDE_CBC_SHA"
			},

			{
				49156u,
				"TLS_ECDH_ECDSA_WITH_AES_128_CBC_SHA"
			},

			{
				49157u,
				"TLS_ECDH_ECDSA_WITH_AES_256_CBC_SHA"
			},

			{
				49159u,
				"TLS_ECDHE_ECDSA_WITH_RC4_128_SHA"
			},

			{
				49160u,
				"TLS_ECDHE_ECDSA_WITH_3DES_EDE_CBC_SHA"
			},

			{
				49164u,
				"TLS_ECDH_RSA_WITH_RC4_128_SHA"
			},

			{
				49165u,
				"TLS_ECDH_RSA_WITH_3DES_EDE_CBC_SHA"
			},

			{
				49166u,
				"TLS_ECDH_RSA_WITH_AES_128_CBC_SHA"
			},

			{
				49167u,
				"TLS_ECDH_RSA_WITH_AES_256_CBC_SHA"
			},

			{
				49169u,
				"TLS_ECDHE_RSA_WITH_RC4_128_SHA"
			},

			{
				49170u,
				"TLS_ECDHE_RSA_WITH_3DES_EDE_CBC_SHA"
			},

			{
				49161u,
				"TLS1_CK_ECDHE_ECDSA_WITH_AES_128_CBC_SHA"
			},

			{
				49162u,
				"TLS1_CK_ECDHE_ECDSA_WITH_AES_256_CBC_SHA"
			},

			{
				49171u,
				"TLS1_CK_ECDHE_RSA_WITH_AES_128_CBC_SHA"
			},

			{
				49172u,
				"TLS1_CK_ECDHE_RSA_WITH_AES_256_CBC_SHA"
			},

			{
				49179u,
				"TLS_SRP_SHA_RSA_WITH_3DES_EDE_CBC_SHA"
			},

			{
				49180u,
				"TLS_SRP_SHA_DSS_WITH_3DES_EDE_CBC_SHA"
			},

			{
				49182u,
				"TLS_SRP_SHA_RSA_WITH_AES_128_CBC_SHA"
			},

			{
				49183u,
				"TLS_SRP_SHA_DSS_WITH_AES_128_CBC_SHA"
			},

			{
				49185u,
				"TLS_SRP_SHA_RSA_WITH_AES_256_CBC_SHA"
			},

			{
				49186u,
				"TLS_SRP_SHA_DSS_WITH_AES_256_CBC_SHA"
			},

			{
				60u,
				"TLS_RSA_WITH_AES_128_CBC_SHA256"
			},

			{
				61u,
				"TLS_RSA_WITH_AES_256_CBC_SHA256"
			},

			{
				64u,
				"TLS_DHE_DSS_WITH_AES_128_CBC_SHA256"
			},

			{
				106u,
				"TLS_DHE_DSS_WITH_AES_256_CBC_SHA256"
			},

			{
				49187u,
				"TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA256"
			},

			{
				49188u,
				"TLS_ECDHE_ECDSA_WITH_AES_256_CBC_SHA384"
			},

			{
				49191u,
				"TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA256"
			},

			{
				49192u,
				"TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA384"
			},

			{
				49195u,
				"TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256"
			},

			{
				49196u,
				"TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384"
			},

			{
				59u,
				"TLS_RSA_WITH_NULL_SHA256"
			},

			{
				49153u,
				"TLS_ECDH_ECDSA_WITH_NULL_SHA"
			},

			{
				49158u,
				"TLS_ECDHE_ECDSA_WITH_NULL_SHA"
			},

			{
				49163u,
				"TLS_ECDH_RSA_WITH_NULL_SHA"
			},

			{
				49168u,
				"TLS_ECDHE_RSA_WITH_NULL_SHA"
			},

			{
				49189u,
				"TLS_ECDH_ECDSA_WITH_AES_128_CBC_SHA256"
			},

			{
				49190u,
				"TLS_ECDH_ECDSA_WITH_AES_256_CBC_SHA384"
			},

			{
				49193u,
				"TLS_ECDH_RSA_WITH_AES_128_CBC_SHA256"
			},

			{
				49194u,
				"TLS_ECDH_RSA_WITH_AES_256_CBC_SHA384"
			},

			{
				65281u,
				"SSL_EN_RC4_128_WITH_MD5"
			},

			{
				65282u,
				"SSL_EN_RC4_128_EXPORT40_WITH_MD5"
			},

			{
				65283u,
				"SSL_EN_RC2_128_WITH_MD5"
			},

			{
				65284u,
				"SSL_EN_RC2_128_EXPORT40_WITH_MD5"
			},

			{
				65285u,
				"SSL_EN_IDEA_128_WITH_MD5"
			},

			{
				65286u,
				"SSL_EN_DES_64_WITH_MD5"
			},

			{
				65287u,
				"SSL_EN_DES_192_EDE3_WITH_MD5"
			},

			{
				65279u,
				"SSL_RSA_FIPS_WITH_3DES_EDE_SHA"
			},

			{
				65278u,
				"SSL_RSA_FIPS_WITH_DES_SHA"
			},

			{
				65504u,
				"SSL_RSA_NSFIPS_WITH_3DES_EDE_SHA"
			},

			{
				65505u,
				"SSL_RSA_NSFIPS_WITH_DES_SHA"
			},

			{
				65664u,
				"SSL2_RC4_128_WITH_MD5"
			},

			{
				131200u,
				"SSL2_RC4_128_EXPORT40_WITH_MD5"
			},

			{
				196736u,
				"SSL2_RC2_128_WITH_MD5"
			},

			{
				262272u,
				"SSL2_RC2_128_EXPORT40_WITH_MD5"
			},

			{
				327808u,
				"SSL2_IDEA_128_WITH_MD5"
			},

			{
				393280u,
				"SSL2_DES_64_WITH_MD5"
			},

			{
				458944u,
				"SSL2_DES_192_EDE3_WITH_MD5"
			}
		};
		public string ServerNameIndicator
		{
			get
			{
				if (!string.IsNullOrEmpty(this._ServerNameIndicator))
				{
					return this._ServerNameIndicator;
				}
				return string.Empty;
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
		private static string CipherSuitesToString(uint[] inArr)
		{
			if (inArr == null)
			{
				return "null";
			}
			if (inArr.Length == 0)
			{
				return "empty";
			}
			StringBuilder stringBuilder = new StringBuilder(inArr.Length * 20);
			for (int i = 0; i < inArr.Length; i++)
			{
				stringBuilder.Append("\t[" + inArr[i].ToString("X4") + "]\t");
				if ((ulong)inArr[i] < (ulong)((long)HTTPSClientHello.SSL3CipherSuites.Length))
				{
					stringBuilder.AppendLine(HTTPSClientHello.SSL3CipherSuites[(int)((UIntPtr)inArr[i])]);
				}
				else
				{
					if (HTTPSClientHello.dictTLSCipherSuites.ContainsKey(inArr[i]))
					{
						stringBuilder.AppendLine(HTTPSClientHello.dictTLSCipherSuites[inArr[i]]);
					}
					else
					{
						stringBuilder.AppendLine("Unrecognized cipher - See http://www.iana.org/assignments/tls-parameters/");
					}
				}
			}
			return stringBuilder.ToString();
		}
		private static string CompressionSuitesToString(byte[] inArr)
		{
			if (inArr == null)
			{
				return "(not specified)";
			}
			if (inArr.Length == 0)
			{
				return "(none)";
			}
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < inArr.Length; i++)
			{
				stringBuilder.Append("\t[" + inArr[i].ToString("X2") + "]\t");
				if ((int)inArr[i] < HTTPSClientHello.HTTPSCompressionSuites.Length)
				{
					stringBuilder.AppendLine(HTTPSClientHello.HTTPSCompressionSuites[(int)inArr[i]]);
				}
				else
				{
					stringBuilder.AppendLine("Unrecognized compression format");
				}
			}
			return stringBuilder.ToString();
		}
		private static string ExtensionListToString(List<string> slExts)
		{
			if (slExts != null && slExts.Count >= 1)
			{
				return string.Join("\n", slExts.ToArray());
			}
			return "\tnone";
		}
		public override string ToString()
		{
			StringBuilder stringBuilder = new StringBuilder(512);
			if (this._HandshakeVersion == 2)
			{
				stringBuilder.Append("A SSLv2-compatible ClientHello handshake was found. Fiddler extracted the parameters below.\n\n");
			}
			else
			{
				stringBuilder.Append("A SSLv3-compatible ClientHello handshake was found. Fiddler extracted the parameters below.\n\n");
			}
			stringBuilder.AppendFormat("Version: {0}\n", HTTPSUtilities.HTTPSVersionToString(this._MajorVersion, this._MinorVersion));
			stringBuilder.AppendFormat("Random: {0}\n", Utilities.ByteArrayToString(this._Random));
			stringBuilder.AppendFormat("SessionID: {0}\n", Utilities.ByteArrayToString(this._SessionID));
			stringBuilder.AppendFormat("Extensions: \n{0}\n", HTTPSClientHello.ExtensionListToString(this._Extensions));
			stringBuilder.AppendFormat("Ciphers: \n{0}\n", HTTPSClientHello.CipherSuitesToString(this._CipherSuites));
			stringBuilder.AppendFormat("Compression: \n{0}\n", HTTPSClientHello.CompressionSuitesToString(this._CompressionSuites));
			return stringBuilder.ToString();
		}
		internal bool LoadFromStream(Stream oNS)
		{
			int num = oNS.ReadByte();
			if (num == 128)
			{
				this._HandshakeVersion = 2;
				oNS.ReadByte();
				oNS.ReadByte();
				this._MajorVersion = oNS.ReadByte();
				this._MinorVersion = oNS.ReadByte();
				if (this._MajorVersion == 0 && this._MinorVersion == 2)
				{
					this._MajorVersion = 2;
					this._MinorVersion = 0;
				}
				int num2 = oNS.ReadByte() << 8;
				num2 += oNS.ReadByte();
				int num3 = oNS.ReadByte() << 8;
				num3 += oNS.ReadByte();
				int num4 = oNS.ReadByte() << 8;
				num4 += oNS.ReadByte();
				this._CipherSuites = new uint[num2 / 3];
				for (int i = 0; i < this._CipherSuites.Length; i++)
				{
					this._CipherSuites[i] = (uint)((oNS.ReadByte() << 16) + (oNS.ReadByte() << 8) + oNS.ReadByte());
				}
				this._SessionID = new byte[num3];
				int num5 = oNS.Read(this._SessionID, 0, this._SessionID.Length);
				this._Random = new byte[num4];
				num5 = oNS.Read(this._Random, 0, this._Random.Length);
			}
			else
			{
				if (num == 22)
				{
					this._HandshakeVersion = 3;
					this._MajorVersion = oNS.ReadByte();
					this._MinorVersion = oNS.ReadByte();
					int num6 = oNS.ReadByte() << 8;
					num6 += oNS.ReadByte();
					int num7 = oNS.ReadByte();
					if (num7 != 1)
					{
						return false;
					}
					byte[] array = new byte[3];
					int num5 = oNS.Read(array, 0, array.Length);
					if (num5 < 3)
					{
						return false;
					}
					this._MessageLen = ((int)array[0] << 16) + ((int)array[1] << 8) + (int)array[2];
					this._MajorVersion = oNS.ReadByte();
					this._MinorVersion = oNS.ReadByte();
					this._Random = new byte[32];
					num5 = oNS.Read(this._Random, 0, 32);
					if (num5 < 32)
					{
						return false;
					}
					int num8 = oNS.ReadByte();
					this._SessionID = new byte[num8];
					num5 = oNS.Read(this._SessionID, 0, this._SessionID.Length);
					array = new byte[2];
					num5 = oNS.Read(array, 0, array.Length);
					if (num5 < 2)
					{
						return false;
					}
					int num9 = ((int)array[0] << 8) + (int)array[1];
					this._CipherSuites = new uint[num9 / 2];
					array = new byte[num9];
					num5 = oNS.Read(array, 0, array.Length);
					if (num5 != array.Length)
					{
						return false;
					}
					for (int j = 0; j < this._CipherSuites.Length; j++)
					{
						this._CipherSuites[j] = (uint)(((int)array[2 * j] << 8) + (int)array[2 * j + 1]);
					}
					int num10 = oNS.ReadByte();
					if (num10 < 1)
					{
						return false;
					}
					this._CompressionSuites = new byte[num10];
					for (int k = 0; k < this._CompressionSuites.Length; k++)
					{
						int num11 = oNS.ReadByte();
						if (num11 < 0)
						{
							return false;
						}
						this._CompressionSuites[k] = (byte)num11;
					}
					if (this._MajorVersion < 3 || (this._MajorVersion == 3 && this._MinorVersion < 1))
					{
						return true;
					}
					array = new byte[2];
					num5 = oNS.Read(array, 0, array.Length);
					if (num5 < 2)
					{
						return true;
					}
					int num12 = ((int)array[0] << 8) + (int)array[1];
					if (num12 < 1)
					{
						return true;
					}
					array = new byte[num12];
					num5 = oNS.Read(array, 0, array.Length);
					if (num5 == array.Length)
					{
						this.ParseClientHelloExtensions(array);
					}
				}
			}
			return true;
		}
		private void ParseClientHelloExtension(int iExtType, byte[] arrData)
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
				{
					StringBuilder stringBuilder = new StringBuilder();
					int num2;
					for (int i = 2; i < arrData.Length; i += 3 + num2)
					{
						int num = (int)arrData[i];
						num2 = ((int)arrData[i + 1] << 8) + (int)arrData[i + 2];
						string @string = Encoding.ASCII.GetString(arrData, i + 3, num2);
						if (num == 0)
						{
							this._ServerNameIndicator = @string;
							stringBuilder.AppendFormat("{0}{1}", (stringBuilder.Length > 1) ? "; " : string.Empty, @string);
						}
					}
					this._Extensions.Add(string.Format("\tserver_name\t{0}", stringBuilder.ToString()));
					return;
				}
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
				if (iExtType == 13172)
				{
					this._Extensions.Add(string.Format("\tNextProtocolNegotiation\t{0}", Utilities.ByteArrayToString(arrData)));
					return;
				}
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
			this._Extensions.Add(string.Format("\t0x{0:x4}\t\t{1}", iExtType, Utilities.ByteArrayToString(arrData)));
		}
		private void ParseClientHelloExtensions(byte[] arrExtensionsData)
		{
			int num;
			for (int i = 0; i < arrExtensionsData.Length; i += 4 + num)
			{
				int iExtType = ((int)arrExtensionsData[i] << 8) + (int)arrExtensionsData[i + 1];
				num = ((int)arrExtensionsData[i + 2] << 8) + (int)arrExtensionsData[i + 3];
				byte[] array = new byte[num];
				Buffer.BlockCopy(arrExtensionsData, i + 4, array, 0, array.Length);
				this.ParseClientHelloExtension(iExtType, array);
			}
		}
	}
}
