using System;
using System.IO;
namespace Fiddler
{
	internal class PipeReadBuffer : MemoryStream
	{
		private const int ONE_GIG = 1073741823;
		private const int SIXTYFOUR_MEG = 67108864;
		private const int LARGE_OBJECT_HEAP_SIZE = 81920;
		public PipeReadBuffer(bool bIsRequest) : base(bIsRequest ? 8192 : 16384)
		{
		}
		public PipeReadBuffer(int iDefaultCapacity) : base(iDefaultCapacity)
		{
		}
		public override void Write(byte[] buffer, int offset, int count)
		{
			int capacity = base.Capacity;
			uint num = (uint)(base.Position + (long)count);
			if ((ulong)num > (ulong)((long)capacity))
			{
				if (num < 81920u)
				{
					if (capacity * 2 > 81920)
					{
						this.Capacity = 81920;
					}
				}
				else
				{
					if (num > 1073741823u)
					{
						uint num2 = num + 67108864u;
						if (num2 >= 2147483647u)
						{
							throw new Exception("Sorry, the .NET Framework (and Fiddler) cannot handle streams larger than 2 Gigabytes.");
						}
						this.Capacity = (int)num2;
					}
				}
			}
			base.Write(buffer, offset, count);
		}
		internal void HintTotalSize(int iHint)
		{
		}
	}
}
