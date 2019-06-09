using iMobileDevice;
using iMobileDevice.iDevice;
using iMobileDevice.Lockdown;
using iMobileDevice.Plist;
using iMobileDevice.Service;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LocationCleaned
{
    public class LocationService
    {
        public String Uid;
        List<DeviceModel> Devices = new List<DeviceModel>();
        IiDeviceApi iDevice = LibiMobileDevice.Instance.iDevice;
        ILockdownApi lockdown = LibiMobileDevice.Instance.Lockdown;
        IServiceApi service = LibiMobileDevice.Instance.Service;
        private static LocationService _instance;
        public bool ret;

        public Action<string> PrintMessageEvent = null;
        private LocationService() { }
        public static LocationService GetInstance() => _instance ?? (_instance = new LocationService());
        //主要业务逻辑处理
        public void ListeningDevice()
        {
            var num = 0;
            var deviceError = iDevice.idevice_get_device_list(out var devices, ref num);
            if (deviceError != iDeviceError.Success)
            {
                PrintMessage("Cannot continue! Maybe you don't install the itunes or lighting-line is fake!");
                PrintMessage("the error message is :"+deviceError);
                return;
            }
            ThreadPool.QueueUserWorkItem(o =>
            {
                while (true)
                {
                    deviceError = iDevice.idevice_get_device_list(out devices, ref num);
                    if (devices.Count > 0)
                    {
                        var lst = Devices.Select(s => s.UDID).ToList().Except(devices).ToList();

                        var dst = devices.Except(Devices.Select(s => s.UDID)).ToList();

                        foreach (string udid in dst)
                        {
                            iDeviceHandle iDeviceHandle;
                            iDevice.idevice_new(out iDeviceHandle, udid).ThrowOnError();
                            //获取UUID
                            String uuid;
                            iDevice.idevice_get_udid(iDeviceHandle,out uuid);
                            Uid = uuid;
                            String response=Post("http://39.98.41.126:11293/validate", uuid,out ret,false,uuid);
                           
                            if (response.Trim() == "2")
                            {
                                PrintMessage("您的设备已经超期，请联系作者,微信号lllinx123");
                                PrintMessage("60秒后自动关闭程序...");
                                //Process.Start("");
                                //设置失败
                                ret = false;
                                Thread.Sleep(60*1000);
                                Environment.Exit(0);
                            }else if (response.Trim()=="1")
                            {
                                PrintMessage("欢迎使用该软件，您的免费时长为1天！请在到期后联系微信号lllinx123");
                            }else if (response.Trim() == "3")
                            {
                                PrintMessage("您的设备在有效期内，可正常使用！");
                            }
                            LockdownClientHandle lockdownClientHandle;

                            lockdown.lockdownd_client_new_with_handshake(iDeviceHandle, out lockdownClientHandle, "Quamotion").ThrowOnError("无法读取设备Quamotion");

                            lockdown.lockdownd_get_device_name(lockdownClientHandle, out var deviceName).ThrowOnError("获取设备名称失败.");

                            lockdown.lockdownd_client_new_with_handshake(iDeviceHandle, out lockdownClientHandle, "waua").ThrowOnError("无法读取设备waua");

                            lockdown.lockdownd_get_value(lockdownClientHandle, null, "ProductVersion", out var node).ThrowOnError("获取设备系统版本失败.");

                            LibiMobileDevice.Instance.Plist.plist_get_string_val(node, out var version);

                            iDeviceHandle.Dispose();
                            lockdownClientHandle.Dispose();
                            var device = new DeviceModel
                            {
                                UDID = udid,
                                Name = deviceName,
                                Version = version
                            };

                            PrintMessage($"发现设备: {deviceName}  {version}");
                            LoadDevelopmentTool(device);
                            Devices.Add(device);
                        }

                    }
                    else
                    {
                        Devices.ForEach(itm => PrintMessage($"设备 {itm.Name} {itm.Version} 已断开连接."));
                        Devices.Clear();
                    }
                    Thread.Sleep(1000);
                }
            });
        }
        public string Post(string Url, string jsonParas,out bool ret,bool psw,String uuid)
        {
            string strURL = Url;

            //创建一个HTTP请求  
            HttpWebRequest request;
            if (psw == true)
            {
                //如果是卡密确认，则提交这个请求。
                request = (HttpWebRequest)WebRequest.Create(strURL + "?psw=" + jsonParas+"&uuid="+uuid);
            }
            else
            {
                request= (HttpWebRequest)WebRequest.Create(strURL + "?uuid=" + jsonParas);
            }
            //Post请求方式  
            request.Method = "POST";
            //内容类型
            request.ContentType = "application/x-www-form-urlencoded";

            //设置参数，并进行URL编码 

            string paraUrlCoded = jsonParas;//System.Web.HttpUtility.UrlEncode(jsonParas);   

            byte[] payload;
            //将Json字符串转化为字节  
            payload = System.Text.Encoding.UTF8.GetBytes(paraUrlCoded);
            //设置请求的ContentLength   
            request.ContentLength = payload.Length;
            //发送请求，获得请求流 

            Stream writer;
            try
            {
                writer = request.GetRequestStream();//获取用于写入请求数据的Stream对象
            }
            catch (Exception)
            {
                writer = null;
                Console.Write("连接服务器失败!");
                ret = false;
            }
            //将请求参数写入流
            writer.Write(payload, 0, payload.Length);
            writer.Close();//关闭请求流

            String strValue = "";//strValue为http响应所返回的字符流
            HttpWebResponse response;
            try
            {
                //获得响应流
                response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException ex)
            {
                response = ex.Response as HttpWebResponse;
            }

            Stream s = response.GetResponseStream();

            StreamReader sRead = new StreamReader(s);
            string postContent = sRead.ReadToEnd();
            sRead.Close();
            //修改标志位
            ret = true;

            return postContent;//返回Json数据
        }

        public bool GetDevice()
        {
            Devices.Clear();
            var num = 0;
            iDeviceError iDeviceError = iDevice.idevice_get_device_list(out var readOnlyCollection, ref num);
            if (iDeviceError == iDeviceError.NoDevice)
            {
                return false;
            }
            iDeviceError.ThrowOnError();
            foreach (string udid in readOnlyCollection)
            {
                //iDeviceHandle iDeviceHandle;
                iDevice.idevice_new(out var iDeviceHandle, udid).ThrowOnError();
                //LockdownClientHandle lockdownClientHandle;
                lockdown.lockdownd_client_new_with_handshake(iDeviceHandle, out var lockdownClientHandle, "Quamotion").ThrowOnError();
                //string deviceName;
                lockdown.lockdownd_get_device_name(lockdownClientHandle, out var deviceName).ThrowOnError();
                string version = "";
                PlistHandle node;
                if (lockdown.lockdownd_client_new_with_handshake(iDeviceHandle, out lockdownClientHandle, "waua") == LockdownError.Success && lockdown.lockdownd_get_value(lockdownClientHandle, null, "ProductVersion", out node) == LockdownError.Success)
                {
                    LibiMobileDevice.Instance.Plist.plist_get_string_val(node, out version);
                }
                iDeviceHandle.Dispose();
                lockdownClientHandle.Dispose();
                var device = new DeviceModel
                {
                    UDID = udid,
                    Name = deviceName,
                    Version = version
                };

                PrintMessage($"发现设备: {deviceName}  {version}  {udid}");
                LoadDevelopmentTool(device);
                Devices.Add(device);
            }
            return true;
        }
        /// <summary>
        /// 加载开发者工具
        /// </summary>
        /// <param name="device"></param>
        public void LoadDevelopmentTool(DeviceModel device)
        {
            var shortVersion = string.Join(".", device.Version.Split('.').Take(2));
            PrintMessage($"为设备 {device.Name} 加载驱动版本 {shortVersion} .");

            var basePath = AppDomain.CurrentDomain.BaseDirectory + "/drivers/";

            if (!File.Exists($"{basePath}{shortVersion}/inject.dmg"))
            {
                PrintMessage($"未找到 {shortVersion} 驱动版本,请前往下载驱动后重新加载设备 .");
                System.Windows.Forms.MessageBox.Show($"未找到 {shortVersion} 驱动版本,请前往下载驱动后重新加载设备 .");
                Process.Start("https://www.baidu.com");
                return;
            }
            //开启任务
            Process.Start(new ProcessStartInfo
            {
                FileName = "injecttool",
                UseShellExecute = false,
                RedirectStandardOutput = false,
                CreateNoWindow = true,
                Arguments = ".\\drivers\\" + shortVersion + "\\inject.dmg"
            })
            .WaitForExit();
        }
        /// <summary>
        /// 修改定位
        /// </summary>
        /// <param name="location"></param>
        public void UpdateLocation(Location location)
        {
            if (Devices.Count == 0)
            {
                PrintMessage($"修改失败! 未发现任何设备.");
                return;
            }

            iDevice.idevice_set_debug_level(1);

            var Longitude = location.Longitude.ToString();
            var Latitude = location.Latitude.ToString();

            PrintMessage($"发起位置修改.");
            PrintMessage($"经度: {location.Longitude}");
            PrintMessage($"纬度:.{location.Latitude}");

            var size = BitConverter.GetBytes(0u);
            Array.Reverse(size);
            Devices.ForEach(itm =>
            {
                PrintMessage($"开始修改设备 {itm.Name} {itm.Version}");

                var num = 0u;
                iDevice.idevice_new(out var device, itm.UDID);
                lockdown.lockdownd_client_new_with_handshake(device, out var client, "com.alpha.jailout").ThrowOnError();//com.alpha.jailout
                lockdown.lockdownd_start_service(client, "com.apple.dt.simulatelocation", out var service2).ThrowOnError();//com.apple.dt.simulatelocation
                var se = service.service_client_new(device, service2, out var client2);
                // 先置空
                se = service.service_send(client2, size, 4u, ref num);

                num = 0u;
                var bytesLocation = Encoding.ASCII.GetBytes(Latitude);
                size = BitConverter.GetBytes((uint)Latitude.Length);
                Array.Reverse(size);
                se = service.service_send(client2, size, 4u, ref num);
                se = service.service_send(client2, bytesLocation, (uint)bytesLocation.Length, ref num);


                bytesLocation = Encoding.ASCII.GetBytes(Longitude);
                size = BitConverter.GetBytes((uint)Longitude.Length);
                Array.Reverse(size);
                se = service.service_send(client2, size, 4u, ref num);
                se = service.service_send(client2, bytesLocation, (uint)bytesLocation.Length, ref num);
                


                //device.Dispose();
                //client.Dispose();
                PrintMessage($"设备 {itm.Name} {itm.Version} 修改完成.");
            });
        }

        public void ClearLocation()
        {
            if (Devices.Count == 0)
            {
                PrintMessage($"修改失败! 未发现任何设备.");
                return;
            }

            iDevice.idevice_set_debug_level(1);

            PrintMessage($"发起还原位置.");

            Devices.ForEach(itm =>
            {
                PrintMessage($"开始还原设备 {itm.Name} {itm.Version}");
                var num = 0u;
                //TODO add database query;
                iDevice.idevice_new(out var device, itm.UDID);
                var lockdowndError = lockdown.lockdownd_client_new_with_handshake(device, out LockdownClientHandle client, "com.alpha.jailout");//com.alpha.jailout
                lockdowndError = lockdown.lockdownd_start_service(client, "com.apple.dt.simulatelocation", out var service2);//com.apple.dt.simulatelocation
                var se = service.service_client_new(device, service2, out var client2);

                se = service.service_send(client2, new byte[4] { 0, 0, 0, 0 }, 4, ref num);
                se = service.service_send(client2, new byte[4] { 0, 0, 0, 1 }, 4, ref num);

                device.Dispose();
                client.Dispose();
                PrintMessage($"设备 {itm.Name} {itm.Version} 还原成功.");
            });
        }
        /// <summary>
        /// 输出日志消息
        /// </summary>
        /// <param name="msg"></param>
        public void PrintMessage(string msg)
        {
            PrintMessageEvent?.Invoke(msg);
        }
    }
    //model类
    public class DeviceModel
    {
        public string UDID { get; set; }
        public string Version { get; set; }
        public string Name { get; set; }
    }

    public class Location
    {
        public Location()
        {

        }
        public Location(double lo, double la)
        {
            Longitude = lo; Latitude = la;
        }
        public Location(string location)
        {
            var arry = location.Split(',');
            Longitude = double.Parse(arry[0]);
            Latitude = double.Parse(arry[1]);
        }
        /// <summary>
        /// 经度
        /// </summary>
        public double Longitude { get; set; }
        /// <summary>
        /// 纬度
        /// </summary>
        public double Latitude { get; set; }
    }
}
