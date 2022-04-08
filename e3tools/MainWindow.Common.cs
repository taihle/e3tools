// ------------------------------------------------------------------------------
// Copyright (c) 2016 - Allen Technologies www.allentek.com. All Rights Reserved.
// Author: Tai H. Le <tle@allentek.com>
// 
// Main Window - Common functions
// ------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Xml;
using Renci.SshNet;
using Ati.VigoPC.WebServices.REST;
using System.Dynamic;

namespace e3tools
{
    public partial class MainWindow
    {
        void InitServers()
        {
            Server selectedServer = null;
            string selectedServerName = Properties.Settings.Default.ActiveServer;            
            CboServers.ItemsSource = App.gServers;
            LvHospitals.ItemsSource = App.gServers;
            if (App.gServers.Count > 0)
            {
                selectedServer = App.gServers[0];
                if (!string.IsNullOrEmpty(selectedServerName))
                {
                    selectedServer = App.gServers.FirstOrDefault(x => x.Name == selectedServerName);
                    if (null == selectedServer) selectedServer = App.gServers[0];
                }
                CboServers.SelectedItem = selectedServer;
            }

            if (null != selectedServer && null != selectedServer.Stbs)
            {
                LvStbs.ItemsSource = selectedServer.Stbs;
            }
        }

        void HideElement(FrameworkElement e)
        {
            e.Visibility = System.Windows.Visibility.Collapsed;
        }

        void ShowElement(FrameworkElement e)
        {
            e.Visibility = System.Windows.Visibility.Visible;
        }

        void ResetLineCount(RichTextBox rtb)
        {
            if (_maxLines <= 0) return;

            string txt = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd).Text;
            int count = 0;
            int p = txt.IndexOf('\n');
            while (p != -1)
            {
                count++;
                p = txt.IndexOf('\n', p + 1);
            }
            if (count > _maxLines)
            {
                rtb.Document.Blocks.Clear();
            }
        }

        void AppendLogText(RichTextBox rtb, string txt, bool resetLineCount = true)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    if (null == rtb || string.IsNullOrEmpty(txt)) return;

                    if (resetLineCount)
                    {
                        ResetLineCount(rtb);
                    }

                    rtb.AppendText(txt);

