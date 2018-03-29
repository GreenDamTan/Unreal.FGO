using Fiddler;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Globalization;
namespace Unreal.FGO.Anget
{
    public partial class frmMain : Form
    {
        public frmMain()
        {
            InitializeComponent();
        }
        static Proxy oSecureEndpoint;
        static string sSecureEndpointHostname = "localhost";
        int iPort = 8877;
        static int iSecureEndpointPort = 7777;
        List<Session> oAllSessions = new List<Session>();
        Dictionary<Session, int> dgvIndex = new Dictionary<Session, int>();
        Dictionary<string, string> platfromInfos = new Dictionary<string, string>();

        private void initFiddler()
        {
            // For the purposes of this demo, we'll forbid connections to HTTPS 
            // sites that use invalid certificates. Change this from the default only
            // if you know EXACTLY what that implies.
            Fiddler.CONFIG.IgnoreServerCertErrors = false;

            // ... but you can allow a specific (even invalid) certificate by implementing and assigning a callback...

            FiddlerApplication.Prefs.SetBoolPref("fiddler.network.streaming.abortifclientaborts", true);

            // For forward-compatibility with updated FiddlerCore libraries, it is strongly recommended that you 
            // start with the DEFAULT options and manually disable specific unwanted options.
            FiddlerCoreStartupFlags oFCSF = FiddlerCoreStartupFlags.AllowRemoteClients | FiddlerCoreStartupFlags.DecryptSSL | FiddlerCoreStartupFlags.ChainToUpstreamGateway | FiddlerCoreStartupFlags.CaptureLocalhostTraffic | FiddlerCoreStartupFlags.MonitorAllConnections | FiddlerCoreStartupFlags.OptimizeThreadPool;
            Fiddler.FiddlerApplication.Startup(iPort, oFCSF);

            // We'll also create a HTTPS listener, useful for when FiddlerCore is masquerading as a HTTPS server
            // instead of acting as a normal CERN-style proxy server.
            oSecureEndpoint = FiddlerApplication.CreateProxyEndpoint(iSecureEndpointPort, true, sSecureEndpointHostname);
            Fiddler.FiddlerApplication.BeforeRequest += FiddlerApplication_BeforeRequest;
            Fiddler.FiddlerApplication.BeforeResponse += FiddlerApplication_BeforeResponse;
        }

        private void FiddlerApplication_BeforeResponse(Session oSession)
        {
            if (oSession.RequestMethod == "CONNECT")
                return;
            if (string.IsNullOrEmpty(oSession.oRequest.headers["X-Unity-Version"]) && oSession.host.IndexOf("bili") == -1)
                return;
            if (getRequestName(oSession) == "battlesetup")
            {
                modifyBattleAtk(oSession);
            }
            Monitor.Enter(oAllSessions);
            {
                if (dgvIndex.ContainsKey(oSession))
                {
                    BeginInvoke(() =>
                    {
                        var row = dgvSessions.Rows[dgvIndex[oSession]];
                        row.Cells[2].Value = oSession.responseCode.ToString();
                    });
                }
                oAllSessions.Add(oSession);
            }
            Monitor.Exit(oAllSessions);
            updateDatabase(oSession);
        }

        private void updateDatabase(Session oSession)
        {

        }

        private void modifyBattleAtk(Session oSession)
        {
            try
            {
                var content = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(Fiddler.Utilities.UrlDecode(oSession.GetResponseBodyAsString())));
                var root = JsonConvert.DeserializeObject<JObject>(content);
                var svts = (JArray)root.SelectToken("cache.replaced.battle[0].battleInfo.userSvt");
                for (var i = 0; i < svts.Count; i++)
                {
                    var svtObj = (JObject)svts[i];
                    if (svtObj.Property("userId").Value.ToString() == "0")
                    {
                        svtObj.Property("atk").Value = 10;
                        svtObj.Property("hp").Value = 40000;
                    }
                    else
                    {
                        svtObj.Property("atk").Value = 40000;
                        svtObj.Property("hp").Value = 40000;
                    }
                    svts[i] = svtObj;
                }
                var json = root.ToString();
                var encodeTxt = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
                encodeTxt = System.Web.HttpUtility.UrlEncode(encodeTxt);
                oSession.utilSetResponseBody(encodeTxt);
            }
            catch (Exception ex)
            {
            }
        }

