// ------------------------------------------------------------------------------
// Copyright (c) 2015 - Allen Technologies Inc. allentek.com. All Rights Reserved.
// Author: Tai H. Le <tle@allentek.com>
// 
// Hospital Selection Dialog
// ------------------------------------------------------------------------------
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Ati.VigoPC.WebServices.REST;
using System.Xml.Linq;
using System.ComponentModel;

namespace e3tools
{
    /// <summary>
    /// Interaction logic for StbRegisterWindow.xaml
    /// </summary>
    public partial class SelectHospitalWindow : Window
    {
        public SelectHospitalWindow()
        {
            InitializeComponent();
        }

        public Server Server { get; set; }

        public string Mode
        {
            get { return BtnOk.Content.ToString(); }
            set 
            { 
                BtnOk.Content = value;
                this.Title = value + " Hospital";
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TxtStatus.Text = "Loading, please wait...";
            BtnOk.IsEnabled = false;

            CboLogsPaths.ItemsSource = Properties.Settings.Default.LogsPaths;

            if (null == App.gVigoHospitals || App.gVigoHospitals.Count <= 0)
            {
                App.gVigoHospitals = await App.gVigoUserClient.GetHospitalsAsync();
            }
            
            CboHospitals.ItemsSource = App.gVigoHospitals;
            CboHospitals.Items.Refresh();
            WSHospital selectedHospital = null;
            
            if (null != this.Server)
            {
                selectedHospital = App.gVigoHospitals.FirstOrDefault(x => x.identity == this.Server.Id);
            }

            if (null == selectedHospital && App.gVigoHospitals.Count > 0)
            {
                selectedHospital = App.gVigoHospitals[0];
            }

            CboHospitals.SelectedItem = selectedHospital;
            TxtStatus.Text = Properties.Settings.Default.VigoServer;

            if (this.Mode == "Add")
            {

            }
            else
            {

            }
            
            BtnOk.IsEnabled = true;
        }

        private async void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                WSHospital h = CboHospitals.SelectedItem as WSHospital;
                Server s = null;
                if (null != h)
                {
                    s = App.gServers.FirstOrDefault(x => x.Id == h.identity);
                }

                if (null == s)
                {
                    s = new Server(h);
                    App.gServers.Add(s);
                }

                UpdateServerProperties(s);

                if (true == ChkUpdateStbs.IsChecked)
                {
                    await s.RefreshStbs();
                }

                this.Server = s;
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                Helper.ShowErrorMessage(ex);
            }
        }

