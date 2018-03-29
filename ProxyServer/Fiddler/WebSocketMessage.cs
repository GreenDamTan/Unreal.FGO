using System;
using System.IO;
using System.Text;
namespace Fiddler
{
	public class WebSocketMessage
	{
		private WebSocket _wsOwner;
		private bool _bIsFinalFragment;
		private byte _byteReservedFlags;
		private bool _bOutbound;
		private int _iID;
		private bool _bAborted;
		private bool _bBreakpoint;
		public WebSocketTimers Timers = new WebSocketTimers();
		private byte[] _arrRawPayload;
		private byte[] _arrMask;
		private WebSocketFrameTypes _wsftType;
		public bool IsFinalFrame
		{
			get
			{
				return this._bIsFinalFragment;
			}
		}
		[CodeDescription("Indicates whether this WebSocketMessage was aborted.")]
		public bool WasAborted
		{
			get
			{
				return this._bAborted;
			}
		}
		[CodeDescription("Returns TRUE if this is a Client->Server message, FALSE if this is a message from Server->Client.")]
		public bool IsOutbound
		{
			get
			{
				return this._bOutbound;
			}
		}
		public int ID
		{
			get
			{
				return this._iID;
			}
		}
		[CodeDescription("Returns the raw payload data, which may be masked.")]
		public byte[] PayloadData
		{
			get
			{
				return this._arrRawPayload;
			}
			internal set
			{
				this._arrRawPayload = value;
			}
		}
		[CodeDescription("Returns the WebSocketMessage's masking key, if any.")]
		public byte[] MaskingKey
		{
			get
			{
				return this._arrMask;
			}
			internal set
			{
				this._arrMask = value;
			}
		}
		public WebSocketFrameTypes FrameType
		{
			get
			{
				return this._wsftType;
			}
			internal set
			{
				this._wsftType = value;
			}
		}
		[CodeDescription("If this is a Close frame, returns the close code. Otherwise, returns -1.")]
		public int iCloseReason
		{
			get
			{
				if (this.FrameType == WebSocketFrameTypes.Close && this._arrRawPayload != null && this._arrRawPayload.Length >= 2)
				{
					byte[] array = new byte[2];
					Buffer.BlockCopy(this._arrRawPayload, 0, array, 0, 2);
					WebSocketMessage.UnmaskData(array, this._arrMask, array);
					return ((int)array[0] << 8) + (int)array[1];
				}
				return -1;
			}
		}
		internal void AssignHeader(byte byteHeader)
		{
			this._bIsFinalFragment = (128 == (byteHeader & 128));
			this._byteReservedFlags = (byte)((byteHeader & 112) >> 4);
			this.FrameType = (WebSocketFrameTypes)(byteHeader & 15);
		}
		[CodeDescription("Cancel transmission of this WebSocketMessage.")]
		public void Abort()
		{
			this._bAborted = true;
		}
		[CodeDescription("Returns the entire WebSocketMessage, including headers.")]
		public byte[] ToByteArray()
		{
			if (this._arrRawPayload == null)
			{
				return Utilities.emptyByteArray;
			}
			MemoryStream memoryStream = new MemoryStream();
			memoryStream.WriteByte((byte)((this._bIsFinalFragment ? ((WebSocketFrameTypes)128) : WebSocketFrameTypes.Continuation) | (WebSocketFrameTypes)(this._byteReservedFlags << 4) | this.FrameType));
			ulong num = (ulong)((long)this._arrRawPayload.Length);
			byte[] array;
			if (this._arrRawPayload.Length < 126)
			{
				array = new byte[]
				{
					(byte)this._arrRawPayload.Length
				};
			}
			else
			{
				if (this._arrRawPayload.Length < 65535)
				{
					array = new byte[]
					{
						126,
						(byte)(num >> 8),
						(byte)(num & 255uL)
					};
				}
				else
				{
					array = new byte[]
					{
						127,
						(byte)(num >> 56),
						(byte)((num & 71776119061217280uL) >> 48),
						(byte)((num & 280375465082880uL) >> 40),
						(byte)((num & 1095216660480uL) >> 32),
						(byte)((num & (ulong)16777216) >> 24),
						(byte)((num & 16711680uL) >> 16),
						(byte)((num & 65280uL) >> 8),
						(byte)(num & 255uL)
					};
				}
			}
			if (this._arrMask != null)
			{
				byte[] expr_130_cp_0 = array;
				int expr_130_cp_1 = 0;
				expr_130_cp_0[expr_130_cp_1] |= 128;
			}
			memoryStream.Write(array, 0, array.Length);
			if (this._arrMask != null)
			{
				memoryStream.Write(this._arrMask, 0, 4);
			}
			memoryStream.Write(this._arrRawPayload, 0, this._arrRawPayload.Length);
			return memoryStream.ToArray();
		}
		internal WebSocketMessage(WebSocket oWSOwner, int iID, bool bIsOutbound)
		{
			this._wsOwner = oWSOwner;
			this._iID = iID;
			this._bOutbound = bIsOutbound;
		}
		[CodeDescription("Returns all info about this message.")]
		public override string ToString()
		{
			return string.Format("WS{0}\nMessageID:\t{1}.{2}\nMessageType:\t{3}\nPayloadString:\t{4}\nMasking:\t{5}\n", new object[]
			{
				this._wsOwner.ToString(),
				this._bOutbound ? "Client" : "Server",
				this._iID,
				this._wsftType,
				this.PayloadAsString(),
				(this._arrMask == null) ? "<none>" : BitConverter.ToString(this._arrMask)
			});
		}
		private static void UnmaskData(byte[] arrIn, byte[] arrKey, byte[] arrOut)
		{
			for (int i = 0; i < arrIn.Length; i++)
			{
				arrOut[i] = (byte)(arrIn[i] ^ arrKey[i % 4]);
			}
		}
		private static void MaskDataInPlace(byte[] arrInOut, byte[] arrKey)
		{
			if (arrKey == null)
			{
				return;
			}
			for (int i = 0; i < arrInOut.Length; i++)
			{
				arrInOut[i] ^= arrKey[i % 4];
			}
		}
		[CodeDescription("Replaces the WebSocketMessage's payload with the specified string, masking if needed.")]
		public void SetPayload(string sPayload)
		{
			this._SetPayloadWithoutCopy(Encoding.UTF8.GetBytes(sPayload));
		}
		[CodeDescription("Replaces the WebSocketMessage's payload with the specified byte array, masking if needed.")]
		public void SetPayload(byte[] arrNewPayload)
		{
			byte[] array = new byte[arrNewPayload.Length];
			Buffer.BlockCopy(arrNewPayload, 0, array, 0, arrNewPayload.Length);
			this._SetPayloadWithoutCopy(array);
		}
		private void _SetPayloadWithoutCopy(byte[] arrNewPayload)
		{
			WebSocketMessage.MaskDataInPlace(arrNewPayload, this._arrMask);
			this._arrRawPayload = arrNewPayload;
		}
		[CodeDescription("Returns the WebSocketMessage's payload as a string, unmasking if needed.")]
		public string PayloadAsString()
		{
			if (this._arrRawPayload == null)
			{
				return "<NoPayload>";
			}
			byte[] array;
			if (this._arrMask != null)
			{
				array = new byte[this._arrRawPayload.Length];
				WebSocketMessage.UnmaskData(this._arrRawPayload, this._arrMask, array);
			}
			else
			{
				array = this._arrRawPayload;
			}
			if (this._wsftType == WebSocketFrameTypes.Text)
			{
				return Encoding.UTF8.GetString(array);
			}
			return BitConverter.ToString(array);
		}
		[CodeDescription("Returns the WebSocketMessage's payload as byte[], unmasking if needed.")]
		public byte[] PayloadAsBytes()
		{
			if (this._arrRawPayload == null)
			{
				return Utilities.emptyByteArray;
			}
			byte[] array = new byte[this._arrRawPayload.Length];
			if (this._arrMask != null)
			{
				WebSocketMessage.UnmaskData(this._arrRawPayload, this._arrMask, array);
			}
			else
			{
				Buffer.BlockCopy(this._arrRawPayload, 0, array, 0, array.Length);
			}
			return array;
		}
	}
}
