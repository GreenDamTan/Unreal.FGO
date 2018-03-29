using System;
namespace Fiddler
{
	public class WebSocketMessageEventArgs : EventArgs
	{
		public WebSocketMessage oWSM
		{
			get;
			private set;
		}
		public WebSocketMessageEventArgs(WebSocketMessage _inMsg)
		{
			this.oWSM = _inMsg;
		}
	}
}
