using Akka.Actor;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unreal.FGO.Console.Actor;
using Unreal.FGO.Console.Helper;
using Unreal.FGO.Repostory;
using Unreal.FGO.Repostory.Model;


public class AkkaMain
{
    public static Db Db = DbHelper.DB;
    public static void InitDB()
    {
        if (Db.users.Count() == 0)
        {
            var user = new user()
            {
                create_time = DateTime.Now,
                last_login_time = DateTime.Now,
                password = "ss30338475",
                username = "ss22219"
            };

            Db.users.Add(user);
            Db.SaveChanges();
            var preset = new device_preset()
            {
                platform_type = 1,
                dp = "1242*2208",
                model = "iPhone9,2",
                os = "iOS 10.2",
                pf_ver = "10.2",
                ptype = "iPhone9,2"
            };

            Db.devicePresets.Add(preset);
            Db.SaveChanges();

            var device = new device(preset) { user_id = user.id };
            device.GenerateDeviceid();
            Db.devices.Add(device);
            Db.SaveChanges();

            var userRole = new user_role()
            {
                device_id = device.id,
                create_time = DateTime.Now,
                last_update_time = DateTime.Now,
                password = "303384755",
                user_id = user.id,
                username = "ss22219@qq.com"
            };
            Db.userRoles.Add(userRole);
            Db.SaveChanges();

            Db.systemInfos.AddRange(
                new List<system_info>() {
                        new system_info()
                        {
                            name = "version",
                            value = "44"
                        },
                        new system_info()
                        {
                            name = "dataVer",
                            value = "44"
                        },
                        new system_info()
                        {
                            name = "appVer",
                            value = "1.8.8"
                        },
                        new system_info()
                        {
                            name = "dateVer",
                            value = "1483128000"
                        }
            });
            Db.SaveChanges();

            Db.userTasks.Add(new user_task()
            {
                action = "Login",
                enable = true,
                last_update_time = DateTime.Now,
                create_time = DateTime.Now,
                user_role_id = 1,
                user_id = 1
            });
            Db.roleData.Add(JsonConvert.DeserializeObject<role_data>("{\"role_id\":1,\"bilibili_id\":\"2657998\",\"rguid\":\"1673226\",\"id\":0,\"usk\":\"879cb2af38d7779e0092\",\"access_token\":\"d51c6663401b0138de0852884e11506c\",\"access_token_expires\":1485822519000,\"nickname\":\"幻影gool\",\"game_user_id\":\"100101673226\",\"face\":\"http://i1.hdslb.com/bfs/face/268e3bad390a60242450c10a09769993e78ec50c.jpg\",\"s_face\":\"http://i0.hdslb.com/bfs/face/268e3bad390a60242450c10a09769993e78ec50c.jpg\"}"));
            Db.SaveChanges();
        }
    }

    public static void Main(string[] args)
    {
        InitDB();
        var role = Db.GetUserRoleById(1);
        var device = Db.GetDeviceById(1);
        Db.Database.ExecuteSqlCommand("update user_task set state=0");
        ActorSystem system = ActorSystem.Create("root");
        {
            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    var tasks = Db.userTasks.Where(t => t.enable == true && t.action != "Login" && t.state != 1).ToList();
                    
                    foreach (var task in tasks)
                    {
                        system.ActorOf<TaskExcuteActor>().Tell(task.id);
                    }
                    await Task.Delay(10000);
                }
            });
            Console.ReadKey();
        }
    }
}
