using System;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Text;
namespace Fiddler
{
	internal class FTPGateway
	{
		public static void MakeFTPRequest(Session oSession, ref PipeReadBuffer buffBody, out HTTPResponseHeaders oRH)
		{
			if (oSession.oRequest == null || oSession.oRequest.headers == null)
			{
				throw new ArgumentException("Session missing request objects.");
			}
			if (buffBody == null)
			{
				throw new ArgumentException("Response Stream may not be null.");
			}
			string fullUrl = oSession.fullUrl;
			FtpWebRequest ftpWebRequest = (FtpWebRequest)WebRequest.Create(fullUrl);
			ftpWebRequest.CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);
			if (fullUrl.EndsWith("/"))
			{
				ftpWebRequest.Method = "LIST";
			}
			else
			{
				ftpWebRequest.Method = "RETR";
				if (oSession.oFlags.ContainsKey("FTP-UseASCII"))
				{
					ftpWebRequest.UseBinary = false;
				}
				else
				{
					ftpWebRequest.UseBinary = FiddlerApplication.Prefs.GetBoolPref("fiddler.ftp.UseBinary", true);
				}
			}
			if (!string.IsNullOrEmpty(oSession.oRequest.headers._uriUserInfo))
			{
				string text = Utilities.TrimAfter(oSession.oRequest.headers._uriUserInfo, '@');
				text = Utilities.UrlDecode(text);
				string userName = Utilities.TrimAfter(text, ':');
				string password = text.Contains(":") ? Utilities.TrimBefore(text, ':') : string.Empty;
				ftpWebRequest.Credentials = new NetworkCredential(userName, password);
			}
			else
			{
				if (oSession.oRequest.headers.ExistsAndContains("Authorization", "Basic "))
				{
					string text2 = oSession.oRequest.headers["Authorization"].Substring(6);
					text2 = Encoding.UTF8.GetString(Convert.FromBase64String(text2));
					string userName2 = Utilities.TrimAfter(text2, ':');
					string password2 = Utilities.TrimBefore(text2, ':');
					ftpWebRequest.Credentials = new NetworkCredential(userName2, password2);
				}
				else
				{
					if (oSession.oFlags.ContainsKey("x-AutoAuth") && oSession.oFlags["x-AutoAuth"].Contains(":"))
					{
						string userName3 = Utilities.TrimAfter(oSession.oFlags["x-AutoAuth"], ':');
						string password3 = Utilities.TrimBefore(oSession.oFlags["x-AutoAuth"], ':');
						ftpWebRequest.Credentials = new NetworkCredential(userName3, password3);
					}
					else
					{
						if (FiddlerApplication.Prefs.GetBoolPref("fiddler.ftp.AlwaysDemandCredentials", false))
						{
							byte[] bytes = Encoding.UTF8.GetBytes("Please provide login credentials for this FTP server".PadRight(512, ' '));
							buffBody.Write(bytes, 0, bytes.Length);
							oRH = new HTTPResponseHeaders();
							oRH.SetStatus(401, "Need Creds");
							oRH.Add("Content-Length", buffBody.Length.ToString());
							oRH.Add("WWW-Authenticate", "Basic realm=\"ftp://" + oSession.host + "\"");
							return;
						}
					}
				}
			}
			ftpWebRequest.UsePassive = FiddlerApplication.Prefs.GetBoolPref("fiddler.ftp.UsePassive", true);
			ftpWebRequest.Proxy = null;
			FtpWebResponse ftpWebResponse;
			try
			{
				ftpWebResponse = (FtpWebResponse)ftpWebRequest.GetResponse();
			}
			catch (WebException ex)
			{
				FtpWebResponse ftpWebResponse2 = (FtpWebResponse)ex.Response;
				if (ftpWebResponse2 != null)
				{
					byte[] bytes2;
					if (FtpStatusCode.NotLoggedIn == ftpWebResponse2.StatusCode)
					{
						bytes2 = Encoding.UTF8.GetBytes("This FTP server requires login credentials".PadRight(512, ' '));
						buffBody.Write(bytes2, 0, bytes2.Length);
						oRH = new HTTPResponseHeaders();
						oRH.SetStatus(401, "Need Creds");
						oRH.Add("Content-Length", buffBody.Length.ToString());
						oRH.Add("WWW-Authenticate", "Basic realm=\"ftp://" + oSession.host + "\"");
						return;
					}
					bytes2 = Encoding.UTF8.GetBytes(string.Format("{0}{1}{2}", "Fiddler was unable to act as a HTTP-to-FTP gateway for this response. ", ftpWebResponse2.StatusDescription, string.Empty.PadRight(512, ' ')));
					buffBody.Write(bytes2, 0, bytes2.Length);
				}
				else
				{
					byte[] bytes2 = Encoding.UTF8.GetBytes(string.Format("{0}{1}{2}", "Fiddler was unable to act as a HTTP-to-FTP gateway for this response. ", ex.Message, string.Empty.PadRight(512, ' ')));
					buffBody.Write(bytes2, 0, bytes2.Length);
				}
				oRH = new HTTPResponseHeaders();
				oRH.SetStatus(504, "HTTP-FTP Gateway failed");
				oRH.Add("Content-Length", buffBody.Length.ToString());
				return;
			}
			Stream responseStream = ftpWebResponse.GetResponseStream();
			byte[] buffer = new byte[8192];
			for (int i = responseStream.Read(buffer, 0, 8192); i > 0; i = responseStream.Read(buffer, 0, 8192))
			{
				buffBody.Write(buffer, 0, i);
			}
			oRH = new HTTPResponseHeaders();
			oRH.SetStatus(200, "OK");
			oRH.Add("FTP-Status", Utilities.ConvertCRAndLFToSpaces(ftpWebResponse.StatusDescription));
			oRH.Add("Content-Length", buffBody.Length.ToString());
			ftpWebResponse.Close();
		}
	}
}
