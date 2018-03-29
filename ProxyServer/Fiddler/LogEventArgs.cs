using System;
namespace Fiddler
{
	public class LogEventArgs : EventArgs
	{
		private readonly string _sMessage;
		public string LogString
		{
			get
			{
				return this._sMessage;
			}
		}
		internal LogEventArgs(string sMsg)
		{
			this._sMessage = sMsg;
		}
	}
}
