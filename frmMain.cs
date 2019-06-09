using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using iMobileDevice;
using iMobileDevice.iDevice;
using iMobileDevice.Lockdown;
using iMobileDevice.Service;

namespace LocationCleaned
{
    public partial class frmMain : Form
    {
        frmMap map = new frmMap();
        LocationService service;
        public frmMain()
        {
            //初始化组件，new各种组件
            InitializeComponent();
        }
        //主界面的backgroud加载任务，主要是获取移动设备
        private void frmMain_Load(object sender, EventArgs e)
        {
            NativeLibraries.Load();
            //获取实例
            service = LocationService.GetInstance();
            service.PrintMessageEvent = PrintMessage;
            //监听实例
            service.ListeningDevice();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            map.ShowDialog();
            txtLocation.Text = $"{map.Location.Longitude} : {map.Location.Latitude}";
        }

        private void button2_Click(object sender, EventArgs e)
        {
            //var location = new Location(txtLocationTest.Text);
            //service.UpdateLocation(location);
            //先进行验证
            if (service.ret == false)
            {
                PrintMessage("设备已经过期，无法使用，请联系作者，微信号 lllinx123 ");
            }
            else
            {
                //地址的修改
                service.UpdateLocation(map.Location);
            }
     
        }

        public void PrintMessage(string msg)
        {
            if (rtbxLog.InvokeRequired)
            {
                this.Invoke(new Action<string>(PrintMessage), msg);
            }
            else
            {
                rtbxLog.AppendText($"{DateTime.Now.ToString("HH:mm:ss")}： {msg}\r\n");
            }
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            service.ClearLocation();
        }

        private void TxtLocation_TextChanged(object sender, EventArgs e)
        {
            
        }

        private void GroupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void Button3_Click(object sender, EventArgs e)
        {
            if (textBox1.Text == null)
            {
                PrintMessage("你输入的卡密为空，请确认后输入！");
            }
            else
            {
                //添加POST方法
                bool ret;
                String status=service.Post("http://39.98.41.126:11293/commit", textBox1.Text,out ret,true,service.Uid);
                //TODO对status的判断
                switch (status)
                {
                    case "1": {
                            PrintMessage("卡密错误或者已经使用！");
                            break; }
                    case "2":
                        {
                            PrintMessage("续约成功!重新重启软件后正常使用！");
                            service.ret = true;
                            break;
                        }
                    case "0":
                        {
                            PrintMessage("数据问题或者系统原因，请联系作者！");
                            break;
                        }
                }

            }
        }

        private void TextBox1_TextChanged(object sender, EventArgs e)
        {
            
        }
    }

}
