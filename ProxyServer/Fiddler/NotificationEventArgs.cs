using System;
namespace Fiddler
{
	public class NotificationEventArgs : EventArgs
	{
		private readonly string _sMessage;
		public string NotifyString
		{
			get
			{
				return this._sMessage;
			}
		}
		internal NotificationEventArgs(string sMsg)
		{
			this._sMessage = sMsg;
		}
	}
}
