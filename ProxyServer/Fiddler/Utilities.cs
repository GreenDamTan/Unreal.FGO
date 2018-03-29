using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Authentication;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
namespace Fiddler
{
	public static class Utilities
	{
		[Flags]
		internal enum SoundFlags : uint
		{
			SND_SYNC = 0u,
			SND_ASYNC = 1u,
			SND_NODEFAULT = 2u,
			SND_MEMORY = 4u,
			SND_LOOP = 8u,
			SND_NOSTOP = 16u,
			SND_NOWAIT = 8192u,
			SND_ALIAS = 65536u,
			SND_ALIAS_ID = 1114112u,
			SND_FILENAME = 131072u,
			SND_RESOURCE = 262148u
		}
		internal struct COPYDATASTRUCT
		{
			public IntPtr dwData;
			public int cbData;
			public IntPtr lpData;
		}
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		internal struct SendDataStruct
		{
			public IntPtr dwData;
			public int cbData;
			public string strData;
		}
		internal const int WM_HOTKEY = 786;
		internal const int WM_COPYDATA = 74;
		internal const int WM_SIZE = 5;
		internal const int WM_SHOWWINDOW = 24;
		internal const int WM_QUERYENDSESSION = 17;
		public const string sCommonRequestHeaders = "Cache-Control,If-None-Match,If-Modified-Since,Pragma,If-Unmodified-Since,If-Range,If-Match,Content-Length,Content-Type,Referer,Origin,Expect,Content-Encoding,TE,Transfer-Encoding,Proxy-Connection,Connection,Accept,Accept-Charset,Accept-Encoding,Accept-Language,User-Agent,UA-Color,UA-CPU,UA-OS,UA-Pixels,Cookie,Cookie2,DNT,Authorization,Proxy-Authorization";
		public const string sCommonResponseHeaders = "Age,Cache-control,Date,Expires,Pragma,Vary,Content-Length,ETag,Last-Modified,Content-Type,Content-Disposition,Content-Encoding,Transfer-encoding,Via,Keep-Alive,Location,Proxy-Connection,Connection,Set-Cookie,WWW-Authenticate,Proxy-Authenticate,P3P,X-UA-Compatible,X-Frame-options,X-Content-Type-Options,X-XSS-Protection,Strict-Transport-Security,X-Content-Security-Policy,Access-Control-Allow-Origin";
		public static readonly byte[] emptyByteArray = new byte[0];
		private static Encoding[] sniffableEncodings = new Encoding[]
		{
			Encoding.UTF32,
			Encoding.BigEndianUnicode,
			Encoding.Unicode,
			Encoding.UTF8
		};
		public static T EnsureInRange<T>(T current, T gparam_0, T gparam_1)
		{
			if (Comparer<T>.Default.Compare(current, gparam_0) < 0)
			{
				return gparam_0;
			}
			if (Comparer<T>.Default.Compare(current, gparam_1) > 0)
			{
				return gparam_1;
			}
			return current;
		}
		public static string ObtainSaveFilename(string sDialogTitle, string sFilter)
		{
			return Utilities.ObtainSaveFilename(sDialogTitle, sFilter, null);
		}
		public static string ObtainSaveFilename(string sDialogTitle, string sFilter, string sInitialDirectory)
		{
			FileDialog fileDialog = new SaveFileDialog();
			fileDialog.Title = sDialogTitle;
			fileDialog.Filter = sFilter;
			if (!string.IsNullOrEmpty(sInitialDirectory))
			{
				fileDialog.InitialDirectory = sInitialDirectory;
				fileDialog.RestoreDirectory = true;
			}
			fileDialog.CustomPlaces.Add(CONFIG.GetPath("Captures"));
			string result = null;
			if (DialogResult.OK == fileDialog.ShowDialog())
			{
				result = fileDialog.FileName;
			}
			fileDialog.Dispose();
			return result;
		}
		public static string ObtainOpenFilename(string sDialogTitle, string sFilter)
		{
			return Utilities.ObtainOpenFilename(sDialogTitle, sFilter, null);
		}
		public static string[] ObtainFilenames(string sDialogTitle, string sFilter, string sInitialDirectory, bool bAllowMultiple)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Title = sDialogTitle;
			openFileDialog.Multiselect = bAllowMultiple;
			openFileDialog.Filter = sFilter;
			if (!string.IsNullOrEmpty(sInitialDirectory))
			{
				openFileDialog.InitialDirectory = sInitialDirectory;
				openFileDialog.RestoreDirectory = true;
			}
			openFileDialog.CustomPlaces.Add(CONFIG.GetPath("Captures"));
			string[] result = null;
			if (DialogResult.OK == openFileDialog.ShowDialog())
			{
				result = openFileDialog.FileNames;
			}
			openFileDialog.Dispose();
			return result;
		}
		public static string ObtainOpenFilename(string sDialogTitle, string sFilter, string sInitialDirectory)
		{
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Title = sDialogTitle;
			openFileDialog.Filter = sFilter;
			if (!string.IsNullOrEmpty(sInitialDirectory))
			{
				openFileDialog.InitialDirectory = sInitialDirectory;
				openFileDialog.RestoreDirectory = true;
			}
			openFileDialog.CustomPlaces.Add(CONFIG.GetPath("Captures"));
			string result = null;
			if (DialogResult.OK == openFileDialog.ShowDialog())
			{
				result = openFileDialog.FileName;
			}
			openFileDialog.Dispose();
			return result;
		}
		internal static bool FiddlerMeetsVersionRequirement(Assembly assemblyInput, string sWhatType)
		{
			if (!assemblyInput.IsDefined(typeof(RequiredVersionAttribute), false))
			{
				return false;
			}
			RequiredVersionAttribute requiredVersionAttribute = (RequiredVersionAttribute)Attribute.GetCustomAttribute(assemblyInput, typeof(RequiredVersionAttribute));
			int num = Utilities.CompareVersions(requiredVersionAttribute.RequiredVersion, CONFIG.FiddlerVersionInfo);
			if (num > 0)
			{
				FiddlerApplication.DoNotifyUser(string.Format("The {0} in {1} require Fiddler v{2} or later. (You have v{3})\n\nPlease install the latest version of Fiddler from http://getfiddler.com.\n\nCode: {4}", new object[]
				{
					sWhatType,
					assemblyInput.CodeBase,
					requiredVersionAttribute.RequiredVersion,
					CONFIG.FiddlerVersionInfo,
					num
				}), "Extension Not Loaded");
				return false;
			}
			return true;
		}
		public static int CompareVersions(string sRequiredVersion, Version verTest)
		{
			string[] array = sRequiredVersion.Split(new char[]
			{
				'.'
			});
			if (array.Length != 4)
			{
				return 5;
			}
			VersionStruct versionStruct = new VersionStruct();
			if (!int.TryParse(array[0], NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out versionStruct.Major) || !int.TryParse(array[1], NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out versionStruct.Minor) || !int.TryParse(array[2], NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out versionStruct.Build) || !int.TryParse(array[3], NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out versionStruct.Private))
			{
				return 6;
			}
			if (versionStruct.Major > verTest.Major)
			{
				return 4;
			}
			if (verTest.Major > versionStruct.Major)
			{
				return -4;
			}
			if (versionStruct.Minor > verTest.Minor)
			{
				return 3;
			}
			if (verTest.Minor > versionStruct.Minor)
			{
				return -3;
			}
			if (versionStruct.Build > verTest.Build)
			{
				return 2;
			}
			if (verTest.Build > versionStruct.Build)
			{
				return -2;
			}
			if (versionStruct.Private > verTest.Revision)
			{
				return 1;
			}
			if (verTest.Revision > versionStruct.Private)
			{
				return -1;
			}
			return 0;
		}
        