        private void UpdateServerProperties(Server s, WSHospital h = null)
        {
            if (null != h) s.Name = h.name;
            if (null != h) s.Id = h.identity;
            s.Ip = TxtServerIp.Text;
            s.Username = TxtUsername.Text;
            s.Password = TxtPassword.Password;
            s.E3DevicesOnly = ChkE3DevicesOnly.IsChecked.Value;
            s.LogsPath = CboLogsPaths.Text;
            s.IconUrl = TxtIconUrl.Text;
            s.WSHost = TxtWSHost.Text;
            
            try
            {
                s.WSPort = Convert.ToInt32(TxtWSPort.Text);
            }
            catch (Exception ex) { }

            if (!Properties.Settings.Default.LogsPaths.Contains(s.LogsPath))
            {
                Properties.Settings.Default.LogsPaths.Add(s.LogsPath);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void CboHospitals_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            WSHospital h = CboHospitals.SelectedItem as WSHospital;
            if (null == h) return;
            PopulateServerProps(App.gServers.FirstOrDefault(x => x.Id == h.identity));
        }

        void PopulateServerProps(Server s)
        {
            TxtServerIp.Text = string.Empty;
            TxtUsername.Text = string.Empty;
            TxtPassword.Password = string.Empty;
            TxtIconUrl.Text = string.Empty;
            TxtWSHost.Text = string.Empty;
            TxtWSPort.Text = string.Empty;
            BtnOk.Content = "Add";
            if (null != s)
            {
                TxtServerIp.Text = s.Ip;
                TxtUsername.Text = s.Username;
                TxtPassword.Password = s.Password;
                ChkE3DevicesOnly.IsChecked = s.E3DevicesOnly;
                CboLogsPaths.Text = s.LogsPath;
                TxtIconUrl.Text = s.IconUrl;
                TxtWSHost.Text = s.WSHost;
                TxtWSPort.Text = s.WSPort.ToString();
                BtnOk.Content = "Update";
            }
        }

        class FtpTestArg
        {
            public string Ip { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
        }

        private void BtnTestLogin_Click(object sender, RoutedEventArgs e)
        {
            this.TxtStatus.Text = "Testing FTP connection, please wait...";
            BtnTestLogin.IsEnabled = false;

            BackgroundWorker bwTestLogin = new BackgroundWorker();
            bwTestLogin.WorkerSupportsCancellation = true;
            bwTestLogin.RunWorkerCompleted += bwTestLogin_RunWorkerCompleted;
            bwTestLogin.DoWork += bwTestLogin_DoWork;
            FtpTestArg data = new FtpTestArg() { Ip = TxtServerIp.Text, Username = TxtUsername.Text, Password = TxtPassword.Password };
            bwTestLogin.RunWorkerAsync(data);
        }

        void bwTestLogin_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.TxtStatus.Text = e.Result.ToString();
            BtnTestLogin.IsEnabled = true;
        }

        void bwTestLogin_DoWork(object sender, DoWorkEventArgs e)
        {
            Server s = new Server();
            FtpTestArg arg = e.Argument as FtpTestArg;
            e.Result = s.TestFtpLogin(arg.Ip, arg.Username, arg.Password);
        }

        private async void BtnIconUrl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BtnIconUrl.IsEnabled = false;
                WSHospital h = CboHospitals.SelectedItem as WSHospital;
                RSVigoStbClient stbClient = new RSVigoStbClient(Properties.Settings.Default.VigoServer);
                string mac_id = "nextgen:dev-pc";
                stbClient.Switchport = mac_id;
                WSInstalledSTB stb = await stbClient.CheckInAsync(mac_id, "E3.TOOLS");

                if (null == stb || stb.hospitalId != h.identity)
                {
                    if (null == stb)
                    {
                        stb = new WSInstalledSTB();
                    }

                    stb.hospital = h.name;
                    stb.hospitalId = h.identity;
                    stb.nursingUnit = h.nursingUnits[0].identity.ToString();
                    stb.room = "NEXTGEN";
                    stb.bed = "DEV-PC";
                    WSInstalledSTB ret = await App.gVigoUserClient.UpdateStbAsync(stb);
                    if (null != ret)
                    {
                        stb = ret;
                    }
                    //List<WSInstalledSTB> stbs = await App.gVigoUserClient.GetStbsByHospitalIdAsync(h.identity);
                    //stb = stbs.FirstOrDefault(x => !string.IsNullOrEmpty(x.switchPort) && (x.macAddress == x.switchPort));

                    stbClient.Switchport = stb.switchPort;
                    stb = await stbClient.CheckInAsync(stb.macAddress, "E3.TOOLS");
                }

                if (null != stb)
                {
                    WSDynamicParameter dp_root = await stbClient.GetParameterAsync("ntb_root_container");
                    if (dp_root != null)
                    {
                        string root_name = dp_root.value;
                        WSContent content = await stbClient.GetContentByNameAsync(root_name);
                        XElement x = XElement.Parse(content.content.Replace("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n", ""));
                        if (null != x)
                        {
                            XElement i = x.Elements().First().Elements().FirstOrDefault(a => a.Name == "image");
                            if (null != i)
                            {
                                string url = i.Attribute("url").Value;
                                if (!string.IsNullOrEmpty(url))
                                {
                                    TxtIconUrl.Text = url;
                                }
                            }
                            else
                            {
                                Helper.ShowErrorMessage("Image in the root content not found!");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Helper.ShowErrorMessage(ex);
            }

            BtnIconUrl.IsEnabled = true;
        }

        private async void BtnInfo_Click(object sender, RoutedEventArgs e)
        {
            WSHospital h = CboHospitals.SelectedItem as WSHospital;
            if (null == h) return;
            Server s = App.gServers.FirstOrDefault(x => x.Id == h.identity);
            if (null == s)
            {
                s = new Server(h);
                UpdateServerProperties(s, h);
            }
            if (null != s)
            {
                this.TxtStatus.Text = "Refreshing Server Info...";
                this.TxtStatus.Text = await s.RefreshInfo();
                if (this.TxtStatus.Text == "OK")
                {
                    this.TxtStatus.Text = "Client Version(s): " + s.Versions + "; Deployed Offlines: " + s.Offlines;
                }
            }
        }
    }
}
