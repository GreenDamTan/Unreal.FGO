using System;
namespace Fiddler
{
	[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
	public sealed class ProfferFormatAttribute : Attribute
	{
		private string _sFormatName;
		private string _sFormatDesc;
		public string FormatName
		{
			get
			{
				return this._sFormatName;
			}
		}
		public string FormatDescription
		{
			get
			{
				return this._sFormatDesc;
			}
		}
		public ProfferFormatAttribute(string sFormatName, string sDescription)
		{
			this._sFormatName = sFormatName;
			this._sFormatDesc = sDescription;
		}
	}
}