        Regex regex = new Regex("^.+://.+/");
        private void FiddlerApplication_BeforeRequest(Session oSession)
        {
            var oS = oSession;
            if ((oS.oRequest.pipeClient.LocalPort == iSecureEndpointPort) && (oS.hostname == sSecureEndpointHostname))
            {
                oS.utilCreateResponseAndBypassServer();
                oS.oResponse.headers.SetStatus(200, "Ok");
                oS.oResponse["Content-Type"] = "text/html; charset=UTF-8";
                oS.oResponse["Cache-Control"] = "private, max-age=0";
                oS.utilSetResponseBody("<html><body>Request for httpS://" + sSecureEndpointHostname + ":" + iSecureEndpointPort.ToString() + " received. Your request was:<br /><plaintext>" + oS.oRequest.headers.ToString());
                return;
            }
            if (oSession.RequestMethod == "CONNECT")
                return;
            if (string.IsNullOrEmpty(oSession.oRequest.headers["X-Unity-Version"]) && oSession.host.IndexOf("bili") == -1 && oSession.host.IndexOf("www.im9.com") == -1)
                return;
            oSession.bBufferResponse = true;
            Monitor.Enter(oAllSessions);
            {
                oAllSessions.Add(oSession);
                BeginInvoke(() =>
                {
                    setPlatfromInfo(oSession);
                    var index = dgvSessions.Rows.Add();
                    var row = dgvSessions.Rows[index];
                    if (row == null)
                        return;
                    row.Tag = oSession;
                    row.Cells[0].Value = getRequestName(oSession);
                    row.Cells[1].Value = oSession.RequestMethod;
                    row.Cells[2].Value = "0";
                    dgvIndex[oSession] = index;
                });
            }
            Monitor.Exit(oAllSessions);
        }

        private void setPlatfromInfo(Session oSession)
        {
            var dic = getQueryDic(oSession.PathAndQuery);
            getQueryDic(oSession.GetRequestBodyAsString(), dic);
            foreach (var item in dic)
            {
                platfromInfos[item.Key] = item.Value;
            }
            var text = "{\r\n";
            foreach (var kv in platfromInfos)
            {
                text += "   { \"" + kv.Key + "\",\"" + kv.Value + "\"},\r\n";
            }
            if (text.EndsWith(",\r\n"))
                text = text.Substring(0, text.Length - 3);
            text += "\r\n}";
            txtPlatfromInfo.Text = text;
        }

        private string getRequestName(Session oSession)
        {
            var url = oSession.PathAndQuery;
            if (url.IndexOf(".txt") != -1 || url.IndexOf(".bin") != -1 || url.IndexOf(".json") != -1)
                return Path.GetFileName(url);

            if (url.IndexOf("login.php") != -1)
                return "Login";
            if (url.IndexOf("ac.php") != -1)
            {
                var dic = getQueryDic(oSession.GetRequestBodyAsString());
                if (dic.ContainsKey("key"))
                    return dic["key"];
                else if (dic.ContainsKey("ac"))
                    return dic["ac"];
            }
            else if (url.IndexOf(".php") != -1)
                return Path.GetFileNameWithoutExtension(url);
            return url;
        }

