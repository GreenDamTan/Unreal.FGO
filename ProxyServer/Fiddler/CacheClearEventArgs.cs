using System;
using System.ComponentModel;
namespace Fiddler
{
	public class CacheClearEventArgs : CancelEventArgs
	{
		public bool ClearCacheFiles
		{
			get;
			set;
		}
		public bool ClearCookies
		{
			get;
			set;
		}
		public CacheClearEventArgs(bool bClearFiles, bool bClearCookies)
		{
			this.ClearCacheFiles = bClearFiles;
			this.ClearCookies = bClearCookies;
		}
	}
}
