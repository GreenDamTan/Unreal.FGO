using System;
using System.Collections;
using System.Globalization;
using System.Text;
namespace Fiddler.WebFormats
{
	public class JSON
	{
		public class JSONParseResult
		{
			public object JSONObject
			{
				get;
				set;
			}
			public JSON.JSONParseErrors JSONErrors
			{
				get;
				set;
			}
		}
		public class JSONParseErrors
		{
			public int iErrorIndex
			{
				get;
				set;
			}
			public string sWarningText
			{
				get;
				set;
			}
		}
		internal enum JSONTokens : byte
		{
			NONE,
			CURLY_OPEN,
			CURLY_CLOSE,
			SQUARED_OPEN,
			SQUARED_CLOSE,
			COLON,
			COMMA,
			STRING,
			NUMBER,
			TRUE,
			FALSE,
			NULL,
			IMPLIED_IDENTIFIER_NAME
		}
		private const int BUILDER_DEFAULT_CAPACITY = 2048;
		public static object JsonDecode(string sJSON)
		{
			JSON.JSONParseErrors jSONErrors;
			return new JSON.JSONParseResult
			{
				JSONObject = JSON.JsonDecode(sJSON, out jSONErrors),
				JSONErrors = jSONErrors
			};
		}
		public static object JsonDecode(string sJSON, out JSON.JSONParseErrors oErrors)
		{
			oErrors = new JSON.JSONParseErrors
			{
				iErrorIndex = -1,
				sWarningText = string.Empty
			};
			if (!string.IsNullOrEmpty(sJSON))
			{
				char[] json = sJSON.ToCharArray();
				int num = 0;
				bool flag = true;
				return JSON.ParseValue(json, ref num, ref flag, ref oErrors);
			}
			return null;
		}
		private static Hashtable ParseObject(char[] json, ref int index, ref JSON.JSONParseErrors oErrors)
		{
			Hashtable hashtable = new Hashtable();
			JSON.NextToken(json, ref index);
			bool flag = false;
			while (!flag)
			{
				JSON.JSONTokens jSONTokens = JSON.LookAhead(json, index);
				if (jSONTokens == JSON.JSONTokens.NONE)
				{
					return null;
				}
				if (jSONTokens == JSON.JSONTokens.COMMA)
				{
					JSON.NextToken(json, ref index);
				}
				else
				{
					if (jSONTokens == JSON.JSONTokens.CURLY_CLOSE)
					{
						JSON.NextToken(json, ref index);
						return hashtable;
					}
					string text;
					if (jSONTokens == JSON.JSONTokens.IMPLIED_IDENTIFIER_NAME)
					{
						text = JSON.ParseUnquotedIdentifier(json, ref index, ref oErrors);
						if (text == null)
						{
							if (oErrors.iErrorIndex < 0)
							{
								oErrors.iErrorIndex = index;
							}
							return null;
						}
					}
					else
					{
						text = JSON.ParseString(json, ref index);
						if (text == null)
						{
							if (oErrors.iErrorIndex < 0)
							{
								oErrors.iErrorIndex = index;
							}
							return null;
						}
					}
					jSONTokens = JSON.NextToken(json, ref index);
					if (jSONTokens != JSON.JSONTokens.COLON)
					{
						if (oErrors.iErrorIndex < 0)
						{
							oErrors.iErrorIndex = index;
						}
						return null;
					}
					bool flag2 = true;
					object value = JSON.ParseValue(json, ref index, ref flag2, ref oErrors);
					if (!flag2)
					{
						oErrors.iErrorIndex = index;
						return null;
					}
					hashtable[text] = value;
				}
			}
			return hashtable;
		}
		private static ArrayList ParseArray(char[] json, ref int index, ref JSON.JSONParseErrors oErrors)
		{
			ArrayList arrayList = new ArrayList();
			JSON.NextToken(json, ref index);
			bool flag = false;
			while (!flag)
			{
				JSON.JSONTokens jSONTokens = JSON.LookAhead(json, index);
				if (jSONTokens == JSON.JSONTokens.NONE)
				{
					if (oErrors.iErrorIndex < 0)
					{
						oErrors.iErrorIndex = index;
					}
					return null;
				}
				if (jSONTokens == JSON.JSONTokens.COMMA)
				{
					JSON.NextToken(json, ref index);
				}
				else
				{
					if (jSONTokens == JSON.JSONTokens.SQUARED_CLOSE)
					{
						JSON.NextToken(json, ref index);
						return arrayList;
					}
					bool flag2 = true;
					object value = JSON.ParseValue(json, ref index, ref flag2, ref oErrors);
					if (!flag2)
					{
						if (oErrors.iErrorIndex < 0)
						{
							oErrors.iErrorIndex = index;
						}
						return null;
					}
					arrayList.Add(value);
				}
			}
			return arrayList;
		}
		private static object ParseValue(char[] json, ref int index, ref bool success, ref JSON.JSONParseErrors oErrors)
		{
			switch (JSON.LookAhead(json, index))
			{
			case JSON.JSONTokens.CURLY_OPEN:
				return JSON.ParseObject(json, ref index, ref oErrors);
			case JSON.JSONTokens.SQUARED_OPEN:
				return JSON.ParseArray(json, ref index, ref oErrors);
			case JSON.JSONTokens.STRING:
				return JSON.ParseString(json, ref index);
			case JSON.JSONTokens.NUMBER:
				return JSON.ParseNumber(json, ref index);
			case JSON.JSONTokens.TRUE:
				JSON.NextToken(json, ref index);
				return true;
			case JSON.JSONTokens.FALSE:
				JSON.NextToken(json, ref index);
				return false;
			case JSON.JSONTokens.NULL:
				JSON.NextToken(json, ref index);
				return null;
			case JSON.JSONTokens.IMPLIED_IDENTIFIER_NAME:
				return JSON.ParseUnquotedIdentifier(json, ref index, ref oErrors);
			}
			success = false;
			return null;
		}
		private static string ParseUnquotedIdentifier(char[] json, ref int index, ref JSON.JSONParseErrors oErrors)
		{
			JSON.EatWhitespace(json, ref index);
			int num = index;
			StringBuilder stringBuilder = new StringBuilder(2048);
			bool flag = false;
			while (!flag)
			{
				if (index != json.Length)
				{
					char c = json[index];
					if (JSON.isValidIdentifierChar(c))
					{
						stringBuilder.Append(c);
						index++;
						continue;
					}
					if (stringBuilder.Length < 1)
					{
						return null;
					}
					flag = true;
				}
				IL_4D:
				if (!flag)
				{
					return null;
				}
				oErrors.sWarningText = string.Format("{0}Illegal/Unquoted identifier '{1}' at position {2}.\n", oErrors.sWarningText, stringBuilder.ToString(), num);
				return stringBuilder.ToString();
			}
            if (!flag)
            {
                return null;
            }
            oErrors.sWarningText = string.Format("{0}Illegal/Unquoted identifier '{1}' at position {2}.\n", oErrors.sWarningText, stringBuilder.ToString(), num);
            return stringBuilder.ToString();
		}
		private static string ParseString(char[] json, ref int index)
		{
			StringBuilder stringBuilder = new StringBuilder(2048);
			JSON.EatWhitespace(json, ref index);
			index++;
			bool flag = false;
			while (!flag && index != json.Length)
			{
				char c = json[index++];
				if (c == '"')
				{
					flag = true;
					break;
				}
				if (c == '\\')
				{
					if (index == json.Length)
					{
						break;
					}
					c = json[index++];
					if (c == '"')
					{
						stringBuilder.Append('"');
						continue;
					}
					if (c == '\\')
					{
						stringBuilder.Append('\\');
						continue;
					}
					if (c == '/')
					{
						stringBuilder.Append('/');
						continue;
					}
					if (c == 'b')
					{
						stringBuilder.Append('\b');
						continue;
					}
					if (c == 'f')
					{
						stringBuilder.Append('\f');
						continue;
					}
					if (c == 'n')
					{
						stringBuilder.Append('\n');
						continue;
					}
					if (c == 'r')
					{
						stringBuilder.Append('\r');
						continue;
					}
					if (c == 't')
					{
						stringBuilder.Append('\t');
						continue;
					}
					if (c != 'u')
					{
						continue;
					}
					int num = json.Length - index;
					if (num >= 4)
					{
						uint utf = uint.Parse(new string(json, index, 4), NumberStyles.HexNumber);
						string value;
						try
						{
							value = char.ConvertFromUtf32((int)utf);
							goto IL_13A;
						}
						catch (Exception)
						{
							value = "�";
							goto IL_13A;
						}
						goto IL_12D;
						IL_13A:
						stringBuilder.Append(value);
						index += 4;
						continue;
					}
					break;
				}
				IL_12D:
				stringBuilder.Append(c);
			}
			if (!flag)
			{
				return null;
			}
			return stringBuilder.ToString();
		}
		private static double ParseNumber(char[] json, ref int index)
		{
			JSON.EatWhitespace(json, ref index);
			int lastIndexOfNumber = JSON.GetLastIndexOfNumber(json, index);
			int length = lastIndexOfNumber - index + 1;
			string s = new string(json, index, length);
			index = lastIndexOfNumber + 1;
			return double.Parse(s, CultureInfo.InvariantCulture);
		}
		private static int GetLastIndexOfNumber(char[] json, int index)
		{
			int num = index;
			while (num < json.Length && "0123456789+-.eE".IndexOf(json[num]) != -1)
			{
				num++;
			}
			return num - 1;
		}
		private static void EatWhitespace(char[] json, ref int index)
		{
			while (index < json.Length)
			{
				if (" \t\n\r".IndexOf(json[index]) == -1)
				{
					return;
				}
				index++;
			}
		}
		private static JSON.JSONTokens LookAhead(char[] json, int index)
		{
			int num = index;
			return JSON.NextToken(json, ref num);
		}
		private static JSON.JSONTokens NextToken(char[] json, ref int index)
		{
			JSON.EatWhitespace(json, ref index);
			if (index == json.Length)
			{
				return JSON.JSONTokens.NONE;
			}
			char c = json[index];
			index++;
			char c2 = c;
			switch (c2)
			{
			case '"':
				return JSON.JSONTokens.STRING;
			case '#':
			case '$':
			case '%':
			case '&':
			case '\'':
			case '(':
			case ')':
			case '*':
			case '+':
			case '.':
			case '/':
				break;
			case ',':
				return JSON.JSONTokens.COMMA;
			case '-':
			case '0':
			case '1':
			case '2':
			case '3':
			case '4':
			case '5':
			case '6':
			case '7':
			case '8':
			case '9':
				return JSON.JSONTokens.NUMBER;
			case ':':
				return JSON.JSONTokens.COLON;
			default:
				switch (c2)
				{
				case '[':
					return JSON.JSONTokens.SQUARED_OPEN;
				case '\\':
					break;
				case ']':
					return JSON.JSONTokens.SQUARED_CLOSE;
				default:
					switch (c2)
					{
					case '{':
						return JSON.JSONTokens.CURLY_OPEN;
					case '}':
						return JSON.JSONTokens.CURLY_CLOSE;
					}
					break;
				}
				break;
			}
			index--;
			int num = json.Length - index;
			if (num >= 5 && json[index] == 'f' && json[index + 1] == 'a' && json[index + 2] == 'l' && json[index + 3] == 's' && json[index + 4] == 'e')
			{
				index += 5;
				return JSON.JSONTokens.FALSE;
			}
			if (num >= 4 && json[index] == 't' && json[index + 1] == 'r' && json[index + 2] == 'u' && json[index + 3] == 'e')
			{
				index += 4;
				return JSON.JSONTokens.TRUE;
			}
			if (num >= 4 && json[index] == 'n' && json[index + 1] == 'u' && json[index + 2] == 'l' && json[index + 3] == 'l')
			{
				index += 4;
				return JSON.JSONTokens.NULL;
			}
			if (JSON.isValidIdentifierStart(json[index]))
			{
				return JSON.JSONTokens.IMPLIED_IDENTIFIER_NAME;
			}
			return JSON.JSONTokens.NONE;
		}
		private static bool isValidIdentifierStart(char char_0)
		{
			if (char_0 != '_')
			{
				if (char_0 != '$')
				{
					return char_0 == '\'' || char.IsLetter(char_0);
				}
			}
			return true;
		}
		private static bool isValidIdentifierChar(char char_0)
		{
			if (char_0 != '-' && char_0 != '_')
			{
				if (char_0 != '$')
				{
					return char.IsLetterOrDigit(char_0) || char_0 == '\'';
				}
			}
			return true;
		}
		public static string JsonEncode(object json)
		{
			StringBuilder stringBuilder = new StringBuilder(2048);
			if (!JSON.SerializeValue(json, stringBuilder))
			{
				return null;
			}
			return stringBuilder.ToString();
		}
		private static bool SerializeObject(IDictionary anObject, StringBuilder builder)
		{
			builder.Append("{");
			IDictionaryEnumerator enumerator = anObject.GetEnumerator();
			bool flag = true;
			while (enumerator.MoveNext())
			{
				string aString = enumerator.Key.ToString();
				object value = enumerator.Value;
				if (!flag)
				{
					builder.Append(", ");
				}
				JSON.SerializeString(aString, builder);
				builder.Append(":");
				if (!JSON.SerializeValue(value, builder))
				{
					return false;
				}
				flag = false;
			}
			builder.Append("}");
			return true;
		}
		private static bool SerializeArray(IList anArray, StringBuilder builder)
		{
			builder.Append("[");
			bool flag = true;
			for (int i = 0; i < anArray.Count; i++)
			{
				object value = anArray[i];
				if (!flag)
				{
					builder.Append(", ");
				}
				if (!JSON.SerializeValue(value, builder))
				{
					return false;
				}
				flag = false;
			}
			builder.Append("]");
			return true;
		}
		private static bool SerializeValue(object value, StringBuilder builder)
		{
			if (value == null)
			{
				builder.Append("null");
			}
			else
			{
				if (value is string)
				{
					JSON.SerializeString((string)value, builder);
				}
				else
				{
					if (value is Hashtable)
					{
						JSON.SerializeObject((Hashtable)value, builder);
					}
					else
					{
						if (value is ArrayList)
						{
							JSON.SerializeArray((ArrayList)value, builder);
						}
						else
						{
							if (JSON.IsNumeric(value))
							{
								JSON.SerializeNumber(Convert.ToDouble(value), builder);
							}
							else
							{
								if (value is bool && (bool)value)
								{
									builder.Append("true");
								}
								else
								{
									if (!(value is bool) || (bool)value)
									{
										return false;
									}
									builder.Append("false");
								}
							}
						}
					}
				}
			}
			return true;
		}
		private static void SerializeString(string aString, StringBuilder builder)
		{
			builder.Append("\"");
			char[] array = aString.ToCharArray();
			int i = 0;
			while (i < array.Length)
			{
				char c = array[i];
				switch (c)
				{
				case '\b':
					builder.Append("\\b");
					break;
				case '\t':
					builder.Append("\\t");
					break;
				case '\n':
					builder.Append("\\n");
					break;
				case '\v':
					goto IL_9A;
				case '\f':
					builder.Append("\\f");
					break;
				case '\r':
					builder.Append("\\r");
					break;
				default:
					if (c != '"')
					{
						if (c != '\\')
						{
							goto IL_9A;
						}
						builder.Append("\\\\");
					}
					else
					{
						builder.Append("\\\"");
					}
					break;
				}
				IL_FA:
				i++;
				continue;
				IL_9A:
				char value = array[i];
				int num = Convert.ToInt32(value);
				if (num >= 32 && num <= 126)
				{
					builder.Append(value);
					goto IL_FA;
				}
				builder.Append("\\u" + num.ToString("x").PadLeft(4, '0'));
				goto IL_FA;
			}
			builder.Append("\"");
		}
		private static void SerializeNumber(double number, StringBuilder builder)
		{
			builder.Append(Convert.ToString(number, CultureInfo.InvariantCulture));
		}
		private static bool IsNumeric(object object_0)
		{
			bool result;
			try
			{
				double.Parse(object_0.ToString());
				return true;
			}
			catch (Exception)
			{
				result = false;
			}
			return result;
		}
	}
}
