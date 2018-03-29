using System;
namespace Fiddler
{
	public interface ISAZWriter
	{
		string Comment
		{
			set;
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
		void AddFile(string sFilename, SAZWriterDelegate oSWD);
		bool SetPassword(string sPassword);
		bool CompleteArchive();
	}
}
