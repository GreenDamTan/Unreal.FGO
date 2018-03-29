using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
namespace Fiddler
{
	public class HTTPResponseHeaders : HTTPHeaders, IEnumerable<HTTPHeaderItem>, ICloneable
	{
		[CodeDescription("Status code from HTTP Response. Call SetStatus() instead of manipulating directly.")]
		public int HTTPResponseCode;
		[CodeDescription("Status text from HTTP Response (e.g. '200 OK'). Call SetStatus() instead of manipulating directly.")]
		public string HTTPResponseStatus = string.Empty;
		public new IEnumerator<HTTPHeaderItem> GetEnumerator()
		{
			return this.storage.GetEnumerator();
		}
		IEnumerator IEnumerable.GetEnumerator()
		{
			return this.storage.GetEnumerator();
		}
		public object Clone()
		{
			HTTPResponseHeaders hTTPResponseHeaders = (HTTPResponseHeaders)base.MemberwiseClone();
			hTTPResponseHeaders.storage = new List<HTTPHeaderItem>(this.storage.Count);
			foreach (HTTPHeaderItem current in this.storage)
			{
				hTTPResponseHeaders.storage.Add((HTTPHeaderItem)current.Clone());
			}
			return hTTPResponseHeaders;
		}
		public void SetStatus(int iStatusCode, string sStatusText)
		{
			this.HTTPResponseCode = iStatusCode;
			this.HTTPResponseStatus = string.Format("{0} {1}", iStatusCode, sStatusText);
		}
		public HTTPResponseHeaders()
		{
		}
		public HTTPResponseHeaders(int iStatus, string[] sHeaders)
		{
			this.SetStatus(iStatus, "Generated");
			if (sHeaders != null)
			{
				string empty = string.Empty;
				Parser.ParseNVPHeaders(this, sHeaders, 0, ref empty);
			}
		}
		public HTTPResponseHeaders(Encoding encodingForHeaders)
		{
			this._HeaderEncoding = encodingForHeaders;
		}
		[CodeDescription("Returns a byte[] representing the HTTP headers.")]
		public byte[] ToByteArray(bool prependStatusLine, bool appendEmptyLine)
		{
			return this._HeaderEncoding.GetBytes(this.ToString(prependStatusLine, appendEmptyLine));
		}
		[CodeDescription("Returns a string representing the HTTP headers.")]
		public string ToString(bool prependStatusLine, bool appendEmptyLine)
		{
			StringBuilder stringBuilder = new StringBuilder(512);
			if (prependStatusLine)
			{
				stringBuilder.AppendFormat("{0} {1}\r\n", this.HTTPVersion, this.HTTPResponseStatus);
			}
			for (int i = 0; i < this.storage.Count; i++)
			{
				stringBuilder.AppendFormat("{0}: {1}\r\n", this.storage[i].Name, this.storage[i].Value);
			}
			if (appendEmptyLine)
			{
				stringBuilder.Append("\r\n");
			}
			return stringBuilder.ToString();
		}
		[CodeDescription("Returns a string containing the HTTP Response headers.")]
		public override string ToString()
		{
			return this.ToString(true, false);
		}
		[CodeDescription("Replaces the current Response header set using a string representing the new HTTP headers.")]
		public override bool AssignFromString(string sHeaders)
		{
			if (string.IsNullOrEmpty(sHeaders))
			{
				throw new ArgumentException("Header string must not be null or empty");
			}
			if (!sHeaders.Contains("\r\n\r\n"))
			{
				sHeaders += "\r\n\r\n";
			}
			HTTPResponseHeaders hTTPResponseHeaders = null;
			try
			{
				hTTPResponseHeaders = Parser.ParseResponse(sHeaders);
			}
			catch (Exception)
			{
			}
			if (hTTPResponseHeaders == null)
			{
				return false;
			}
			this.SetStatus(hTTPResponseHeaders.HTTPResponseCode, hTTPResponseHeaders.HTTPResponseStatus);
			this.HTTPVersion = hTTPResponseHeaders.HTTPVersion;
			this.storage = hTTPResponseHeaders.storage;
			return true;
		}
	}
}
