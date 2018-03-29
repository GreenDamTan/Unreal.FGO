using System;
namespace Fiddler
{
	public class ProgressCallbackEventArgs : EventArgs
	{
		private readonly string _sProgressText;
		private readonly int _PercentDone;
		public bool Cancel
		{
			get;
			set;
		}
		public string ProgressText
		{
			get
			{
				return this._sProgressText;
			}
		}
		public int PercentComplete
		{
			get
			{
				return this._PercentDone;
			}
		}
		public ProgressCallbackEventArgs(float flCompletionRatio, string sProgressText)
		{
			this._sProgressText = (sProgressText ?? string.Empty);
			this._PercentDone = (int)Math.Truncate((double)(100f * Math.Max(0f, Math.Min(1f, flCompletionRatio))));
		}
	}
}
