using System;
namespace Fiddler
{
	public class PrefChangeEventArgs : EventArgs
	{
		private readonly string _prefName;
		private readonly string _prefValueString;
		public string PrefName
		{
			get
			{
				return this._prefName;
			}
		}
		public string ValueString
		{
			get
			{
				return this._prefValueString;
			}
		}
		public bool ValueBool
		{
			get
			{
				return "True".OICEquals(this._prefValueString);
			}
		}
		internal PrefChangeEventArgs(string prefName, string prefValueString)
		{
			this._prefName = prefName;
			this._prefValueString = prefValueString;
		}
	}
}
