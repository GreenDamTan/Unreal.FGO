using System;
namespace Fiddler
{
	public class RawReadEventArgs : EventArgs
	{
		private readonly byte[] _arrData;
		private readonly int _iCountBytes;
		private readonly Session _oS;
		public bool AbortReading
		{
			get;
			set;
		}
		public Session sessionOwner
		{
			get
			{
				return this._oS;
			}
		}
		public byte[] arrDataBuffer
		{
			get
			{
				return this._arrData;
			}
		}
		public int iCountOfBytes
		{
			get
			{
				return this._iCountBytes;
			}
		}
		internal RawReadEventArgs(Session oS, byte[] arrData, int iCountBytes)
		{
			this._arrData = arrData;
			this._iCountBytes = iCountBytes;
			this._oS = oS;
		}
	}
}
