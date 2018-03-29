using System;
using System.Text;
namespace Fiddler
{
	public class WebSocketTimers
	{
		public DateTime dtDoneRead;
		public DateTime dtBeginSend;
		public DateTime dtDoneSend;
		internal string ToHeaderString()
		{
			StringBuilder stringBuilder = new StringBuilder();
			if (this.dtDoneRead.Ticks > 0L)
			{
				stringBuilder.AppendFormat("DoneRead: {0}\r\n", this.dtDoneRead.ToString("o"));
			}
			if (this.dtBeginSend.Ticks > 0L)
			{
				stringBuilder.AppendFormat("BeginSend: {0}\r\n", this.dtBeginSend.ToString("o"));
			}
			if (this.dtDoneSend.Ticks > 0L)
			{
				stringBuilder.AppendFormat("DoneSend: {0}\r\n", this.dtDoneSend.ToString("o"));
			}
			return stringBuilder.ToString();
		}
		public override string ToString()
		{
			return this.ToString(false);
		}
		public string ToString(bool bMultiLine)
		{
			if (bMultiLine)
			{
				return string.Format("DoneRead:\t{0:HH:mm:ss.fff}\r\nBeginSend:\t{1:HH:mm:ss.fff}\r\nDoneSend:\t{2:HH:mm:ss.fff}\r\n{3}", new object[]
				{
					this.dtDoneRead,
					this.dtBeginSend,
					this.dtDoneSend,
					(TimeSpan.Zero < this.dtDoneSend - this.dtDoneRead) ? string.Format("\r\n\tOverall Elapsed:\t{0:h\\:mm\\:ss\\.fff}\r\n", this.dtDoneSend - this.dtDoneRead) : string.Empty
				});
			}
			return string.Format("DoneRead: {0:HH:mm:ss.fff}, BeginSend: {1:HH:mm:ss.fff}, DoneSend: {2:HH:mm:ss.fff}{3}", new object[]
			{
				this.dtDoneRead,
				this.dtBeginSend,
				this.dtDoneSend,
				(TimeSpan.Zero < this.dtDoneSend - this.dtDoneRead) ? string.Format(",Overall Elapsed: {0:h\\:mm\\:ss\\.fff}", this.dtDoneSend - this.dtDoneRead) : string.Empty
			});
		}
	}
}
