using System;
namespace Fiddler
{
	public interface ITunnel
	{
		long IngressByteCount
		{
			get;
		}
		long EgressByteCount
		{
			get;
		}
		bool IsOpen
		{
			get;
		}
		void CloseTunnel();
	}
}
