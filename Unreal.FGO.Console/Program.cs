using Akka.Actor;
using Disunity;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unreal.FGO.Helper;
using Unreal.FGO.Core;
using Unreal.FGO.Core.Api;
using AutoMapper;
using Unreal.FGO.Common;
using Unreal.FGO.TaskService.TaskExcutor;
using Unreal.FGO.Common.ActorResult;
using Unreal.FGO.Common.ActorParam;
using Unreal.FGO.Repostory;
using Akka.Configuration;
using Unreal.FGO.Repostory.Model;
using log4net.Config;
using log4net;
using Unreal.FGO.TaskService.Actor;
using System.Configuration;

namespace Unreal.FGO
{
    class Program
    {
        static ActorSystem system;

        private static void InitLog4Net()
        {
            var logCfg = new FileInfo(AppDomain.CurrentDomain.BaseDirectory + "log4net.config");
            XmlConfigurator.ConfigureAndWatch(logCfg);
        }
        public static int loginError = 0;
        public static int threadEnd = 0;
        public static bool registError = false;
        static ILog logger = LogManager.GetLogger(typeof(Program));

        public static user admin { get; private set; }

        static void Main(string[] args)
        {
            InitLog4Net();
            system = ActorSystem.Create("regist");
            try
            {
                var db = new Db();
                admin = db.users.Where(u => u.username == "super_admin").FirstOrDefault();
                var dataVer = db.GetSystemInfoById("dataVer").value;
                db.Dispose();
                AssetManage.LoadDatabase(dataVer);
                logger.Info("启动成功");
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                CloseApp();
                return;
            }
            for (int k = 0; k < taskCount; k++)
            {
                Task.Factory.StartNew(async () =>
                {
                    while (!registError)
                    {
                        await Regist();
                    }
                    Interlocked.Increment(ref threadEnd);
                    if (threadEnd == taskCount)
                        CloseApp();
                });
                if (taskDelay > 0)
                    Thread.Sleep(taskDelay);
            }
            Thread.Sleep(TimeSpan.FromHours(1));
            CloseApp();
        }

        private static void CloseApp()
        {
            try
            {
                var db = new Db();
                db.Database.ExecuteSqlCommand(@"update devices set user_id=@p0 where user_id=0", admin.id);
                db.Database.ExecuteSqlCommand(@"update user_role set user_id=@p0 where user_id=0 and inited=1", admin.id);
                db.Dispose();
            }
            catch (Exception)
            {
            }
            System.Diagnostics.Process.Start(System.Reflection.Assembly.GetExecutingAssembly().Location);
            System.Diagnostics.Process.GetCurrentProcess().Kill();
        }
        public static int platform = int.Parse(System.Configuration.ConfigurationManager.AppSettings["platform"]);
        public static int taskCount = int.Parse(System.Configuration.ConfigurationManager.AppSettings["taskCount"]);
        public static int taskDelay = int.Parse(System.Configuration.ConfigurationManager.AppSettings["taskDelay"]);

        private static async Task Regist()
        {
            if (registError)
                return;

            RegistStart:
            var username = GenerateUserName();
            logger.Info(username);

            var registRes = await system.ActorOf<Regist>().Ask<RegistResut>(new RegistParam()
            {
                paltform = platform,
                password = GeneratePwd(),
                userId = 0,
                username = username
            });

            if (registRes.code != 0)
            {
                logger.Info("注册失败");
                logger.Error(registRes.message);
                if (registRes.message.IndexOf("bili account server error") != -1)
                {
                    registError = true;
                    return;
                }

                Interlocked.Increment(ref loginError);
                if (loginError > 10)
                {
                    registError = true;
                    return;
                }
            }
            else
                logger.Info("注册成功");
            if (registRes.code == 0)
            {
                var db = new Db();
                var role = db.GetUserRoleById(registRes.roleId);
                if (role == null)
                {
                    logger.Error("角色为空");
                    goto RegistStart;
                }
                db.Dispose();
                await Task.Delay(10000);
            }
        }

        private static string GenerateUserName()
        {
            var length = new Random().Next(6, 12);
            return GetRandomString(length, true, true);
        }

        private static string GeneratePwd()
        {
            return GetRandomString(6);
        }

