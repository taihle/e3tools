// ------------------------------------------------------------------------------
// Copyright (c) 2016 - Allen Technologies www.allentek.com. All Rights Reserved.
// Author: Tai H. Le <tle@allentek.com>
// 
// Main Window - Commands
// ------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace e3tools
{
    public partial class MainWindow
    {        
        public static RoutedCommand AddStb = new RoutedCommand();
        public static RoutedCommand HighLightItems = new RoutedCommand();
        public static RoutedCommand DeleteAll = new RoutedCommand();
        public static RoutedCommand CmdStbRemote = new RoutedCommand();
        public static RoutedCommand CmdStbViewLog = new RoutedCommand();
        public static RoutedCommand CmdStbViewInfo = new RoutedCommand();
        public static RoutedCommand CmdStbRegister = new RoutedCommand();
        public static RoutedCommand CmdStbData = new RoutedCommand();

        public static RoutedCommand CmdHospitalAdd = new RoutedCommand();
        public static RoutedCommand CmdHospitalEdit = new RoutedCommand();
        public static RoutedCommand CmdHospitalDelete = new RoutedCommand();
        public static RoutedCommand CmdHospitalConnect = new RoutedCommand();
        public static RoutedCommand CmdListHospitals = new RoutedCommand();
        public static RoutedCommand CmdHospitalsInfo = new RoutedCommand();

        public static RoutedCommand CmdRefreshStbs = new RoutedCommand();
        public static RoutedCommand CmdCloseAllLogs = new RoutedCommand();
        public static RoutedCommand CmdShowHideRemoteControl = new RoutedCommand();
        public static RoutedCommand CmdCreateZipLoader = new RoutedCommand();

        public static RoutedCommand CmdViewLogFile = new RoutedCommand();
        public static RoutedCommand CmdOpenRemoteFile = new RoutedCommand();

        public static RoutedCommand CmdCheckSoftwareUpdate = new RoutedCommand();        

        #region Init
        private void InitCommands()
        {
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Find, Find_Executed, Always_Enabled));
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, Copy_Executed, SelectedOneOrMore_Enabled));
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, Delete_Executed, SelectedOneOrMore_Enabled));
            this.CommandBindings.Add(new CommandBinding(ApplicationCommands.Properties, Properties_Executed, SelectedOnce_Enabled));

            this.CommandBindings.Add(new CommandBinding(MainWindow.AddStb, AddStb_Executed, Always_Enabled));            
            MainWindow.DeleteAll.InputGestures.Add(new KeyGesture(Key.Delete, ModifierKeys.Alt, "Del+Alt"));
            this.CommandBindings.Add(new CommandBinding(MainWindow.DeleteAll, DeleteAll_Executed, Always_Enabled));

            MainWindow.HighLightItems.InputGestures.Add(new KeyGesture(Key.F11, ModifierKeys.None, "F11"));
            this.CommandBindings.Add(new CommandBinding(MainWindow.HighLightItems, HighLightItems_Executed, SelectedOneOrMore_Enabled));

            this.CommandBindings.Add(new CommandBinding(MainWindow.CmdStbViewLog, CmdStbViewLog_Executed, SelectedOnce_Enabled));
            this.CommandBindings.Add(new CommandBinding(MainWindow.CmdStbViewInfo, CmdStbViewInfo_Executed, CmdStbViewInfo_Enabled));
            this.CommandBindings.Add(new CommandBinding(MainWindow.CmdStbRegister, CmdStbRegister_Executed, SelectedOnce_Enabled));
            this.CommandBindings.Add(new CommandBinding(MainWindow.CmdStbData, CmdStbData_Executed, SelectedOnce_Enabled));

            this.CommandBindings.Add(new CommandBinding(MainWindow.CmdStbRemote, CmdStbRemote_Executed, SelectedOneOrMore_Enabled));

            this.CommandBindings.Add(new CommandBinding(MainWindow.CmdHospitalAdd, CmdHospitalAdd_Executed, Always_Enabled));
            this.CommandBindings.Add(new CommandBinding(MainWindow.CmdHospitalEdit, CmdHospitalEdit_Executed, ServerSelectedOne_Enabled));
            this.CommandBindings.Add(new CommandBinding(MainWindow.CmdHospitalDelete, CmdHospitalDelete_Executed, ServerSelectedOne_Enabled));
            this.CommandBindings.Add(new CommandBinding(MainWindow.CmdHospitalConnect, CmdHospitalConnect_Executed, CmdHospitalConnect_Enabled));
            this.CommandBindings.Add(new CommandBinding(MainWindow.CmdListHospitals, CmdListHospitals_Executed, Always_Enabled));
            this.CommandBindings.Add(new CommandBinding(MainWindow.CmdHospitalsInfo, CmdHospitalsInfo_Executed, Always_Enabled));

            this.CommandBindings.Add(new CommandBinding(MainWindow.CmdRefreshStbs, CmdRefreshStbs_Executed, Always_Enabled));
            this.CommandBindings.Add(new CommandBinding(MainWindow.CmdCloseAllLogs, CmdCloseAllLogs_Executed, Always_Enabled));

            this.CommandBindings.Add(new CommandBinding(MainWindow.CmdShowHideRemoteControl, CmdShowHideRemoteControl_Executed, CmdShowHideRemoteControl_Enabled));

            this.CommandBindings.Add(new CommandBinding(MainWindow.CmdCreateZipLoader, CmdCreateZipLoader_Executed, Always_Enabled));

            this.CommandBindings.Add(new CommandBinding(MainWindow.CmdOpenRemoteFile, CmdOpenRemoteFile_Executed, Always_Enabled));

            this.CommandBindings.Add(new CommandBinding(MainWindow.CmdCheckSoftwareUpdate, CmdCheckSoftwareUpdate_Executed, Always_Enabled));
            
        }
        #endregion

        #region CmdCloseAllLogs
        private void CmdOpenRemoteFile_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                if (null == Properties.Settings.Default.LastOpenFile) Properties.Settings.Default.LastOpenFile = "";
                if (null == Properties.Settings.Default.LastOpenFiles) Properties.Settings.Default.LastOpenFiles = new StringCollection();

                string remoteFilename = Properties.Settings.Default.LastOpenFile;
                Server server = CboServers.SelectedItem as Server;
                Stb stb = null;

                TabItem tabItem = TcLogTabs.SelectedItem as TabItem;
                if (null != tabItem)
                {
                    if (tabItem.Header.ToString().StartsWith("CPU/PTC Log"))
                    {
                        if (null != _cpuLogFile && null != (_cpuLogFile.BaseStream as FileStream) && !string.IsNullOrEmpty((_cpuLogFile.BaseStream as FileStream).Name)) {
                            remoteFilename = (_cpuLogFile.BaseStream as FileStream).Name;
                            System.Diagnostics.Process.Start(remoteFilename);
                            return;
                        }                        
                    }
                    else if (tabItem.Header.ToString().StartsWith("Remote Status"))
                    {
                        DateTime dt = DateTime.Now;
                        remoteFilename = server.LogsPath + "__e3ws__" + dt.ToString("yyyy-MM-dd") + ".log";
                    }
                    else
                    {
                        StbLog log = tabItem.Tag as StbLog;
                        if (null != log && null != log.Host)
                        {
                            stb = log.Host as Stb;
                            server = stb.Server;
                            if (null != stb)
                            {
                                DateTime dt = DateTime.Now;
                                remoteFilename = stb.GetLogFilePath(dt);
                            }
                        }
                        else
                        {
                            ServerLog slog = tabItem.Tag as ServerLog;
                            if (null != slog && null != slog.Host)
                            {
                                server = slog.Host as Server;
                                DateTime dt = DateTime.Now;
                                remoteFilename = server.GetLogFilePath(dt);
                            }
                        }
                    }
                }

                remoteFilename = InputWindow.ShowInput("Open remote file from: " + server.Ip, remoteFilename, Properties.Settings.Default.LastOpenFiles);

                if (!string.IsNullOrEmpty(remoteFilename))
                {
                    DownloadLogFileAsync(server, remoteFilename);

                    Properties.Settings.Default.LastOpenFile = remoteFilename;
                    if (!Properties.Settings.Default.LastOpenFiles.Contains(remoteFilename))
                    {
                        Properties.Settings.Default.LastOpenFiles.Add(remoteFilename);
                    }
                    Properties.Settings.Default.Save();
                }
            }
            catch (Exception ex)
            {
                Helper.ShowErrorMessage(ex, "Download Remote File Error");
            }
        }
        #endregion

        #region CmdHospitalAdd
        private void CmdHospitalAdd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (null != App.gVigoUser)
            {
                SelectHospital("Add");
            }
            else
            {
                if (App.Login(this))
                {
                    SelectHospital("Add");
                }
            }
        }
        #endregion

        #region CmdHospitalEdit
        private void CmdHospitalEdit_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (null != App.gVigoUser)
            {
                SelectHospital("Update");
            }
            else
            {
                if (App.Login(this))
                {
                    SelectHospital("Update");
                }
            }
        }
        #endregion

        #region CmdHospitalDelete
        private void CmdHospitalDelete_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                HostBase server = CboServers.SelectedItem as HostBase;
                if (null == server) return;
                if (System.Windows.MessageBox.Show("Remove " + server + "?", "Remove Hospital", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    App.gServers.Remove(server as Server);
                    CboServers.Items.Refresh();
                    if (App.gServers.Count > 0)
                    {
                        CboServers.SelectedIndex = 0;
                    }
                }
            }
            catch(Exception ex)
            {
                Helper.ShowErrorMessage(ex, "Remove Hospital Error");
            }
        }
        #endregion

        #region CmdHospitalConnect
        private void CmdHospitalConnect_Enabled(object sender, CanExecuteRoutedEventArgs e)
        {
            HostBase server = CboServers.SelectedItem as HostBase;
            if (null != HasLogView(server))
            {
                MnuServerConnect.Header = "Disconnect";
            }
            else
            {
                MnuServerConnect.Header = "Connect";
            }
            e.CanExecute = (null != server);
        }

        private void CmdHospitalConnect_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            HostBase server = CboServers.SelectedItem as HostBase;
            if (null == server) return;
            LogBase serverLog = server.Logger;
            if (null != serverLog)
            {
                System.Windows.MessageBox.Show("TODO: disconnect server...");
            }
            else
            {
                // MnuServerConnect.IsEnabled = false;
                ServerLog sl = new ServerLog(server as Server);
                sl.OnStatusReceived += OnServerStatusReceived;
                sl.OnLogDataReceived += OnLogDataReceived;
                sl.Connect();
            }
        }

        private void CmdListHospitals_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (LvHospitals.Visibility == System.Windows.Visibility.Collapsed)
            {
                MnuHospitalListView.Header = "Hide Table View...";
                LvHospitals.Visibility = System.Windows.Visibility.Visible;
                LvStbs.Visibility = System.Windows.Visibility.Collapsed;

                BackgroundWorker bwListHospitals = new BackgroundWorker();
                bwListHospitals.DoWork += bwListHospitals_DoWork;
                bwListHospitals.WorkerReportsProgress = true;
                bwListHospitals.ProgressChanged += bwListHospitals_ProgressChanged;
                bwListHospitals.RunWorkerCompleted += bwListHospitals_RunWorkerCompleted;
                bwListHospitals.RunWorkerAsync();
            }
            else
            {
                MnuHospitalListView.Header = "Show Table View...";
                LvHospitals.Visibility = System.Windows.Visibility.Collapsed;
                LvStbs.Visibility = System.Windows.Visibility.Visible;
            }
        }

        void bwListHospitals_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            TxtStatus.Text = e.ProgressPercentage + "% - " + e.UserState.ToString();
        }

        void bwListHospitals_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            TxtStatus.Text = "";
        }

        void bwListHospitals_DoWork(object sender, DoWorkEventArgs e)
        {
            int count = 0;
            int total = App.gServers.Count;
            foreach (Server s in App.gServers)
            {
                s.RefreshInfo();
                count++;
                BackgroundWorker bw = (sender as BackgroundWorker);
                if (null != bw)
                {
                    bw.ReportProgress((int)(count * 100 / total), s);
                }
            }
        }

        private void CmdHospitalsInfo_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            PullDeviceAndVersionInfo(LvStbs.ItemsSource as List<Stb>);
        }

        public class E3DeviceInfo
        {
            // public string Model { get; set; }
            public string Firmware { get; set; }
            public int Count { get; set; }

            public override string ToString()
            {
                return Firmware + " (" + Count + ")";
            }
        };

        //private E3DeviceInfo GetE3DeviceByModel(List<E3DeviceInfo> lst, string model)
        //{
        //    foreach(E3DeviceInfo d in lst)
        //    {
        //        if (d.Model == model)
        //        {
        //            return d;
        //        }
        //    }
        //    return null;
        //}

        private E3DeviceInfo GetE3DeviceByFirmware(List<E3DeviceInfo> lst, string firmware)
        {
            foreach (E3DeviceInfo d in lst)
            {
                if (d.Firmware == firmware)
                {
                    return d;
                }
            }
            return null;
        }

        private void PullDeviceAndVersionInfo(List<Stb> stbs)
        {
            try
            {
                Dictionary<string, List<E3DeviceInfo>> model_firmware = new Dictionary<string, List<E3DeviceInfo>>();
                foreach (Stb stb in stbs)
                {
                    if (!string.IsNullOrEmpty(stb.InstalledStb.hwModel))
                    {
                        string hwModel = stb.InstalledStb.hwModel;
                        string hwFirmwareVersion = stb.InstalledStb.hwFirmwareVersion;
                        if (model_firmware.ContainsKey(hwModel))
                        {
                            E3DeviceInfo d = GetE3DeviceByFirmware(model_firmware[hwModel], hwFirmwareVersion);
                            if (d != null)
                            {
                                d.Count += 1;
                            }
                            else
                            { 
                                model_firmware[hwModel].Add(new E3DeviceInfo() { Firmware = hwFirmwareVersion, Count = 1 });
                            }
                        }
                        else
                        {
                            model_firmware.Add(hwModel, new List<E3DeviceInfo>());
                            model_firmware[hwModel].Add(new E3DeviceInfo() { Firmware = hwFirmwareVersion, Count = 1 });
                        }
                    }
                }

                string ret = "\nModel - Firmware Info: Total STB = " + stbs.Count + ", TV Models = " + model_firmware.Keys.Count + "\n";
                foreach (string k in model_firmware.Keys)
                {
                    ret += k + ": ";
                    for (int i = 0; i < model_firmware[k].Count; i++)
                    {
                        if (i > 0) ret += ", ";
                        ret += model_firmware[k][i];
                    }
                    ret += "\n";
                }
                AppendLogText(TxtConsole, ret, true);
            }
            catch (Exception ex)
            {
                AppendLogText(TxtConsole, "Exception - " + ex.Message, true);
            }
        }

        #endregion

        #region CmdCloseAllLogs
        private void CmdCloseAllLogs_Enabled(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = false;
            Server server = CboServers.SelectedItem as Server;
            if (null != server)
            {
                foreach (HostBase stb in server.Stbs)
                {
                    if (null != HasLogView(stb))
                    {
                        e.CanExecute = true;
                        break;
                    }
                }
            }
        }

        private void CmdCloseAllLogs_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Server server = CboServers.SelectedItem as Server;
            if (null != server)
            {
                foreach (HostBase stb in server.Stbs)
                {
                    LogBase lb = HasLogView(stb);
                    if (null != lb)
                    {
                        lb.Stop();
                    }
                }
            }
        }
        #endregion

        #region CmdRefreshStbs
        private async void CmdRefreshStbs_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Server s = CboServers.SelectedItem as Server;
            if (null != s)
            {
                LvStbs.ItemsSource = null;
                List<Stb> stbs = await s.RefreshStbs();
                foreach (Stb stb in stbs)
                {
                    SendRemoteControlCommand(stb, "status");
                }
                LvStbs.ItemsSource = stbs;
                LvStbs.Items.Refresh();
            }
        }
        #endregion

        #region Common Commands Enabler
        private void ServerSelectedOne_Enabled(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = (null != (CboServers.SelectedItem as Server));
        }

        private void SelectedOnce_Enabled(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = (null != this.LvStbs.SelectedItem);
        }

        private void SelectedOneOrMore_Enabled(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = (this.LvStbs.SelectedItems.Count > 0);
        }

        private void Always_Enabled(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }
        #endregion

        #region Find Command
        private void Find_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            string txt = InputWindow.ShowInput("Find STB");
            if (null == txt || txt.Length <= 0)
            {
                return;
            }
            int count = FindStb(txt);
            if (count > 0)
            {
                MessageBox.Show("Found " + count + " item(s).", "Find Stb Devices");
            }
            else
            {
                MessageBox.Show("Not found!\n" + txt, "Find Stb Devices");
            }
        }

        private int FindStb(string txt)
        {
            Server server = CboServers.SelectedItem as Server;
            if (null == server) return 0;
            IEnumerable<Stb> stbs = server.Stbs.Where(x => x.ToString().ToLower().Contains(txt.ToLower()));
            int count = stbs.Count();
            if (null != stbs && count > 0)
            {
                if (count == 1)
                {
                    LvStbs.SelectedItem = stbs.FirstOrDefault();
                }
                else
                {
                    foreach (Stb stb in stbs)
                    {
                        LvStbs.SelectedItems.Add(stb);
                    }
                }
            }
            return count;
        }      
        #endregion

        #region Delete Command
        private void Delete_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Server server = CboServers.SelectedItem as Server;
            if (null == server) return;
            int count = this.LvStbs.SelectedItems.Count;
            if (count <= 0)
            {
                return;
            }
            else if (count > 1) 
            {
                if (MessageBox.Show("Remove " + count + " items?", "Remove TV/STB", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    foreach (Stb stb in this.LvStbs.SelectedItems)
                    {
                        server.Stbs.Remove(stb);
                    }
                    LvStbs.Items.Refresh();
                }
            }
            else
            {
                Stb stb = this.LvStbs.SelectedItems[0] as Stb;
                if (MessageBox.Show("Remove " + stb + "?", "Remove TV/STB", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    server.Stbs.Remove(stb);
                    LvStbs.Items.Refresh();
                }
            }
        }

        private void DeleteAll_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Server server = CboServers.SelectedItem as Server;
            if (null == server) return;
            if (MessageBox.Show("Remove all stbs?", "Remove TV/STB", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                server.Stbs.Clear();
                LvStbs.Items.Refresh();
            }
        }
        #endregion

        #region Copy Command
        private void Copy_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                List<string> props = GetVisibleColumns(this.LvStbs);
                StringBuilder sb = new StringBuilder();
                foreach (string prop in props)
                {
                    sb.Append(prop + "\t");
                }
                sb.Append("\n");

                foreach (Stb stb in this.LvStbs.SelectedItems)
                {
                    Type t = stb.GetType();
                    if (null != t)
                    {
                        foreach (string prop in props)
                        {
                            PropertyInfo p = t.GetProperty(prop);
                            if (null != p)
                            {
                                object value = p.GetValue(stb, null);
                                sb.Append((null != value) ? (value.ToString()) : "");
                                sb.Append(string.Empty + "\t");
                            }
                            else if (prop.IndexOf("InstalledStb.") >= 0)
                            {
                                Type st = stb.InstalledStb.GetType();
                                if (null != st)
                                {
                                    p = st.GetProperty(prop.Replace("InstalledStb.", ""));
                                    if (null != p)
                                    {
                                        object value = p.GetValue(stb.InstalledStb, null);
                                        sb.Append((null != value) ? (value.ToString()) : "");
                                        sb.Append(string.Empty + "\t");
                                    }
                                }
                            }
                        }
                        sb.Append("\n");
                    }
                }

                Clipboard.Clear();
                Clipboard.SetDataObject(sb.ToString());
                // AppendStatusText("\n" + sb.ToString());
            }
            catch (Exception ex)
            {
                Helper.ShowErrorMessage(ex);
            }
        }

        private List<string> GetVisibleColumns(ListView lv)
        {
            List<string> props = new List<string>();
            System.Windows.Controls.GridView gv = lv.View as System.Windows.Controls.GridView;
            foreach (System.Windows.Controls.GridViewColumn gvc in gv.Columns)
            {
                System.Windows.Data.Binding b = gvc.DisplayMemberBinding as System.Windows.Data.Binding;
                props.Add(b.Path.Path);
            }
            return props;
        }
        #endregion        

        #region Properties Command
        private void Properties_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (ProgGridStb.Visibility == System.Windows.Visibility.Visible)
            {
                ProgGridStb.Visibility = System.Windows.Visibility.Collapsed;
            }
            else 
            {
                ProgGridStb.Visibility = System.Windows.Visibility.Visible;
            }            
        }
        #endregion

        #region AddStb Command
        private void AddStb_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                Stb stb = new Stb() { Name = "NEW_STB", MacId = "MAC_ID" };
                Server s = CboServers.SelectedItem as Server;
                if (null != s && LvStbs.ItemsSource == s.Stbs)
                {
                    stb.Server = s;
                    s.Stbs.Add(stb);
                }
                else 
                {
                    this.LvStbs.Items.Add(stb);
                }
                this.LvStbs.Items.Refresh();
            }
            catch (Exception ex)
            {
                Helper.ShowErrorMessage(ex);
            }
        }
        #endregion

        #region HighLightItems Command
        private void HighLightItems_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            int index = -1;

            try { index = Convert.ToInt32(e.Parameter.ToString()); }
            catch { }

            foreach (Stb stb in this.LvStbs.SelectedItems)
            {
                stb.HighLightedBrushIndex = index;
            }

            this.LvStbs.Items.Refresh();
        }
        #endregion

        #region CmdStbRemote 
        private void CmdStbRemote_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            foreach (Stb stb in this.LvStbs.SelectedItems)
            {
                SendRemoteControlCommand(stb, e.Parameter.ToString());
            }
        }
        #endregion        

        #region CmdStbViewLog
        private void CmdStbViewLog_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Stb stb = this.LvStbs.SelectedItem as Stb;
            if (null == stb) return;

            LogBase log = HasLogView(stb);
            if (null != log)
            {
                log.Stop();
                return;
            }

            Server server = CboServers.SelectedItem as Server;
            ServerLog serverLog = server.Logger as ServerLog;
            if (null == serverLog)
            {
                _pendingLogStart = stb;
                serverLog = new ServerLog(server);
                serverLog.OnStatusReceived += OnServerStatusReceived;
                serverLog.OnLogDataReceived += OnLogDataReceived;
                serverLog.Connect();
                return;
            }

            StartViewLog(stb, serverLog);
        }
        #endregion        

        #region CmdStbViewInfo
        bool _executingCmdStbViewInfo = false;
        private void CmdStbViewInfo_Enabled(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = (null != this.LvStbs.SelectedItem) && !_executingCmdStbViewInfo;
        }

        private async void CmdStbViewInfo_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                Stb stb = LvStbs.SelectedItem as Stb;
                if (null == stb) return;

                _executingCmdStbViewInfo = true;
                string info = await Task.Run(() =>
                {
                    return ParseSystemInfoFromLogs(stb);
                });

                LogBase log = HasLogView(stb);
                if (null != log)
                {
                    AppendLogText(log.LogViewer, info, false);
                }
                else
                {
                    MessageBox.Show(info, "System Info - " + stb.ToString());
                }
            }
            catch (Exception ex)
            {
                Helper.ShowErrorMessage(ex);
            }
            _executingCmdStbViewInfo = false;
        }
        #endregion        

        #region CmdStbRegister
        private void CmdStbRegister_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Stb stb = LvStbs.SelectedItem as Stb;
            if (null == stb) return;

            if (null != App.gVigoUser)
            {
                RegisterVigoStb(stb);
            }
            else
            {
                if (App.Login(this))
                {
                    RegisterVigoStb(stb);
                }
            }
        }
        #endregion

        #region CmdStbData
        private void CmdStbData_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Stb stb = LvStbs.SelectedItem as Stb;
            if (null == stb) return;

            if (null != App.gVigoUser)
            {
                GetStbData(stb);
            }
            else
            {
                if (App.Login(this))
                {
                    GetStbData(stb);
                }
            }
        }
        #endregion

        #region CmdShowHideRemoteControl
        private void CmdShowHideRemoteControl_Enabled(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = false;
            int c = LvStbs.SelectedItems.Count;
            Button b = (e.OriginalSource as Button);
            if (c > 0)
            {
                e.CanExecute = true;
                if (c == 1)
                {
                    b.Content = "Remote Control - " + (LvStbs.SelectedItem as Stb).Name;
                }
                else
                {
                    b.Content = "Remote Control - Multiples (" + c +  ")";
                }
            }
            else
            {
                b.Content = "Remote Control";
            }
        }

        private void CmdShowHideRemoteControl_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (PnlRemoteControl.Visibility == System.Windows.Visibility.Visible)
            {
                PnlRemoteControl.Visibility = System.Windows.Visibility.Collapsed;
            }
            else
            {
                PnlRemoteControl.Visibility = System.Windows.Visibility.Visible;
            }
        }
        #endregion

        private void CmdCreateZipLoader_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                CreateZipLoaderWindow w = new CreateZipLoaderWindow();
                // w.SelectedHospitalId = (this.CboServers.SelectedItem as Server).Id;
                w.ShowDialog();
            }
            catch (Exception ex)
            {
                Helper.ShowErrorMessage(ex);
            }
        }

        private void CmdCheckSoftwareUpdate_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Updater.Check(this, true);
        }
        
    }

    public class StatusToColorBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (null != value && value is Stb)
            {
                string status = (value as Stb).Status;
                if (status.StartsWith("Error") || status.StartsWith("Down"))
                {
                    return Brushes.Red;
                }
                else if (status.StartsWith("..."))
                {
                    return Brushes.Chocolate;
                }
                else if (status.StartsWith("Up"))
                {
                    return Brushes.DarkGreen;
                }
            }
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class HighLightedBrushIndexToColorBrushConverter : IValueConverter
    {
        public static List<SolidColorBrush> HighLightedBrushes = new List<SolidColorBrush>()
            {
                Brushes.Red,
                Brushes.Orange,
                Brushes.Yellow,
                Brushes.LightGreen,
                Brushes.Cyan,
                Brushes.LightBlue,
                Brushes.Magenta                
            };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (null != value && value is Stb)
            {
                int index = (value as Stb).HighLightedBrushIndex;
                if (index >= 0 && index < HighLightedBrushes.Count)
                {
                    return HighLightedBrushes[index];
                }
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
