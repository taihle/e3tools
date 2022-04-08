// ------------------------------------------------------------------------------
// Copyright (c) 2016 - Allen Technologies www.allentek.com. All Rights Reserved.
// Author: Tai H. Le <tle@allentek.com>
// 
// Main Window
// ------------------------------------------------------------------------------
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.ComponentModel;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using System.IO;

namespace e3tools
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Stb _pendingLogStart = null;
        int _maxLines = 1000;

        public MainWindow()
        {
            InitializeComponent();
            InitCommands();
            InitStbListView();
        }

        private void InitStbListView()
        {
            LvStbs.View = new GridView();
            GridViewColumnCollection gvcc = (LvStbs.View as GridView).Columns;
            foreach (SortableGridViewColumn i in App.gDefaultGridViewColumns.Where(x => x.IsVisible == true).OrderBy(x => x.Index))
            {
                gvcc.Add(i);
            }
            // InitializeColumnHeaderContextMenu();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Title += " - " + Properties.Settings.Default.VigoServer;
            App.InitServersData();
            InitServers();

            App.Login(this);

            CheckStbStatus();

            Updater.UpdateCompleted += Updater_UpdateCompleted;
            Updater.Check(this);
        }

        private void Updater_UpdateCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            string ret = e.Result.ToString();
            if (!string.IsNullOrEmpty(ret))
            {
                AppendLogText(TxtConsole, ret);
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Stb stb = this.LvStbs.SelectedItem as Stb;
            if (null != stb)
            {
                Properties.Settings.Default.ActiveStb = stb.Name;
            }

            Server server = CboServers.SelectedItem as Server;
            if (null != server)
            {
                Properties.Settings.Default.ActiveServer = server.Name;
            }

            App.SaveServersData(Server.Save(App.gServers));

            Properties.Settings.Default.Save();
            
            // TODO: close connections
        }

        void OnLogDataReceived(object sender, string data)
        {
            if (ChkPauseLog.IsChecked.Value) return;
            LogBase log = (sender as LogBase);
            AppendLogText(log.LogViewer, data);
        }

        void OnServerStatusReceived(object sender, string status)
        {
            // TODO: MnuServerConnect.IsEnabled = true;
            if (status == "CONNECTED")
            {
                ServerLog serverLog = (sender as ServerLog);
                
                TabItem tabItem = new TabItem();
                tabItem.Header = serverLog.Host.Name;
                // tabItem.Style = TryFindResource("StbLoggerTabItemStyle") as Style;
                tabItem.ToolTip = serverLog.Host.ToString();

                System.Windows.Controls.RichTextBox rtb = new System.Windows.Controls.RichTextBox();
                rtb.Style = this.TryFindResource("RichTextBoxLogStyleServer") as Style;
                tabItem.Content = rtb;
                TcLogTabs.Items.Add(tabItem);
                TcLogTabs.SelectedIndex = TcLogTabs.Items.Count - 1;
                serverLog.LogViewer = rtb;
                serverLog.Start();
                tabItem.Tag = serverLog;

                if (null != _pendingLogStart)
                {
                    StartViewLog(_pendingLogStart, serverLog);
                    _pendingLogStart = null;
                }
            }
            else if (status.StartsWith("ERROR: "))
            {
                Helper.ShowErrorMessage(status);
            }
        }

        void StartViewLog(Stb stb, ServerLog serverLog)
        {
            StbLog logItem = new StbLog(serverLog.Ssh, stb);
            logItem.OnLogDataReceived += OnLogDataReceived;
            logItem.OnStatusReceived += OnStatusReceived;

            TabItem tabItem = new TabItem();
            tabItem.Style = TryFindResource("StbLoggerTabItemStyle") as Style;
            tabItem.Header = logItem.Host;
            tabItem.ToolTip = logItem.Host.ToString();

            System.Windows.Controls.RichTextBox rtb = new System.Windows.Controls.RichTextBox();
            rtb.Style = this.TryFindResource("RichTextBoxLogStyle") as Style;

            tabItem.Content = rtb;
            TcLogTabs.Items.Add(tabItem);
            TcLogTabs.SelectedIndex = TcLogTabs.Items.Count - 1;
            logItem.LogViewer = rtb;
            tabItem.Tag = logItem;

            logItem.Start();
        }

        void OnStatusReceived(object sender, string data)
        {
            StbLog logItem = (sender as StbLog); 
            if (data == "CLOSED")
            {
                TabItem tabItem = null;
                foreach (TabItem ti in TcLogTabs.Items)
                {
                    if (ti.Tag == logItem)
                    {
                        tabItem = ti;
                        break;
                    }
                }
                if (null != tabItem)
                {
                    TcLogTabs.Items.Remove(tabItem);
                }
                return;
            }
        }

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            string txt = TxtSearch.Text.Trim();
            LogBase lb = ((TcLogTabs.SelectedItem as TabItem).Tag as LogBase);
            if (null != lb)
            {
                lb.ApplyFilter(txt);
            }
        }

        LogBase FindLogFromHost(HostBase host)
        {
            foreach (TabItem ti in TcLogTabs.Items)
            {
                if (null != ti.Tag && (ti.Tag as LogBase).Host == host)
                {
                    return ti.Tag as LogBase;
                }
            }
            return null;
        }

        private void CboServers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Server server = (CboServers.SelectedItem as Server);
            if (null != server)
            {
                LvStbs.ItemsSource = server.Stbs;
                InitRemoteControlWebSocket();
            }
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            TabItem tabItem = TcLogTabs.SelectedItem as TabItem;
            System.Windows.Controls.RichTextBox rtb = (tabItem.Content as System.Windows.Controls.RichTextBox);
            if (null != rtb)
            {
                rtb.Document.Blocks.Clear();
            }
        }

        private void EnableRemoteControl(bool enabled)
        {
            BdrRemoteControl.Background = (enabled ? Brushes.LightGreen : Brushes.LightGray);
            BdrRemoteControl.IsEnabled = enabled;
        }

        private void TxtMaxLines_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _maxLines = Convert.ToInt32(TxtMaxLines.Text);
            }
            catch 
            {
                if (TxtMaxLines.Text.Length > 2) 
                {
                    TxtMaxLines.Text = _maxLines.ToString();
                }                
            }
        }

        private void TcLogTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                TabItem tabItem = e.AddedItems[0] as TabItem;
                LogBase log = (tabItem.Tag as LogBase);
                if (null != log)
                {
                    HostBase host = log.Host;
                    if (host is Server)
                    {
                        CboServers.SelectedItem = host;
                    }
                }
            }
            catch(Exception ex)
            {
                // TODO
            }
        }

        #region Menu Commands - File
        private void MnuImportFromFile_Click(object sender, RoutedEventArgs e)
        {
            string path = Helper.OpenFileDialog("Select File", "E3 data file (*.xml)|*.xml|All (*.*)|*.*");
            if (!string.IsNullOrEmpty(path))
            {
                OpenFile(path);
            }
        }

        private void MnuExportToFile_Click(object sender, RoutedEventArgs e)
        {
            string path = Helper.SaveFileDialog("Select File", "E3 data file (*.xml)|*.xml|All (*.*)|*.*");
            if (!string.IsNullOrEmpty(path))
            {
                SaveFile(path);
            }
        }

        private void MnuSettings_Click(object sender, RoutedEventArgs e)
        {
            SettingsWindow w = new SettingsWindow();
            w.Owner = this;
            if (true == w.ShowDialog())
            {
                Application.Current.Shutdown();
                System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);                
            }
        }

        private void MnuExit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        #endregion

        //#region Menu Commands - Stb
        //private void CloseLogView(Stb stb)
        //{
        //    StbLog logItem = null;
        //    foreach (TabItem ti in TcLogTabs.Items)
        //    {
        //        logItem = (ti.Tag as StbLog);
        //        if (null != logItem && logItem.Host.Id == stb.Id)
        //        {
        //            logItem.Stop();
        //            break;
        //        }
        //    }
        //}
        //#endregion

        private LogBase HasLogView(HostBase host)
        {
            if (null == host) return null;

            foreach (TabItem ti in TcLogTabs.Items)
            {
                LogBase li = (ti.Tag as LogBase);
                if (null != li && li.Host.Id == host.Id)
                {
                    return li;
                }
            }

            return null;
        }

        private void MnuTvStb_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            Stb stb = LvStbs.SelectedItem as Stb;

            if (null != stb && null != HasLogView(stb))
            {
                MnuStbViewLog.Header = "Stop Log View";
            }
            else
            {
                MnuStbViewLog.Header = "Start Log View";
            }

            bool cpuLogEnabled = (null != stb && stb.Type == Stb.TVTYPE_LG);
            if (null != _rtbCpuPtcLog)
            {
                cpuLogEnabled = cpuLogEnabled && (stb == _rtbCpuPtcLog.Tag);
            }
            MnuStbViewCPUPTCLog.IsEnabled = cpuLogEnabled;
        }

        private void MnuStbViewCPUPTCLog_Click(object sender, RoutedEventArgs e)
        {
            Stb stb = LvStbs.SelectedItem as Stb;
            if (stb.Type != Stb.TVTYPE_LG)
            {
                System.Windows.MessageBox.Show("LG TV Only!");
                return;
            }
            ViewCPULog(stb);
        }

        private void MnuStbEnableDisableCPULog_Click(object sender, RoutedEventArgs e)
        {
            Stb stb = LvStbs.SelectedItem as Stb;
            if (stb.Type != Stb.TVTYPE_LG)
            {
                System.Windows.MessageBox.Show("LG TV Only!");
                return;
            }
            EnableCPULog(stb);
        }

        #region Vigo BE
        private void SelectHospital(string mode)
        {
            SelectHospitalWindow w = new SelectHospitalWindow();
            w.Owner = this;
            w.Mode = mode;
            w.Server = CboServers.SelectedItem as Server;
            if (true == w.ShowDialog())
            {
                CboServers.Items.Refresh();
                if (null != w.Server)
                {
                    LvStbs.ItemsSource = null;
                    CboServers.SelectedItem = w.Server;
                    LvStbs.ItemsSource = w.Server.Stbs;
                    LvStbs.Items.Refresh();
                }
            }
        }

        void RegisterVigoStb(Stb stb)
        {
            StbRegisterWindow w = new StbRegisterWindow();
            w.Owner = this;
            w.Stb = stb;
            w.ShowDialog();
        }

        async void GetStbData(Stb stb)
        {
            Server server = CboServers.SelectedItem as Server;
            if (null == server) return;
            if (null == server.Hospital)
            {
                await server.RefreshStbs();
            }
            string hospName = server.Hospital.abbreviation;
            string str = await GetEMRData(stb, hospName);

            AppendLogText(TxtConsole, str);
        }

        #endregion
        private void LstStbs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TxtStatus.Text = "Selected " + e.AddedItems.Count + "/" + LvStbs.Items.Count;
            Stb stb = (LvStbs.SelectedItem as Stb);
            ProgGridStb.DataContext = stb;
            if (null == stb)
            {
                // EnableRemoteControl(false);
                return;
            }

            foreach (TabItem ti in TcLogTabs.Items)
            {
                if (null != ti.Tag && (ti.Tag as LogBase).Host.Id == stb.Id)
                {
                    TcLogTabs.SelectedItem = ti;
                    break;
                }
            }
        }

        private void GridViewColumnHeaderClickedHandler(object sender, RoutedEventArgs e)
        {
            GridViewColumnHeader clickedHeader = e.OriginalSource as GridViewColumnHeader;
            if (null == clickedHeader) return;
            SortableGridViewColumn.SortListView((sender as ListView), clickedHeader);
        }

        private void MnuHelpAbout_Click(object sender, RoutedEventArgs e)
        {
            Assembly a = Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(a.Location);
            System.Windows.MessageBox.Show("Version: " + fvi.FileVersion + "\nWS Remote: " + this.MY_WS_ID, "E3 Tools");
        }

        private void BtnCloseLoggerItem_Click(object sender, RoutedEventArgs e)
        {
            HostBase host = (sender as Button).Tag as HostBase;
            if (null != host)
            {
                LogBase log = HasLogView(host);
                if (null != log)
                {
                    log.Stop();
                }
            }
        }

        private void MnuDebug_Click(object sender, RoutedEventArgs e)
        {
            ListAccountData();
        }

        async Task<string> GetEMRData(Stb stb, string hospName)
        {
            string str = stb.Name + ":\t" + stb.MacId + "\t" + stb.InstalledStb.patientAccountId + "\t" + stb.InstalledStb.patientMRN + "\t" + stb.InstalledStb.patientADTAccountNumber + "\t";
            try
            {
                string altMRN = string.Empty;
                string mrn = stb.InstalledStb.patientMRN;
                if (string.IsNullOrEmpty(mrn)) mrn = stb.InstalledStb.patientADTAccountNumber;
                if (string.IsNullOrEmpty(mrn)) return string.Empty;

                string emr_data = await App.gVigoUserClient.GetPatientEMRDataAsync(hospName, mrn);
                JObject o = JObject.Parse(emr_data);
                string status = o["status"].ToString();

                // try different account id
                if (status == "ERROR" && mrn != stb.InstalledStb.patientADTAccountNumber)
                {
                    altMRN = mrn;
                    mrn = stb.InstalledStb.patientADTAccountNumber;
                    emr_data = await App.gVigoUserClient.GetPatientEMRDataAsync(hospName, mrn);
                    o = JObject.Parse(emr_data);
                    status = o["status"].ToString();
                }

                str += status + ": ";

                if (status == "ERROR")
                {
                    status = o["message"].ToString();
                    str += status;
                    if (!string.IsNullOrEmpty(altMRN)) str += "/" + altMRN;
                }
                else
                {
                    str += o["createdate"] + "-" + o["createtm"] + " (" + mrn + ")";
                    string emrFile = @"c:\temp\" + stb.MacId.Replace(":","") + "-" + stb.InstalledStb.room + "." + stb.InstalledStb.bed + "-" + o["createdate"] + "-" + o["createtm"] + ".json";
                    File.WriteAllText(emrFile, emr_data);
                }

                str += "\n";
            }
            catch (Exception ex)
            {
                str += "EXCEPTION";
            }

            return str;
        }

        async void ListAccountData()
        {
            Server server = CboServers.SelectedItem as Server;
            if (null == server) return;
            if (null == server.Hospital)
            {
                await server.RefreshStbs();
            }

            IEnumerable<Stb> stbs = server.Stbs.Where(x => (null != x.InstalledStb && x.InstalledStb.patientAccountId > 0));
            AppendLogText(TxtConsole, "room/bed:\t\ttmac-id\tacct\tadt-mrn\tadt-acct\temr data\n\n");
            string hospName = server.Hospital.abbreviation;
            int totalProcessed = 0;

            foreach (Stb stb in stbs)
            {
                string str = await GetEMRData(stb, hospName);
                if (string.IsNullOrEmpty(str)) continue;
                totalProcessed++;
                AppendLogText(TxtConsole, str);
            }

            AppendLogText(TxtConsole, "\nTotal: " + totalProcessed + "/" + stbs.Count());
        }

        private void BtnClearLogOnServer_Click(object sender, RoutedEventArgs e)
        {
            LogBase lb = ((TcLogTabs.SelectedItem as TabItem).Tag as LogBase);
            if (null != lb)
            {
                lb.ClearLogOnServer();
            }
        }
    }

    public class SortableGridViewColumn : GridViewColumn
    {
        public SortableGridViewColumn()
        {
        }

        public SortableGridViewColumn(string header, string sortName): this(header, sortName, sortName)
        {
        }

        public SortableGridViewColumn(string header, string sortName, string bindingValue)
        {
            this.Header = header;
            this.SortPropertyName = sortName;
            if (!string.IsNullOrEmpty(bindingValue))
            {
                this.DisplayMemberBinding = new Binding(bindingValue);
            }
        }

        public int Index
        {
            get { return (int)GetValue(IndexProperty); }
            set { SetValue(IndexProperty, value); }
        }

        public static readonly DependencyProperty IndexProperty =
            DependencyProperty.Register("Index", typeof(int), typeof(SortableGridViewColumn), new UIPropertyMetadata(0));

        public bool IsVisible
        {
            get { return (bool)GetValue(IsVisibleProperty); }
            set { SetValue(IsVisibleProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SortPropertyName.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsVisibleProperty =
            DependencyProperty.Register("IsVisible", typeof(bool), typeof(SortableGridViewColumn), new UIPropertyMetadata(true));

        public string SortPropertyName
        {
            get { return (string)GetValue(SortPropertyNameProperty); }
            set { SetValue(SortPropertyNameProperty, value); }
        }

        // Using a DependencyProperty as the backing store for SortPropertyName.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty SortPropertyNameProperty =
            DependencyProperty.Register("SortPropertyName", typeof(string), typeof(SortableGridViewColumn), new UIPropertyMetadata(""));

        public bool IsDefaultSortColumn
        {
            get { return (bool)GetValue(IsDefaultSortColumnProperty); }
            set { SetValue(IsDefaultSortColumnProperty, value); }
        }

        public static readonly DependencyProperty IsDefaultSortColumnProperty =
            DependencyProperty.Register("IsDefaultSortColumn", typeof(bool), typeof(SortableGridViewColumn), new UIPropertyMetadata(false));

        /// <summary>
        /// Helper method to sort the columns
        /// </summary>
        /// <param name="lv"></param>
        /// <param name="clickedHeader"></param>
        public static void SortListView(ListView lv, GridViewColumnHeader clickedHeader)
        {
            if (clickedHeader.Role == GridViewColumnHeaderRole.Padding)
            {
                return;
            }

            ListSortDirection direction = ListSortDirection.Ascending;
            if (null != clickedHeader.Tag)
            {
                try
                {
                    direction = (ListSortDirection)(clickedHeader.Tag);
                    direction = (direction == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending);
                }
                catch
                {
                }
            }

            if (SortListView(lv, clickedHeader.Column, direction)) // (clickedHeader.Column as SortableGridViewColumn), direction))
            {
                clickedHeader.Tag = direction;
            }
        }

        public static bool SortListView(ListView lv, GridViewColumn col, ListSortDirection direction) // SortableGridViewColumn col, ListSortDirection direction)
        {
            try
            {
                string sortBy = (col.DisplayMemberBinding as System.Windows.Data.Binding).Path.Path; // col.Header.ToString();
                //if (col.SortPropertyName != null) {
                //    sortBy = col.SortPropertyName as string;
                //}
                ICollectionView dataView = CollectionViewSource.GetDefaultView(lv.ItemsSource);
                dataView.SortDescriptions.Clear();
                SortDescription sd = new SortDescription(sortBy, direction);
                dataView.SortDescriptions.Add(sd);
                dataView.Refresh();
                return true;
            }
            catch(Exception ex)
            {
                Console.Error.WriteLine("SortListView(): exception - " + ex.Message);
            }
            return false;
        }
    }
}