        public static string GetRandomString(int length, bool useNum = true, bool useLow = true, bool useUpp = false, bool useSpe = false, string custom = "")
        {
            byte[] b = new byte[4];
            new System.Security.Cryptography.RNGCryptoServiceProvider().GetBytes(b);
            Random r = new Random(BitConverter.ToInt32(b, 0));
            string s = null, str = custom;
            if (useNum == true) { str += "0123456789"; }
            if (useLow == true) { str += "abcdefghijklmnopqrstuvwxyz"; }
            if (useUpp == true) { str += "ABCDEFGHIJKLMNOPQRSTUVWXYZ"; }
            if (useSpe == true) { str += "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~"; }
            for (int i = 0; i < length; i++)
            {
                s += str.Substring(r.Next(0, str.Length - 1), 1);
            }
            return s;
        }
        //    var db = AssetManage.Database;
        //    foreach (var svt in AssetManage.Database.mstSvt)
        //    {
        //        for (int i = 0; i < 3; i++)
        //        {
        //            var task = AssetManage.GetAsset(svt.CharaFigureAssetName(i));
        //            task.Wait();
        //            var assetData = task.Result;
        //            if (assetData == null)
        //                Console.WriteLine(svt.CharaFigureAssetName(i) + " Not Find");
        //        }
        //    }
        //    ServerApi api = new ServerApi();
        //    api.Member().Wait();
        //    if (File.Exists("userData.json"))
        //    {
        //        var saveData = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("userData.json"));
        //        api.Update(saveData);
        //    }
        //    if (GetAccessToken(api))
        //    {
        //        File.WriteAllText("userData.json", JsonConvert.SerializeObject(api.SaveData()));
        //        if (Login(api))
        //        {
        //            Console.WriteLine("登陆成功");
        //            var qids = home.replaced.userQuest.Select(q => q.questId);
        //            var qs = db.mstQuest.Where(q1 => qids.Contains(q1.id));
        //            qs = qs.Where(q => q.name.IndexOf("1日1次") != -1); 
        //            var item3 = int.Parse(home.replaced.userItem.Where(i => i.itemId == "94000903").First().num);
        //            Console.WriteLine("茶具3：" + item3);
        //            File.WriteAllText("userData.json", JsonConvert.SerializeObject(api.SaveData()));
        //            while (item3 < 500)
        //            {
        //                if (api.ApNum >= 40)
        //                {

        //                    var setup = api.Battlesetup(home.replaced.userDeck[0].id, "100101077798", "94000931");
        //                    setup.Wait();
        //                    var setupResult = setup.Result;
        //                    if (setupResult.code != 0)
        //                    {
        //                        Console.WriteLine("battresult:");
        //                        Console.WriteLine(setupResult.message);
        //                        Console.WriteLine("Reqest:");
        //                        Console.WriteLine(setupResult.RequestMessage);
        //                        break;
        //                    }
        //                    File.WriteAllText("userData.json", JsonConvert.SerializeObject(api.SaveData()));

        //                    var battleId = setup.Result.cache.replaced.battle[0].id;
        //                    var random = new Random();
        //                    var time = random.Next((int)TimeSpan.FromMinutes(2).TotalMilliseconds, (int)TimeSpan.FromMinutes(3).TotalMilliseconds);
        //                    var timespan = TimeSpan.FromMilliseconds(time);
        //                    Console.WriteLine("战斗开始，等待" + timespan.Minutes + "分" + timespan.Seconds + "秒");
        //                    Thread.Sleep(time);
        //                    var count = random.Next(20, 35);
        //                    var action = new BattleresultActionItem[count];
        //                    for (int i = 0; i < count; i++)
        //                    {
        //                        action[i] = new BattleresultActionItem()
        //                        {
        //                            ty = random.Next(1, 4),
        //                            uid = random.Next(1, 4)
        //                        };
        //                    }

