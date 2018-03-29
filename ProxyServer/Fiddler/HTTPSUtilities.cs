using System;
using System.Collections.Generic;
using System.Text;
namespace Fiddler
{
	internal class HTTPSUtilities
	{
		internal static string GetExtensionString(byte[] arrData)
		{
			StringBuilder stringBuilder = new StringBuilder();
			int num;
			for (int i = 0; i < arrData.Length; i += 1 + num)
			{
				num = (int)arrData[i];
				stringBuilder.AppendFormat("{0}; ", Encoding.ASCII.GetString(arrData, i + 1, num));
			}
			return stringBuilder.ToString();
		}
		internal static string GetProtocolListAsString(byte[] arrData)
		{
			int num = ((int)arrData[0] << 8) + (int)arrData[1];
			byte[] array = new byte[num];
			Buffer.BlockCopy(arrData, 2, array, 0, array.Length);
			return HTTPSUtilities.GetExtensionString(array);
		}
		internal static string GetECCCurvesAsString(byte[] eccCurves)
		{
			List<string> list = new List<string>();
			for (int i = 0; i < eccCurves.Length - 1; i += 2)
			{
				ushort num = (ushort)((int)eccCurves[i] << 8 | (int)eccCurves[i + 1]);
				ushort num2 = num;
				switch (num2)
				{
				case 1:
					list.Add("sect163k1 [0x1]");
					break;
				case 2:
					list.Add("sect163r1 [0x2]");
					break;
				case 3:
					list.Add("sect163r2 [0x3]");
					break;
				case 4:
					list.Add("sect193r1 [0x4]");
					break;
				case 5:
					list.Add("sect193r2 [0x5]");
					break;
				case 6:
					list.Add("sect233k1 [0x6]");
					break;
				case 7:
					list.Add("sect233r1 [0x7]");
					break;
				case 8:
					list.Add("sect239k1 [0x8]");
					break;
				case 9:
					list.Add("sect283k1 [0x9]");
					break;
				case 10:
					list.Add("sect283r1 [0xA]");
					break;
				case 11:
					list.Add("sect409k1 [0xB]");
					break;
				case 12:
					list.Add("sect409r1 [0xC]");
					break;
				case 13:
					list.Add("sect571k1 [0xD]");
					break;
				case 14:
					list.Add("sect571r1 [0xE]");
					break;
				case 15:
					list.Add("secp160k1 [0xF]");
					break;
				case 16:
					list.Add("secp160r1 [0x10]");
					break;
				case 17:
					list.Add("secp160r2 [0x11]");
					break;
				case 18:
					list.Add("secp192k1 [0x12]");
					break;
				case 19:
					list.Add("secp192r1 [0x13]");
					break;
				case 20:
					list.Add("secp224k1 [0x14]");
					break;
				case 21:
					list.Add("secp224r1 [0x15]");
					break;
				case 22:
					list.Add("secp256k1 [0x16]");
					break;
				case 23:
					list.Add("secp256r1 [0x17]");
					break;
				case 24:
					list.Add("secp384r1 [0x18]");
					break;
				case 25:
					list.Add("secp521r1 [0x19]");
					break;
				default:
					switch (num2)
					{
					case 65281:
						list.Add("arbitrary_explicit_prime_curves [0xFF01]");
						break;
					case 65282:
						list.Add("arbitrary_explicit_char2_curves [0xFF02]");
						break;
					default:
						list.Add(string.Format("unknown [0x{0:X})", num));
						break;
					}
					break;
				}
			}
			return string.Join(", ", list.ToArray());
		}
		internal static string GetECCPointFormatsAsString(byte[] eccPoints)
		{
			List<string> list = new List<string>();
			for (int i = 0; i < eccPoints.Length; i++)
			{
				byte b = eccPoints[i];
				switch (b)
				{
				case 0:
					list.Add("uncompressed [0x0]");
					break;
				case 1:
					list.Add("ansiX962_compressed_prime [0x1]");
					break;
				case 2:
					list.Add("ansiX962_compressed_char2  [0x2]");
					break;
				default:
					list.Add(string.Format("unknown [0x{0:X}]", b));
					break;
				}
			}
			return string.Join(", ", list.ToArray());
		}
		internal static string HTTPSVersionToString(int iMajor, int iMinor)
		{
			string arg = "Unknown";
			if (iMajor == 3 && iMinor == 3)
			{
				arg = "TLS/1.2";
			}
			else
			{
				if (iMajor == 3 && iMinor == 2)
				{
					arg = "TLS/1.1";
				}
				else
				{
					if (iMajor == 3 && iMinor == 1)
					{
						arg = "TLS/1.0";
					}
					else
					{
						if (iMajor == 3 && iMinor == 0)
						{
							arg = "SSL/3.0";
						}
						else
						{
							if (iMajor == 2 && iMinor == 0)
							{
								arg = "SSL/2.0";
							}
						}
					}
				}
			}
			return string.Format("{0}.{1} ({2})", iMajor, iMinor, arg);
		}
	}
}
