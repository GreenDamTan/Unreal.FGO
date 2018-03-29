using System;
namespace Fiddler
{
	public class TranscoderTuple
	{
		public string sFormatDescription;
		public Type typeFormatter;
		internal TranscoderTuple(string sDescription, Type oFormatter)
		{
			this.sFormatDescription = sDescription;
			this.typeFormatter = oFormatter;
		}
	}
}
