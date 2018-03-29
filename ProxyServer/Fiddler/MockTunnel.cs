using System;
namespace Fiddler
{
	internal class MockTunnel : ITunnel
	{
		private long _lngBytesEgress;
		private long _lngBytesIngress;
		public long IngressByteCount
		{
			get
			{
				return this._lngBytesIngress;
			}
		}
		public long EgressByteCount
		{
			get
			{
				return this._lngBytesEgress;
			}
		}
		public bool IsOpen
		{
			get
			{
				return false;
			}
		}
		public MockTunnel(long lngEgress, long lngIngress)
		{
			this._lngBytesEgress = lngEgress;
			this._lngBytesIngress = lngIngress;
		}
		public void CloseTunnel()
		{
		}
	}
}
