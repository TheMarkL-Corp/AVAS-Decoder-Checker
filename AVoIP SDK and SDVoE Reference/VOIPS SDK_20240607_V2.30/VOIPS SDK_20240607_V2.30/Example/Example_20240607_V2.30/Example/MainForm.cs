using System;
using System.Windows.Forms;
using Newtonsoft.Json;
using System.Drawing;
using System.Diagnostics;
using System.Threading.Tasks;
using VOIPS_LIB;
using System.Collections.Generic;
using System.IO;
using static VOIPS_LIB.VOIPS;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Net.Sockets;
using System.Text;

namespace Example
{
    public partial class MainForm : Form
    {
        VOIPS voips;
        ICRON_USB usb = new ICRON_USB();
        THUMBNAIL_AVP thumbnail_avp;
        string DeviceListFileName = $"{Environment.CurrentDirectory}\\device.txt";
        delegate void MyInvoke(string str);

        public MainForm()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.Text = "VOIPS Example V" + Application.ProductVersion;
#if DEBUG
            RunServer(null, false);
#else
            RunServer();
#endif

            VOIPS_PARAM_INIT();
        }

        private void VOIPS_PARAM_INIT()
        {
            int i;

            voips = new VOIPS();

            // set control server ip and http port
            //voips = new VOIPS("192.168.8.65");

            foreach (VOIPS.TIMING item in Enum.GetValues(typeof(VOIPS.TIMING)))
            {
                string Desc = VOIPS.GetEnumDescription(item);
                if (Desc == "") continue;

                CB_Timing.Items.Add(Desc);
                CB_Timing2.Items.Add(Desc);
                CB_MultiviewTiming.Items.Add(Desc);
            }

            foreach (VOIPS.VIDEOWALL_RESOLUTION item in Enum.GetValues(typeof(VOIPS.VIDEOWALL_RESOLUTION)))
            {
                string Desc = VOIPS.GetEnumDescription(item);
                if (Desc == "") continue;

                CB_VideoWallTiming.Items.Add(Desc);
            }

            foreach (VOIPS.VIDEOWALL_ASPECT_RATIO item in Enum.GetValues(typeof(VOIPS.VIDEOWALL_ASPECT_RATIO)))
                CB_AspectRatio2.Items.Add(VOIPS.GetEnumDescription(item));

            CB_AspectRatio.Items.Clear();
            CB_AspectRatio.Items.Add(VOIPS.GetEnumDescription(VOIPS.VIDEOWALL_ASPECT_RATIO.eFull));
            CB_AspectRatio.Items.Add(VOIPS.GetEnumDescription(VOIPS.VIDEOWALL_ASPECT_RATIO.eBest_Fit));

            foreach (VOIPS.EVENT_TYPES item in Enum.GetValues(typeof(VOIPS.EVENT_TYPES)))
            {
                CB_EventType.Items.Add(VOIPS.GetEnumDescription(item));
            }

            foreach (VOIPS.IP_MODE item in Enum.GetValues(typeof(VOIPS.IP_MODE)))
            {
                string Desc = VOIPS.GetEnumDescription(item);
                if (Desc == "") continue;
                CB_IpMode.Items.Add(Desc);
            }

            foreach (VOIPS.REF_TYPE item in Enum.GetValues(typeof(VOIPS.REF_TYPE)))
            {
                CB_Mode.Items.Add(VOIPS.GetEnumDescription(item));
            }

            foreach (VOIPS.AUDIO_STREAM item in Enum.GetValues(typeof(VOIPS.AUDIO_STREAM)))
            {
                CB_AudioMode.Items.Add(VOIPS.GetEnumDescription(item));
                CB_AudioMode2.Items.Add(VOIPS.GetEnumDescription(item));
            }

            foreach (VOIPS.DEVICE_GROUPS item in Enum.GetValues(typeof(VOIPS.DEVICE_GROUPS)))
            {
                CB_Group.Items.Add(VOIPS.GetEnumDescription(item));
            }

            CB_Group.SelectedIndex = 0;
            CB_Timing.SelectedIndex = (int)TIMING.e1080p60;
            CB_Timing2.SelectedIndex = (int)TIMING.e1080p60;
            CB_MultiviewTiming.SelectedIndex = (int)TIMING.e3840x2160p60;

            CB_VideoWallTiming.SelectedIndex = (int)VIDEOWALL_RESOLUTION.e1920x1080;

            CB_IpMode.SelectedIndex = 0;
            CB_Mode.SelectedIndex = 0;
            CB_AudioMode.SelectedIndex = 0;
            CB_Row.SelectedIndex = 0;
            CB_Column.SelectedIndex = 0;
            CB_AspectRatio.SelectedIndex = (int)VOIPS.VIDEOWALL_ASPECT_RATIO.eFull;
            CB_AspectRatio2.SelectedIndex = (int)VOIPS.VIDEOWALL_ASPECT_RATIO.eFull;
            CB_RxHdmiAudioSource.SelectedIndex = 0;
            CB_RxStereoAudioSource.SelectedIndex = 0;
            CB_AudioMode2.SelectedIndex = 0;
            CB_EventType.SelectedIndex = 7;
            CB_SET_MULTI_LINK_MODE.SelectedIndex = 1;

            for (i = 0; i < VOIPS.MultiviewTableInfo.Length; i++)
            {
                Button bn = new Button()
                {
                    Name = $"Bn_Layout_{i}",
                    Tag = i,
                    Width = 160,
                    Height = 100,
                    Image = (Bitmap)Properties.Resources.ResourceManager.GetObject($"Layout_{i}"),
                    Parent = flowLayoutPanel1,
                };
                ((Button)this.Controls.Find($"Bn_Layout_{i}", true)[0]).Click += Layout_Click;
            }
            Layout_Click(((Button)this.Controls.Find($"Bn_Layout_0", true)[0]), new EventArgs());

            if (File.Exists(DeviceListFileName))
            {
                voips.LOAD_DEVICE_LIST(DeviceListFileName);
            }
        }

