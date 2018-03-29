using System;
using System.IO;
namespace Fiddler
{
	public interface ISAZReader
	{
		string Comment
		{
			get;
		}
		string Filename
		{
			get;
		}
		string EncryptionStrength
		{
			get;
		}
		string EncryptionMethod
		{
			get;
		}
		string[] GetRequestFileList();
		Stream GetFileStream(string sFilename);
		byte[] GetFileBytes(string sFilename);
		void Close();
	}
}