        public static int IndexOfNth(string sString, int n, char chSeek)
        {
            if (!string.IsNullOrEmpty(sString))
            {
                if (n < 1)
                {
                    throw new ArgumentException("index must be greater than 0");
                }
                for (int i = 0; i < sString.Length; i++)
                {
                    if (sString[i] == chSeek)
                    {
                        n--;
                        if (n == 0)
                        {
                            return i;
                        }
                    }
                }
            }
            return -1;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
		internal static extern IntPtr GlobalFree(IntPtr hMem);
		internal static void GlobalFreeIfNonZero(IntPtr hMem)
		{
			if (IntPtr.Zero != hMem)
			{
				Utilities.GlobalFree(hMem);
			}
		}
		[DllImport("winmm.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool PlaySound(string pszSound, IntPtr hMod, Utilities.SoundFlags soundFlags_0);
		internal static void PlaySoundFile(string sFilename)
		{
			Utilities.PlaySound(sFilename, IntPtr.Zero, Utilities.SoundFlags.SND_ASYNC | Utilities.SoundFlags.SND_NODEFAULT | Utilities.SoundFlags.SND_FILENAME);
		}
		internal static void PlayNamedSound(string sSoundName)
		{
			Utilities.PlaySound(sSoundName, IntPtr.Zero, Utilities.SoundFlags.SND_ASYNC | Utilities.SoundFlags.SND_ALIAS);
		}
		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool RegisterHotKey(IntPtr hWnd, int int_0, int fsModifiers, int int_1);
		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		internal static extern bool UnregisterHotKey(IntPtr hWnd, int int_0);
		[DllImport("user32.dll")]
		internal static extern IntPtr SendMessage(IntPtr hWnd, uint uint_0, IntPtr wParam, IntPtr lParam);
		[DllImport("shlwapi.dll", CharSet = CharSet.Auto)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool PathUnExpandEnvStrings(string pszPath, [Out] StringBuilder pszBuf, int cchBuf);
		[DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool PathCompactPathEx(StringBuilder pszOut, string pszSrc, uint cchMax, uint dwFlags);
		public static string CompactPath(string sPath, int iCharLen)
		{
			if (string.IsNullOrEmpty(sPath))
			{
				return string.Empty;
			}
			if (sPath.Length <= iCharLen)
			{
				return sPath;
			}
			StringBuilder stringBuilder = new StringBuilder(iCharLen + 1);
			if (Utilities.PathCompactPathEx(stringBuilder, sPath, (uint)(iCharLen + 1), 0u))
			{
				return stringBuilder.ToString();
			}
			return sPath;
		}
		[CodeDescription("Convert a full path into one that uses environment variables, e.g. %SYSTEM%")]
		public static string CollapsePath(string sPath)
		{
			StringBuilder stringBuilder = new StringBuilder(259);
			if (Utilities.PathUnExpandEnvStrings(sPath, stringBuilder, stringBuilder.Capacity))
			{
				return stringBuilder.ToString();
			}
			return sPath;
		}
		public static string EnsureValidAsPath(string sTargetFolder)
		{
			string result;
			try
			{
				if (Directory.Exists(sTargetFolder))
				{
					result = sTargetFolder;
				}
				else
				{
					string text = Path.GetPathRoot(sTargetFolder);
					if (Directory.Exists(text))
					{
						if (text[text.Length - 1] != Path.DirectorySeparatorChar)
						{
							text += Path.DirectorySeparatorChar;
						}
						sTargetFolder = sTargetFolder.Substring(text.Length);
						string[] array = sTargetFolder.Split(new char[]
						{
							Path.DirectorySeparatorChar
						}, StringSplitOptions.RemoveEmptyEntries);
						string text2 = text;
						int i = 0;
						while (i < array.Length)
						{
							if (!File.Exists(text2 + array[i]))
							{
								if (Directory.Exists(text2 + array[i]))
								{
									text2 = string.Format("{0}{1}{2}{1}", text2, Path.DirectorySeparatorChar, array[i]);
									i++;
									continue;
								}
							}
							else
							{
								int num = 1;
								string arg = array[i];
								do
								{
									array[i] = string.Format("{0}[{1}]", arg, num);
									num++;
								}
								while (File.Exists(text2 + array[i]));
							}
							IL_FB:
							result = string.Format("{0}{1}", text, string.Join(new string(Path.DirectorySeparatorChar, 1), array));
							return result;
						}
                        result = string.Format("{0}{1}", text, string.Join(new string(Path.DirectorySeparatorChar, 1), array));
                        return result;
					}
					result = sTargetFolder;
				}
			}
			catch (Exception)
			{
				result = sTargetFolder;
			}
			return result;
		}
		public static string EnsureUniqueFilename(string sFilename)
		{
			string text = sFilename;
			try
			{
				string directoryName = Path.GetDirectoryName(sFilename);
				string text2 = Utilities.EnsureValidAsPath(directoryName);
				if (directoryName != text2)
				{
					text = string.Format("{0}{1}{2}", text2, Path.DirectorySeparatorChar, Path.GetFileName(sFilename));
				}
				if (Utilities.FileOrFolderExists(text))
				{
					string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text);
					string extension = Path.GetExtension(text);
					int num = 1;
					do
					{
						text = string.Format("{0}{1}{2}[{3}]{4}", new object[]
						{
							directoryName,
							Path.DirectorySeparatorChar,
							fileNameWithoutExtension,
							num.ToString(),
							extension
						});
						num++;
					}
					while (Utilities.FileOrFolderExists(text) || num > 16384);
				}
			}
			catch (Exception)
			{
			}
			return text;
		}
		internal static bool FileOrFolderExists(string sResult)
		{
			bool result;
			try
			{
				result = (File.Exists(sResult) || Directory.Exists(sResult));
			}
			catch (Exception)
			{
				result = true;
			}
			return result;
		}
		public static void EnsureOverwritable(string sFilename)
		{
			if (!Directory.Exists(Path.GetDirectoryName(sFilename)))
			{
				Directory.CreateDirectory(Path.GetDirectoryName(sFilename));
			}
			if (File.Exists(sFilename))
			{
				FileAttributes attributes = File.GetAttributes(sFilename);
				File.SetAttributes(sFilename, attributes & ~(FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System));
			}
		}
		[CodeDescription("Writes arrBytes to a file, creating the target directory and overwriting if the file exists.")]
		public static void WriteArrayToFile(string sFilename, byte[] arrBytes)
		{
			if (arrBytes == null)
			{
				arrBytes = Utilities.emptyByteArray;
			}
			Utilities.EnsureOverwritable(sFilename);
			File.WriteAllBytes(sFilename, arrBytes);
		}
		[CodeDescription("Reads oStream until arrBytes is filled.")]
		public static int ReadEntireStream(Stream oStream, byte[] arrBytes)
		{
			int num = 0;
			while ((long)num < arrBytes.LongLength)
			{
				num += oStream.Read(arrBytes, num, arrBytes.Length - num);
			}
			return num;
		}
		public static byte[] ReadEntireStream(Stream oS)
		{
			MemoryStream memoryStream = new MemoryStream();
			byte[] array = new byte[32768];
			int count;
			while ((count = oS.Read(array, 0, array.Length)) > 0)
			{
				memoryStream.Write(array, 0, count);
			}
			return memoryStream.ToArray();
		}
		public static byte[] JoinByteArrays(byte[] arr1, byte[] arr2)
		{
			byte[] array = new byte[arr1.Length + arr2.Length];
			Buffer.BlockCopy(arr1, 0, array, 0, arr1.Length);
			Buffer.BlockCopy(arr2, 0, array, arr1.Length, arr2.Length);
			return array;
		}
		internal static string ConvertCRAndLFToSpaces(string sIn)
		{
			sIn = sIn.Replace("\r\n", " ");
			sIn = sIn.Replace('\r', ' ');
			sIn = sIn.Replace('\n', ' ');
			return sIn;
		}
		public static string GetCommaTokenValue(string sString, string sTokenName)
		{
			string result = null;
			if (sString != null && sString.Length > 0)
			{
				Regex regex = new Regex(sTokenName + "\\s?=?\\s?[\"]?(?<TokenValue>[^\";,]*)", RegexOptions.IgnoreCase);
				Match match = regex.Match(sString);
				if (match.Success && match.Groups["TokenValue"] != null)
				{
					result = match.Groups["TokenValue"].Value;
				}
			}
			return result;
		}
		[CodeDescription("Returns the first iMaxLength or fewer characters from the target string.")]
		public static string TrimTo(string sString, int iMaxLength)
		{
			if (string.IsNullOrEmpty(sString))
			{
				return string.Empty;
			}
			if (iMaxLength >= sString.Length)
			{
				return sString;
			}
			return sString.Substring(0, iMaxLength);
		}
		public static string EllipsizeIfNeeded(string sString, int iMaxLength)
		{
			if (string.IsNullOrEmpty(sString))
			{
				return string.Empty;
			}
			if (iMaxLength >= sString.Length)
			{
				return sString;
			}
			return sString.Substring(0, iMaxLength - 1) + '…';
		}
		[CodeDescription("Returns the part of a string up to (but NOT including) the first instance of specified substring. If delim not found, returns entire string.")]
		public static string TrimAfter(string sString, string sDelim)
		{
			if (sString == null)
			{
				return string.Empty;
			}
			if (sDelim == null)
			{
				return sString;
			}
			int num = sString.IndexOf(sDelim);
			if (num < 0)
			{
				return sString;
			}
			return sString.Substring(0, num);
		}
		[CodeDescription("Returns the part of a string up to (but NOT including) the first instance of specified delimiter. If delim not found, returns entire string.")]
		public static string TrimAfter(string sString, char chDelim)
		{
			if (sString == null)
			{
				return string.Empty;
			}
			int num = sString.IndexOf(chDelim);
			if (num < 0)
			{
				return sString;
			}
			return sString.Substring(0, num);
		}

        internal static string GetExceptionInfo(Exception exception)
        {
            StringBuilder builder = new StringBuilder(0x200);
            builder.AppendFormat("{0} {1}", exception.GetType(), exception.Message);
            if (exception.InnerException != null)
            {
                builder.AppendFormat(" < {0}", exception.InnerException.Message);
            }
            return builder.ToString();
        }

        public static string TrimAfter(string sString, int iMaxLength)
		{
			return Utilities.TrimTo(sString, iMaxLength);
		}
		[CodeDescription("Returns the part of a string after (but NOT including) the first instance of specified delimiter. If delim not found, returns entire string.")]
		public static string TrimBefore(string sString, char chDelim)
		{
			if (sString == null)
			{
				return string.Empty;
			}
			int num = sString.IndexOf(chDelim);
			if (num < 0)
			{
				return sString;
			}
			return sString.Substring(num + 1);
		}
		[CodeDescription("Returns the part of a string after (but NOT including) the first instance of specified substring. If delim not found, returns entire string.")]
		public static string TrimBefore(string sString, string sDelim)
		{
			if (sString == null)
			{
				return string.Empty;
			}
			if (sDelim == null)
			{
				return sString;
			}
			int num = sString.IndexOf(sDelim);
			if (num < 0)
			{
				return sString;
			}
			return sString.Substring(num + sDelim.Length);
		}
		[CodeDescription("Returns the part of a string after (and including) the first instance of specified substring. If delim not found, returns entire string.")]
		public static string TrimUpTo(string sString, string sDelim)
		{
			if (sString == null)
			{
				return string.Empty;
			}
			if (sDelim == null)
			{
				return sString;
			}
			int num = sString.IndexOf(sDelim);
			if (num < 0)
			{
				return sString;
			}
			return sString.Substring(num);
		}
		[CodeDescription("Returns the part of a string after (but not including) the last instance of specified delimiter. If delim not found, returns entire string.")]
		public static string TrimBeforeLast(string sString, char chDelim)
		{
			if (sString == null)
			{
				return string.Empty;
			}
			int num = sString.LastIndexOf(chDelim);
			if (num < 0)
			{
				return sString;
			}
			return sString.Substring(num + 1);
		}
		[CodeDescription("Returns the part of a string after (but not including) the last instance of specified substring. If delim not found, returns entire string.")]
		public static string TrimBeforeLast(string sString, string sDelim)
		{
			if (sString == null)
			{
				return string.Empty;
			}
			if (sDelim == null)
			{
				return sString;
			}
			int num = sString.LastIndexOf(sDelim);
			if (num < 0)
			{
				return sString;
			}
			return sString.Substring(num + sDelim.Length);
		}
		[CodeDescription("Returns TRUE if the HTTP Method MUST have a body.")]
		public static bool HTTPMethodRequiresBody(string sMethod)
		{
			return "PROPPATCH" == sMethod || "PATCH" == sMethod;
		}
		[CodeDescription("Returns TRUE if the HTTP Method MAY have a body.")]
		public static bool HTTPMethodAllowsBody(string sMethod)
		{
			return "POST" == sMethod || "PUT" == sMethod || "PROPPATCH" == sMethod || "PATCH" == sMethod || "LOCK" == sMethod || "PROPFIND" == sMethod || "SEARCH" == sMethod;
		}
		[CodeDescription("Returns TRUE if a response body is allowed for this responseCode.")]
		public static bool HTTPStatusAllowsBody(int iResponseCode)
		{
			return 204 != iResponseCode && 205 != iResponseCode && 304 != iResponseCode && (iResponseCode <= 99 || iResponseCode >= 200);
		}
		public static bool IsRedirectStatus(int iResponseCode)
		{
			return iResponseCode == 301 || iResponseCode == 302 || iResponseCode == 303 || iResponseCode == 307 || iResponseCode == 308;
		}
		internal static bool HasImageFileExtension(string sExt)
		{
			return sExt.EndsWith(".gif") || sExt.EndsWith(".jpg") || sExt.EndsWith(".jpeg") || sExt.EndsWith(".png") || sExt.EndsWith(".webp") || sExt.EndsWith(".ico");
		}
		public static bool IsBinaryMIME(string sContentType)
		{
			if (string.IsNullOrEmpty(sContentType))
			{
				return false;
			}
			if (sContentType.OICStartsWith("image/"))
			{
				return !sContentType.OICStartsWith("image/svg+xml");
			}
			return sContentType.OICStartsWith("audio/") || sContentType.OICStartsWith("video/") || (!sContentType.OICStartsWith("text/") && (sContentType.OICContains("msbin1") || sContentType.OICStartsWith("application/octet") || sContentType.OICStartsWith("application/x-shockwave")));
		}
		[CodeDescription("Gets a string from a byte-array, stripping a BOM if present.")]
		public static string GetStringFromArrayRemovingBOM(byte[] arrInput, Encoding oDefaultEncoding)
		{
			if (arrInput == null)
			{
				return string.Empty;
			}
			if (arrInput.Length < 2)
			{
				return oDefaultEncoding.GetString(arrInput);
			}
			Encoding[] array = Utilities.sniffableEncodings;
			for (int i = 0; i < array.Length; i++)
			{
				Encoding encoding = array[i];
				byte[] preamble = encoding.GetPreamble();
				if (arrInput.Length >= preamble.Length)
				{
					bool flag = preamble.Length > 0;
					int j = 0;
					while (j < preamble.Length)
					{
						if (preamble[j] != arrInput[j])
						{
							flag = false;
							IL_66:
							if (!flag)
							{
								goto IL_69;
							}
							int num = encoding.GetPreamble().Length;
							return encoding.GetString(arrInput, num, arrInput.Length - num);
						}
						else
						{
							j++;
						}
					}
                    if (!flag)
                    {
                        goto IL_69;
                    }
                    int num1 = encoding.GetPreamble().Length;
                    return encoding.GetString(arrInput, num1, arrInput.Length - num1);
				}
				IL_69:;
			}
			return oDefaultEncoding.GetString(arrInput);
		}
		[CodeDescription("Gets (via Headers or Sniff) the provided body's text Encoding. Returns CONFIG.oHeaderEncoding (usually UTF-8) if unknown. Potentially slow.")]
		public static Encoding getEntityBodyEncoding(HTTPHeaders oHeaders, byte[] oBody)
		{
			if (oHeaders != null)
			{
				string tokenValue = oHeaders.GetTokenValue("Content-Type", "charset");
				if (tokenValue != null)
				{
					Encoding encoding;
					try
					{
						encoding = Encoding.GetEncoding(tokenValue);
					}
					catch (Exception)
					{
						goto IL_28;
					}
					return encoding;
				}
			}
			IL_28:
			Encoding encoding2 = CONFIG.oHeaderEncoding;
			if (oBody != null && oBody.Length >= 2)
			{
				Encoding[] array = Utilities.sniffableEncodings;
				for (int i = 0; i < array.Length; i++)
				{
					Encoding encoding3 = array[i];
					byte[] preamble = encoding3.GetPreamble();
					if (oBody.Length >= preamble.Length)
					{
						bool flag = preamble.Length > 0;
						int j = 0;
						while (j < preamble.Length)
						{
							if (preamble[j] != oBody[j])
							{
								flag = false;
								IL_8F:
								if (!flag)
								{
									goto IL_93;
								}
								encoding2 = encoding3;
								goto IL_A6;
							}
							else
							{
								j++;
							}
						}
                        if (!flag)
                        {
                            goto IL_93;
                        }
                        encoding2 = encoding3;
                        goto IL_A6;
						IL_A6:
						if (oHeaders != null && oHeaders.Exists("Content-Type"))
						{
							if (oHeaders.ExistsAndContains("Content-Type", "multipart/form-data"))
							{
								string @string = encoding2.GetString(oBody, 0, Math.Min(8192, oBody.Length));
								Regex regex = new Regex(".*Content-Disposition: form-data; name=\"_charset_\"\\s+(?<thecharset>[^\\s'&>\\\"]*)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);
								MatchCollection matchCollection = regex.Matches(@string);
								if (matchCollection.Count > 0 && matchCollection[0].Groups.Count > 0)
								{
									try
									{
										string value = matchCollection[0].Groups[1].Value;
										Encoding encoding4 = Encoding.GetEncoding(value);
										encoding2 = encoding4;
									}
									catch (Exception)
									{
									}
								}
							}
							if (oHeaders.ExistsAndContains("Content-Type", "application/x-www-form-urlencoded"))
							{
								string string2 = encoding2.GetString(oBody, 0, Math.Min(4096, oBody.Length));
								Regex regex2 = new Regex(".*_charset_=(?<thecharset>[^'&>\\\"]*)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);
								MatchCollection matchCollection2 = regex2.Matches(string2);
								if (matchCollection2.Count > 0 && matchCollection2[0].Groups.Count > 0)
								{
									try
									{
										string value2 = matchCollection2[0].Groups[1].Value;
										Encoding encoding5 = Encoding.GetEncoding(value2);
										encoding2 = encoding5;
									}
									catch (Exception)
									{
									}
								}
							}
							if (oHeaders.ExistsAndContains("Content-Type", "html"))
							{
								string string3 = encoding2.GetString(oBody, 0, Math.Min(4096, oBody.Length));
								Regex regex3 = new Regex("<meta\\s.*charset\\s*=\\s*['\\\"]?(?<thecharset>[^'>\\\"]*)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);
								MatchCollection matchCollection3 = regex3.Matches(string3);
								if (matchCollection3.Count > 0 && matchCollection3[0].Groups.Count > 0)
								{
									try
									{
										string value3 = matchCollection3[0].Groups[1].Value;
										Encoding encoding6 = Encoding.GetEncoding(value3);
										if (encoding6 != encoding2 && (encoding2 != Encoding.UTF8 || (encoding6 != Encoding.BigEndianUnicode && encoding6 != Encoding.Unicode && encoding6 != Encoding.UTF32)) && (encoding6 != Encoding.UTF8 || (encoding2 != Encoding.BigEndianUnicode && encoding2 != Encoding.Unicode && encoding2 != Encoding.UTF32)))
										{
											encoding2 = encoding6;
										}
									}
									catch (Exception)
									{
									}
								}
							}
						}
						return encoding2;
					}
					IL_93:;
				}
				//goto IL_A6;
			}
			return encoding2;
		}
		[CodeDescription("Gets (via Headers or Sniff) the Response Text Encoding. Returns CONFIG.oHeaderEncoding (usually UTF-8) if unknown. Potentially slow.")]
		public static Encoding getResponseBodyEncoding(Session oSession)
		{
			if (oSession == null)
			{
				return CONFIG.oHeaderEncoding;
			}
			if (!oSession.bHasResponse)
			{
				return CONFIG.oHeaderEncoding;
			}
			return Utilities.getEntityBodyEncoding(oSession.oResponse.headers, oSession.responseBodyBytes);
		}
		public static string HtmlEncode(string sInput)
		{
			if (sInput == null)
			{
				return null;
			}
			StringBuilder stringBuilder = new StringBuilder(sInput.Length);
			int length = sInput.Length;
			for (int i = 0; i < length; i++)
			{
				char c = sInput[i];
				if (c != '"')
				{
					if (c != '&')
					{
						switch (c)
						{
						case '<':
							stringBuilder.Append("&lt;");
							goto IL_D8;
						case '>':
							stringBuilder.Append("&gt;");
							goto IL_D8;
						}
						if (sInput[i] > '\u009f')
						{
							stringBuilder.Append("&#");
							stringBuilder.Append(((int)sInput[i]).ToString(NumberFormatInfo.InvariantInfo));
							stringBuilder.Append(";");
						}
						else
						{
							stringBuilder.Append(sInput[i]);
						}
					}
					else
					{
						stringBuilder.Append("&amp;");
					}
				}
				else
				{
					stringBuilder.Append("&quot;");
				}
				IL_D8:;
			}
			return stringBuilder.ToString();
		}
		private static int HexToByte(char char_0)
		{
			if (char_0 >= '0' && char_0 <= '9')
			{
				return (int)(char_0 - '0');
			}
			if (char_0 >= 'a' && char_0 <= 'f')
			{
				return (int)(char_0 - 'a' + '\n');
			}
			if (char_0 >= 'A' && char_0 <= 'F')
			{
				return (int)(char_0 - 'A' + '\n');
			}
			return -1;
		}
		private static bool IsHexDigit(char char_0)
		{
			return (char_0 >= '0' && char_0 <= '9') || (char_0 >= 'A' && char_0 <= 'F') || (char_0 >= 'a' && char_0 <= 'f');
		}
		private static string GetUTF8HexString(string sInput, ref int iX)
		{
			MemoryStream memoryStream = new MemoryStream();
			do
			{
				if (iX > sInput.Length - 2)
				{
					memoryStream.WriteByte(37);
					iX += 2;
				}
				else
				{
					if (Utilities.IsHexDigit(sInput[iX + 1]) && Utilities.IsHexDigit(sInput[iX + 2]))
					{
						byte value = (byte)((Utilities.HexToByte(sInput[iX + 1]) << 4) + Utilities.HexToByte(sInput[iX + 2]));
						memoryStream.WriteByte(value);
						iX += 3;
					}
					else
					{
						memoryStream.WriteByte(37);
						iX++;
					}
				}
				if (iX >= sInput.Length)
				{
					break;
				}
			}
			while ('%' == sInput[iX]);
			iX--;
			return Encoding.UTF8.GetString(memoryStream.ToArray());
		}
		public static string UrlDecode(string sInput)
		{
			if (string.IsNullOrEmpty(sInput))
			{
				return string.Empty;
			}
			if (sInput.IndexOf('%') < 0)
			{
				return sInput;
			}
			StringBuilder stringBuilder = new StringBuilder(sInput.Length);
			for (int i = 0; i < sInput.Length; i++)
			{
				if ('%' == sInput[i])
				{
					stringBuilder.Append(Utilities.GetUTF8HexString(sInput, ref i));
				}
				else
				{
					stringBuilder.Append(sInput[i]);
				}
			}
			return stringBuilder.ToString();
		}
		private static string UrlEncodeChars(string string_0, Encoding oEnc)
		{
			if (string.IsNullOrEmpty(string_0))
			{
				return string_0;
			}
			StringBuilder stringBuilder = new StringBuilder();
			int i = 0;
			while (i < string_0.Length)
			{
				char c = string_0[i];
				if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '.' || c == '(' || c == ')' || c == '*' || c == '\'' || c == '_')
				{
					goto IL_DD;
				}
				if (c == '!')
				{
					goto IL_DD;
				}
				if (c == ' ')
				{
					stringBuilder.Append("+");
				}
				else
				{
					byte[] bytes = oEnc.GetBytes(new char[]
					{
						c
					});
					byte[] array = bytes;
					for (int j = 0; j < array.Length; j++)
					{
						byte b = array[j];
						stringBuilder.Append("%");
						stringBuilder.Append(b.ToString("X2"));
					}
				}
				IL_E5:
				i++;
				continue;
				IL_DD:
				stringBuilder.Append(c);
				goto IL_E5;
			}
			return stringBuilder.ToString();
		}
		public static string UrlEncode(string sInput)
		{
			return Utilities.UrlEncodeChars(sInput, Encoding.UTF8);
		}
		public static string UrlEncode(string sInput, Encoding oEnc)
		{
			return Utilities.UrlEncodeChars(sInput, oEnc);
		}
		private static string UrlPathEncodeChars(string string_0)
		{
			if (string.IsNullOrEmpty(string_0))
			{
				return string_0;
			}
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < string_0.Length; i++)
			{
				char c = string_0[i];
				if (c > ' ' && c < '\u007f')
				{
					stringBuilder.Append(c);
				}
				else
				{
					if (c < '!')
					{
						stringBuilder.Append("%");
						stringBuilder.Append(((byte)c).ToString("X2"));
					}
					else
					{
						byte[] bytes = Encoding.UTF8.GetBytes(new char[]
						{
							c
						});
						byte[] array = bytes;
						for (int j = 0; j < array.Length; j++)
						{
							byte b = array[j];
							stringBuilder.Append("%");
							stringBuilder.Append(b.ToString("X2"));
						}
					}
				}
			}
			return stringBuilder.ToString();
		}
		public static string UrlPathEncode(string string_0)
		{
			if (string.IsNullOrEmpty(string_0))
			{
				return string_0;
			}
			int num = string_0.IndexOf('?');
			if (num >= 0)
			{
				return Utilities.UrlPathEncode(string_0.Substring(0, num)) + string_0.Substring(num);
			}
			return Utilities.UrlPathEncodeChars(string_0);
		}
		[CodeDescription("Tokenize a string into tokens. Delimits on whitespace; \" marks are dropped unless preceded by \\ characters.")]
		public static string[] Parameterize(string sInput)
		{
			List<string> list = new List<string>();
			bool flag = false;
			StringBuilder stringBuilder = new StringBuilder();
			int i = 0;
			while (i < sInput.Length)
			{
				char c = sInput[i];
				if (c != '\t')
				{
					switch (c)
					{
					case ' ':
						goto IL_7D;
					case '!':
						IL_3A:
						stringBuilder.Append(sInput[i]);
						goto IL_BD;
					case '"':
						if (i > 0 && sInput[i - 1] == '\\')
						{
							stringBuilder.Remove(stringBuilder.Length - 1, 1);
							stringBuilder.Append('"');
							goto IL_BD;
						}
						flag = !flag;
						goto IL_BD;
					}
                    stringBuilder.Append(sInput[i]);
                    goto IL_BD;
				}
				goto IL_7D;
				IL_BD:
				i++;
				continue;
				IL_7D:
				if (flag)
				{
					stringBuilder.Append(sInput[i]);
					goto IL_BD;
				}
				if (stringBuilder.Length > 0 || (i > 0 && sInput[i - 1] == '"'))
				{
					list.Add(stringBuilder.ToString());
					stringBuilder.Length = 0;
					goto IL_BD;
				}
				goto IL_BD;
			}
			if (stringBuilder.Length > 0)
			{
				list.Add(stringBuilder.ToString());
			}
			return list.ToArray();
		}
		[CodeDescription("Returns a string representing a Hex view of a byte array. Slow.")]
		public static string ByteArrayToHexView(byte[] inArr, int iBytesPerLine)
		{
			return Utilities.ByteArrayToHexView(inArr, iBytesPerLine, inArr.Length, true);
		}
		[CodeDescription("Returns a string representing a Hex view of a byte array. PERF: Slow.")]
		public static string ByteArrayToHexView(byte[] inArr, int iBytesPerLine, int iMaxByteCount)
		{
			return Utilities.ByteArrayToHexView(inArr, iBytesPerLine, iMaxByteCount, true);
		}
		[CodeDescription("Returns a string representing a Hex view of a byte array. PERF: Slow.")]
		public static string ByteArrayToHexView(byte[] inArr, int iBytesPerLine, int iMaxByteCount, bool bShowASCII)
		{
			return Utilities.ByteArrayToHexView(inArr, 0, iBytesPerLine, iMaxByteCount, bShowASCII);
		}
		[CodeDescription("Returns a string representing a Hex view of a byte array. PERF: Slow.")]
		public static string ByteArrayToHexView(byte[] inArr, int iStartAt, int iBytesPerLine, int iMaxByteCount, bool bShowASCII)
		{
			if (inArr == null || inArr.Length == 0)
			{
				return string.Empty;
			}
			if (iBytesPerLine >= 1 && iMaxByteCount >= 1)
			{
				iMaxByteCount = Math.Min(iMaxByteCount, inArr.Length);
				StringBuilder stringBuilder = new StringBuilder(iMaxByteCount * 5);
				for (int i = iStartAt; i < iMaxByteCount; i += iBytesPerLine)
				{
					int num = Math.Min(iBytesPerLine, iMaxByteCount - i);
					bool flag = num < iBytesPerLine;
					for (int j = 0; j < num; j++)
					{
						stringBuilder.Append(inArr[i + j].ToString("X2"));
						stringBuilder.Append(" ");
					}
					if (flag)
					{
						stringBuilder.Append(new string(' ', 3 * (iBytesPerLine - num)));
					}
					if (bShowASCII)
					{
						stringBuilder.Append(" ");
						for (int k = 0; k < num; k++)
						{
							if (inArr[i + k] < 32)
							{
								stringBuilder.Append(".");
							}
							else
							{
								stringBuilder.Append((char)inArr[i + k]);
							}
						}
						if (flag)
						{
							stringBuilder.Append(new string(' ', iBytesPerLine - num));
						}
					}
					stringBuilder.Append("\r\n");
				}
				return stringBuilder.ToString();
			}
			return string.Empty;
		}
		[CodeDescription("Returns a string representing a Hex stream of a byte array. Slow.")]
		public static string ByteArrayToString(byte[] inArr)
		{
			if (inArr == null)
			{
				return "null";
			}
			if (inArr.Length == 0)
			{
				return "empty";
			}
			return BitConverter.ToString(inArr).Replace('-', ' ');
		}
		internal static string StringToCF_HTML(string inStr)
		{
			string text = "<HTML><HEAD><STYLE>.REQUEST { font: 8pt Courier New; color: blue;} .RESPONSE { font: 8pt Courier New; color: green;}</STYLE></HEAD><BODY>" + inStr + "</BODY></HTML>";
			string text2 = "Version:1.0\r\nStartHTML:{0:00000000}\r\nEndHTML:{1:00000000}\r\nStartFragment:{0:00000000}\r\nEndFragment:{1:00000000}\r\n";
			return string.Format(text2, text2.Length - 16, text.Length + text2.Length - 16) + text;
		}
		[CodeDescription("Returns an integer from the registry, or iDefault if the registry key is missing or cannot be used as an integer.")]
		public static int GetRegistryInt(RegistryKey oReg, string sName, int iDefault)
		{
			int result = iDefault;
			object value = oReg.GetValue(sName);
			if (value is int)
			{
				result = (int)value;
			}
			else
			{
				string text = value as string;
				if (text != null && !int.TryParse(text, out result))
				{
					return iDefault;
				}
			}
			return result;
		}
		[CodeDescription("Save a string to the registry. Correctly handles null Value, saving as String.Empty.")]
		public static void SetRegistryString(RegistryKey oReg, string sName, string sValue)
		{
			if (sName == null)
			{
				return;
			}
			if (sValue == null)
			{
				sValue = string.Empty;
			}
			oReg.SetValue(sName, sValue);
		}
		[CodeDescription("Returns an float from the registry, or flDefault if the registry key is missing or cannot be used as an float.")]
		public static float GetRegistryFloat(RegistryKey oReg, string sName, float flDefault)
		{
			float result = flDefault;
			object value = oReg.GetValue(sName);
			if (value is int)
			{
				result = (float)value;
			}
			else
			{
				string text = value as string;
				if (text != null && !float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
				{
					result = flDefault;
				}
			}
			return result;
		}
		[CodeDescription("Returns an bool from the registry, or bDefault if the registry key is missing or cannot be used as an bool.")]
		public static bool GetRegistryBool(RegistryKey oReg, string sName, bool bDefault)
		{
			bool result = bDefault;
			object value = oReg.GetValue(sName);
			if (value is int)
			{
				result = (1 == (int)value);
			}
			else
			{
				string text = value as string;
				if (text != null)
				{
					result = "true".OICEquals(text);
				}
			}
			return result;
		}
		internal static string FileExtensionForMIMEType(string sMIME)
		{
			if (!string.IsNullOrEmpty(sMIME) && sMIME.Length <= 255)
			{
				sMIME = sMIME.ToLower();
				string key;
				switch (key = sMIME)
				{
				case "text/css":
					return ".css";
				case "text/html":
					return ".htm";
				case "text/javascript":
				case "application/javascript":
				case "application/x-javascript":
					return ".js";
				case "text/cache-manifest":
					return ".appcache";
				case "image/jpg":
				case "image/jpeg":
					return ".jpg";
				case "image/gif":
					return ".gif";
				case "image/png":
					return ".png";
				case "image/x-icon":
					return ".ico";
				case "text/xml":
					return ".xml";
				case "video/x-flv":
					return ".flv";
				case "video/mp4":
					return ".mp4";
				case "text/plain":
				case "application/octet-stream":
					return ".txt";
				}
				try
				{
					RegistryKey registryKey = Registry.ClassesRoot.OpenSubKey(string.Format("\\MIME\\Database\\Content Type\\{0}", sMIME), RegistryKeyPermissionCheck.ReadSubTree);
					if (registryKey != null)
					{
						string text = (string)registryKey.GetValue("Extension");
						registryKey.Close();
						if (!string.IsNullOrEmpty(text))
						{
							string result = text;
							return result;
						}
					}
				}
				catch
				{
				}
				if (sMIME.EndsWith("+xml"))
				{
					return ".xml";
				}
				return ".txt";
			}
			return ".txt";
		}
		internal static string ContentTypeForFilename(string sFilename)
		{
			string sExtension = string.Empty;
			string result;
			try
			{
				sExtension = Path.GetExtension(sFilename);
				goto IL_1A;
			}
			catch (Exception)
			{
				result = "application/octet-stream";
			}
			return result;
			IL_1A:
			string text = Utilities.ContentTypeForFileExtension(sExtension);
			if (string.IsNullOrEmpty(text))
			{
				return "application/octet-stream";
			}
			return text;
		}
		internal static string ContentTypeForFileExtension(string sExtension)
		{
			if (string.IsNullOrEmpty(sExtension) || sExtension.Length > 255)
			{
				return null;
			}
			if (sExtension == ".js")
			{
				return "text/javascript";
			}
			if (sExtension == ".json")
			{
				return "application/json";
			}
			if (sExtension == ".css")
			{
				return "text/css";
			}
			if (sExtension == ".htm")
			{
				return "text/html";
			}
			if (sExtension == ".html")
			{
				return "text/html";
			}
			if (sExtension == ".appcache")
			{
				return "text/cache-manifest";
			}
			if (sExtension == ".flv")
			{
				return "video/x-flv";
			}
			string text = null;
			try
			{
				RegistryKey registryKey = Registry.ClassesRoot.OpenSubKey(sExtension, RegistryKeyPermissionCheck.ReadSubTree);
				if (registryKey != null)
				{
					text = (string)registryKey.GetValue("Content Type");
					if (string.IsNullOrEmpty(text))
					{
						string text2 = (string)registryKey.GetValue("");
						if (!string.IsNullOrEmpty(text2))
						{
							RegistryKey registryKey2 = Registry.ClassesRoot.OpenSubKey(text2, RegistryKeyPermissionCheck.ReadSubTree);
							if (registryKey2 != null)
							{
								text = (string)registryKey2.GetValue("Content Type");
								registryKey2.Close();
							}
						}
					}
					registryKey.Close();
				}
			}
			catch (SecurityException)
			{
			}
			catch (Exception eX)
			{
				FiddlerApplication.ReportException(eX, "Registry Failure");
			}
			return text;
		}
		internal static bool IsChunkedBodyComplete(Session m_session, byte[] oRawBuffer, long iStartAtOffset, long iEndAtOffset, out long outStartOfLatestChunk, out long outEndOfEntity)
		{
			int num = (int)iStartAtOffset;
			outStartOfLatestChunk = (long)num;
			outEndOfEntity = -1L;
			while ((long)num < iEndAtOffset)
			{
				outStartOfLatestChunk = (long)num;
				string text = Encoding.ASCII.GetString(oRawBuffer, num, Math.Min(64, (int)(iEndAtOffset - (long)num)));
				int num2 = text.IndexOf("\r\n", StringComparison.Ordinal);
				if (num2 <= -1)
				{
					return false;
				}
				num += num2 + 2;
				text = text.Substring(0, num2);
				num2 = text.IndexOf(';');
				if (num2 > -1)
				{
					text = text.Substring(0, num2);
				}
				int num3 = 0;
				if (!Utilities.TryHexParse(text, out num3))
				{
					if (m_session != null)
					{
						SessionFlags flagViolation = (m_session.state <= SessionStates.ReadingRequest) ? SessionFlags.ProtocolViolationInRequest : SessionFlags.ProtocolViolationInResponse;
						FiddlerApplication.HandleHTTPError(m_session, flagViolation, true, true, "Illegal chunked encoding. '" + text + "' is not a hexadecimal number.");
					}
					return true;
				}
				if (num3 != 0)
				{
					num += num3 + 2;
				}
				else
				{
					bool flag = true;
					bool flag2 = false;
					if ((long)(num + 2) > iEndAtOffset)
					{
						return false;
					}
					int num4 = (int)oRawBuffer[num++];
					while ((long)num <= iEndAtOffset)
					{
						int num5 = num4;
						if (num5 != 10)
						{
							if (num5 == 13)
							{
								flag2 = true;
							}
							else
							{
								flag2 = false;
								flag = false;
							}
						}
						else
						{
							if (flag2)
							{
								if (flag)
								{
									outEndOfEntity = (long)num;
									return true;
								}
								flag = true;
								flag2 = false;
							}
							else
							{
								flag2 = false;
								flag = false;
							}
						}
						num4 = (int)oRawBuffer[num++];
					}
					return false;
				}
			}
			return false;
		}
		internal static bool IsChunkedBodyComplete(Session m_session, MemoryStream oData, long iStartAtOffset, out long outStartOfLatestChunk, out long outEndOfEntity)
		{
			return Utilities.IsChunkedBodyComplete(m_session, oData.GetBuffer(), iStartAtOffset, oData.Length, out outStartOfLatestChunk, out outEndOfEntity);
		}
		private static void _WriteChunkSizeToStream(MemoryStream oMS, int iLen)
		{
			string s = iLen.ToString("x");
			byte[] bytes = Encoding.ASCII.GetBytes(s);
			oMS.Write(bytes, 0, bytes.Length);
		}
		private static void _WriteCRLFToStream(MemoryStream oMS)
		{
			oMS.WriteByte(13);
			oMS.WriteByte(10);
		}
		public static byte[] doChunk(byte[] writeData, int iSuggestedChunkCount)
		{
			if (writeData != null && writeData.Length >= 1)
			{
				if (iSuggestedChunkCount < 1)
				{
					iSuggestedChunkCount = 1;
				}
				if (iSuggestedChunkCount > writeData.Length)
				{
					iSuggestedChunkCount = writeData.Length;
				}
				MemoryStream memoryStream = new MemoryStream(writeData.Length + 10 * iSuggestedChunkCount);
				int num = 0;
				do
				{
					int num2 = writeData.Length - num;
					int num3 = num2 / iSuggestedChunkCount;
					num3 = Math.Max(1, num3);
					num3 = Math.Min(num2, num3);
					Utilities._WriteChunkSizeToStream(memoryStream, num3);
					Utilities._WriteCRLFToStream(memoryStream);
					memoryStream.Write(writeData, num, num3);
					Utilities._WriteCRLFToStream(memoryStream);
					num += num3;
					iSuggestedChunkCount--;
					if (iSuggestedChunkCount < 1)
					{
						iSuggestedChunkCount = 1;
					}
				}
				while (num < writeData.Length);
				Utilities._WriteChunkSizeToStream(memoryStream, 0);
				Utilities._WriteCRLFToStream(memoryStream);
				Utilities._WriteCRLFToStream(memoryStream);
				return memoryStream.ToArray();
			}
			return Encoding.ASCII.GetBytes("0\r\n\r\n");
		}
		public static byte[] doUnchunk(byte[] writeData)
		{
			if (writeData != null && writeData.Length != 0)
			{
				MemoryStream memoryStream = new MemoryStream(writeData.Length);
				int num = 0;
				bool flag = false;
				while (!flag && num <= writeData.Length - 3)
				{
					string text = Encoding.ASCII.GetString(writeData, num, Math.Min(64, writeData.Length - num));
					int num2 = text.IndexOf("\r\n", StringComparison.Ordinal);
					if (num2 <= 0)
					{
						throw new InvalidDataException("HTTP Error: The chunked content is corrupt. Cannot find Chunk-Length in expected location. Offset: " + num.ToString());
					}
					num += num2 + 2;
					text = text.Substring(0, num2);
					num2 = text.IndexOf(';');
					if (num2 > 0)
					{
						text = text.Substring(0, num2);
					}
					int num3;
					if (!Utilities.TryHexParse(text, out num3))
					{
						throw new InvalidDataException("HTTP Error: The chunked content is corrupt. Chunk Length was malformed. Offset: " + num.ToString());
					}
					if (num3 == 0)
					{
						flag = true;
					}
					else
					{
						if (writeData.Length < num3 + num)
						{
							throw new InvalidDataException("HTTP Error: The chunked entity body is corrupt. The final chunk length is greater than the number of bytes remaining.");
						}
						memoryStream.Write(writeData, num, num3);
						num += num3 + 2;
					}
				}
				byte[] array = new byte[memoryStream.Length];
				Buffer.BlockCopy(memoryStream.GetBuffer(), 0, array, 0, array.Length);
				return array;
			}
			return Utilities.emptyByteArray;
		}
		internal static bool arrayContainsNonText(byte[] arrIn)
		{
			if (arrIn == null)
			{
				return false;
			}
			for (int i = 0; i < arrIn.Length; i++)
			{
				if (arrIn[i] == 0)
				{
					return true;
				}
			}
			return false;
		}
		public static bool isUnsupportedEncoding(string sTE, string sCE)
		{
			return (!string.IsNullOrEmpty(sTE) && sTE.OICContains("xpress")) || (!string.IsNullOrEmpty(sCE) && sCE.OICContains("xpress"));
		}
		private static void _DecodeInOrder(string sEncodingsInOrder, bool bAllowChunks, ref byte[] arrBody)
		{
			if (string.IsNullOrEmpty(sEncodingsInOrder))
			{
				return;
			}
			string[] array = sEncodingsInOrder.ToLower().Split(new char[]
			{
				','
			});
			int i = array.Length - 1;
			while (i >= 0)
			{
				string text = array[i].Trim();
				string a;
				if ((a = text) == null)
				{
					goto IL_F0;
				}
				if (!(a == "gzip"))
				{
					if (!(a == "deflate"))
					{
						if (!(a == "bzip2"))
						{
							if (!(a == "chunked"))
							{
								goto IL_F0;
							}
							if (bAllowChunks)
							{
								if (i != array.Length - 1)
								{
									FiddlerApplication.Log.LogFormat("!Chunked Encoding must be the LAST Transfer-Encoding applied!", new object[]
									{
										sEncodingsInOrder
									});
								}
								arrBody = Utilities.doUnchunk(arrBody);
							}
							else
							{
								FiddlerApplication.Log.LogFormat("!Chunked encoding is permitted only in the Transfer-Encoding header. Content-Encoding: {0}", new object[]
								{
									text
								});
							}
						}
						else
						{
							arrBody = Utilities.bzip2Expand(arrBody, true);
						}
					}
					else
					{
						arrBody = Utilities.DeflaterExpand(arrBody, true);
					}
				}
				else
				{
					arrBody = Utilities.GzipExpand(arrBody, true);
				}
				IL_10E:
				i--;
				continue;
				IL_F0:
				FiddlerApplication.Log.LogFormat("!Cannot decode HTTP response using Encoding: {0}", new object[]
				{
					text
				});
				goto IL_10E;
			}
		}
		public static void utilDecodeHTTPBody(HTTPHeaders oHeaders, ref byte[] arrBody)
		{
			if (!Utilities.IsNullOrEmpty(arrBody))
			{
				Utilities._DecodeInOrder(oHeaders["Transfer-Encoding"], true, ref arrBody);
				Utilities._DecodeInOrder(oHeaders["Content-Encoding"], false, ref arrBody);
			}
		}
		public static byte[] ZLibExpand(byte[] compressedData)
		{
			if (compressedData != null && compressedData.Length != 0)
			{
				throw new NotSupportedException("This application was compiled without ZLib support.");
			}
			return Utilities.emptyByteArray;
		}
		[CodeDescription("Returns a byte[] containing a gzip-compressed copy of writeData[]")]
		public static byte[] GzipCompress(byte[] writeData)
		{
			byte[] result;
			try
			{
				MemoryStream memoryStream = new MemoryStream();
				using (GZipStream gZipStream = new GZipStream(memoryStream, CompressionMode.Compress))
				{
					gZipStream.Write(writeData, 0, writeData.Length);
				}
				result = memoryStream.ToArray();
			}
			catch (Exception ex)
			{
				FiddlerApplication.DoNotifyUser("The content could not be compressed.\n\n" + ex.Message, "Fiddler: GZip failed");
				result = writeData;
			}
			return result;
		}
		public static byte[] GzipExpandInternal(bool bUseXceed, byte[] compressedData)
		{
			if (compressedData == null || compressedData.Length == 0)
			{
				return Utilities.emptyByteArray;
			}
			MemoryStream stream = new MemoryStream(compressedData);
			MemoryStream memoryStream = new MemoryStream(compressedData.Length);
			if (bUseXceed)
			{
				throw new NotSupportedException("This application was compiled without Xceed support.");
			}
			using (GZipStream gZipStream = new GZipStream(stream, CompressionMode.Decompress))
			{
				byte[] array = new byte[32768];
				int count;
				while ((count = gZipStream.Read(array, 0, array.Length)) > 0)
				{
					memoryStream.Write(array, 0, count);
				}
			}
			return memoryStream.ToArray();
		}
		[CodeDescription("Returns a byte[] containing an un-gzipped copy of compressedData[]")]
		public static byte[] GzipExpand(byte[] compressedData)
		{
			return Utilities.GzipExpand(compressedData, false);
		}
		public static byte[] GzipExpand(byte[] compressedData, bool bThrowErrors)
		{
			byte[] result;
			try
			{
				result = Utilities.GzipExpandInternal(CONFIG.bUseXceedDecompressForGZIP, compressedData);
			}
			catch (Exception ex)
			{
				if (bThrowErrors)
				{
					throw new InvalidDataException("The content could not be ungzipped", ex);
				}
				FiddlerApplication.DoNotifyUser("The content could not be decompressed.\n\n" + ex.Message, "Fiddler: UnGZip failed");
				result = Utilities.emptyByteArray;
			}
			return result;
		}
		[CodeDescription("Returns a byte[] containing a DEFLATE'd copy of writeData[]")]
		public static byte[] DeflaterCompress(byte[] writeData)
		{
			if (writeData != null && writeData.Length != 0)
			{
				byte[] result;
				try
				{
					MemoryStream memoryStream = new MemoryStream();
					using (DeflateStream deflateStream = new DeflateStream(memoryStream, CompressionMode.Compress))
					{
						deflateStream.Write(writeData, 0, writeData.Length);
					}
					result = memoryStream.ToArray();
				}
				catch (Exception ex)
				{
					FiddlerApplication.DoNotifyUser("The content could not be compressed.\n\n" + ex.Message, "Fiddler: Deflation failed");
					result = writeData;
				}
				return result;
			}
			return Utilities.emptyByteArray;
		}
		public static byte[] DeflaterExpandInternal(bool bUseXceed, byte[] compressedData)
		{
			if (compressedData == null || compressedData.Length == 0)
			{
				return Utilities.emptyByteArray;
			}
			int num = 0;
			if (compressedData.Length > 2 && compressedData[0] == 120 && compressedData[1] == 156)
			{
				num = 2;
			}
			if (bUseXceed)
			{
				throw new NotSupportedException("This application was compiled without Xceed support.");
			}
			MemoryStream stream = new MemoryStream(compressedData, num, compressedData.Length - num);
			MemoryStream memoryStream = new MemoryStream(compressedData.Length);
			using (DeflateStream deflateStream = new DeflateStream(stream, CompressionMode.Decompress))
			{
				byte[] array = new byte[32768];
				int count;
				while ((count = deflateStream.Read(array, 0, array.Length)) > 0)
				{
					memoryStream.Write(array, 0, count);
				}
			}
			return memoryStream.ToArray();
		}
		[CodeDescription("Returns a byte[] representing the INFLATE'd representation of compressedData[]")]
		public static byte[] DeflaterExpand(byte[] compressedData)
		{
			return Utilities.DeflaterExpand(compressedData, false);
		}
		public static byte[] DeflaterExpand(byte[] compressedData, bool bThrowErrors)
		{
			byte[] result;
			try
			{
				result = Utilities.DeflaterExpandInternal(CONFIG.bUseXceedDecompressForDeflate, compressedData);
			}
			catch (Exception ex)
			{
				if (bThrowErrors)
				{
					throw new InvalidDataException("The content could not be inFlated", ex);
				}
				FiddlerApplication.DoNotifyUser("The content could not be decompressed.\n\n" + ex.Message, "Fiddler: Inflation failed");
				result = Utilities.emptyByteArray;
			}
			return result;
		}
		[CodeDescription("Returns a byte[] representing the bzip2'd representation of writeData[]")]
		public static byte[] bzip2Compress(byte[] writeData)
		{
			if (writeData != null && writeData.Length != 0)
			{
				throw new NotSupportedException("This application was compiled without BZIP2 support.");
			}
			return Utilities.emptyByteArray;
		}
		public static byte[] bzip2Expand(byte[] compressedData)
		{
			return Utilities.bzip2Expand(compressedData, false);
		}
		public static byte[] bzip2Expand(byte[] compressedData, bool bThrowErrors)
		{
			if (compressedData != null && compressedData.Length != 0)
			{
				throw new NotSupportedException("This application was compiled without BZIP2 support.");
			}
			return Utilities.emptyByteArray;
		}
		[CodeDescription("Try parsing the string for a Hex-formatted int. If it fails, return false and 0 in iOutput.")]
		public static bool TryHexParse(string sInput, out int iOutput)
		{
			return int.TryParse(sInput, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out iOutput);
		}
		public static bool areOriginsEquivalent(string sOrigin1, string sOrigin2, int iDefaultPort)
		{
			if (string.Equals(sOrigin1, sOrigin2, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			int num = iDefaultPort;
			string arg;
			Utilities.CrackHostAndPort(sOrigin1, out arg, ref num);
			string inStr = string.Format("{0}:{1}", arg, num);
			num = iDefaultPort;
			Utilities.CrackHostAndPort(sOrigin2, out arg, ref num);
			string toMatch = string.Format("{0}:{1}", arg, num);
			return inStr.OICEquals(toMatch);
		}
		[CodeDescription("Returns false if Hostname contains any dots or colons.")]
		public static bool isPlainHostName(string sHostAndPort)
		{
			int num = 0;
			string text;
			Utilities.CrackHostAndPort(sHostAndPort, out text, ref num);
			char[] anyOf = new char[]
			{
				'.',
				':'
			};
			return text.IndexOfAny(anyOf) < 0;
		}
		[CodeDescription("Returns true if True if the sHostAndPort's host is 127.0.0.1, 'localhost', or ::1. Note that list is not complete.")]
		public static bool isLocalhost(string sHostAndPort)
		{
			int num = 0;
			string sHostname;
			Utilities.CrackHostAndPort(sHostAndPort, out sHostname, ref num);
			return Utilities.isLocalhostname(sHostname);
		}
		[CodeDescription("Returns true if True if the sHostname is 127.0.0.1, 'localhost', or ::1. Note that list is not complete.")]
		public static bool isLocalhostname(string sHostname)
		{
			return "localhost".OICEquals(sHostname) || "127.0.0.1".Equals(sHostname) || "localhost.".OICEquals(sHostname) || "::1".Equals(sHostname);
		}
		[CodeDescription("This function cracks the Host/Port combo, removing IPV6 brackets if needed.")]
		public static void CrackHostAndPort(string sHostPort, out string sHostname, ref int iPort)
		{
			int num = sHostPort.LastIndexOf(':');
			if (num > -1 && num > sHostPort.LastIndexOf(']'))
			{
				if (!int.TryParse(sHostPort.Substring(num + 1), NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out iPort))
				{
					iPort = -1;
				}
				sHostname = sHostPort.Substring(0, num);
			}
			else
			{
				sHostname = sHostPort;
			}
			if (sHostname.StartsWith("[", StringComparison.Ordinal) && sHostname.EndsWith("]", StringComparison.Ordinal))
			{
				sHostname = sHostname.Substring(1, sHostname.Length - 2);
			}
		}
		public static IPEndPoint IPEndPointFromHostPortString(string sHostAndPort)
		{
			if (Utilities.IsNullOrWhiteSpace(sHostAndPort))
			{
				return null;
			}
			sHostAndPort = Utilities.TrimAfter(sHostAndPort, ';');
			IPEndPoint result;
			try
			{
				int port = 80;
				string sRemoteHost;
				Utilities.CrackHostAndPort(sHostAndPort, out sRemoteHost, ref port);
				IPAddress iPAddress = DNSResolver.GetIPAddress(sRemoteHost, true);
				IPEndPoint iPEndPoint = new IPEndPoint(iPAddress, port);
				result = iPEndPoint;
			}
			catch (Exception)
			{
				result = null;
			}
			return result;
		}
		public static IPEndPoint[] IPEndPointListFromHostPortString(string sAllHostAndPorts)
		{
			if (Utilities.IsNullOrWhiteSpace(sAllHostAndPorts))
			{
				return null;
			}
			string[] array = sAllHostAndPorts.Split(new char[]
			{
				';'
			}, StringSplitOptions.RemoveEmptyEntries);
			List<IPEndPoint> list = new List<IPEndPoint>();
			string[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				string sHostPort = array2[i];
				try
				{
					int port = 80;
					string sRemoteHost;
					Utilities.CrackHostAndPort(sHostPort, out sRemoteHost, ref port);
					IPAddress[] iPAddressList = DNSResolver.GetIPAddressList(sRemoteHost, true, null);
					IPAddress[] array3 = iPAddressList;
					for (int j = 0; j < array3.Length; j++)
					{
						IPAddress address = array3[j];
						list.Add(new IPEndPoint(address, port));
					}
				}
				catch (Exception)
				{
				}
			}
			if (list.Count < 1)
			{
				return null;
			}
			return list.ToArray();
		}
		[CodeDescription("This function attempts to be a ~fast~ way to return an IP from a hoststring that contains an IP-Literal. ")]
		public static IPAddress IPFromString(string sHost)
		{
			for (int i = 0; i < sHost.Length; i++)
			{
				if (sHost[i] != '.' && sHost[i] != ':' && (sHost[i] < '0' || sHost[i] > '9') && (sHost[i] < 'A' || sHost[i] > 'F') && (sHost[i] < 'a' || sHost[i] > 'f'))
				{
					return null;
				}
			}
			if (sHost.EndsWith("."))
			{
				sHost = Utilities.TrimBeforeLast(sHost, '.');
			}
			IPAddress result;
			try
			{
				result = IPAddress.Parse(sHost);
			}
			catch
			{
				result = null;
			}
			return result;
		}
		[CodeDescription("ShellExecutes the sURL.")]
		public static bool LaunchHyperlink(string sURL)
		{
			try
			{
				using (Process.Start(sURL))
				{
				}
				return true;
			}
			catch (Exception ex)
			{
				FiddlerApplication.DoNotifyUser("Your web browser is not correctly configured to launch hyperlinks.\n\nTo see this content, visit:\n\t" + sURL + "\n...in your web browser.\n\nError: " + ex.Message, "Error");
			}
			return false;
		}
		internal static bool LaunchBrowser(string sExe, string sParams, string sURL)
		{
			if (!string.IsNullOrEmpty(sParams))
			{
				sParams = sParams.Replace("%U", sURL);
			}
			else
			{
				sParams = sURL;
			}
			return Utilities.RunExecutable(sExe, sParams);
		}
		public static bool RunExecutable(string sExecute, string sParams)
		{
			try
			{
				using (Process.Start(sExecute, sParams))
				{
				}
				return true;
			}
			catch (Exception ex)
			{
				if (!(ex is Win32Exception) || 1223 != (ex as Win32Exception).NativeErrorCode)
				{
					FiddlerApplication.DoNotifyUser(string.Format("Failed to execute: {0}\r\n{1}\r\n\r\n{2}\r\n{3}", new object[]
					{
						sExecute,
						string.IsNullOrEmpty(sParams) ? string.Empty : ("with parameters: " + sParams),
						ex.Message,
						ex.StackTrace.ToString()
					}), "ShellExecute Failed");
				}
			}
			return false;
		}
		[CodeDescription("Run an executable and wait for it to exit.")]
		public static bool RunExecutableAndWait(string sExecute, string sParams)
		{
			bool result;
			try
			{
				Process process = new Process();
				process.StartInfo.FileName = sExecute;
				process.StartInfo.Arguments = sParams;
				process.Start();
				process.WaitForExit();
				process.Dispose();
				result = true;
			}
			catch (Exception ex)
			{
				if (!(ex is Win32Exception) || 1223 != (ex as Win32Exception).NativeErrorCode)
				{
					FiddlerApplication.DoNotifyUser("Fiddler Exception thrown: " + ex.ToString() + "\r\n" + ex.StackTrace.ToString(), "ShellExecute Failed");
				}
				result = false;
			}
			return result;
		}
		[CodeDescription("Run an executable, wait for it to exit, and return its output as a string.")]
		public static string GetExecutableOutput(string sExecute, string sParams, out int iExitCode)
		{
			iExitCode = -999;
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(string.Concat(new string[]
			{
				"Results from ",
				sExecute,
				" ",
				sParams,
				"\r\n\r\n"
			}));
			try
			{
				Process process = new Process();
				process.StartInfo.UseShellExecute = false;
				process.StartInfo.RedirectStandardOutput = true;
				process.StartInfo.RedirectStandardError = false;
				process.StartInfo.CreateNoWindow = true;
				process.StartInfo.FileName = sExecute;
				process.StartInfo.Arguments = sParams;
				process.Start();
				string text;
				while ((text = process.StandardOutput.ReadLine()) != null)
				{
					text = text.TrimEnd(new char[0]);
					if (text.Length > 0)
					{
						stringBuilder.AppendLine(text);
					}
				}
				iExitCode = process.ExitCode;
				process.Dispose();
			}
			catch (Exception ex)
			{
				stringBuilder.Append("Exception thrown: " + ex.ToString() + "\r\n" + ex.StackTrace.ToString());
			}
			stringBuilder.Append("-------------------------------------------\r\n");
			return stringBuilder.ToString();
		}
		[CodeDescription("Copy a string to the clipboard, with exception handling.")]
		public static bool CopyToClipboard(string sText)
		{
			DataObject dataObject = new DataObject();
			dataObject.SetData(DataFormats.Text, sText);
			return Utilities.CopyToClipboard(dataObject);
		}
		public static bool CopyToClipboard(DataObject oData)
		{
			bool result;
			try
			{
				Clipboard.SetDataObject(oData, true);
				result = true;
			}
			catch (Exception ex)
			{
				FiddlerApplication.DoNotifyUser("Please disable any clipboard monitoring tools and try again.\n\n" + ex.Message, ".NET Framework Bug");
				result = true;
			}
			return result;
		}
		internal static string RegExEscape(string sString, bool bAddPrefixCaret, bool bAddSuffixDollarSign)
		{
			StringBuilder stringBuilder = new StringBuilder();
			if (bAddPrefixCaret)
			{
				stringBuilder.Append("^");
			}
			int i = 0;
			while (i < sString.Length)
			{
				char c = sString[i];
				char c2 = c;
				if (c2 <= '?')
				{
					switch (c2)
					{
					case '#':
					case '$':
					case '(':
					case ')':
					case '+':
					case '.':
						goto IL_AA;
					case '%':
					case '&':
					case '\'':
					case ',':
					case '-':
						break;
					case '*':
						stringBuilder.Append('.');
						break;
					default:
						if (c2 == '?')
						{
							goto IL_AA;
						}
						break;
					}
				}
				else
				{
					switch (c2)
					{
					case '[':
					case '\\':
					case '^':
						goto IL_AA;
					case ']':
						break;
					default:
						switch (c2)
						{
						case '{':
						case '|':
							goto IL_AA;
						}
						break;
					}
				}
				IL_B3:
				stringBuilder.Append(c);
				i++;
				continue;
				IL_AA:
				stringBuilder.Append('\\');
				goto IL_B3;
			}
			if (bAddSuffixDollarSign)
			{
				stringBuilder.Append('$');
			}
			return stringBuilder.ToString();
		}
		public static bool HasMagicBytes(byte[] arrData, byte[] arrMagics)
		{
			if (arrData == null)
			{
				return false;
			}
			if (arrData.Length < arrMagics.Length)
			{
				return false;
			}
			for (int i = 0; i < arrMagics.Length; i++)
			{
				if (arrData[i] != arrMagics[i])
				{
					return false;
				}
			}
			return true;
		}
		public static bool HasMagicBytes(byte[] arrData, string sMagics)
		{
			return Utilities.HasMagicBytes(arrData, Encoding.ASCII.GetBytes(sMagics));
		}
		internal static bool isRPCOverHTTPSMethod(string sMethod)
		{
			return sMethod == "RPC_IN_DATA" || sMethod == "RPC_OUT_DATA";
		}
		internal static bool isHTTP200Array(byte[] arrData)
		{
			return arrData.Length > 12 && arrData[0] == 72 && arrData[1] == 84 && arrData[2] == 84 && arrData[3] == 80 && arrData[4] == 47 && arrData[5] == 49 && arrData[6] == 46 && arrData[9] == 50 && arrData[10] == 48 && arrData[11] == 48;
		}
		internal static bool isHTTP407Array(byte[] arrData)
		{
			return arrData.Length > 12 && arrData[0] == 72 && arrData[1] == 84 && arrData[2] == 84 && arrData[3] == 80 && arrData[4] == 47 && arrData[5] == 49 && arrData[6] == 46 && arrData[9] == 52 && arrData[10] == 48 && arrData[11] == 55;
		}
		public static bool IsBrowserProcessName(string sProcessName)
		{
			return !string.IsNullOrEmpty(sProcessName) && sProcessName.OICStartsWithAny(new string[]
			{
				"ie",
				"chrom",
				"firefox",
				"tbb-",
				"opera",
				"webkit",
				"safari"
			});
		}
		public static string EnsurePathIsAbsolute(string sRootPath, string sFilename)
		{
			try
			{
				if (!Path.IsPathRooted(sFilename))
				{
					sFilename = sRootPath + sFilename;
				}
			}
			catch (Exception)
			{
			}
			return sFilename;
		}
		internal static string GetFirstLocalResponse(string sFilename)
		{
			sFilename = Utilities.TrimAfter(sFilename, '?');
			try
			{
				if (!Path.IsPathRooted(sFilename))
				{
					string str = sFilename;
					sFilename = CONFIG.GetPath("TemplateResponses") + str;
					if (!File.Exists(sFilename))
					{
						sFilename = CONFIG.GetPath("Responses") + str;
					}
				}
			}
			catch (Exception)
			{
			}
			return sFilename;
		}
		internal static string DescribeException(Exception eX)
		{
			StringBuilder stringBuilder = new StringBuilder(512);
			stringBuilder.Append(eX.Message);
			if (eX.InnerException != null)
			{
				stringBuilder.AppendFormat(" < {0}", eX.InnerException.Message);
			}
			return stringBuilder.ToString();
		}
		[DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
		private static extern ulong GetTickCount64();
		public static ulong GetTickCount()
		{
			if (Environment.OSVersion.Version.Major > 5)
			{
				return Utilities.GetTickCount64();
			}
			int tickCount = Environment.TickCount;
			if (tickCount > 0)
			{
				return (ulong)((long)tickCount);
			}
			return (ulong)2 - (ulong)((long)(-(long)tickCount));
		}
		[DllImport("shell32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool IsUserAnAdmin();
		internal static object GetOSVerString()
		{
			return Environment.OSVersion.VersionString.Replace("Microsoft Windows ", "Win").Replace("Service Pack ", "SP");
		}
		internal static bool IsNullOrWhiteSpace(string sInput)
		{
			return sInput == null || 0 == sInput.Trim().Length;
		}
		internal static SslProtocols ParseSSLProtocolString(string sList)
		{
			SslProtocols sslProtocols = SslProtocols.None;
			if (sList.OICContains("ssl2"))
			{
				sslProtocols |= SslProtocols.Ssl2;
			}
			if (sList.OICContains("ssl3"))
			{
				sslProtocols |= SslProtocols.Ssl3;
			}
			if (sList.OICContains("tls1.0"))
			{
				sslProtocols |= SslProtocols.Tls;
			}
			return sslProtocols;
		}
		public static byte[] Dupe(byte[] bIn)
		{
			if (bIn == null)
			{
				return Utilities.emptyByteArray;
			}
			byte[] array = new byte[bIn.Length];
			Buffer.BlockCopy(bIn, 0, array, 0, bIn.Length);
			return array;
		}
		public static bool IsNullOrEmpty(byte[] bIn)
		{
			return bIn == null || bIn.Length == 0;
		}
		internal static bool HasHeaders(ServerChatter oSC)
		{
			return oSC != null && null != oSC.headers;
		}
		internal static bool HasHeaders(ClientChatter oCC)
		{
			return oCC != null && null != oCC.headers;
		}
		internal static string GetLocalIPList(bool bLeadingTab)
		{
			IPAddress[] hostAddresses = Dns.GetHostAddresses(string.Empty);
			StringBuilder stringBuilder = new StringBuilder();
			IPAddress[] array = hostAddresses;
			for (int i = 0; i < array.Length; i++)
			{
				IPAddress iPAddress = array[i];
				stringBuilder.AppendFormat("{0}{1}\n", bLeadingTab ? "\t" : string.Empty, iPAddress.ToString());
			}
			return stringBuilder.ToString();
		}
		internal static string GetNetworkInfo()
		{
			string result;
			try
			{
				StringBuilder stringBuilder = new StringBuilder();
				long num = 0L;
				NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
				Array.Sort<NetworkInterface>(allNetworkInterfaces, (NetworkInterface networkInterface_0, NetworkInterface networkInterface_1) => string.Compare(networkInterface_1.OperationalStatus.ToString(), networkInterface_0.OperationalStatus.ToString()));
				NetworkInterface[] array = allNetworkInterfaces;
				for (int i = 0; i < array.Length; i++)
				{
					NetworkInterface networkInterface = array[i];
					stringBuilder.AppendFormat("{0,32}\t '{1}' Type: {2} @ {3:N0}/sec. Status: {4}\n", new object[]
					{
						networkInterface.Name,
						networkInterface.Description,
						networkInterface.NetworkInterfaceType,
						networkInterface.Speed,
						networkInterface.OperationalStatus.ToString().ToUpperInvariant()
					});
					if (networkInterface.OperationalStatus == OperationalStatus.Up && networkInterface.NetworkInterfaceType != NetworkInterfaceType.Loopback && networkInterface.NetworkInterfaceType != NetworkInterfaceType.Tunnel && networkInterface.NetworkInterfaceType != NetworkInterfaceType.Unknown && !networkInterface.IsReceiveOnly)
					{
						num += networkInterface.GetIPv4Statistics().BytesReceived;
					}
				}
				stringBuilder.AppendFormat("\nTotal bytes received (IPv4): {0:N0}\n", num);
				stringBuilder.AppendFormat("\nLocal Addresses:\n{0}", Utilities.GetLocalIPList(true));
				result = stringBuilder.ToString();
			}
			catch (Exception eX)
			{
				result = "Failed to obtain NetworkInterfaces information. " + Utilities.DescribeException(eX);
			}
			return result;
		}
		internal static void PingTarget(string sTarget)
		{
			FiddlerApplication.Log.LogFormat("Pinging: {0}...", new object[]
			{
				sTarget
			});
			Ping ping = new Ping();
			ping.PingCompleted += delegate(object sender, PingCompletedEventArgs e)
			{
				StringBuilder stringBuilder = new StringBuilder();
				if (e.Reply == null)
				{
					stringBuilder.AppendFormat("Pinging '{0}' failed: {1}\n", e.UserState, e.Error.InnerException.ToString());
				}
				else
				{
					stringBuilder.AppendFormat("Pinged '{0}'.\n\tFinal Result:\t{1}\n", e.UserState as string, e.Reply.Status.ToString());
					if (e.Reply.Status == IPStatus.Success)
					{
						stringBuilder.AppendFormat("\tTarget Address:\t{0}\n", e.Reply.Address);
						stringBuilder.AppendFormat("\tRoundTrip time:\t{0}", e.Reply.RoundtripTime);
					}
				}
				FiddlerApplication.Log.LogString(stringBuilder.ToString());
			};
			ping.SendAsync(sTarget, 60000, new byte[0], new PingOptions(128, true), sTarget);
		}
		internal static bool IsNotExtension(string sFilename)
		{
			return sFilename.StartsWith("_") || sFilename.OICStartsWithAny(new string[]
			{
				"qwhale.",
				"Be.Windows.Forms.",
				"Telerik.WinControls."
			});
		}
		[CodeDescription("Save the specified .SAZ session archive")]
		public static bool WriteSessionArchive(string sFilename, Session[] arrSessions, string sPassword, bool bVerboseDialogs)
		{
			if (arrSessions == null || arrSessions.Length < 1)
			{
				if (bVerboseDialogs)
				{
					FiddlerApplication.DoNotifyUser("No sessions were provided to save to the archive.", "WriteSessionArchive - No Input");
				}
				return false;
			}
			if (FiddlerApplication.oSAZProvider == null)
			{
				throw new NotSupportedException("This application was compiled without .SAZ support.");
			}
			bool result;
			try
			{
				if (File.Exists(sFilename))
				{
					File.Delete(sFilename);
				}
				ISAZWriter iSAZWriter = FiddlerApplication.oSAZProvider.CreateSAZ(sFilename);
				if (!string.IsNullOrEmpty(sPassword))
				{
					iSAZWriter.SetPassword(sPassword);
				}
				iSAZWriter.Comment = "Fiddler (v" + Application.ProductVersion + ") Session Archive. See http://www.fiddler2.com";
				int num = 1;
				string sFileNumberFormat = "D" + arrSessions.Length.ToString().Length;
				for (int i = 0; i < arrSessions.Length; i++)
				{
					Session oSession = arrSessions[i];
					Utilities.WriteSessionToSAZ(oSession, iSAZWriter, num, sFileNumberFormat, null, bVerboseDialogs);
					num++;
				}
				iSAZWriter.CompleteArchive();
				result = true;
			}
			catch (Exception ex)
			{
				if (bVerboseDialogs)
				{
					FiddlerApplication.DoNotifyUser("Failed to save Session Archive.\n\n" + ex.Message, "Save Failed");
				}
				result = false;
			}
			return result;
		}
		internal static void WriteSessionToSAZ(Session oSession, ISAZWriter oISW, int iFileNumber, string sFileNumberFormat, StringBuilder sbHTML, bool bVerboseDialogs)
		{
			string text = "raw\\" + iFileNumber.ToString(sFileNumberFormat);
			string text2 = text + "_c.txt";
			string text3 = text + "_s.txt";
			string text4 = text + "_m.xml";
			try
			{
				oISW.AddFile(text2, delegate(Stream oS)
				{
					oSession.WriteRequestToStream(false, true, oS);
				});
			}
			catch (Exception ex)
			{
				if (bVerboseDialogs)
				{
					FiddlerApplication.DoNotifyUser("Unable to add " + text2 + "\n\n" + ex.Message, "Archive Failure");
				}
			}
			try
			{
				oISW.AddFile(text3, delegate(Stream oS)
				{
					oSession.WriteResponseToStream(oS, false);
				});
			}
			catch (Exception ex2)
			{
				if (bVerboseDialogs)
				{
					FiddlerApplication.DoNotifyUser("Unable to add " + text3 + "\n\n" + ex2.Message, "Archive Failure");
				}
			}
			try
			{
				oISW.AddFile(text4, delegate(Stream oS)
				{
					oSession.WriteMetadataToStream(oS);
				});
			}
			catch (Exception ex3)
			{
				if (bVerboseDialogs)
				{
					FiddlerApplication.DoNotifyUser("Unable to add " + text4 + "\n\n" + ex3.Message, "Archive Failure");
				}
			}
			if (oSession.bHasWebSocketMessages)
			{
				string text5 = text + "_w.txt";
				try
				{
					oISW.AddFile(text5, delegate(Stream oS)
					{
						oSession.WriteWebSocketMessagesToStream(oS);
					});
				}
				catch (Exception ex4)
				{
					if (bVerboseDialogs)
					{
						FiddlerApplication.DoNotifyUser("Unable to add " + text5 + "\n\n" + ex4.Message, "Archive Failure");
					}
				}
			}
			if (sbHTML != null)
			{
				sbHTML.Append("<tr>");
				sbHTML.AppendFormat("<TD><a href='{0}'>C</a>&nbsp;", text2);
				sbHTML.AppendFormat("<a href='{0}'>S</a>&nbsp;", text3);
				sbHTML.AppendFormat("<a href='{0}'>M</a>", text4);
				if (oSession.bHasWebSocketMessages)
				{
					sbHTML.AppendFormat("&nbsp;<a href='{0}_w.txt'>W</a>", text);
				}
				sbHTML.AppendFormat("</TD>", new object[0]);
				sbHTML.Append("</tr>");
			}
		}
		public static Session[] ReadSessionArchive(string sFilename, bool bVerboseDialogs)
		{
			return Utilities.ReadSessionArchive(sFilename, bVerboseDialogs, string.Empty);
		}
		[CodeDescription("Load the specified .SAZ or .ZIP session archive")]
		public static Session[] ReadSessionArchive(string sFilename, bool bVerboseDialogs, string sContext)
		{
			if (!File.Exists(sFilename))
			{
				if (bVerboseDialogs)
				{
					FiddlerApplication.DoNotifyUser("File " + sFilename + " does not exist.", "ReadSessionArchive Failed", MessageBoxIcon.Hand);
				}
				return null;
			}
			if (FiddlerApplication.oSAZProvider == null)
			{
				throw new NotSupportedException("This application was compiled without .SAZ support.");
			}
			Application.DoEvents();
			List<Session> list = new List<Session>();
			Session[] result;
			try
			{
				using (FileStream fileStream = File.Open(sFilename, FileMode.Open, FileAccess.Read, FileShare.Read))
				{
					if (fileStream.Length >= 64L && fileStream.ReadByte() == 80)
					{
						if (fileStream.ReadByte() == 75)
						{
							goto IL_A5;
						}
					}
					string arg = null;
					if (bVerboseDialogs)
					{
						FiddlerApplication.DoNotifyUser(string.Format("{0} is not a Fiddler-generated .SAZ archive of Web Sessions.{1}", sFilename, arg), "ReadSessionArchive Failed", MessageBoxIcon.Hand);
					}
					result = null;
					return result;
				}
				IL_A5:
				ISAZReader iSAZReader = FiddlerApplication.oSAZProvider.LoadSAZ(sFilename);
				string[] requestFileList = iSAZReader.GetRequestFileList();
				if (requestFileList.Length >= 1)
				{
					string[] array = requestFileList;
					for (int i = 0; i < array.Length; i++)
					{
						string text = array[i];
						try
						{
							byte[] fileBytes;
							try
							{
								fileBytes = iSAZReader.GetFileBytes(text);
							}
							catch (OperationCanceledException)
							{
								iSAZReader.Close();
								result = null;
								return result;
							}
							string sFilename2 = text.Replace("_c.txt", "_s.txt");
							byte[] fileBytes2 = iSAZReader.GetFileBytes(sFilename2);
							string sFilename3 = text.Replace("_c.txt", "_m.xml");
							Stream fileStream2 = iSAZReader.GetFileStream(sFilename3);
							Session session = new Session(fileBytes, fileBytes2);
							if (fileStream2 != null)
							{
								session.LoadMetadata(fileStream2);
							}
							session.oFlags["x-LoadedFrom"] = text.Replace("_c.txt", "_s.txt");
							if (session.isAnyFlagSet(SessionFlags.IsWebSocketTunnel) && !session.HTTPMethodIs("CONNECT"))
							{
								string sFilename4 = text.Replace("_c.txt", "_w.txt");
								Stream fileStream3 = iSAZReader.GetFileStream(sFilename4);
								if (fileStream3 != null)
								{
									WebSocket.LoadWebSocketMessagesFromStream(session, fileStream3);
								}
								else
								{
									session.oFlags["X-WS-SAZ"] = "SAZ File did not contain any WebSocket messages.";
								}
							}
							list.Add(session);
						}
						catch (Exception ex)
						{
							if (bVerboseDialogs)
							{
								FiddlerApplication.DoNotifyUser(string.Format("Invalid data was present for session [{0}].\n\n{1}\n{2}", Utilities.TrimAfter(text, "_"), Utilities.DescribeException(ex), ex.StackTrace), "Archive Incomplete", MessageBoxIcon.Hand);
							}
						}
					}
					iSAZReader.Close();
					goto IL_247;
				}
				if (bVerboseDialogs)
				{
					FiddlerApplication.DoNotifyUser("The selected file is not a Fiddler-generated .SAZ archive of Web Sessions.", "Invalid Archive", MessageBoxIcon.Hand);
				}
				iSAZReader.Close();
				result = null;
			}
			catch (Exception eX)
			{
				FiddlerApplication.ReportException(eX);
				result = null;
			}
			return result;
			IL_247:
			return list.ToArray();
		}
	}
}
