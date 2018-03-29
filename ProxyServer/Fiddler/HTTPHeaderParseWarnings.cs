using System;
namespace Fiddler
{
	[Flags]
	public enum HTTPHeaderParseWarnings
	{
		None = 0,
		EndedWithLFLF = 1,
		EndedWithLFCRLF = 2,
		Malformed = 4
	}
}