        private void RunServer(string Filename = null, bool Hidden = true)
        {
            string path = "";

            var BR_Proc = Process.GetProcessesByName("controlserver");
            if (BR_Proc.Length > 0) return;

            if (Filename == null)
            {
                path = String.Format(@"{0}\control_server\", Environment.CurrentDirectory);
            }
            else
            {
                path = Filename;
            }

            var startInfo = new ProcessStartInfo();
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = true;
            if (Hidden)
            {
                startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            }
            startInfo.WorkingDirectory = path;
            startInfo.FileName = @"controlserver.exe";

            Task.Factory.StartNew(() =>
            {
                var proc = new Process();
                proc.StartInfo = startInfo;
                proc.Start();
                proc.WaitForExit();
            });
        }

        private void CloseServer()
        {
            Process[] MyProcess = Process.GetProcessesByName("controlserver");
            if (MyProcess.Length > 0)
            {
                MyProcess[0].Kill(); //關閉執行中的程式
            }
        }

        public void BindTreeView(TreeView treeView, string strJson)
        {
            treeView.Nodes.Clear();

            if (IsJOjbect(strJson))
            {
                JObject jo = (JObject)JsonConvert.DeserializeObject(strJson);

                foreach (var item in jo)
                {
                    TreeNode tree;
                    if (item.Value.GetType() == typeof(JObject))
                    {
                        tree = new TreeNode(item.Key);
                        AddTreeChildNode(ref tree, item.Value.ToString());
                        treeView.Nodes.Add(tree);
                    }
                    else if (item.Value.GetType() == typeof(JArray))
                    {
                        tree = new TreeNode(item.Key);
                        AddTreeChildNode(ref tree, item.Value.ToString());
                        treeView.Nodes.Add(tree);
                    }
                    else
                    {
                        tree = new TreeNode(item.Key + ":" + item.Value.ToString());
                        treeView.Nodes.Add(tree);
                    }
                }
            }
            if (IsJArray(strJson))
            {
                JArray ja = (JArray)JsonConvert.DeserializeObject(strJson);

                foreach (JObject item in ja)
                {
                    TreeNode tree = new TreeNode();
                    foreach (var itemOb in item)
                    {
                        TreeNode treeOb;
                        if (itemOb.Value.GetType() == typeof(JObject))
                        {
                            treeOb = new TreeNode(itemOb.Key);
                            AddTreeChildNode(ref treeOb, itemOb.Value.ToString());
                            tree.Nodes.Add(treeOb);

                        }
                        else if (itemOb.Value.GetType() == typeof(JArray))
                        {
                            treeOb = new TreeNode(itemOb.Key);
                            AddTreeChildNode(ref treeOb, itemOb.Value.ToString());
                            tree.Nodes.Add(treeOb);
                        }
                        else
                        {
                            treeOb = new TreeNode(itemOb.Key + ":" + itemOb.Value.ToString());
                            tree.Nodes.Add(treeOb);
                        }
                    }
                    treeView.Nodes.Add(tree);
                }
            }
            treeView.ExpandAll();
        }

        public void AddTreeChildNode(ref TreeNode parantNode, string value)
        {
            if (IsJOjbect(value))
            {
                JObject jo = (JObject)JsonConvert.DeserializeObject(value);
                foreach (var item in jo)
                {
                    TreeNode tree;
                    if (item.Value.GetType() == typeof(JObject))
                    {
                        tree = new TreeNode(item.Key);
                        AddTreeChildNode(ref tree, item.Value.ToString());
                        parantNode.Nodes.Add(tree);
                    }
                    else if (item.Value.GetType() == typeof(JArray))
                    {
                        tree = new TreeNode(item.Key);
                        AddTreeChildNode(ref tree, item.Value.ToString());
                        parantNode.Nodes.Add(tree);
                    }
                    else
                    {
                        tree = new TreeNode(item.Key + ":" + item.Value.ToString());
                        parantNode.Nodes.Add(tree);
                    }
                }
            }
            if (IsJArray(value))
            {
                JArray ja = (JArray)JsonConvert.DeserializeObject(value);

                foreach (JObject item in ja)
                {
                    TreeNode tree = new TreeNode();
                    parantNode.Nodes.Add(tree);
                    foreach (var itemOb in item)
                    {
                        TreeNode treeOb;
                        if (itemOb.Value.GetType() == typeof(JObject))
                        {
                            treeOb = new TreeNode(itemOb.Key);
                            AddTreeChildNode(ref treeOb, itemOb.Value.ToString());
                            tree.Nodes.Add(treeOb);

                        }
                        else if (itemOb.Value.GetType() == typeof(JArray))
                        {
                            treeOb = new TreeNode(itemOb.Key);
                            AddTreeChildNode(ref treeOb, itemOb.Value.ToString());
                            tree.Nodes.Add(treeOb);
                        }
                        else
                        {
                            treeOb = new TreeNode(itemOb.Key + ":" + itemOb.Value.ToString());
                            tree.Nodes.Add(treeOb);
                        }
                    }
                }
            }
        }

        public bool IsJOjbect(string value)
        {
            try
            {
                JObject ja = JObject.Parse(value);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool IsJArray(string value)
        {
            try
            {
                JArray ja = JArray.Parse(value);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        void Layout_Click(object sender, EventArgs e)
        {
            Button bn = sender as Button;
            int num = Convert.ToInt32(bn.Tag);
            TB_LayoutID.Text = num.ToString();

            Pn_MV.Controls.Clear();
            for (int i = 0; i < VOIPS.MultiviewTableInfo[num].WinQty; i++)
            {
                Panel Pn = new Panel()
                {
                    Name = $"Pn_WinID_{i}",

                    Width = (Pn_MV.Width / VOIPS.MultiviewTableInfo[num].Layout[i].WidthDivisor) * VOIPS.MultiviewTableInfo[num].Layout[i].WidthMultiple,
                    Height = (Pn_MV.Height / VOIPS.MultiviewTableInfo[num].Layout[i].HeightDivisor) * VOIPS.MultiviewTableInfo[num].Layout[i].HeightMultiple,
                    Location = new Point((Pn_MV.Width / VOIPS.MultiviewTableInfo[num].Layout[i].PositionX_Divisor) * VOIPS.MultiviewTableInfo[num].Layout[i].PositionX_Multiple,
                    (Pn_MV.Height / VOIPS.MultiviewTableInfo[num].Layout[i].PositionY_Divisor) * VOIPS.MultiviewTableInfo[num].Layout[i].PositionY_Multiple),
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = (VOIPS.MultiviewTableInfo[num].Layout[i].TargetID == "black") ? Color.Black : Color.DarkOrange,
                    Parent = Pn_MV,
                };

                if (VOIPS.MultiviewTableInfo[num].Layout[i].TargetID == "black") continue;

                Panel Pn2 = new Panel()
                {
                    Name = $"Pn_Info_{i}",
                    Width = Pn.Width,
                    Location = new Point(0, (Pn.Height - 25) / 2),
                    Parent = Pn,
                };

                TextBox tb = new TextBox
                {
                    Name = $"TB_WinID_{i}",
                    Font = new Font("Calibri", 11),
                    Dock = DockStyle.Fill,
                    //TextAlign = HorizontalAlignment.Center,
                    Parent = Pn2,
                };

                TextBox tb2 = new TextBox
                {
                    Name = $"TB_MappingID_{i}",
                    Font = new Font("Calibri", 11),
                    Width = 40,
                    Dock = DockStyle.Left,
                    Parent = Pn2,
                    Text = i.ToString(),
                };

                Label Lab = new Label()
                {
                    Name = $"Lab_WinID_{i}",
                    Text = $"ID {VOIPS.MultiviewTableInfo[num].Layout[i].WinID + 1}",
                    Dock = DockStyle.Top,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Parent = Pn,
                };


            }
        }

        private void Bn_DiscoveryS_Click(object sender, EventArgs e)
        {
            TB_DiscoveryS.Clear();
            BR_RESULT result = voips.Discovery();
            if (result.status != "SUCCESS") return;

            TB_DiscoveryS.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
            BindTreeView(treeView1, TB_DiscoveryS.Text);
        }

        private void Bn_DiscoveryS_Subset_Click(object sender, EventArgs e)
        {
            TB_DiscoveryS.Clear();
            BR_RESULT result = voips.DiscoverySubset();
            if (result.status != "SUCCESS") return;

            TB_DiscoveryS.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
            BindTreeView(treeView1, TB_DiscoveryS.Text);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            CloseServer();
        }

        private void Bn_ROUTE_VIDEO_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            BR_RESULT result = voips.ROUTE_VIDEO(TB_SourceMAC.Text, TB_DestinationMAC.Text);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_ROUTE_VIDEO_GENLOCK_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            BR_RESULT result = voips.ROUTE_VIDEO_GENLOCK(TB_SourceMAC.Text, TB_DestinationMAC.Text);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_ROUTE_VIDEO_FAST_SWITCH_KEEP_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            BR_RESULT result = voips.ROUTE_VIDEO_FAST_SWITCH_KEEP(TB_SourceMAC.Text, TB_DestinationMAC.Text, (VOIPS.TIMING)CB_Timing.SelectedIndex);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_ROUTE_VIDEO_FAST_SWITCH_STRETCH_Click(object sender, EventArgs e)
        {

            TB_Msg.Text = "";
            BR_RESULT result = voips.ROUTE_VIDEO_FAST_SWITCH_STRETCH(TB_SourceMAC.Text, TB_DestinationMAC.Text, (VOIPS.TIMING)CB_Timing.SelectedIndex);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_ROUTE_VIDEO_FAST_SWITCH_CROP_Click(object sender, EventArgs e)
        {

            TB_Msg.Text = "";
            BR_RESULT result = voips.ROUTE_VIDEO_FAST_SWITCH_CROP(TB_SourceMAC.Text, TB_DestinationMAC.Text, (VOIPS.TIMING)CB_Timing.SelectedIndex);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_ROUTE_AUDIO_HDMI_Click(object sender, EventArgs e)
        {

            TB_Msg.Text = "";
            BR_RESULT result = voips.ROUTE_AUDIO_HDMI(TB_SourceMAC.Text, TB_DestinationMAC.Text);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_SET_DEVICE_NAME2_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            BR_RESULT result = voips.SET_DEVICE_NAME(TB_MAC.Text, TB_Name.Text);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_REBOOT2_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            BR_RESULT result = voips.REBOOT(TB_MAC.Text);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });

            //voips.REBOOT(TB_MAC.Text);
        }

        private void Bn_FACTORY_DEFAULT2_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            var Result = voips.FACTORY_DEFAULT(TB_MAC.Text);
            TB_Msg.Text = JsonConvert.SerializeObject(Result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_NETWORK_SETTING_Click(object sender, EventArgs e)
        {
            BR_RESULT result;
            TB_Msg.Text = "";
            if (CB_IpMode.SelectedIndex == 0)
                result = voips.NETWORK_SETTING(TB_MAC.Text, IP_MODE.DHCP);
            else
                result = voips.NETWORK_SETTING(TB_MAC.Text, IP_MODE.STATIC, TB_IpAddr.Text, TB_Netmask.Text, TB_Gateway.Text);

            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Chk_SET_HELLO_STATUS_CheckedChanged(object sender, EventArgs e)
        {

            TB_Msg.Text = "";
            BR_RESULT result = voips.SET_HELLO_STATUS(TB_MAC.Text, Chk_SET_HELLO_STATUS.Checked);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_TX_STOP_VIDEO_Click(object sender, EventArgs e)
        {

            TB_Msg.Text = "";
            BR_RESULT result = voips.TX_STOP_VIDEO(TB_TxMAC.Text, Convert.ToInt32(TB_HdmiIndex.Text));
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_TX_START_VIDEO_Click(object sender, EventArgs e)
        {

            TB_Msg.Text = "";
            BR_RESULT result = voips.TX_START_VIDEO(TB_TxMAC.Text, Convert.ToInt32(TB_HdmiIndex.Text));
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_TX_DIVIDE_FRAME_RATE_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            BR_RESULT result = voips.TX_DIVIDE_FRAME_RATE(TB_TxMAC.Text, CB_Mode.SelectedIndex, Convert.ToInt32(TB_Value.Text));
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_TX_AUDIO_SOURCE_Click(object sender, EventArgs e)
        {

            TB_Msg.Text = "";
            BR_RESULT result = voips.TX_AUDIO_SOURCE(TB_TxMAC.Text, Convert.ToInt32(TB_AudioSource.Text));
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Chk_TX_HDCP_SUPPORT_Click(object sender, EventArgs e)
        {

            TB_Msg.Text = "";
            BR_RESULT result = voips.TX_HDCP_SUPPORT(TB_TxMAC.Text, Chk_TX_HDCP_SUPPORT.Checked);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Chk_TX_HDCP22_SUPPORT_Click(object sender, EventArgs e)
        {

            TB_Msg.Text = "";
            BR_RESULT result = voips.TX_HDCP22_SUPPORT(TB_TxMAC.Text, Chk_TX_HDCP22_SUPPORT.Checked);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_TX_STOP_AUDIO_Click(object sender, EventArgs e)
        {

            TB_Msg.Text = "";
            BR_RESULT result = voips.TX_STOP_AUDIO(TB_TxMAC.Text, CB_AudioMode.SelectedIndex);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_TX_START_AUDIO_Click(object sender, EventArgs e)
        {

            TB_Msg.Text = "";
            BR_RESULT result = voips.TX_START_AUDIO(TB_TxMAC.Text, CB_AudioMode.SelectedIndex);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_TX_SET_EDID_Click(object sender, EventArgs e)
        {

            TB_Msg.Text = "";
            BR_RESULT result = voips.TX_SET_EDID(TB_TxMAC.Text, TB_EDID.Text);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_RX_LEAVE_VIDEO_Click(object sender, EventArgs e)
        {

            TB_Msg.Text = "";
            BR_RESULT result = voips.RX_LEAVE_VIDEO(TB_RxMAC.Text, Convert.ToInt32(TB_VideoIndex.Text));
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_RX_SET_GENLOCK_Click(object sender, EventArgs e)
        {

            TB_Msg.Text = "";
            BR_RESULT result = voips.RX_SET_GENLOCK(TB_RxMAC.Text);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_RX_SET_GENLOCK_SCALING_Click(object sender, EventArgs e)
        {

            TB_Msg.Text = "";
            BR_RESULT result = voips.RX_SET_GENLOCK_SCALING(TB_RxMAC.Text, (VOIPS.TIMING)CB_Timing2.SelectedIndex);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_RX_SET_FAST_SWITCH_KEEP_Click(object sender, EventArgs e)
        {

            TB_Msg.Text = "";
            BR_RESULT result = voips.RX_SET_FAST_SWITCH_KEEP(TB_RxMAC.Text, (VOIPS.TIMING)CB_Timing2.SelectedIndex);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_RX_SET_FAST_SWITCH_STRETCH_Click(object sender, EventArgs e)
        {

            TB_Msg.Text = "";
            BR_RESULT result = voips.RX_SET_FAST_SWITCH_STRETCH(TB_RxMAC.Text, (VOIPS.TIMING)CB_Timing2.SelectedIndex);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_RX_SET_FAST_SWITCH_CROP_Click(object sender, EventArgs e)
        {

            TB_Msg.Text = "";
            BR_RESULT result = voips.RX_SET_FAST_SWITCH_CROP(TB_RxMAC.Text, (VOIPS.TIMING)CB_Timing2.SelectedIndex);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_RX_HDMI_AUDIO_SOURCE_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            BR_RESULT result = voips.RX_HDMI_AUDIO_SOURCE(TB_RxMAC.Text, Convert.ToInt32(CB_RxHdmiAudioSource.Text.Substring(0, 1)));
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_RX_ANALOG_AUDIO_SOURCE_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            BR_RESULT result = voips.RX_STEREO_AUDIO_SOURCE(TB_RxMAC.Text, Convert.ToInt32(CB_RxStereoAudioSource.Text.Substring(0, 1)));
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_RX_LEAVE_AUDIO_Click(object sender, EventArgs e)
        {

            TB_Msg.Text = "";
            BR_RESULT result = voips.RX_LEAVE_AUDIO(TB_RxMAC.Text, CB_AudioMode2.SelectedIndex);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private int UpdateEventID(int AfterID)
        {
            BR_EventResult result;
            int ID = AfterID;
            do
            {
                result = voips.QueryEvent(AfterID, 3000);
                if (result.result.events.Count > 0)
                {
                    AfterID = result.result.events[result.result.events.Count - 1].event_id;
                    ID = AfterID;
                }
            }
            while (result.result.events.Count > 10); // 0->10
            return ID;
        }

        private void Bn_GET_ALL_SETTINGS_Click(object sender, EventArgs e)
        {
            bool bFinish = false;
            string status = "start";
            int EventID = 0;
            DateTimeOffset TimeEnd = new DateTimeOffset();
            BR_RESULT result = new BR_RESULT();

            while (bFinish == false)
            {
                switch (status)
                {
                    case "start":
                        Bn_GET_ALL_SETTINGS.Enabled = false;
                        TB_Msg.Text = "";
                        TB_Info.Text = "";
                        status = "update id";
                        break;
                    case "update id":
                        EventID = UpdateEventID(EventID);
                        status = "get all settings";
                        break;
                    case "get all settings":
                        result = voips.GET_ALL_SETTINGS(Bn_MAC.Text);
                        TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
                        TimeEnd = DateTimeOffset.UtcNow.Add(TimeSpan.FromSeconds(5));
                        if (result.status == "PROCESSING")
                        {
                            TB_RequestID.Text = result.request_id.ToString();
                            status = "wait complete";
                        }
                        else if (result.status == "ERROR") //MAC輸入錯誤之類的
                        {
                            status = "exit";
                        }
                        else //機台斷線時
                        {
                            if (result.result.error.Count > 0)
                            {
                                status = "exit";
                            }
                        }
                        break;
                    case "wait complete":
                        BR_EventResult EventResult = voips.QueryEvent("REQUEST_COMPLETE", EventID, 100);
                        if (EventResult.status == "SUCCESS")
                        {
                            if (EventResult.result.events.Count > 0)
                            {
                                foreach (var item in EventResult.result.events)
                                {
                                    EventID = item.event_id;
                                    if (item.request_id == result.request_id)
                                    {
                                        status = "get data";
                                    }
                                }
                            }
                        }
                        if (DateTimeOffset.UtcNow >= TimeEnd)
                        {
                            status = "get data";
                        }
                        break;
                    case "get data":
                        BR_RESULT result2 = voips.GET_REQUEST((int)result.request_id);
                        TB_Info.Text = JsonConvert.SerializeObject(result2, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
                        if (Chk_TreeView2.Checked)
                        {
                            BindTreeView(treeView2, TB_Info.Text);
                        }
                        status = "exit";
                        break;
                    case "exit":
                        Bn_GET_ALL_SETTINGS.Enabled = true;
                        bFinish = true;
                        break;
                }
                Application.DoEvents();
            }
        }

        private void CB_Ver_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (CB_Row.SelectedIndex < 0) return;
            if (CB_Column.SelectedIndex < 0) return;
            CreateWall(CB_Row.SelectedIndex + 1, CB_Column.SelectedIndex + 1);
        }

        private void CB_Hor_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (CB_Row.SelectedIndex < 0) return;
            if (CB_Column.SelectedIndex < 0) return;
            CreateWall(CB_Row.SelectedIndex + 1, CB_Column.SelectedIndex + 1);
        }

        private void CreateWall(int Row, int Column)
        {
            int i, j;
            int BaseX = 10, BaseY = 10;
            int SizeW, SizeH;

            SizeW = (Pn_Wall_Right.Width - BaseX * 2 - 20) / Column;
            SizeH = (Pn_Wall_Right.Height - BaseY * 2 - 20) / Row;
            if (SizeW < 110) SizeW = 110;
            if (SizeH < 50) SizeH = 50;

            if (SizeW > 250) SizeW = 250;
            if (SizeH > 125) SizeH = 125;

            for (i = Pn_Wall_Right.Controls.Count - 1; i >= 0; i--)
                Pn_Wall_Right.Controls[i].Dispose();

            for (i = 0; i < Row; i++)
            {
                for (j = 0; j < Column; j++)
                {
                    Panel panel = new Panel
                    {
                        Name = $"Pn_Wall_{i}_{j}",
                        Width = SizeW,
                        Height = SizeH,
                        Location = new Point(BaseX + (j * SizeW), BaseY + i * SizeH),
                        BorderStyle = BorderStyle.FixedSingle,
                        BackColor = Color.SkyBlue,
                        Parent = Pn_Wall_Right
                    };

                    TextBox tb = new TextBox
                    {
                        Name = $"TB_Wall_{i}_{j}",
                        Parent = ((Panel)this.Controls.Find($"Pn_Wall_{i}_{j}", true)[0]),
                        Font = new Font("Calibri", 11),
                        Width = SizeW - 16,
                        Location = new Point(8, (SizeH - 25) / 2),
                    };
                }
            }
        }

        private void Bn_SET_VIDEO_WALL_Click(object sender, EventArgs e)
        {
            int i, j;
            VOIPS.WALL_PARAMETER vw = new VOIPS.WALL_PARAMETER();

            vw.SourceMAC = TB_VideoWall_Source.Text;
            vw.Resolution = VWResolution_TO_TIMING((VOIPS.VIDEOWALL_RESOLUTION)CB_VideoWallTiming.SelectedIndex);

            vw.BezelInfo.Top = Convert.ToInt16(TB_BezelTop.Text);
            vw.BezelInfo.Bottom = Convert.ToInt16(TB_BezelBottom.Text);
            vw.BezelInfo.Left = Convert.ToInt16(TB_BezelLeft.Text);
            vw.BezelInfo.Right = Convert.ToInt16(TB_BezelRight.Text);

            vw.AspectRation = (VOIPS.VIDEOWALL_ASPECT_RATIO)CB_AspectRatio.SelectedIndex;
            vw.FPS = Convert.ToInt16(TB_FPS.Text);

            vw.Width = Convert.ToInt16(TB_Width.Text);
            vw.Height = Convert.ToInt16(TB_Height.Text);

            vw.Row = CB_Row.SelectedIndex + 1;
            vw.Column = CB_Column.SelectedIndex + 1;

            for (i = 0; i < CB_Row.SelectedIndex + 1; i++)
            {
                for (j = 0; j < CB_Column.SelectedIndex + 1; j++)
                {
                    vw.DestinationMAC[i * (CB_Column.SelectedIndex + 1) + j] = ((TextBox)this.Controls.Find($"TB_Wall_{i}_{j}", true)[0]).Text;
                }
            }

            voips.SET_VIDEO_WALL(vw);
        }

        private VOIPS.TIMING VWResolution_TO_TIMING(VOIPS.VIDEOWALL_RESOLUTION resolution)
        {
            switch (resolution)
            {
                case VIDEOWALL_RESOLUTION.e1024x768:
                    return TIMING.e1024x768p60;
                case VIDEOWALL_RESOLUTION.e1280x768:
                    return TIMING.e1280x768p60;
                case VIDEOWALL_RESOLUTION.e1280x960:
                    return TIMING.e1280x960p60;
                case VIDEOWALL_RESOLUTION.e1280x1024:
                    return TIMING.e1280x1024p60;
                case VIDEOWALL_RESOLUTION.e1360x768:
                    return TIMING.e1360x768p60;
                case VIDEOWALL_RESOLUTION.e1400x1050:
                    return TIMING.e1400x1050p60;
                case VIDEOWALL_RESOLUTION.e1600x1200:
                    return TIMING.e1600x1200p60;
                case VIDEOWALL_RESOLUTION.e1680x1050:
                    return TIMING.e1680x1050p60;
                case VIDEOWALL_RESOLUTION.e1920x1200:
                    return TIMING.e1920x1200p60;
                case VIDEOWALL_RESOLUTION.e1280x720:
                    return TIMING.e720p60;
                case VIDEOWALL_RESOLUTION.e1920x1080:
                    return TIMING.e1080p60;
                case VIDEOWALL_RESOLUTION.e3840x2160:
                    return TIMING.e3840x2160p60;
                case VIDEOWALL_RESOLUTION.e4096x2160:
                    return TIMING.e4096x2160p60;
                default:
                    return TIMING.eEnd;
            }
        }

        private void Bn_SET_MULTI_VIEW_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            VOIPS.MULTIVIEW_PARAMETER mp = new VOIPS.MULTIVIEW_PARAMETER();

            mp.MultiviewDescription = TB_Description.Text;
            mp.LayoutID = Convert.ToInt16(TB_LayoutID.Text);
            mp.DestinationMAC = TB_Receiver.Text;
            mp.AspectRatio = (VOIPS.VIDEOWALL_ASPECT_RATIO)CB_AspectRatio2.SelectedIndex;
            mp.Resolution = (VOIPS.TIMING)CB_MultiviewTiming.SelectedIndex;

            int SourceQty = 0;
            for (int i = 0; i < VOIPS.MultiviewTableInfo[mp.LayoutID].WinQty; i++)
            {
                if (VOIPS.MultiviewTableInfo[mp.LayoutID].Layout[i].TargetID == "black")
                {
                    mp.SourceMAC[i] = "none";
                    continue;
                }

                mp.SourceMAC[i] = ((TextBox)this.Controls.Find($"TB_WinID_{i}", true)[0]).Text;
                //MAC一樣，則MappingID一樣，即可在不同分割畫面使用同一Source
                //if (SourceQty > 0)
                //{
                //    bool Check = false;
                //    for (int j = 0; j < SourceQty; j++)
                //    {
                //        if (mp.SourceMAC[i] == mp.SourceMAC[j])
                //        {
                //            Check = true;
                //            mp.MappingID[i] = mp.MappingID[j];
                //            break;
                //        }
                //    }
                //    if (Check == false)
                //    {
                //        mp.MappingID[i] = Convert.ToByte(i);
                //    }
                //}
                //else
                //{
                //    mp.MappingID[i] = Convert.ToByte(i);
                //}
                //mp.SourceInfo[i].Width = 1920;
                //mp.SourceInfo[i].Height = 1080;

                string MappingID = ((TextBox)this.Controls.Find($"TB_MappingID_{i}", true)[0]).Text;

                mp.MappingID[i] = (MappingID == "") ? Convert.ToByte(i) : Convert.ToByte(MappingID);
                mp.SourceInfo[i].Width = Convert.ToUInt16(TB_SourceWidth.Text);
                mp.SourceInfo[i].Height = Convert.ToUInt16(TB_SourceHeight.Text);

                SourceQty++;
            }

            mp.SourceQty = SourceQty;

            BR_RESULT result = voips.SET_MULTI_VIEW(mp);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_SET_COMMAND_Click(object sender, EventArgs e)
        {

        }

        private void TB_Command_KeyDown(object sender, KeyEventArgs e)
        {

        }

        private void Bn_GET_REQUEST_Click(object sender, EventArgs e)
        {
            TB_Info.Text = "";
            BR_RESULT result = voips.GET_REQUEST(Convert.ToInt32(TB_RequestID.Text));
            TB_Info.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
            if (Chk_TreeView2.Checked)
            {
                BindTreeView(treeView2, TB_Info.Text);
            }

        }

        private void Bn_ClearMsg_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
        }

        private void Bn_QueryEvent_Click(object sender, EventArgs e)
        {
            TB_Info.Text = "";
            BR_EventResult result = voips.QueryEvent(Convert.ToInt32(TB_AfterID.Text), Convert.ToInt32(TB_Limit.Text));
            TB_Info.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_STOP_USB_HID_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            BR_RESULT result = voips.STOP_USB_HID(TB_MAC.Text, Convert.ToInt16(TB_UsbHidIndex.Text));
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_ROUTE_IO_USB_HID_Click(object sender, EventArgs e)
        {
            //-----------------------------------------------------------
            // 使用AVIP-P5101TR-B1F測試
            // 1. RX端將USB Control Mode設為Host
            // 2. RX端HDMI OUT接螢幕
            // 3. RX端USB接滑鼠
            // 4. TX端將USB Control Mode設為Device
            // 5. TX端HDMI IN接PC
            // 6. TX端Mini USB接至PC USB
            // 7. USB HID Route RX->TX
            // 8. USB HID Route TX->RX
            //-----------------------------------------------------------
            TB_Msg.Text = "";
            BR_RESULT result = voips.ROUTE_IO_USB_HID(TB_SourceMAC.Text, TB_DestinationMAC.Text, Convert.ToInt32(TB_Index.Text));
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
            result = voips.ROUTE_IO_USB_HID(TB_DestinationMAC.Text, TB_SourceMAC.Text, Convert.ToInt32(TB_Index.Text));
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_QueryEvent_Type_Click(object sender, EventArgs e)
        {
            Bn_QueryEvent_Type.Enabled = false;
            TB_Info.Text = "";
            BR_EventResult result = voips.QueryEvent(CB_EventType.Text, Convert.ToInt32(TB_AfterID.Text), Convert.ToInt32(TB_Limit.Text));

            if (result.status == "ERROR")
            {
                TB_Info.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
            }
            else if (CB_EventType.Text == "RS232_RECEIVED")
            {
                foreach (var Event in result.result.events)
                {
                    DateTime dt = (new DateTime(1970, 1, 1, 0, 0, 0)).AddHours(8).AddSeconds(Event.timestamp);
                    TB_AfterID.Text = Event.request_id.ToString();
                    BR_RESULT rs = voips.GET_REQUEST((int)Event.request_id);
                    foreach (var rs232 in rs.result.rs232)
                    {
                        TB_Info.AppendText($"[{dt:HH:mm:ss}][{Event.event_id:000000}][{rs232.device_id}] {rs232.rs232_data}\r\n");
                    }
                    Application.DoEvents();
                }
            }
            else
            {
                TB_Info.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
            }
            Bn_QueryEvent_Type.Enabled = true;
        }

        private async void Bn_DiscoveryAdd_Click(object sender, EventArgs e)
        {
            Bn_DiscoveryAdd.Enabled = false;
            List<string> device = await voips.ADD_DEVICE_ALL();
            TB_Msg.Text = JsonConvert.SerializeObject(device, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
            Bn_DiscoveryAdd.Enabled = true;
        }

        private async void Bn_Query_Click(object sender, EventArgs e)
        {
            int i, j;
            bool Result = await usb.QUERY();

            TB_Msg.Text = "";
            for (i = 0; i < usb.ListDeviceInfo.Count; i++)
            {
                TB_Msg.Text += "-------------------\r\n";
                TB_Msg.Text += "MAC: ";
                for (j = 0; j < 6; j++)
                {
                    TB_Msg.Text += $"{usb.ListDeviceInfo[i].mac[j]:X2}";
                }
                TB_Msg.Text += "\r\n";

                TB_Msg.Text += "IP: ";
                for (j = 0; j < 4; j++)
                {
                    TB_Msg.Text += $"{(j > 0 ? "." : "")}{Convert.ToInt32(usb.ListDeviceInfo[i].ipAddr[j])}";
                }
                TB_Msg.Text += "\r\n";

                TB_Msg.Text += $"Network Mode: {(usb.ListDeviceInfo[i].NetworkMode == 0 ? "DHCP" : "Static")}\r\n";
                TB_Msg.Text += $"Vendor: {usb.ListDeviceInfo[i].Vendor}\r\n";
                TB_Msg.Text += $"Product: {usb.ListDeviceInfo[i].Product}\r\n";
                TB_Msg.Text += $"Revision: {usb.ListDeviceInfo[i].Revision}\r\n";
            }
        }

        private void Bn_GET_DEVICE_LIST_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            List<string> device = voips.GET_DEVICE_LIST();
            TB_Msg.Text = JsonConvert.SerializeObject(device, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_SAVE_DEVICE_LIST_Click(object sender, EventArgs e)
        {
            voips.SAVE_DEVICE_LIST(DeviceListFileName);
        }

        private void Bn_LOAD_DEVICE_LIST_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                voips.LOAD_DEVICE_LIST(openFileDialog1.FileName);

                TB_Msg.Text = "";
                List<string> device = voips.GET_DEVICE_LIST();
                TB_Msg.Text = JsonConvert.SerializeObject(device, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
            }
        }

        private void Bn_DLM_START_VIDEO_Click(object sender, EventArgs e)
        {
            //In dual link mode, this command will start video on AVP0 and AVP1 in one command.
            TB_Msg.Text = "";
            BR_RESULT result = voips.TX_START_VIDEO(TB_TX_AVP0.Text, 0);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_DLM_ROUTE_VIDEO_Click(object sender, EventArgs e)
        {
            //In dual link mode, this command will start video on AVP0 and AVP1 in one command.
            TB_Msg.Text = "";
            BR_RESULT result = voips.ROUTE_VIDEO(TB_TX_AVP0.Text, TB_RX_AVP0.Text);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_DLM_STOP_VIDEO_Click(object sender, EventArgs e)
        {
            //In dual link mode, this command will start video on AVP0 and AVP1 in one command.
            TB_Msg.Text = "";
            BR_RESULT result = voips.TX_STOP_VIDEO(TB_TX_AVP0.Text, 0);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_DLM_LeaveVideo_Click(object sender, EventArgs e)
        {
            //
            TB_Msg.Text = "";
            BR_RESULT result1 = voips.RX_LEAVE_VIDEO(TB_RX_AVP0.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result1, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });

            BR_RESULT result2 = voips.RX_LEAVE_VIDEO(TB_RX_AVP1.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result2, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_DLM_START_AUDIO_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            BR_RESULT result1 = voips.TX_START_AUDIO(TB_TX_AVP0.Text, 0); //HDMI Audio
            TB_Msg.Text += JsonConvert.SerializeObject(result1, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });

            BR_RESULT result2 = voips.TX_START_AUDIO(TB_TX_AVP1.Text, 0); //HDMI Audio
            TB_Msg.Text += JsonConvert.SerializeObject(result2, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_DLM_ROUTE_AUDIO_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            BR_RESULT result1 = voips.ROUTE_AUDIO_HDMI(TB_TX_AVP0.Text, TB_RX_AVP0.Text);
            TB_Msg.Text += JsonConvert.SerializeObject(result1, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });

            BR_RESULT result2 = voips.ROUTE_AUDIO_HDMI(TB_TX_AVP1.Text, TB_RX_AVP1.Text);
            TB_Msg.Text += JsonConvert.SerializeObject(result2, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_DLM_STOP_AUDIO_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            BR_RESULT result1 = voips.TX_STOP_AUDIO(TB_TX_AVP0.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result1, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });

            BR_RESULT result2 = voips.TX_STOP_AUDIO(TB_TX_AVP1.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result2, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_DLM_LeaveHdmiAudio_Click(object sender, EventArgs e)
        {
            BR_RESULT result;
            TB_Msg.Text = "";
            result = voips.RX_LEAVE_AUDIO(TB_RX_AVP0.Text, AUDIO_STREAM.HDMI_AUDIO);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.RX_LEAVE_AUDIO(TB_RX_AVP1.Text, AUDIO_STREAM.HDMI_AUDIO);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";
        }

        private void Bn_DLM_StartMultiAudio_Click(object sender, EventArgs e)
        {
            BR_RESULT result;
            TB_Msg.Text = "";

            result = voips.TX_START_AUDIO(TB_TX_AVP0.Text, 2);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.TX_START_AUDIO(TB_TX_AVP1.Text, 2);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.TX_START_AUDIO(TB_RX_AVP0.Text, 2);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.TX_START_AUDIO(TB_RX_AVP1.Text, 2);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";
        }

        private void Bn_DLM_RouteMultiAudio_Click(object sender, EventArgs e)
        {
            BR_RESULT result;
            TB_Msg.Text = "";
            result = voips.ROUTE_MULTICH_AUDIO(TB_TX_AVP0.Text, TB_RX_AVP0.Text);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.ROUTE_MULTICH_AUDIO(TB_TX_AVP1.Text, TB_RX_AVP1.Text);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.ROUTE_MULTICH_AUDIO(TB_RX_AVP0.Text, TB_TX_AVP0.Text);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.ROUTE_MULTICH_AUDIO(TB_RX_AVP1.Text, TB_TX_AVP1.Text);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";
        }

        private void Bn_DLM_StopMultiAudio_Click(object sender, EventArgs e)
        {
            BR_RESULT result;
            TB_Msg.Text = "";
            result = voips.TX_STOP_AUDIO(TB_TX_AVP0.Text, 2);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.TX_STOP_AUDIO(TB_TX_AVP1.Text, 2);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.TX_STOP_AUDIO(TB_RX_AVP0.Text, 2);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.TX_STOP_AUDIO(TB_RX_AVP1.Text, 2);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";
        }

        private void Bn_LeaveMultiAudio_Click(object sender, EventArgs e)
        {
            BR_RESULT result;
            TB_Msg.Text = "";
            result = voips.RX_LEAVE_AUDIO(TB_RX_AVP0.Text, AUDIO_STREAM.MULTICH_AUDIO);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.RX_LEAVE_AUDIO(TB_RX_AVP1.Text, AUDIO_STREAM.MULTICH_AUDIO);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";
        }

        private void Bn_DLM_RouteUsbHid_Click(object sender, EventArgs e)
        {
            BR_RESULT result;
            TB_Msg.Text = "";
            result = voips.ROUTE_IO_USB_HID(TB_TX_AVP0.Text, TB_RX_AVP1.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.ROUTE_IO_USB_HID(TB_TX_AVP1.Text, TB_RX_AVP0.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.ROUTE_IO_USB_HID(TB_RX_AVP0.Text, TB_TX_AVP1.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.ROUTE_IO_USB_HID(TB_RX_AVP1.Text, TB_TX_AVP0.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";
        }

        private void Bn_DLM_StopUsbHid_Click(object sender, EventArgs e)
        {
            BR_RESULT result;
            TB_Msg.Text = "";
            result = voips.STOP_USB_HID(TB_TX_AVP0.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.STOP_USB_HID(TB_TX_AVP1.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.STOP_USB_HID(TB_RX_AVP0.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.STOP_USB_HID(TB_RX_AVP1.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";
        }

        private void Bn_DLM_RouteRs232_Click(object sender, EventArgs e)
        {
            BR_RESULT result;
            TB_Msg.Text = "";
            result = voips.ROUTE_IO_RS232(TB_TX_AVP0.Text, TB_RX_AVP0.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.ROUTE_IO_RS232(TB_TX_AVP1.Text, TB_RX_AVP1.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.ROUTE_IO_RS232(TB_RX_AVP0.Text, TB_TX_AVP0.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.ROUTE_IO_RS232(TB_RX_AVP1.Text, TB_TX_AVP1.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";
        }

        private void Bn_DLM_StopRs232_Click(object sender, EventArgs e)
        {
            BR_RESULT result;
            TB_Msg.Text = "";
            result = voips.STOP_RS232(TB_TX_AVP0.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.STOP_RS232(TB_TX_AVP1.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.STOP_RS232(TB_RX_AVP0.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.STOP_RS232(TB_RX_AVP1.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";
        }

        private void Bn_DLM_RouteInfrared_Click(object sender, EventArgs e)
        {
            BR_RESULT result;
            TB_Msg.Text = "";
            result = voips.ROUTE_IO_INFRARED(TB_TX_AVP0.Text, TB_RX_AVP0.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.ROUTE_IO_INFRARED(TB_TX_AVP1.Text, TB_RX_AVP1.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.ROUTE_IO_INFRARED(TB_RX_AVP0.Text, TB_TX_AVP0.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.ROUTE_IO_INFRARED(TB_RX_AVP1.Text, TB_TX_AVP1.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";
        }

        private void Bn_DLM_StopInfrared_Click(object sender, EventArgs e)
        {
            BR_RESULT result;
            TB_Msg.Text = "";
            result = voips.STOP_INFRARED(TB_TX_AVP0.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.STOP_INFRARED(TB_TX_AVP1.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.STOP_INFRARED(TB_RX_AVP0.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";

            result = voips.STOP_INFRARED(TB_RX_AVP1.Text, 0);
            TB_Msg.Text += JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented }) + "\r\n";
        }

        private void Bn_LIST_FIRMWARE_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            BR_RESULT result = voips.LIST_FIRMWARE();
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });

            CB_Filename.Items.Clear();
            foreach (var item in result.result.firmware)
            {
                CB_Filename.Items.Add(item.file_name);
            }
            if (CB_Filename.Items.Count > 0) CB_Filename.SelectedIndex = 0;
        }

        private void Bn_Update_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            BR_RESULT result = voips.UPDATE(TB_MacUpdate.Text, CB_Filename.Text);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_TX_SET_THUMBNAIL_AVP_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            BR_RESULT result = voips.TX_AVP_SET_THUMBNAIL(TB_MAC_ThumAvp.Text, TB_MulticastAddress_StartThumAvp.Text);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_TX_START_THUMBNAIL_AVP_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            BR_RESULT result = voips.TX_AVP_START_THUMBNAIL(TB_MAC_ThumAvp.Text);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_TX_STOP_THUMBNAIL_AVP_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            BR_RESULT result = voips.TX_AVP_STOP_THUMBNAIL(TB_MAC_ThumAvp.Text);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void OnDrawAvp_Event(Bitmap bmp, string IP)
        {
            PB_PreviewAvp.Image = bmp;

            MyInvoke mi = new MyInvoke(UpdateInfo);
            this.BeginInvoke(mi, new Object[] { IP });
        }

        public void UpdateInfo(string param)
        {
            TB_SourceIP.Text = param;
        }

        private void Bn_Listen_Click(object sender, EventArgs e)
        {
            Bn_Listen.Enabled = false;
            thumbnail_avp = new THUMBNAIL_AVP(TB_MulticastAddressAvp.Text, OnDrawAvp_Event);
            Bn_StartAvp.PerformClick();
            Bn_CloseAvp.Enabled = true;
        }

        private void Bn_CloseAvp_Click(object sender, EventArgs e)
        {
            thumbnail_avp.Close();
            Bn_StartAvp.Enabled = true;
            Bn_StopAvp.Enabled = false;
            Bn_Listen.Enabled = true;
            Bn_CloseAvp.Enabled = false;
        }

        private void Bn_StartAvp_Click(object sender, EventArgs e)
        {
            Bn_StartAvp.Enabled = false;
            Bn_StopAvp.Enabled = true;
            thumbnail_avp.Start();
        }

        private void Bn_StopAvp_Click(object sender, EventArgs e)
        {
            thumbnail_avp.Stop();
            Bn_StartAvp.Enabled = true;
            Bn_StopAvp.Enabled = false;
        }

        private void Bn_GetThumbnailAvp_Click(object sender, EventArgs e)
        {
            PB_ThumbnailAvp.Image = thumbnail_avp.GET_THUMBNAIL(TB_SourceIP.Text);
        }

        private void Bn_Update2_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            var result = voips.UPDATE((DEVICE_GROUPS)CB_Group.SelectedIndex, CB_Filename.Text);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (TB_DiscoveryS.Visible)
            {
                TB_DiscoveryS.Visible = false;
                treeView1.Visible = true;
            }
            else
            {
                treeView1.Visible = false;
                TB_DiscoveryS.Visible = true;

            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (TB_Info.Visible)
            {
                TB_Info.Visible = false;
                treeView2.Visible = true;
            }
            else
            {
                treeView2.Visible = false;
                TB_Info.Visible = true;
            }
        }

        private void Bn_Expand2_Click(object sender, EventArgs e)
        {
            treeView2.ExpandAll();
        }

        private void Bn_Collapse2_Click(object sender, EventArgs e)
        {
            treeView2.CollapseAll();
        }

        private void Chk_TX_REMOVE_HDMI_AUDIO_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            BR_RESULT result = voips.TX_REMOVE_HDMI_AUDIO(TB_TxMAC.Text, Chk_TX_REMOVE_HDMI_AUDIO.Checked);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }


        private void Bn_SET_MULTI_LINK_MODE_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            BR_RESULT result = voips.TX_SET_MULTI_LINK_MODE(TB_TX_AVP0.Text, (MULTI_LINK_MODE)CB_SET_MULTI_LINK_MODE.SelectedIndex);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        public void Send(TcpClient tcp, string Data)
        {
            byte[] CommandData = Encoding.ASCII.GetBytes(Data);
            tcp.Client.Send(CommandData, CommandData.Length, SocketFlags.None);
        }

        private void Bn_SET_HTTP_PORT_Click(object sender, EventArgs e)
        {
            voips.SET_HTTP_PORT(Convert.ToInt32(TB_HttpPort.Text));
        }

        private void Bn_TX_Factory_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            var Result1 = voips.FACTORY_DEFAULT(TB_TX_AVP0.Text);
            TB_Msg.Text += JsonConvert.SerializeObject(Result1, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
            var Result2 = voips.FACTORY_DEFAULT(TB_TX_AVP1.Text);
            TB_Msg.Text += JsonConvert.SerializeObject(Result2, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_RX_Factory_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            var Result1 = voips.FACTORY_DEFAULT(TB_RX_AVP0.Text);
            TB_Msg.Text += JsonConvert.SerializeObject(Result1, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
            var Result2 = voips.FACTORY_DEFAULT(TB_RX_AVP1.Text);
            TB_Msg.Text += JsonConvert.SerializeObject(Result2, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_DLM_START_VIDEO1_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            BR_RESULT result = voips.TX_START_VIDEO(TB_TX_AVP0.Text, 1);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_DLM_ROUTE_VIDEO_GENLOCK_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            BR_RESULT result = voips.ROUTE_VIDEO_GENLOCK(TB_TX_AVP0.Text, TB_RX_AVP0.Text);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_DLM_STOP_VIDEO1_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            BR_RESULT result1 = voips.TX_STOP_VIDEO(TB_TX_AVP0.Text, 1);
            TB_Msg.Text += JsonConvert.SerializeObject(result1, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });

            BR_RESULT result2 = voips.TX_STOP_VIDEO(TB_TX_AVP1.Text, 1);
            TB_Msg.Text += JsonConvert.SerializeObject(result2, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_DLM_LeaveVideo1_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            BR_RESULT result1 = voips.RX_LEAVE_VIDEO(TB_RX_AVP0.Text, 1);
            TB_Msg.Text += JsonConvert.SerializeObject(result1, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });

            BR_RESULT result2 = voips.RX_LEAVE_VIDEO(TB_RX_AVP1.Text, 1);
            TB_Msg.Text += JsonConvert.SerializeObject(result2, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_SET_MULTIVIEW_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            VOIPS.MULTIVIEW_PARAMETER mp = new VOIPS.MULTIVIEW_PARAMETER();

            mp.MultiviewDescription = TB_Description.Text;
            mp.LayoutID = Convert.ToInt16(TB_LayoutID.Text);
            mp.DestinationMAC = TB_Receiver.Text;
            mp.AspectRatio = (VOIPS.VIDEOWALL_ASPECT_RATIO)CB_AspectRatio2.SelectedIndex;
            mp.Resolution = (VOIPS.TIMING)CB_MultiviewTiming.SelectedIndex;

            int SourceQty = 0;
            for (int i = 0; i < VOIPS.MultiviewTableInfo[mp.LayoutID].WinQty; i++)
            {
                if (VOIPS.MultiviewTableInfo[mp.LayoutID].Layout[i].TargetID == "black")
                {
                    mp.SourceMAC[i] = "none";
                    continue;
                }

                mp.SourceMAC[i] = ((TextBox)this.Controls.Find($"TB_WinID_{i}", true)[0]).Text;
                string MappingID = ((TextBox)this.Controls.Find($"TB_MappingID_{i}", true)[0]).Text;

                mp.MappingID[i] = (MappingID == "") ? Convert.ToByte(i) : Convert.ToByte(MappingID);
                mp.SourceInfo[i].Width = Convert.ToUInt16(TB_SourceWidth.Text);
                mp.SourceInfo[i].Height = Convert.ToUInt16(TB_SourceHeight.Text);

                SourceQty++;
            }

            mp.SourceQty = SourceQty;

            BR_RESULT result = voips.SET_MULTIVIEW(mp);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_DiscoveryIdentity_Click(object sender, EventArgs e)
        {
            TB_DiscoveryS.Clear();
            Application.DoEvents();
            BR_RESULT result = voips.DiscoveryIdentity();
            TB_DiscoveryS.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
            if (result.status == "SUCCESS")
            {
                BindTreeView(treeView1, TB_DiscoveryS.Text);
            }
        }

        private async void Bn_GET_SN_Click(object sender, EventArgs e)
        {
            Bn_GET_SN.Enabled = false;
            TB_SN.Text = await voips.GET_SN(TB_MAC.Text);
            Bn_GET_SN.Enabled = true;
        }

        private void Bn_LIST_MULTICAST_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            BR_RESULT result = voips.LIST_MULTICAST();
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }

        private void Bn_SET_CONTROL_SERVER_Click(object sender, EventArgs e)
        {
            //voips.SET_CONTROL_SERVER(TB_ControlServerIP.Text);

            // set ip & port
            voips.SET_CONTROL_SERVER(TB_ControlServerIP.Text, Convert.ToInt32(TB_HttpPort.Text));
        }

        private async void Bn_GET_MCU_IP_Click(object sender, EventArgs e)
        {
            Bn_GET_MCU_IP.Enabled = false;
            TB_MCU_IP.Text = await voips.GET_MCU_IP(TB_MAC.Text);
            Bn_GET_MCU_IP.Enabled = true;
        }

        private async void Bn_GET_MCU_VERSION_Click(object sender, EventArgs e)
        {
            Bn_GET_MCU_VERSION.Enabled = false;
            TB_GET_MCU_VERSION.Text = await voips.GET_MCU_VERSION(TB_MAC.Text);
            Bn_GET_MCU_VERSION.Enabled = true;
        }

        private void Bn_SET_MULTIVIEW_Optimized_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            VOIPS.MULTIVIEW_PARAMETER mp = new VOIPS.MULTIVIEW_PARAMETER();

            mp.MultiviewDescription = TB_Description.Text;
            mp.LayoutID = Convert.ToInt16(TB_LayoutID.Text);
            mp.DestinationMAC = TB_Receiver.Text;
            mp.AspectRatio = (VOIPS.VIDEOWALL_ASPECT_RATIO)CB_AspectRatio2.SelectedIndex;
            mp.Resolution = (VOIPS.TIMING)CB_MultiviewTiming.SelectedIndex;

            int SourceQty = 0;
            for (int i = 0; i < VOIPS.MultiviewTableInfo[mp.LayoutID].WinQty; i++)
            {
                if (VOIPS.MultiviewTableInfo[mp.LayoutID].Layout[i].TargetID == "black")
                {
                    mp.SourceMAC[i] = "none";
                    continue;
                }

                mp.SourceMAC[i] = ((TextBox)this.Controls.Find($"TB_WinID_{i}", true)[0]).Text;
                string MappingID = ((TextBox)this.Controls.Find($"TB_MappingID_{i}", true)[0]).Text;

                mp.MappingID[i] = (MappingID == "") ? Convert.ToByte(i) : Convert.ToByte(MappingID);
                mp.SourceInfo[i].Width = Convert.ToUInt16(TB_SourceWidth.Text);
                mp.SourceInfo[i].Height = Convert.ToUInt16(TB_SourceHeight.Text);

                SourceQty++;
            }

            mp.SourceQty = SourceQty;

            BR_RESULT result = voips.SET_MULTIVIEW(mp, true);
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });

        }

        private void Bn_MULTICH_AUDIO_SOURCE_Click(object sender, EventArgs e)
        {
            TB_Msg.Text = "";
            BR_RESULT result = voips.MULTICH_AUDIO_SOURCE(TB_RxMAC.Text, Convert.ToInt32(CB_RxStereoAudioSource.Text.Substring(0, 1)));
            TB_Msg.Text = JsonConvert.SerializeObject(result, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore, Formatting = Formatting.Indented });
        }
    }
}
