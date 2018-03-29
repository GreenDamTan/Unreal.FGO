using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
namespace Fiddler
{
	public abstract class HTTPHeaders
	{
		protected Encoding _HeaderEncoding = CONFIG.oHeaderEncoding;
		[CodeDescription("HTTP version (e.g. HTTP/1.1).")]
		public string HTTPVersion = "HTTP/1.1";
		protected List<HTTPHeaderItem> storage = new List<HTTPHeaderItem>();
		[CodeDescription("Indexer property. Gets or sets the value of a header. In the case of Gets, the value of the FIRST header of that name is returned.\nIf the header does not exist, returns null.\nIn the case of Sets, the value of the FIRST header of that name is updated.\nIf the header does not exist, it is added.")]
		public string this[string HeaderName]
		{
			get
			{
				for (int i = 0; i < this.storage.Count; i++)
				{
					if (string.Equals(this.storage[i].Name, HeaderName, StringComparison.OrdinalIgnoreCase))
					{
						return this.storage[i].Value;
					}
				}
				return string.Empty;
			}
			set
			{
				for (int i = 0; i < this.storage.Count; i++)
				{
					if (string.Equals(this.storage[i].Name, HeaderName, StringComparison.OrdinalIgnoreCase))
					{
						this.storage[i].Value = value;
						return;
					}
				}
				this.Add(HeaderName, value);
			}
		}
		[CodeDescription("Indexer property. Returns HTTPHeaderItem by index.")]
		public HTTPHeaderItem this[int iHeaderNumber]
		{
			get
			{
				return this.storage[iHeaderNumber];
			}
			set
			{
				this.storage[iHeaderNumber] = value;
			}
		}
		public abstract bool AssignFromString(string sHeaders);
		public int ByteCount()
		{
			return this.ToString().Length;
		}
		[CodeDescription("Returns an integer representing the number of headers.")]
		public int Count()
		{
			return this.storage.Count;
		}
		public List<HTTPHeaderItem> FindAll(string sHeaderName)
		{
			return this.storage.FindAll((HTTPHeaderItem oHI) => string.Equals(sHeaderName, oHI.Name, StringComparison.OrdinalIgnoreCase));
		}
		public int CountOf(string sHeaderName)
		{
			int iResult = 0;
			this.storage.ForEach(delegate(HTTPHeaderItem oHI)
			{
				if (string.Equals(sHeaderName, oHI.Name, StringComparison.OrdinalIgnoreCase))
				{
					iResult++;
				}
			});
			return iResult;
		}
		public IEnumerator GetEnumerator()
		{
			return this.storage.GetEnumerator();
		}
		[CodeDescription("Add a new header containing the specified name and value.")]
		public HTTPHeaderItem Add(string sHeaderName, string sValue)
		{
			HTTPHeaderItem hTTPHeaderItem = new HTTPHeaderItem(sHeaderName, sValue);
			this.storage.Add(hTTPHeaderItem);
			return hTTPHeaderItem;
		}
		[CodeDescription("Returns true if the Headers collection contains a header of the specified (case-insensitive) name.")]
		public bool Exists(string sHeaderName)
		{
			for (int i = 0; i < this.storage.Count; i++)
			{
				if (string.Equals(this.storage[i].Name, sHeaderName, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}
		[CodeDescription("Returns a string representing the value of the named token within the named header.")]
		public string GetTokenValue(string sHeaderName, string sTokenName)
		{
			string result = null;
			string text = this[sHeaderName];
			if (!string.IsNullOrEmpty(text))
			{
				Regex regex = new Regex(sTokenName + "\\s?=\\s?[\"]?(?<TokenValue>[^\";]*)", RegexOptions.IgnoreCase);
				Match match = regex.Match(text);
				if (match.Success && match.Groups["TokenValue"] != null)
				{
					result = match.Groups["TokenValue"].Value;
				}
			}
			return result;
		}
		[CodeDescription("Returns true if the collection contains a header of the specified (case-insensitive) name, and sHeaderValue (case-insensitive) is part of the Header's value.")]
		public bool ExistsAndContains(string sHeaderName, string sHeaderValue)
		{
			for (int i = 0; i < this.storage.Count; i++)
			{
				if (this.storage[i].Name.OICEquals(sHeaderName) && this.storage[i].Value.OICContains(sHeaderValue))
				{
					return true;
				}
			}
			return false;
		}
		[CodeDescription("Returns true if the collection contains a header of the specified (case-insensitive) name, with value sHeaderValue (case-insensitive).")]
		public bool ExistsAndEquals(string sHeaderName, string sHeaderValue)
		{
			for (int i = 0; i < this.storage.Count; i++)
			{
				if (this.storage[i].Name.OICEquals(sHeaderName))
				{
					string inStr = this.storage[i].Value.Trim();
					if (inStr.OICEquals(sHeaderValue))
					{
						return true;
					}
				}
			}
			return false;
		}
		[CodeDescription("Removes ALL headers from the header collection which have the specified (case-insensitive) name.")]
		public void Remove(string sHeaderName)
		{
			for (int i = this.storage.Count - 1; i >= 0; i--)
			{
				if (this.storage[i].Name.OICEquals(sHeaderName))
				{
					this.storage.RemoveAt(i);
				}
			}
		}
		[CodeDescription("Removes ALL headers from the header collection which have the specified (case-insensitive) names.")]
		public void RemoveRange(string[] arrToRemove)
		{
			int i = this.storage.Count - 1;
			IL_4F:
			while (i >= 0)
			{
				for (int j = 0; j < arrToRemove.Length; j++)
				{
					string toMatch = arrToRemove[j];
					if (this.storage[i].Name.OICEquals(toMatch))
					{
						this.storage.RemoveAt(i);
						IL_4B:
						i--;
						goto IL_4F;
					}
				}
				//goto IL_4B;
			}
		}
		public void Remove(HTTPHeaderItem oRemove)
		{
			this.storage.Remove(oRemove);
		}
		public void RemoveAll()
		{
			this.storage.Clear();
		}
		[CodeDescription("Renames ALL headers in the header collection which have the specified (case-insensitive) name.")]
		public bool RenameHeaderItems(string sOldHeaderName, string sNewHeaderName)
		{
			bool result = false;
			for (int i = 0; i < this.storage.Count; i++)
			{
				if (this.storage[i].Name.OICEquals(sOldHeaderName))
				{
					this.storage[i].Name = sNewHeaderName;
					result = true;
				}
			}
			return result;
		}
	}
}