                    if (ChkScroll.IsChecked.Value)
                    {
                        rtb.ScrollToEnd();
                    }
                });
            }
            catch(Exception ex)
            {
            }
        }

        void UpdateMenuHeader(MenuItem mnu, string header)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    mnu.Header = header;
                });
            }
            catch (Exception ex)
            {
            }
        }

        bool IsEmptyText(TextBox tb)
        {
            bool ret = string.IsNullOrWhiteSpace(tb.Text);
            if (ret) tb.Focus();
            return ret;
        }

        void SaveFile(string filename)
        {
            try
            {
                App.gServers = App.gServers.OrderBy(x => x.Name).ToList();
                XmlDocument xd = new XmlDocument();
                xd.AppendChild(Server.ToXml(App.gServers, xd));
                xd.Save(filename);
            }
            catch (Exception ex)
            {
                Helper.ShowErrorMessage(ex);
            }
        }

        bool OpenFile(string filename)
        {
            try
            {
                XmlDocument xd = new XmlDocument();
                xd.Load(filename);
                App.gServers = Server.FromXmls(xd.DocumentElement);
                InitServers();
                return true;
            }
            catch (Exception ex)
            {
                Helper.ShowErrorMessage(ex);
            }
            return false;
        }

        string ParseSystemInfoFromStream(StreamReader sr, out int startInfoLine, out int endInfoLine)
        {
            startInfoLine = 0;
            endInfoLine = 0;
            string info = string.Empty;
            string reportedDateLine = string.Empty;
            int lineNum = 0;
            while (sr.Peek() >= 0)
            {
                string line = sr.ReadLine();
                lineNum++;
                if (line.StartsWith(">>>>>>>>>>>>>>> SYSTEM INFO"))
                {
                    if (startInfoLine <= 0) // 1st pair
                    {
                        startInfoLine = lineNum;
                        endInfoLine = -1;
                        info = reportedDateLine + line + "\n";
                    }
                    else if (endInfoLine <= 0) // end first pair
                    {
                        endInfoLine = lineNum;
                        info += line + "\n";
                    }
                    else // sub sequence pair
                    {
                        startInfoLine = lineNum;
                        endInfoLine = -1;
                        info = reportedDateLine + line + "\n";
                    }
                    continue;
                }
                if (startInfoLine > 0 && endInfoLine <= 0)
                {
                    info += line + "\n";
                }
                else
                {
                    reportedDateLine = line;
                }
            }

            return info;
        }

        string ParseSystemInfoFromLogs(Stb stb)
        {            
            string info = string.Empty;
            DateTime dt = DateTime.Now;
            string remoteFilePath = stb.GetLogFilePath(dt);
            int startInfoLine = 0;
            int endInfoLine = 0;
            
            Server server = stb.Server;

            using (SftpClient sftp = new SftpClient(server.Ip, server.Username, server.Password))
            {
                sftp.Connect();

                bool done = false;
                while (!done)
                {
                    try
                    {
                        using (MemoryStream ms = new MemoryStream(1024))
                        {
                            sftp.DownloadFile(remoteFilePath, ms);
                            ms.Seek(0, SeekOrigin.Begin);
                            using (StreamReader sr = new StreamReader(ms))
                            {
                                info = ParseSystemInfoFromStream(sr, out startInfoLine, out endInfoLine);
                            }
                        }
                    }
                    catch { }

                    if (string.IsNullOrEmpty(info))
                    {
                        if (dt.Subtract(DateTime.Now).Days < 5)
                        {
                            dt = dt.Subtract(TimeSpan.FromDays(1)); // move back one day
                            remoteFilePath = stb.GetLogFilePath(dt);
                        }
                        else
                        {
                            done = true;
                        }
                    }
                    else
                    {
                        done = true;
                    }
                }

                sftp.Disconnect();

                if (string.IsNullOrEmpty(info))
                {
                    info = ">>> unable to pull the system info from log <<<";
                }
                else
                {
                    info = "[SYSTEM INFO FROM: " + remoteFilePath + "] @ [" + startInfoLine + "," + endInfoLine + "]\n" + info;
                }
            }

            return info;
        }

        void DownloadLogFileAsync(Server server, string remoteFilePath)
        {
            BtnOpenLogFile.Tag = BtnOpenLogFile.Content;
            BtnOpenLogFile.Content = "Downloading...";
            BtnOpenLogFile.IsEnabled = false;

            BackgroundWorker bwDownloadRemoteFileAsync = new BackgroundWorker();
            bwDownloadRemoteFileAsync.DoWork += bwDownloadRemoteFileAsync_DoWork;
            bwDownloadRemoteFileAsync.RunWorkerCompleted += bwDownloadRemoteFileAsync_RunWorkerCompleted;
            dynamic args = new ExpandoObject();
            args.server = server;
            args.remoteFilePath = remoteFilePath;
            bwDownloadRemoteFileAsync.RunWorkerAsync(args);
        }

        void bwDownloadRemoteFileAsync_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            string localFile = e.Result.ToString();
            if (File.Exists(localFile))
            {
                System.Diagnostics.Process.Start(localFile);
            }
            else if (!string.IsNullOrEmpty(localFile) && localFile.StartsWith("Error:"))
            {
                Helper.ShowErrorMessage(localFile, "Download Remote File Error");
            }
            BtnOpenLogFile.Content = BtnOpenLogFile.Tag;
            BtnOpenLogFile.IsEnabled = true;
        }

        void bwDownloadRemoteFileAsync_DoWork(object sender, DoWorkEventArgs e)
        {
            IDictionary<string, object> args = (IDictionary<string, object>)(e.Argument);
            Server server = args["server"] as Server;
            string remoteFilePath = args["remoteFilePath"] as string;
            e.Result = DownloadRemoteFile(server, remoteFilePath);
        }

        Renci.SshNet.Sftp.SftpFile __downloadRemoteFileInfo;
        public void OnDownloadRemoteFileStatusUpdate(ulong bytesWritten)
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    ulong percent = bytesWritten;
                    string status = "" + percent;
                    if (null != __downloadRemoteFileInfo && __downloadRemoteFileInfo.Length > 0)
                    {
                        long bytes = __downloadRemoteFileInfo.Length;
                        status = "" + bytes + "B";
                        if (bytes > 1024)
                        {
                            bytes = bytes / 1024;
                            status = "" + bytes + "KB";
                            if (bytes > 1024)
                            {
                                bytes = bytes / 1024;
                                status = "" + bytes + "MB";
                            }
                        }
                        percent = (ulong)(percent * 100 / (ulong)(__downloadRemoteFileInfo.Length));
                        status = "[" + bytesWritten + "B/" + status + " ~ " + percent + "%]";
                    }

                    BtnOpenLogFile.Content = "Downloading... " + status;
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        string DownloadRemoteFile(Server server, string remoteFilePath)
        {
            string localFile = string.Empty;
            try
            {
                using (SftpClient sftp = new SftpClient(server.Ip, server.Username, server.Password))
                {
                    sftp.Connect();
                    localFile = @"c:\temp\" + remoteFilePath.Substring(remoteFilePath.LastIndexOf('/') + 1);
                    using (FileStream fs = new FileStream(localFile, FileMode.Create, FileAccess.Write))
                    {
                        __downloadRemoteFileInfo = sftp.Get(remoteFilePath);
                        sftp.DownloadFile(remoteFilePath, fs, OnDownloadRemoteFileStatusUpdate);
                    }
                    sftp.Disconnect();
                }
            }
            catch (Exception ex)
            {
                localFile = "Error: " + ex.Message + "\n\nServer: " + server + "\nRemote File:" + remoteFilePath;
            }
            return localFile;
        }

        void CheckStbStatus()
        {
            BackgroundWorker bwCheckStbStatus = new BackgroundWorker();
            bwCheckStbStatus.WorkerReportsProgress = true;
            bwCheckStbStatus.DoWork += bwCheckStbStatus_DoWork;
            bwCheckStbStatus.ProgressChanged += bwCheckStbStatus_ProgressChanged;
        }

        void bwCheckStbStatus_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            
        }

        void bwCheckStbStatus_DoWork(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                if (null != App.gVigoUser)
                {
                    foreach (Server server in App.gServers)
                    {
                        WSHospital h = server.Hospital;
                        if (null != h)
                        {
                            List<WSInstalledSTB> stbs = App.gVigoUserClient.GetStbsByHospitalId(h.identity);
                            //foreach (WSInstalledSTB stb in stbs)
                            //{
                            //    Stb stbi = server.Stbs.FirstOrDefault(x => x.Id == stb.identity);
                            //    if (null != stbi)
                            //    {
                            //        stbi.LastBeat = stb.lastBeat;
                            //    }
                            //}
                        }
                        Thread.Sleep(60000); // 1 minute
                    }
                }

                Thread.Sleep(300000); // 5 minutes
            }
        }
    }
}
