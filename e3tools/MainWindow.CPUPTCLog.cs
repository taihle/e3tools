// ------------------------------------------------------------------------------
// Copyright (c) 2015 - Allen Technologies Inc. allentek.com. All Rights Reserved.
// Author: Tai H. Le <tle@allentek.com>
// 
// Handle LG CPU/TCP log data from TV
// ------------------------------------------------------------------------------
using System;
using System.ComponentModel;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace e3tools
{
    public partial class MainWindow
    {
        BackgroundWorker _bwCPULog = null;
        StreamWriter _cpuLogFile = null;
        RichTextBox _rtbCpuPtcLog = null;
        bool _cpuLogEnabled = false;

        bool CreateCPULogViewIfNeeded(Stb stb)
        {
            if (null == _rtbCpuPtcLog)
            {
                TabItem tabItem = new TabItem();
                tabItem.Header = "CPU Log (" + stb.Name + ")";
                _rtbCpuPtcLog = new RichTextBox();
                _rtbCpuPtcLog.Style = this.TryFindResource("RichTextBoxLogStyleServer") as Style;
                tabItem.Content = _rtbCpuPtcLog;
                TcLogTabs.Items.Add(tabItem);
                TcLogTabs.SelectedItem = tabItem;
                _rtbCpuPtcLog.Tag = stb;
            }
            else
            {
                if (stb != _rtbCpuPtcLog.Tag)
                {
                    AppendLogText(_rtbCpuPtcLog, ">>> Please Disable and Stop View CPU log of the current STB first! <<<");
                    return false; // 1 STB at a time for now
                }
                else
                {
                    _rtbCpuPtcLog.Tag = stb;
                }
            }
            return true;
        }

        void EnableCPULog(Stb stb)
        {
            if (!CreateCPULogViewIfNeeded(stb)) return;

            if (_cpuLogEnabled)
            {
                stb.DisableCPUPTCLog(done =>
                {
                    AppendLogText(_rtbCpuPtcLog, done);
                    _cpuLogEnabled = false || (done.Contains("EXCEPTION:"));
                    UpdateMenuHeader(MnuStbEnableDisableCPULog, (_cpuLogEnabled ? "Disable CPU Log" : "Enable CPU Log"));
                    if (null != _rtbCpuPtcLog)
                    {
                        if (null == _bwCPULog)
                        {
                            _rtbCpuPtcLog.Tag = null;
                        }
                    }
                });
            }
            else
            {
                stb.EnableCPUPTCLog(done =>
                {
                    AppendLogText(_rtbCpuPtcLog, done);
                    _cpuLogEnabled = (!done.Contains("EXCEPTION:"));
                    UpdateMenuHeader(MnuStbEnableDisableCPULog, (_cpuLogEnabled ? "Disable CPU Log" : "Enable CPU Log"));
                });
            }
        }

        void ViewCPULog(Stb stb)
        {
            if (!CreateCPULogViewIfNeeded(stb)) return;

            if (null != _bwCPULog)
            {
                _bwCPULog.CancelAsync();
            }
            else
            {
                MnuStbViewCPUPTCLog.IsEnabled = false;

                string path = Helper.SaveFileDialog("Save Log File", "Log file (*.log)|*.log|All (*.*)|*.*", stb.Name.Replace("/", "_") + "_CPU.log", string.Empty);
                if (string.IsNullOrEmpty(path)) return;

                if (null != _cpuLogFile)
                {
                    _cpuLogFile.Close();
                }
                _cpuLogFile = new StreamWriter(path, true);
                _cpuLogFile.AutoFlush = true;

                _bwCPULog = new BackgroundWorker();
                _bwCPULog.WorkerReportsProgress = true;
                _bwCPULog.WorkerSupportsCancellation = true;
                _bwCPULog.ProgressChanged += _bwCPULog_ProgressChanged;
                _bwCPULog.RunWorkerCompleted += _bwCPULog_RunWorkerCompleted;
                _bwCPULog.DoWork += _bwCPULog_DoWork;
                _bwCPULog.RunWorkerAsync(stb);

                UpdateMenuHeader(MnuStbViewCPULog, "Stop View CPU Log");
            }
        }

        void _bwCPULog_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            UpdateMenuHeader(MnuStbViewCPULog, "Start View CPU Log");
            _bwCPULog = null;
            if (null != _rtbCpuPtcLog)
            {
                if (!_cpuLogEnabled)
                {
                    _rtbCpuPtcLog.Tag = null;
                }
            }
        }

        void _bwCPULog_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {            
            string data = e.UserState.ToString();
            if (null != _cpuLogFile)
            {
                _cpuLogFile.Write(data);
            }

            if (ChkPauseLog.IsChecked.Value) return;
            AppendLogText(_rtbCpuPtcLog, data);
        }

        void _bwCPULog_DoWork(object sender, DoWorkEventArgs e)
        {
            Thread.Sleep(1000); // wait a bit

            BackgroundWorker bw = (sender as BackgroundWorker);

            try
            {
                Stb stb = e.Argument as Stb;

                TcpClient server = new TcpClient(stb.Ip, 23);
                NetworkStream ns = server.GetStream();
                byte[] data = new byte[2048];
                int recv = ns.Read(data, 0, data.Length);
                string stringData = Encoding.ASCII.GetString(data, 0, recv);
                bw.ReportProgress(0, stringData);
                while (true)
                {
                    if (bw.CancellationPending)
                    {
                        break;
                    }
                    if (stringData.EndsWith("login: "))
                    {
                        string[] cmds = {"rms"}; // {"rms", "d", "fc", "1", "b", "1", "ff", "exit"};
                        for (int i = 0; i < cmds.Length; i++)
                        {
                            byte[] cmd = Encoding.ASCII.GetBytes(cmds[i] + "\r\n");
                            ns.Write(cmd, 0, cmd.Length);
                            ns.Flush();
                            Thread.Sleep(500);
                        }
                    }
                    recv = ns.Read(data, 0, data.Length);
                    stringData = Encoding.ASCII.GetString(data, 0, recv);
                    bw.ReportProgress(0, stringData);
                    Thread.Sleep(100);
                }
                ns.Close();
                server.Close();
            }
            catch (Exception ex)
            {
                bw.ReportProgress(0, "Exception: " + ex.Message);
            }
        }

    }
}
