// ------------------------------------------------------------------------------
// Copyright (c) 2016 - Allen Technologies www.allentek.com. All Rights Reserved.
// Author: Tai H. Le <tle@allentek.com>
// 
// Log Objects
// ------------------------------------------------------------------------------
using Renci.SshNet;
using System;
using System.ComponentModel;
using System.Threading;
using System.Windows.Controls;

namespace e3tools
{
    public delegate void LogDataCallback(object sender, string data);
    public delegate void ConnectionStatusCallback(object sender, string status);

    public class LogBase
    {
        public HostBase Host { get; set; }
        public RichTextBox LogViewer { get; set; }
        public SshClient Ssh = null;
        protected BackgroundWorker _bw = null;
        protected string _filterText = string.Empty;
        protected bool _updateTailCmd = false;

        public LogBase()
        {
            this.LogViewer = null;
        }

        public void ClearLogOnServer()
        {
            try
            {
                if (null != this.Ssh)
                {
                    string filename = this.Host.GetLogFilePath(DateTime.Now);
                    this.Ssh.RunCommand("cp " + filename + " " + filename + "_clear_bk");
                    this.Ssh.RunCommand("date > " + filename);
                    this.Ssh.RunCommand("echo '- - - - - - - - - - clear from remote tool - - - - - - - - - - - -' >> " + filename);
                }
            }
            catch (Exception ex)
            {
            }
        }

        public void ApplyFilter(string txt)
        {
            if (_filterText != txt)
            {
                _filterText = txt;
                _updateTailCmd = true;
            }
        }

        protected void InitBackgroundWorker(DoWorkEventHandler doWorkHandler, ProgressChangedEventHandler progressHandler, RunWorkerCompletedEventHandler completeHandler)
        {
            if (null == _bw)
            {
                _bw = new BackgroundWorker();
                _bw.WorkerReportsProgress = true;
                _bw.WorkerSupportsCancellation = true;
                _bw.DoWork += doWorkHandler;
                _bw.ProgressChanged += progressHandler;
                _bw.RunWorkerCompleted += completeHandler;
            }
        }

        protected string getTailCmd()
        {
            string logCmd = "tail -f -n 50 " + this.Host.GetLogFilePath(DateTime.Now);
            if (!string.IsNullOrEmpty(this._filterText))
            {
                logCmd += " | grep '" + _filterText + "'";
            }
            return logCmd;
        }

        protected int startTailCommand(string cmd)
        {
            int ret = 0;
            try
            {                
                ShellStream shell = this.Ssh.CreateShellStream("", 125, 25, 800, 600, 8192);

                while (shell.DataAvailable)
                {
                    string s = shell.Read();
                    if (!string.IsNullOrEmpty(s))
                    {
                        _bw.ReportProgress(0, s);
                    }
                }

                shell.WriteLine(cmd);

                while (true)
                {
                    if (_bw.CancellationPending)
                    {
                        break;
                    }
                    if (_updateTailCmd)
                    {
                        _updateTailCmd = false;
                        ret = 1;
                        break;
                    }
                    if (shell.DataAvailable)
                    {
                        string s = shell.Read();
                        if (!string.IsNullOrEmpty(s))
                        {
                            _bw.ReportProgress(0, s);
                        }
                    }
                    Thread.Sleep(1000);
                }
                shell.Close();
            }
            catch (Exception ex)
            {
            }
            return ret;
        }

        public virtual void Stop() 
        { 
        }
    }

    public class StbLog : LogBase
    {
        public DateTime Date { get; set; }
        public event LogDataCallback OnLogDataReceived = null;
        public event ConnectionStatusCallback OnStatusReceived = null;

        public StbLog()
        {
            this.Date = DateTime.Now;
        }

        public StbLog(SshClient ssh, HostBase stb)
            : this()
        {
            this.Host = stb;
            this.Ssh = ssh;
        }

        public override void Stop()
        {
            if (null != _bw)
            {
                _bw.CancelAsync();
            }
        }

        public void Start()
        {
            InitBackgroundWorker(_bw_DoWork, _bw_ProgressChanged, _bw_RunWorkerCompleted);

            if (_bw.IsBusy) return;

            _bw.RunWorkerAsync();
        }

        string getStartCatCmd()
        {
            return "cat " + this.Host.GetLogFilePath(DateTime.Now);
        }

        void _bw_DoWork(object sender, DoWorkEventArgs e)
        {
            int ret = startTailCommand(getTailCmd());
            while (ret != 0)
            {
                ret = startTailCommand(getTailCmd());
            }
        }

        void _bw_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (null != this.OnLogDataReceived)
            {
                this.OnLogDataReceived(this, e.UserState.ToString());
            }
        }

        void _bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.OnStatusReceived(this, "CLOSED");
        }

        public override string ToString()
        {
            return this.Host.ToString();
        }
    }
    
    public class ServerLog : LogBase
    {
        public string Status { get; set; }
        public event LogDataCallback OnLogDataReceived = null;
        public event ConnectionStatusCallback OnStatusReceived = null;
        protected ManualResetEvent _viewLogSet = new ManualResetEvent(false);

        public ServerLog()
        {
            this.Ssh = null;
        }

        public ServerLog(Server host)
            : this()
        {
            this.Host = host;
            host.Logger = this;
        }

        public void Start()
        {
            _viewLogSet.Set();
        }

        public void Connect()
        {
            InitBackgroundWorker(_bw_DoWork, _bw_ProgressChanged, _bw_RunWorkerCompleted);

            if (_bw.IsBusy) return;

            _bw.RunWorkerAsync();
        }


        void _bw_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                if (null != this.Ssh)
                {
                    if (this.Ssh.IsConnected)
                    {
                        return;
                    }
                }
                this.Ssh = null;
                SshClient ssh = new SshClient(this.Host.Ip, (this.Host as Server).Username, (this.Host as Server).Password);
                ssh.Connect();
                this.Ssh = ssh;
                _bw.ReportProgress(1, "CONNECTED");
            }
            catch (Exception ex)
            {
                _bw.ReportProgress(2, "ERROR: " + ex.Message);
                return;
            }

            _viewLogSet.WaitOne();

            int ret = startTailCommand(getTailCmd());
            while (ret != 0)
            {
                ret = startTailCommand(getTailCmd());
            }
        }

        void _bw_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            if (e.ProgressPercentage != 0)
            {
                if (null != this.OnStatusReceived)
                {
                    this.OnStatusReceived(this, e.UserState.ToString());
                }
            }
            else
            {
                if (null != this.OnLogDataReceived)
                {
                    this.OnLogDataReceived(this, e.UserState.ToString());
                }
            }
        }

        void _bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (null != this.Ssh)
            {
                if (this.Ssh.IsConnected)
                {
                    this.Ssh.Disconnect();
                }
            }
            if (null != this.OnStatusReceived)
            {
                this.OnStatusReceived(this, "CLOSED");
            }
        }

        public void Disconnect()
        {
            if (null != _bw)
            {
                _bw.CancelAsync();
            }
        }
    }
}