        delegate void InvokeMain();
        private void BeginInvoke(Action p)
        {
            BeginInvoke(new InvokeMain(p));
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            dgvSessions.AutoGenerateColumns = false;
            dgvSessions.AllowUserToResizeRows = false;
            dgvSessions.MultiSelect = false;
            initFiddler();
            if (File.Exists("session.bin"))
            {
                using (var fs = new FileStream("session.bin", FileMode.Open))
                {
                    System.Runtime.Serialization.Formatters.Binary.BinaryFormatter formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    try
                    {

                        var list = formatter.Deserialize(fs) as List<SessionData>;
                        if (list != null)
                        {
                            SessionData data = null;
                            foreach (var item in list)
                            {
                                if (data != null && data.arrRequest.SequenceEqual(item.arrRequest))
                                    continue;
                                FiddlerApplication_BeforeRequest(new Session(item));
                                data = item;
                            }
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        public void DoQuit()
        {
            if (null != oSecureEndpoint) oSecureEndpoint.Dispose();
            Fiddler.FiddlerApplication.Shutdown();
            var list = oAllSessions.Select(s => new SessionData(s)).ToList();
            using (var fs = new FileStream("session.bin", FileMode.Create))
            {
                System.Runtime.Serialization.Formatters.Binary.BinaryFormatter formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                formatter.Serialize(fs, list);
            }
        }

        private void dgvSessions_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvSessions.SelectedRows.Count > 0)
            {
                var row = dgvSessions.SelectedRows[0];
                var session = (Session)row.Tag;
                if (session == null)
                    return;
                txtServer.Text = session.hostname;
                txtPath.Text = session.PathAndQuery;
                txtRequestContent.Text = requestContent(session);
                if (session.oResponse != null)
                {
                    txtResponseContent.Text = responseContent(session);
                }
                setGenerateClass(session);
            }
        }

        #region GenerateClass
        private Dictionary<string, string> classFiles;
        private void setGenerateClass(Session session)
        {
            var content = session.GetResponseBodyAsString();
            try
            {
                content = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(Fiddler.Utilities.UrlDecode(content)));
            }
            catch (Exception)
            {
            }

            culture = System.Threading.Thread.CurrentThread.CurrentCulture;
            classFiles = new Dictionary<string, string>();
            var data = JsonConvert.DeserializeObject<JObject>(content);
            var regex = new Regex(@"\?.+$");
            var className = regex.Replace(getRequestName(session).Replace("//", ""), "");
            var paths = className.Split('/');
            className = string.Empty;
            foreach (var item in paths)
            {
                className += culture.TextInfo.ToTitleCase(item);
            }
            className = className.Replace(".", "");
            this.className = className;
            var root = new KeyValuePair<string, JToken>("Res", data);
            GenerateResClass(root, true);
            GenerateReqClass(className, session);
            content = "";
            foreach (var item in classFiles)
            {
                content += item.Value + "\r\n";
            }
            foreach (var item in sameClass)
            {
                content = content.Replace(item.Key, item.Value);
            }
            txtReponseClass.Text = content;
        }
        Dictionary<string, string> sameClass = new Dictionary<string, string>();
        string[] ignoreType = new string[] { "fail" };
        private void GenerateMember(StringBuilder sb, KeyValuePair<String, JToken> obj, int ident = 1)
        {
            if (obj.Value.Type == JTokenType.Array)
            {
                if (obj.Value.Count() == 0)
                    return;
            }
            var typeName = ignoreType.Contains(obj.Key) ? obj.Key : className + culture.TextInfo.ToTitleCase(obj.Key);
            sb.Append(' ', ident * 4);
            sb.Append("public ");
            if (obj.Value.Type == JTokenType.Array)
            {
                sb.Append("List<");
                if (obj.Value[0].Type == JTokenType.Object)
                {
                    GenerateResClass(obj);
                    sb.Append(typeName);
                }
                else if (obj.Value.Type == JTokenType.String)
                    sb.Append("string");
                else if (obj.Value.Type == JTokenType.Integer)
                    sb.Append("string");
                else
                    sb.Append("string");
                sb.Append(">");
            }
            else if (obj.Value.Type == JTokenType.Object)
            {
                GenerateResClass(obj);
                sb.Append(typeName);
            }
            else if (obj.Value.Type == JTokenType.String)
                sb.Append("string");
            else if (obj.Value.Type == JTokenType.Integer)
                sb.Append("int");
            else
                sb.Append("string");
            sb.AppendLine(" " + obj.Key + " { get; set; }");
        }

        private void GenerateReqClass(string name, Session oSession)
        {
            var className = name + "Req";
            if (classFiles.ContainsKey(className))
                return;
            var flags = string.IsNullOrEmpty(oSession.oRequest.headers["X-Unity-Version"]);
            var regex = new Regex(@"\?.+$");
            var url = flags ? regex.Replace(oSession.fullUrl, "") : regex.Replace(oSession.PathAndQuery, "");
            var replaceArr = new string[] { "timestamp", "sign" };

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(@"
public class {0}Req: BaseRequest<{0}Res>
{
    public {0}Req(ServerApi api) : base(api)
    {
    }

    public override async Task<{0}Res> Send(params string[] args)
    {".Replace("{0}", name));
            if (flags)
            {
                sb.AppendLine(@"        httpClient = new System.Net.Http.HttpClient();");
                sb.AppendLine(@"        httpClient.DefaultRequestHeaders.Add(""User-Agent"", ""Mozilla/5.0 BSGameSDK"");");
            }
            var postDic = getQueryDic(oSession.GetRequestBodyAsString());
            if (postDic.ContainsKey("ac"))
                sb.AppendLine(@"             PlatfromInfos[""ac""] = """ + postDic["ac"] + @""";");
            if (postDic.ContainsKey("key"))
                sb.AppendLine(@"             PlatfromInfos[""key""] = """ + postDic["key"] + @""";");

            sb.AppendLine(@"        var url = """ + url + @""";");
            var getDic = getQueryDic(oSession.PathAndQuery);
            if (getDic.Keys.Count > 0)
            {
                var getParam = getDic.Select(d => "\"" + d.Key + "\"").Aggregate((c, p) => c + "," + p);
                sb.AppendLine(@"        string getParam = null;");
                sb.AppendLine(@"        getParam = getPlatfromInfo(new string[]{" + getParam + @"});");
                sb.AppendLine(@"        if (!string.IsNullOrEmpty(getParam))");
                sb.AppendLine(@"            url += ""?"" + getParam;");
            }
            if (oSession.RequestMethod == "POST")
            {
                sb.AppendLine(@"        Dictionary<string,string> postParam = null;");
                if (postDic.Keys.Count > 0)
                {
                    var postParam = postDic.Select(d => "\"" + d.Key + "\"").Aggregate((c, p) => c + "," + p);
                    sb.AppendLine(@"        postParam = getPlatfromInfoDic(new string[]{" + postParam + @"});");
                }
                if (!flags)
                    sb.AppendLine(@"        var response = await Post(url, postParam);");
                else
                    sb.AppendLine(@"        var response = await Post(url, postParam, false, false, false);");
            }
            else
            {
                if (!flags)
                    sb.AppendLine(@"        var response = await Get(url);");
                else
                    sb.AppendLine(@"        var response = await Get(url, false, false, false);");
            }

            sb.AppendLine(@"        if (response.code == 0)
            {
                PlatfromInfos[""usk""] = response.response[0].usk;
            }");
            sb.AppendLine(@"        return response;");
            sb.AppendLine("\r\n    }\r\n}");
            classFiles.Add(className, sb.ToString());
        }

        private void GenerateResClass(KeyValuePair<String, JToken> obj, bool top = false)
        {
            if (obj.Value == null)
                return;
            if (obj.Key == "fail")
                return;
            if (classFiles.ContainsKey(obj.Key))
                return;
            if (obj.Value.Type != JTokenType.Object && obj.Value.Type != JTokenType.Array)
                return;
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat(@"public class {0}{1}", className + culture.TextInfo.ToTitleCase(obj.Key), top ? " : BaseResponse" : string.Empty);
            sb.AppendLine("\r\n{");
            if (top)
            {
                sb.AppendLine(@"
    public override int code
    {
        get
        {
            return response != null && response.Count > 0 ? int.Parse(response[0].resCode) : 99;
        }

        set
        {
            base.code = value;
        }
    }
    public override string message
    {
        get
        {
            return response != null && response.Count > 0 && response[0].fail != null ? response[0].fail.title + "" "" + response[0].fail.detail : base.message;
        }

        set
        {
            base.message = value;
        }
    }");
            }
            JToken objec = obj.Value;

            if (obj.Value.Type == JTokenType.Array)
            {
                if (obj.Value.Count() == 0)
                    return;
                objec = obj.Value[0];
                if (!(objec is JObject))
                    return;
            }

            foreach (var item in (JObject)objec)
            {
                GenerateMember(sb, item);
            }
            sb.Append("}");
            var regex = new Regex("public class \\S+");
            var content = sb.ToString();
            var clClass = regex.Replace(content, string.Empty);
            foreach (var item in classFiles)
            {
                if (clClass == regex.Replace(item.Value, string.Empty))
                {
                    sameClass[className + culture.TextInfo.ToTitleCase(obj.Key)] = className + culture.TextInfo.ToTitleCase(item.Key);
                    return;
                }
            }
            if (!classFiles.ContainsKey(obj.Key))
                classFiles.Add(obj.Key, content);
        }
        #endregion

        private string requestContent(Session session)
        {
            var dic = getQueryDic(session.PathAndQuery);
            var body = session.GetRequestBodyAsString();
            var content = body + "\r\n\r\nQuery:\r\n" + JsonConvert.SerializeObject(dic, Formatting.Indented);

            dic.Clear();
            foreach (var item in session.oRequest.headers)
            {
                dic[item.Name] = item.Value == null ? string.Empty : item.Value;
            }
            content += "\r\n\r\nHeader:\r\n" + JsonConvert.SerializeObject(dic, Formatting.Indented);
            if (body.IndexOf("=") != -1)
            {
                dic = getQueryDic(body);
                content += "\r\n\r\nContent:\r\n" + JsonConvert.SerializeObject(dic, Formatting.Indented);
            }
            return content;
        }


        Regex queryRegex = new Regex("([^&=?]+)=([^&=?]*)");
        public Dictionary<string, string> getQueryDic(string query, Dictionary<string, string> dic = null)
        {
            if (dic == null)
                dic = new Dictionary<string, string>();
            var matchs = queryRegex.Matches(query);
            if (matchs.Count > 0)
            {
                foreach (Match item in matchs)
                {
                    dic[item.Groups[1].Value] = Fiddler.Utilities.UrlDecode(item.Groups[2].Value.Replace('+', ' '));
                }
            }
            return dic;
        }

        private string responseContent(Session session)
        {
            var content = session.GetResponseBodyAsString();
            try
            {
                content = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(Fiddler.Utilities.UrlDecode(content)));
                if (content.StartsWith("{"))
                    content = Newtonsoft.Json.JsonConvert.DeserializeObject<JObject>(content).ToString(Formatting.Indented);
            }
            catch (Exception)
            {
            }
            var dic = new Dictionary<string, string>();
            try
            {
                foreach (var item in session.oResponse.headers)
                {
                    dic[item.Name] = item.Value == null ? string.Empty : item.Value;
                }
                content += "\r\n\r\nHeader:\r\n" + JsonConvert.SerializeObject(dic, Formatting.Indented);
            }
            catch (Exception)
            {
            }
            return content;
        }

        private void dgvSessions_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            dgvSessions_SelectionChanged(sender, e);
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            DoQuit();
        }

        private void 清空ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Monitor.Enter(oAllSessions);
            oAllSessions.Clear();
            dgvSessions.Rows.Clear();
            dgvIndex.Clear();
            platfromInfos.Clear();
            Monitor.Exit(oAllSessions);
        }

        private string className;
        private CultureInfo culture;

        private void btnSaveClass_Click(object sender, EventArgs e)
        {
            var txt = @"using System.Threading.Tasks;
using Unreal.FGO.Core.Api;
using System.Collections.Generic;

namespace Unreal.FGO.Core.Api
{
    " + txtReponseClass.Text.Replace("\r\n", "\r\n    ") + @"
}

namespace Unreal.FGO.Core
{
    public partial class ServerApi
    {
        public async Task<" + className + @"Res> " + className + @"()
        {
            return await new " + className + @"Req(this).Send();
        }
    }
}";
            if (!File.Exists("../../../Unreal.FGO.Core/Api/" + className + ".cs"))
                File.WriteAllText("../../../Unreal.FGO.Core/Api/" + className + ".cs", txt);
        }

        private void dgvSessions_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            dgvSessions_SelectionChanged(sender, e);
        }
    }
}