        //                    var battresult = api.Battleresult(battleId, action, new string[] { });
        //                    battresult.Wait();
        //                    if (battresult.Result.code != 0)
        //                    {
        //                        Console.WriteLine("battresult:");
        //                        Console.WriteLine(battresult.Result.message);
        //                        Console.WriteLine("Reqest:");
        //                        Console.WriteLine(battresult.Result.RequestMessage);
        //                        break;
        //                    }
        //                    File.WriteAllText("userData.json", JsonConvert.SerializeObject(api.SaveData()));
        //                    Thread.Sleep(2500);
        //                    var homeTask = api.Home();
        //                    homeTask.Wait();
        //                    var homeResult = homeTask.Result;
        //                    if (homeResult.code != 0)
        //                    {
        //                        Console.WriteLine("homeResult:");
        //                        Console.WriteLine(homeResult.message);
        //                        Console.WriteLine("Reqest:");
        //                        Console.WriteLine(homeResult.RequestMessage);
        //                        break;
        //                    }
        //                    home = homeResult.cache;
        //                    File.WriteAllText("userData.json", JsonConvert.SerializeObject(api.SaveData()));

        //                    Console.WriteLine("userId : " + api.PlatfromInfos["userId"]);
        //                    Console.WriteLine("name : " + home.replaced.userGame[0].name);
        //                    Console.WriteLine("lv : " + home.replaced.userGame[0].lv);
        //                    Console.WriteLine("ap : " + api.ApNum);
        //                }
        //                else
        //                {
        //                    Console.WriteLine("Ap不够了");
        //                    Thread.Sleep((40 - api.ApNum) * 300 * 1000);
        //                }

        //            }
        //        }
        //    }

        //    Console.ReadKey();
        //    while (true)
        //    {
        //        Console.Write("SvtId or Name:");
        //        var svtId = Console.ReadLine();
        //        if (string.IsNullOrWhiteSpace(svtId))
        //            continue;

        //        var svt = AssetManage.Database.mstSvt.GetById(svtId);
        //        if (svt == null)
        //        {
        //            Console.WriteLine("Not Svt");
        //            continue;
        //        }
        //        Console.WriteLine("SvtName:" + svt.name);
        //        Console.Write("Download?: y");
        //        var read = Console.ReadKey();
        //        Console.WriteLine();

        //        if (read.Key == ConsoleKey.Y)
        //        {
        //            var task = AssetManage.GetAsset(svt.CharaFigureAssetName(2));
        //            task.Wait();
        //            var assetData = task.Result;
        //            if (assetData == null)
        //                Console.WriteLine(svt.CharaFigureAssetName(2) + " Not Find");
        //        }
        //    }
        //    var files = Directory.GetFiles(AssetManage.AssetPath);

        //    foreach (var item in files.Where(f => Path.GetExtension(f) == ".assets"))
        //    {
        //        AssetBundleExtrator.ExtratAssetsFile(item);
        //    } 
        //    while (true)
        //    {
        //        Console.Write("SvtId or Name:");
        //        var svtId = Console.ReadLine();
        //        if (string.IsNullOrWhiteSpace(svtId))
        //            continue;
        //        var svt = AssetManage.Database.mstSvt.Where(s => s.id == svtId || s.name.Contains(svtId)).FirstOrDefault();
        //        Console.WriteLine(JsonConvert.SerializeObject(svt, Formatting.Indented));
        //    }
        //    var svtId = "200200";
        //    var svt = AssetManage.Database.mstSvt.Where(s => s.id == svtId || s.name.Contains(svtId)).FirstOrDefault();
        //    AssetManage.getAsset()
        //    if (!File.Exists(AssetManage.getBinName(item.NewName)))
        //    {
        //        webClient.DownloadFile(url, AssetManage.getBinName(item.NewName));
        //    }
        //    AssetManage.LoadAssetList();
        //    var webClient = new WebClient();
        //    foreach (var item in AssetManage.AssetList)
        //    {
        //        if (item.NewName.IndexOf("102200") != -1)
        //        {
        //            Console.WriteLine(item.NewName);
        //            var url = AssetManage.getUrlString(item);
        //            Console.WriteLine(url);

        //            if (!File.Exists(AssetManage.getBinName(item.NewName)))
        //            {
        //                webClient.DownloadFile(url, AssetManage.getBinName(item.NewName));
        //            }
        //        }
        //    }
        //    Console.ReadKey();
        //}


        //static bool GetAccessToken(ServerApi api)
        //{
        //    if (!api.PlatfromInfos.ContainsKey("access_token"))
        //    {
        //        var responseTask = api.ApiClientRsa();
        //        responseTask.Wait();
        //        var resResult = responseTask.Result;
        //        if (resResult.code != 0)
        //        {
        //            Console.WriteLine(resResult.message);
        //            return false;
        //        }
        //        //r497306309 111111
        //        //hsd70559 ott801
        //        var loginTask = api.ApiClientLogin("hsd70559", "ott801", resResult.rsa_key, resResult.hash);
        //        loginTask.Wait();
        //        var loginResult = loginTask.Result;

