using System;
using System.Diagnostics;
namespace Fiddler
{
	internal class DataPoints
	{
		internal long AwaitingRequest;
		internal int iThreadCount
		{
			get
			{
				return Process.GetCurrentProcess().Threads.Count;
			}
		}
	}
}
