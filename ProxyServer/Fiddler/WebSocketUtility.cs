using System;
using System.Security.Cryptography;
using System.Text;
namespace Fiddler
{
	internal class WebSocketUtility
	{
		internal static string ComputeAcceptKey(string sSecWebSocketKeyFromClient)
		{
			SHA1 sHA = SHA1.Create();
			string result = Convert.ToBase64String(sHA.ComputeHash(Encoding.ASCII.GetBytes(sSecWebSocketKeyFromClient + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
			sHA.Clear();
			return result;
		}
	}
}