        //        if (loginResult.code != 0)
        //        {
        //            Console.WriteLine(loginResult.message);
        //            return false;
        //        }

        //        var clientUserInfoTask = api.ApiClientUserInfo(loginResult.access_key);
        //        clientUserInfoTask.Wait();
        //        var clientUserInfoResult = loginTask.Result;
        //        if (clientUserInfoResult.code != 0)
        //        {
        //            Console.WriteLine(clientUserInfoResult.message);
        //            return false;
        //        }
        //    }

        //    Console.WriteLine("欢迎您 " + api.PlatfromInfos["uname"]);
        //    Console.WriteLine("access_token : " + api.PlatfromInfos["access_token"]);
        //    return true;
        //}
        //static HomeCache home = null;
        //static bool Login(ServerApi api)
        //{
        //    if (!api.PlatfromInfos.ContainsKey("usk"))
        //    {
        //        var loginToMemberCenterTask = api.LoginToMemberCenter();
        //        loginToMemberCenterTask.Wait();
        //        var loginToMemberCenterResult = loginToMemberCenterTask.Result;

        //        if (loginToMemberCenterResult.code != 0)
        //        {
        //            Console.WriteLine("loginToMemberCenterError:");
        //            Console.WriteLine(loginToMemberCenterResult.message);
        //            Console.WriteLine("Reqest:");
        //            Console.WriteLine(loginToMemberCenterResult.RequestMessage);
        //            return false;
        //        }
        //        var loginTask = api.Login();
        //        loginTask.Wait();
        //        var loginResult = loginTask.Result;
        //        if (loginResult.code != 0)
        //        {
        //            Console.WriteLine("LoginError:");
        //            Console.WriteLine(loginResult.message);
        //            Console.WriteLine("Reqest:");
        //            Console.WriteLine(loginResult.RequestMessage);
        //            return false;
        //        }

        //        var ApiClientNotifyZoneTask = api.ApiClientNotifyZone();
        //        ApiClientNotifyZoneTask.Wait();
        //        var ApiClientNotifyZoneResult = ApiClientNotifyZoneTask.Result;
        //        if (ApiClientNotifyZoneResult.code != 0)
        //        {
        //            Console.WriteLine("ApiClientNotifyZone:");
        //            Console.WriteLine(ApiClientNotifyZoneResult.message);
        //            Console.WriteLine("Reqest:");
        //            Console.WriteLine(ApiClientNotifyZoneResult.RequestMessage);
        //            return false;
        //        }

        //        var toploginTask = api.Toplogin();
        //        toploginTask.Wait();
        //        var toploginResult = toploginTask.Result;
        //        if (toploginResult.code != 0)
        //        {
        //            Console.WriteLine("toploginResult:");
        //            Console.WriteLine(toploginResult.message);
        //            Console.WriteLine("Reqest:");
        //            Console.WriteLine(toploginResult.RequestMessage);
        //            return false;
        //        }
        //    }

        //    var homeTask = api.Home();
        //    homeTask.Wait();
        //    var homeResult = homeTask.Result;
        //    if (homeResult.code != 0)
        //    {
        //        if (homeResult.code == 88)
        //        {
        //            Console.WriteLine(homeResult.message);
        //            foreach (var item in ServerApi.saveNames)
        //            {
        //                api.PlatfromInfos.Remove(item);
        //            }
        //            if (GetAccessToken(api))
        //                return Login(api);
        //        }
        //        Console.WriteLine("homeResult:");
        //        Console.WriteLine(homeResult.message);
        //        Console.WriteLine("Reqest:");
        //        Console.WriteLine(homeResult.RequestMessage);
        //        return false;
        //    }
        //    home = homeResult.cache;

        //    Console.WriteLine("userId : " + api.PlatfromInfos["userId"]);
        //    Console.WriteLine("name : " + home.replaced.userGame[0].name);
        //    Console.WriteLine("lv : " + home.replaced.userGame[0].lv);
        //    Console.WriteLine("ap : " + api.ApNum);
        //    return true;
        //}

    }
}
