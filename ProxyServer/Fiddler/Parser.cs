using System;
using System.Globalization;
using System.IO;
namespace Fiddler
{
    public class Parser
    {
        internal static void CrackRequestLine(byte[] arrRequest, out int ixURIOffset, out int iURILen, out int ixHeaderNVPOffset, out string sMalformedURI)
        {
            ixHeaderNVPOffset = 0;
            iURILen = 0;
            ixURIOffset = 0;
            int num = 0;
            sMalformedURI = null;
            do
            {
                if (32 == arrRequest[num])
                {
                    if (ixURIOffset == 0)
                    {
                        ixURIOffset = num + 1;
                    }
                    else
                    {
                        if (iURILen == 0)
                        {
                            iURILen = num - ixURIOffset;
                        }
                        else
                        {
                            sMalformedURI = "Extra whitespace found in Request Line";
                        }
                    }
                }
                else
                {
                    if (arrRequest[num] == 10)
                    {
                        ixHeaderNVPOffset = num + 1;
                    }
                }
                num++;
            }
            while (ixHeaderNVPOffset == 0);
        }

        internal static bool FindEndOfHeaders(byte[] arrData, ref int iBodySeekProgress, long lngDataLen, out HTTPHeaderParseWarnings oWarnings)
        {
            bool flag;
            oWarnings = HTTPHeaderParseWarnings.None;
            start:
            flag = false;
            while (((long)iBodySeekProgress) < (lngDataLen - 1L))
            {
                iBodySeekProgress++;
                if (10 == arrData[iBodySeekProgress - 1])
                {
                    flag = true;
                    break;
                }
            }
            if (flag)
            {
                if ((13 != arrData[iBodySeekProgress]) && (10 != arrData[iBodySeekProgress]))
                {
                    iBodySeekProgress++;
                    goto start;
                }
                if (10 == arrData[iBodySeekProgress])
                {
                    oWarnings = HTTPHeaderParseWarnings.EndedWithLFLF;
                    return true;
                }
                iBodySeekProgress++;
                if ((((long)iBodySeekProgress) < lngDataLen) && (10 == arrData[iBodySeekProgress]))
                {
                    if (13 != arrData[iBodySeekProgress - 3])
                    {
                        oWarnings = HTTPHeaderParseWarnings.EndedWithLFCRLF;
                    }
                    return true;
                }
                if (iBodySeekProgress > 3)
                {
                    iBodySeekProgress -= 4;
                }
                else
                {
                    iBodySeekProgress = 0;
                }
            }
            return false;
        }

