using System;
namespace Fiddler
{
	public class StateChangeEventArgs : EventArgs
	{
		public readonly SessionStates oldState;
		public readonly SessionStates newState;
		internal StateChangeEventArgs(SessionStates ssOld, SessionStates ssNew)
		{
			this.oldState = ssOld;
			this.newState = ssNew;
		}
	}
}
