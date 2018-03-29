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

        public static int platform = int.Parse(System.Configuration.ConfigurationManager.AppSettings["platform"]);
        public static int taskCount = int.Parse(System.Configuration.ConfigurationManager.AppSettings["taskCount"]);
        public static int dataCount = int.Parse(System.Configuration.ConfigurationManager.AppSettings["dataCount"]);
        public static int taskDelay = int.Parse(System.Configuration.ConfigurationManager.AppSettings["taskDelay"]);


        public static user admin { get; private set; }

        static void Main(string[] args)
        {
            InitLog4Net();
            var config = ConfigurationFactory.ParseString(@"
                            akka {  
                                stdout-loglevel = DEBUG
                                loglevel = DEBUG
                                actor {
                                    provider = ""Akka.Remote.RemoteActorRefProvider, Akka.Remote""
                                }
                                remote {
                                    helios.tcp {
                                        transport-class = ""Akka.Remote.Transport.Helios.HeliosTcpTransport, Akka.Remote""
                                        applied-adapters = []
                                        transport-protocol = tcp
                                        hostname = localhost
                                    }
                                }
                            }").WithFallback(ConfigurationFactory.ParseString("akka.remote.retry-gate-closed-for = 60s"));
            system = ActorSystem.Create("console", config);
            Db db = null;
            try
            {
                db = new Db();
                admin = db.users.Where(u => u.username == "super_admin").FirstOrDefault();
            }
            catch (Exception ex)
            {
                CloseApp();
                return;
            }

            var dataVer = db.GetSystemInfoById("dataVer").value;
            var time = DateTime.Now.Date;
            var roles = db.userRoles.AsNoTracking().Where(u => !u.chaptered && ((u.user_id == 0 && u.inited == true) || (u.user_id == admin.id && u.inited == true) && db.devices.Any(d => d.id == u.device_id && d.platform_type == platform))).OrderBy(r => r.last_task_time).Take(dataCount).ToList();
            var ids = roles.Select(r => r.id.ToString()).Aggregate((a, b) => a + "," + b);
            db.Database.ExecuteSqlCommand("update user_role set last_task_time=@p0 where id in(" + ids + ")", DateTime.Now);
            db.Dispose();

            Console.WriteLine("加载资料库");
            AssetManage.LoadDatabase(dataVer);
            Console.WriteLine(roles.Count + "个帐号");
            int i = 0;
            if (roles.Count > 0)
            {
                for (int k = 0; k < taskCount; k++)
                {
                    Task.Factory.StartNew(async () =>
                    {
                        bool end = false;
                        user_role role = null;
                    start:
                        Monitor.Enter(typeof(Program));
                        if (!registError && i < roles.Count)
                        {
                            role = roles[i];
                            i++;
                        }
                        else
                            end = true;
                        Monitor.Exit(typeof(Program));

                        if (!end)
                        {
                            await Battle(role);
                            goto start;
                        }

                        Interlocked.Increment(ref threadEnd);
                        logger.Info("Task" + threadEnd + " End");
                        if (threadEnd >= taskCount * 0.8)
                            CloseApp();
                    });
                    if (taskDelay > 0)
                        Thread.Sleep(taskDelay);
                }
            }
            else
            {
                Thread.Sleep(TimeSpan.FromMinutes(1));
                CloseApp();
            }
            Thread.Sleep(TimeSpan.FromHours(1));
            CloseApp();
        }

        private static void CloseApp()
        {
            System.Diagnostics.Process.Start(System.Reflection.Assembly.GetExecutingAssembly().Location);
            System.Diagnostics.Process.GetCurrentProcess().Kill();
        }

        public static Random rnd = new Random();

        private static async Task Battle(user_role role)
        {
            logger.Info(role.username);
            var db = new Db();
            var roleData = db.GetRoleDataByRoleId(role.id);
            var device = db.GetDeviceById(role.device_id);
        battleStart:
            var result = await system.ActorOf<BattleSetup>().Ask<BattleSetupResult>(new BattleSetupParam()
            {
                questId = "1000000",
                roleId = role.id,
            });
            if (result.code != 0)
            {
                if (result.code == -500)
                {
                    Interlocked.Increment(ref loginError);
                }
                if (loginError > 10)
                {
                    registError = true;
                    return;
                }
                logger.Error(role.username + "开始战斗失败：" + result.message);
                return;
            }
            var sec = rnd.Next((int)TimeSpan.FromMinutes(1).TotalSeconds, (int)TimeSpan.FromMinutes(3).TotalSeconds);
            var sleepTime = (TimeSpan.FromSeconds(sec));
            logger.Info("战斗开始成功，等待" + sleepTime.Minutes + "分" + sleepTime.Seconds + "秒");
            await Task.Delay(sleepTime);

            var rresult = await system.ActorOf<BattleResult>().Ask<BattleResultResult>(new BattleResultParam()
            {
                roleId = role.id,
                battleId = result.battleId
            });

            if (rresult.code != 0)
            {
                Interlocked.Increment(ref loginError);
                if (loginError > 10)
                {
                    registError = true;
                    return;
                }
                logger.Error(role.username + "开始结束失败：" + rresult.message);
                return;
            }
            Interlocked.Exchange(ref loginError, 0);
            logger.Info(role.username + "结束战斗成功");
            goto battleStart;
        }

    }
}