        private static bool IsPrefixedWithWhitespace(string string_0)
        {
            return string_0.Length > 0 && char.IsWhiteSpace(string_0[0]);
        }
        internal static bool ParseNVPHeaders(HTTPHeaders oHeaders, string[] sHeaderLines, int iStartAt, ref string sErrors)
        {
            bool result = true;
            int i = iStartAt;
            while (i < sHeaderLines.Length)
            {
                int num = sHeaderLines[i].IndexOf(':');
                HTTPHeaderItem hTTPHeaderItem;
                if (num > 0)
                {
                    hTTPHeaderItem = oHeaders.Add(sHeaderLines[i].Substring(0, num), sHeaderLines[i].Substring(num + 1).TrimStart(new char[]
                    {
                        ' ',
                        '\t'
                    }));
                }
                else
                {
                    if (num == 0)
                    {
                        hTTPHeaderItem = null;
                        sErrors += string.Format("Missing Header name #{0}, {1}\n", 1 + i - iStartAt, sHeaderLines[i]);
                        result = false;
                    }
                    else
                    {
                        hTTPHeaderItem = oHeaders.Add(sHeaderLines[i], string.Empty);
                        sErrors += string.Format("Missing colon in header #{0}, {1}\n", 1 + i - iStartAt, sHeaderLines[i]);
                        result = false;
                    }
                }
                i++;
                bool flag = hTTPHeaderItem != null && i < sHeaderLines.Length && Parser.IsPrefixedWithWhitespace(sHeaderLines[i]);
                while (flag)
                {
                    FiddlerApplication.Log.LogString("[HTTPWarning] Header folding detected. Not all clients properly handle folded headers.");
                    hTTPHeaderItem.Value = hTTPHeaderItem.Value + " " + sHeaderLines[i].TrimStart(new char[]
                    {
                        ' ',
                        '\t'
                    });
                    i++;
                    flag = (i < sHeaderLines.Length && Parser.IsPrefixedWithWhitespace(sHeaderLines[i]));
                }
            }
            return result;
        }
        public static bool FindEntityBodyOffsetFromArray(byte[] arrData, out int iHeadersLen, out int iEntityBodyOffset, out HTTPHeaderParseWarnings outWarnings)
        {
            if (arrData != null && arrData.Length >= 2)
            {
                int num = 0;
                long lngDataLen = (long)arrData.Length;
                if (Parser.FindEndOfHeaders(arrData, ref num, lngDataLen, out outWarnings))
                {
                    iEntityBodyOffset = num + 1;
                    switch (outWarnings)
                    {
                        case HTTPHeaderParseWarnings.None:
                            iHeadersLen = num - 3;
                            return true;
                        case HTTPHeaderParseWarnings.EndedWithLFLF:
                            iHeadersLen = num - 1;
                            return true;
                        case HTTPHeaderParseWarnings.EndedWithLFCRLF:
                            iHeadersLen = num - 2;
                            return true;
                    }
                }
            }
            iEntityBodyOffset = -1;
            iHeadersLen = -1;
            outWarnings = HTTPHeaderParseWarnings.Malformed;
            return false;
        }
        private static int _GetEntityLengthFromHeaders(HTTPHeaders oHeaders, MemoryStream strmData)
        {
            if (oHeaders.ExistsAndEquals("Transfer-encoding", "chunked"))
            {
                long num;
                long num2;
                if (Utilities.IsChunkedBodyComplete(null, strmData, strmData.Position, out num, out num2))
                {
                    return (int)(num2 - strmData.Position);
                }
                return (int)(strmData.Length - strmData.Position);
            }
            else
            {
                string text = oHeaders["Content-Length"];
                if (!string.IsNullOrEmpty(text))
                {
                    long num3 = 0L;
                    if (long.TryParse(text, NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out num3) && num3 >= 0L)
                    {
                        return (int)num3;
                    }
                    return (int)(strmData.Length - strmData.Position);
                }
                else
                {
                    if (oHeaders.ExistsAndContains("Connection", "close"))
                    {
                        return (int)(strmData.Length - strmData.Position);
                    }
                    return 0;
                }
            }
        }
        public static bool TakeRequest(MemoryStream strmClient, out HTTPRequestHeaders headersRequest, out byte[] arrRequestBody)
        {
            headersRequest = null;
            arrRequestBody = Utilities.emptyByteArray;
            if (strmClient.Length - strmClient.Position < 16L)
            {
                return false;
            }
            byte[] buffer = strmClient.GetBuffer();
            long length = strmClient.Length;
            int num = (int)strmClient.Position;
            HTTPHeaderParseWarnings hTTPHeaderParseWarnings;
            if (!Parser.FindEndOfHeaders(buffer, ref num, length, out hTTPHeaderParseWarnings))
            {
                return false;
            }
            byte[] array = new byte[(long)(1 + num) - strmClient.Position];
            strmClient.Read(array, 0, array.Length);
            string @string = CONFIG.oHeaderEncoding.GetString(array);
            headersRequest = Parser.ParseRequest(@string);
            if (headersRequest != null)
            {
                int num2 = Parser._GetEntityLengthFromHeaders(headersRequest, strmClient);
                arrRequestBody = new byte[num2];
                strmClient.Read(arrRequestBody, 0, arrRequestBody.Length);
                return true;
            }
            return false;
        }
        public static bool TakeResponse(MemoryStream strmServer, string sRequestMethod, out HTTPResponseHeaders headersResponse, out byte[] arrResponseBody)
        {
            headersResponse = null;
            arrResponseBody = Utilities.emptyByteArray;
            if (strmServer.Length - strmServer.Position < 16L)
            {
                return false;
            }
            byte[] buffer = strmServer.GetBuffer();
            long length = strmServer.Length;
            int num = (int)strmServer.Position;
            HTTPHeaderParseWarnings hTTPHeaderParseWarnings;
            if (!Parser.FindEndOfHeaders(buffer, ref num, length, out hTTPHeaderParseWarnings))
            {
                return false;
            }
            byte[] array = new byte[(long)(1 + num) - strmServer.Position];
            strmServer.Read(array, 0, array.Length);
            string @string = CONFIG.oHeaderEncoding.GetString(array);
            headersResponse = Parser.ParseResponse(@string);
            if (headersResponse == null)
            {
                return false;
            }
            if (sRequestMethod == "HEAD")
            {
                return true;
            }
            int num2 = Parser._GetEntityLengthFromHeaders(headersResponse, strmServer);
            arrResponseBody = new byte[num2];
            strmServer.Read(arrResponseBody, 0, arrResponseBody.Length);
            return true;
        }
        public static HTTPRequestHeaders ParseRequest(string sRequest)
        {
            string[] array = Parser._GetHeaderLines(sRequest);
            if (array == null)
            {
                return null;
            }
            HTTPRequestHeaders hTTPRequestHeaders = new HTTPRequestHeaders(CONFIG.oHeaderEncoding);
            int num = array[0].IndexOf(' ');
            if (num > 0)
            {
                hTTPRequestHeaders.HTTPMethod = array[0].Substring(0, num).ToUpperInvariant();
                array[0] = array[0].Substring(num).Trim();
            }
            num = array[0].LastIndexOf(' ');
            if (num > 0)
            {
                string text = array[0].Substring(0, num);
                hTTPRequestHeaders.HTTPVersion = array[0].Substring(num).Trim().ToUpperInvariant();
                if (text.OICStartsWith("http://"))
                {
                    hTTPRequestHeaders.UriScheme = "http";
                    num = text.IndexOfAny(new char[]
                    {
                        '/',
                        '?'
                    }, 7);
                    if (num == -1)
                    {
                        hTTPRequestHeaders.RequestPath = "/";
                    }
                    else
                    {
                        hTTPRequestHeaders.RequestPath = text.Substring(num);
                    }
                }
                else
                {
                    if (text.OICStartsWith("https://"))
                    {
                        hTTPRequestHeaders.UriScheme = "https";
                        num = text.IndexOfAny(new char[]
                        {
                            '/',
                            '?'
                        }, 8);
                        if (num == -1)
                        {
                            hTTPRequestHeaders.RequestPath = "/";
                        }
                        else
                        {
                            hTTPRequestHeaders.RequestPath = text.Substring(num);
                        }
                    }
                    else
                    {
                        if (text.OICStartsWith("ftp://"))
                        {
                            hTTPRequestHeaders.UriScheme = "ftp";
                            num = text.IndexOf('/', 6);
                            if (num == -1)
                            {
                                hTTPRequestHeaders.RequestPath = "/";
                            }
                            else
                            {
                                string text2 = text.Substring(6, num - 6);
                                if (text2.Contains("@"))
                                {
                                    hTTPRequestHeaders._uriUserInfo = Utilities.TrimTo(text2, text2.IndexOf("@") + 1);
                                }
                                hTTPRequestHeaders.RequestPath = text.Substring(num);
                            }
                        }
                        else
                        {
                            hTTPRequestHeaders.RequestPath = text;
                        }
                    }
                }
                string empty = string.Empty;
                Parser.ParseNVPHeaders(hTTPRequestHeaders, array, 1, ref empty);
                return hTTPRequestHeaders;
            }
            return null;
        }
        private static string[] _GetHeaderLines(string sInput)
        {
            int num = sInput.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (num < 1)
            {
                num = sInput.Length;
            }
            if (num < 1)
            {
                return null;
            }
            string[] array = sInput.Substring(0, num).Replace("\r\n", "\n").Split(new char[]
            {
                '\n'
            });
            if (array != null && array.Length >= 1)
            {
                return array;
            }
            return null;
        }
        public static HTTPResponseHeaders ParseResponse(string sResponse)
        {
            string[] array = Parser._GetHeaderLines(sResponse);
            if (array == null)
            {
                return null;
            }
            HTTPResponseHeaders hTTPResponseHeaders = new HTTPResponseHeaders(CONFIG.oHeaderEncoding);
            int num = array[0].IndexOf(' ');
            if (num <= 0)
            {
                return null;
            }
            hTTPResponseHeaders.HTTPVersion = array[0].Substring(0, num).ToUpperInvariant();
            array[0] = array[0].Substring(num + 1).Trim();
            if (!hTTPResponseHeaders.HTTPVersion.OICStartsWith("HTTP/"))
            {
                return null;
            }
            hTTPResponseHeaders.HTTPResponseStatus = array[0];
            num = array[0].IndexOf(' ');
            bool flag;
            if (num > 0)
            {
                flag = int.TryParse(array[0].Substring(0, num).Trim(), NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out hTTPResponseHeaders.HTTPResponseCode);
            }
            else
            {
                flag = int.TryParse(array[0].Trim(), NumberStyles.Integer, NumberFormatInfo.InvariantInfo, out hTTPResponseHeaders.HTTPResponseCode);
            }
            if (!flag)
            {
                return null;
            }
            string empty = string.Empty;
            Parser.ParseNVPHeaders(hTTPResponseHeaders, array, 1, ref empty);
            return hTTPResponseHeaders;
        }
    }
}
