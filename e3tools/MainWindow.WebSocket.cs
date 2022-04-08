// ------------------------------------------------------------------------------
// Copyright (c) 2016 - Allen Technologies www.allentek.com. All Rights Reserved.
// Author: Tai H. Le <tle@allentek.com>
// 
// Main Window - Common functions - Web Socket
// ------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using e3lib;
using WebSocket4Net;

namespace e3tools
{
    public partial class MainWindow
    {
        public string MY_WS_ID = "remote/e3tools-" + Process.GetCurrentProcess().Id;
        WebSocket _wsRemote = null;
        Server _server = null;
        List<E3WSClient> _connectedClients = new List<E3WSClient>();

        public void InitRemoteControlWebSocket()
        {
            bool changed = false;
            try
            {
                Server server = (CboServers.SelectedItem as Server);
                if (_server != server)
                {
                    _server = server;
                    changed = true;
                }
            }
            catch (Exception) { }

            if (null == _server || string.IsNullOrEmpty(_server.Ip))
            {
                AppendLogText(TxtConsole, "Error - Invalid Boot Server Config! " + (null != _server ? _server.Name : "NULL"));
                return;
            }

            if (changed)
            {
                CloseWebSocket();
            }

            if (null == _wsRemote)
            {
                _wsRemote = new WebSocket("ws://" + _server.Ip + ":" + _server.WSPort + "/" + MY_WS_ID, "", WebSocketVersion.Rfc6455);
                _wsRemote.Opened += _wsRemote_Opened;
                _wsRemote.Error += _wsRemote_Error;
                _wsRemote.Closed += _wsRemote_Closed;
                _wsRemote.MessageReceived += _wsRemote_MessageReceived;
                _wsRemote.Open();
            }
        }

        void CloseWebSocket()
        {
            if (null != _wsRemote)
            {
                _wsRemote.Close();
                if (null != _wsRemote)
                {
                    _wsRemote.Dispose();
                    _wsRemote = null;
                }
            }
        }

        [DllImport("shell32.dll")]
        public extern static int FindExecutable(
            string forFile,
            string directory,
            StringBuilder result
        );

        StringBuilder _browserLocation = null;
        void startLocalBroswerAppForScreenShot(string clientId)
        {
            E3WSClient c = _connectedClients.FirstOrDefault(x => x.type == "display" && x.id == clientId);
            if (null == c)
            {
                string filename = @"C:\\Dev\E3\tools\e3wsserver\html\display.html";

                if (null == _browserLocation)
                {
                    _browserLocation = new StringBuilder(1024);
                    FindExecutable(filename, null, _browserLocation);
                }

                Process p = Process.Start(
                    _browserLocation.ToString(), @"http://nextgen.local/e3/wsremote/display.html?id=" + clientId.ToLower()
                );
            }
        }

        public string SendRemoteControlCommand(Stb stb, string cmd)
        {
            try
            {
                if (null == _wsRemote)
                {
                    InitRemoteControlWebSocket();
                    return string.Empty;
                }
                switch (cmd)
                {
                    case "poff":
                        stb.PowerOff();
                        break;
                    case "pon":
                        stb.PowerOn();
                        break;
                    case "boot":
                        stb.Reboot();
                        break;
                    default:
                        if (null != _wsRemote && _wsRemote.State == WebSocketState.Open)
                        {
                            E3WSMessage msg = new E3WSMessage() { target_id = E3WSConst.WS_CLIENT_TYPE_STB + "/" + stb.MacId, msg = "sendkey", data = cmd };
                            if (cmd == "vnc")
                            {
                                msg.msg = "vnc";
                                // startLocalBroswerAppForScreenShot(stb.MacId);
                            }
                            else if (cmd == "tv" || cmd == "restart" || cmd == "verify_videos" || cmd == "reboot" || cmd == "verify_tvchannels" || cmd == "get_local_log" || cmd == "reinstall_e3")
                            {
                                msg.msg = cmd;
                            }
                            else if (cmd == "status")
                            {
                                msg.msg = cmd;
                                msg.source_id = MY_WS_ID;
                            }
                            else if (cmd == "url")
                            {
                                string ret = setUrlMessage(cmd, msg);
                                if (string.IsNullOrEmpty(ret))
                                {
                                    return string.Empty;
                                }
                            }
                            else if (cmd == "beurl")
                            {
                                string ret = setBeUrlMessage(cmd, msg);
                                if (string.IsNullOrEmpty(ret))
                                {
                                    return string.Empty;
                                }
                            }
                            else if (cmd == "urlx")
                            {
                                setUrlxMessage(cmd, msg);
                                msg.source_id = MY_WS_ID;
                            }
                            else if (cmd == "update_firmware")
                            {
                                updateFirmwareMessage(msg);
                            }
                            else if (cmd == "alert")
                            {
                                setAlertMessage(msg);
                            }
                            else if (cmd == "log")
                            {
                                setLogConfigMessage(msg);
                            }
                            else if (cmd == "send_tv_msg")
                            {
                                setTvMessage(msg);
                            }
                            else if (cmd == "send_key")
                            {
                                setSendKeyMessage(msg);
                            }
                            else if (cmd == "popup")
                            {
                                msg.msg = "popup";
                                msg.data = "my_inbox";
                            }
                            else if (cmd == "clearcache")
                            {
                                if (!confirmCommand(cmd))
                                {
                                    return "FAILED";
                                }
                                msg.msg = cmd;
                                msg.data = cmd;
                            }
                            string msg_str = msg.ToJson();
                            _wsRemote.Send(msg_str);
                        }
                        break;
                }
                return "OK";
            }
            catch (Exception ex)
            {
                return "SendRemoteControlCommand(): Exception - " + ex.Message;
            }
        }

        bool confirmCommand(string cmd)
        {
            return MessageBox.Show("Hope you know what you're doing. Continue?", "Confirm Command: " + cmd, 
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        void startResponseCheck()
        {

        }

        string setTvMessage(E3WSMessage msg)
        {
            msg.msg = "send_tv_msg";
            string tv_msg = _getDefaultStringsProperty(Properties.Settings.Default.WSRemoteTVMsg, "<SDAP/1.0>SERVER 100 SYSTEM TEST ACK=YES </SDAP/1.0>");
            tv_msg = InputWindow.ShowInput("Enter UDP/LDAP TV Message", tv_msg, Properties.Settings.Default.WSRemoteTVMsg, this);
            if (string.IsNullOrEmpty(tv_msg))
            {
                return string.Empty;
            }
            msg.data = tv_msg;
            _updateStringsProperty(Properties.Settings.Default.WSRemoteTVMsg, tv_msg);
            return tv_msg;
        }

        string _lastSendKey = string.Empty;
        string setSendKeyMessage(E3WSMessage msg)
        {
            msg.msg = "sendkey";
            string key_msg = InputWindow.ShowInput("Enter KEY name", _lastSendKey, Properties.Settings.Default.LastSentRemoteCmds, this);
            if (string.IsNullOrEmpty(key_msg))
            {
                return string.Empty;
            }
            _lastSendKey = key_msg;
            _updateStringsProperty(Properties.Settings.Default.LastSentRemoteCmds, key_msg);
            msg.data = key_msg;
            return key_msg;
        }
        

        string setLogConfigMessage(E3WSMessage msg)
        {
            msg.msg = "log";
            string log_config = _getDefaultStringsProperty(Properties.Settings.Default.WSRemoteLogConfigs, "{\"level\": 5, \"wslog\":\"nextgen.local:8887\"}");
            log_config = InputWindow.ShowInput("Enter Log Config JSON format", log_config, Properties.Settings.Default.WSRemoteLogConfigs, this);
            if (string.IsNullOrEmpty(log_config))
            {
                return string.Empty;
            }
            msg.data = log_config;
            _updateStringsProperty(Properties.Settings.Default.WSRemoteLogConfigs, log_config);
            return log_config;
        }

        string setAlertMessage(E3WSMessage msg)
        {
            msg.msg = "alert";
            string alerts = "[{\"startTime\": \"04/24/2016 22:00:00 -0700\", \"stopTime\": \"04/25/2016 15:00:00 -0700\",	\"messageTitle\": \"test js\", \"messageText\": \"test js performance\", \"displayAlertType\": \"Scrollable\"}]";
            alerts = _getDefaultStringsProperty(Properties.Settings.Default.WSRemoteAlerts, alerts);
            alerts = InputWindow.ShowInput("Enter Sitewide Alert data", alerts, Properties.Settings.Default.WSRemoteAlerts, this);
            if (string.IsNullOrEmpty(alerts))
            {
                return string.Empty;
            }
            msg.data = alerts;
            _updateStringsProperty(Properties.Settings.Default.WSRemoteAlerts, alerts);
            return alerts;
        }

        string setUrlxMessage(string cmd, E3WSMessage msg)
        {
            msg.msg = cmd;
            string url = _getDefaultStringsProperty(Properties.Settings.Default.WSRemoteBEUrls, "https://vpn-portal.allentek.net/e3");
            url = InputWindow.ShowInput("Check URL?", url, Properties.Settings.Default.WSRemoteBEUrls, this);
            if (string.IsNullOrEmpty(url))
            {
                return string.Empty;
            }
            msg.data = url;
            return url;
        }

        string setBeUrlMessage(string cmd, E3WSMessage msg)
        {
            msg.msg = cmd;
            string url = _getDefaultStringsProperty(Properties.Settings.Default.WSRemoteBEUrls, "https://vpn-portal.allentek.net/e3");
            url = InputWindow.ShowInput("Set Backend URL?", url, Properties.Settings.Default.WSRemoteBEUrls, this);
            if (string.IsNullOrEmpty(url))
            {
                return string.Empty;
            }
            msg.data = url;
            _updateStringsProperty(Properties.Settings.Default.WSRemoteBEUrls, url);
            return url;
        }

        string setUrlMessage(string cmd, E3WSMessage msg)
        {
            msg.msg = cmd;
            string url = _getDefaultStringsProperty(Properties.Settings.Default.WSRemoteUrls, "http://nextgen.local/e3");
            url = InputWindow.ShowInput("Load TV App to the following URL?", url, Properties.Settings.Default.WSRemoteUrls, this);
            if (string.IsNullOrEmpty(url))
            {
                return string.Empty;
            }
            msg.data = url;
            _updateStringsProperty(Properties.Settings.Default.WSRemoteUrls, url);
            return url;
        }

        string updateFirmwareMessage(E3WSMessage msg)
        {
            msg.msg = "update_firmware";
            string url = _getDefaultStringsProperty(Properties.Settings.Default.FirmwareUrls, "http://nextgen.local/procentric/sstz/firmware/NF693-TIZEN/T-KTMAKUCB_1020.5.json");
            url = InputWindow.ShowInput("Please enter json data location (url):", url, Properties.Settings.Default.FirmwareUrls, this);
            if (string.IsNullOrEmpty(url))
            {
                return string.Empty;
            }
            msg.data = url;
            _updateStringsProperty(Properties.Settings.Default.FirmwareUrls, url);
            return url;
        }

        void _updateStringsProperty(StringCollection col, string value)
        {
            if (null != col)
            {
                if (!col.Contains(value))
                {
                    col.Insert(0, value);
                }
                else
                {
                    int ind = 0;
                    for (int i= col.Count-1; i>=0; i--)
                    {
                        if (col[i] == value)
                        {
                            ind = i;
                            break;
                        }
                    }
                    if (ind > 0)
                    {
                        col.RemoveAt(ind);
                        col.Insert(0, value);
                    }
                }
                Properties.Settings.Default.Save();
            }
        }

        string _getDefaultStringsProperty(StringCollection col, string defValue)
        {
            if (null != col && col.Count > 0)
            {
                defValue = col[0];
            }
            return defValue;
        } 

        void saveScreenCaptureImage(string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            byte[] filebytes = Convert.FromBase64String(data.Substring("data:image/png;base64,".Length)); // remove data:image/png;base64,
            FileStream fs = new FileStream(@"c:\temp\screencapture.png",
                                           FileMode.CreateNew,
                                           FileAccess.Write,
                                           FileShare.None);
            fs.Write(filebytes, 0, filebytes.Length);
            fs.Close();
        }

        void _wsRemote_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            string msg = e.Message;
            AppendLogText(TxtConsole, msg + "\n");
            try
            {
                E3WSMessage m = E3WSMessage.FromJson(msg);
                if (null != m && !string.IsNullOrEmpty(m.source_id) && m.source_id != MY_WS_ID && null != m.data)
                {
                    if (m.msg == "status")
                    {
                        UpdateStbClientStatus(m.source_id.Replace("stb/", ""), m.data.ToString());
                    }
                    else if (m.msg == "vnc")
                    {
                        saveScreenCaptureImage(m.data.ToString());
                    }
                    else if (m.msg == "urlx")
                    {
                        AppendLogText(TxtConsole, m.data.ToString());
                    }
                }
                else
                {
                    E3WSClient c = E3WSClient.FromJson(msg);
                    if (null != c && null != c.id && null != c.type)
                    {
                        E3WSClient ci = _connectedClients.FirstOrDefault(x => x.id == c.id && x.type == c.type);
                        if (c.status == "connected")
                        {
                            if (null != ci) // this should never happen
                            {
                                ci.status = c.status;
                            }
                            else
                            {
                                _connectedClients.Add(c);
                            }
                        }
                        else if (c.status == "disconnected")
                        {
                            if (null != ci)
                            {
                                _connectedClients.Remove(ci);
                            }
                        }
                        UpdateStbClientStatus(c.id, c.status);
                    }
                }
            }
            catch (Exception ex)
            {
                AppendLogText(TxtConsole, "_wsRemote_MessageReceived(): Exception - " + ex.Message);
                RestartWebSocket();
            }
        }

        void RestartWebSocket()
        {
            CloseWebSocket();
            InitRemoteControlWebSocket();
        }

        void _wsRemote_Closed(object sender, EventArgs e)
        {
            AppendLogText(TxtConsole, "DISCONNECTED -- " + _server.Ip + "\n");
            _wsRemote.Dispose();
            _wsRemote = null;
        }

        void _wsRemote_Error(object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
        {
            AppendLogText(TxtConsole, "ERROR: " + e.Exception.Message + "\n");
            _wsRemote.Close();
        }

        void _wsRemote_Opened(object sender, EventArgs e)
        {
            AppendLogText(TxtConsole, "CONNECTED -- " + _server.Ip + "\n" + sender.ToString() + "\n");
        }

        void UpdateStbClientStatus(string id, string status)
        {
            foreach (Server s in App.gServers)
            {
                Stb stb = s.Stbs.FirstOrDefault(x => x.MacId == id);
                if (null != stb)
                {
                    stb.Status = status;
                    try
                    {
                        E3WSStatusData data = E3WSStatusData.FromJson(status);
                        if (null != data)
                        {
                            if (null != data.state)
                            {
                                stb.Status = data.state + " (" + data.power + ")";
                            }
                            else if (null != data.status)
                            {
                                stb.Status = data.status;
                            }

                            if (null != data.InstalledStb)
                            {
                                stb.InstalledStb = data.InstalledStb;
                            }

                            stb.Uptime = FormatUptime(data.uptime);
                            stb.Idletime = FormatIdletime(data.idletime);
                        }
                    }
                    catch(Exception ex) 
                    {
                        stb.Status = "<error> " + ex.Message;
                    }
                }
            }

            try
            {
                Dispatcher.Invoke(() =>
                {
                    this.LvStbs.Items.Refresh();
                });
            }
            catch (Exception)
            {
            }
        }

        string FormatUptime(int minutes)
        {
            string ret = "";
            if (minutes < 10) ret = "00 00:0" + minutes;
            else if (minutes < 60) ret = "00 00:" + minutes;
            else
            {
                int hours = minutes / 60;
                int new_minutes = minutes - 60 * hours;
                ret = "" + new_minutes;
                if (new_minutes < 10) ret = "0" + ret;
                if (hours < 10)
                {
                    ret = "00 0" + hours + ":" + ret;
                }
                else if (hours < 24)
                {
                    ret = "00 " + hours + ":" + ret;
                }
                else
                {
                    int days = minutes / (60 * 24);
                    int new_hours = hours - 24 * days;
                    ret = new_hours + ":" + ret;
                    if (new_hours < 10) ret = "0" + ret;
                    ret = days + " " + ret;
                    if (days < 10) ret = "0" + ret;
                }
            }
            return ret;
        }

        string FormatIdletime(long seconds)
        {
            string ret = "";
            if (seconds < 10) ret = "00 00:00:0" + seconds;
            else if (seconds < 60) ret = "00 00:00:" + seconds;
            else
            {
                int minutes = (int)(seconds / 60);
                long new_seconds = seconds - 60 * minutes;
                ret = FormatUptime(minutes) + ":";
                if (new_seconds < 10) ret += "0" + new_seconds;
                else if (new_seconds < 60) ret += new_seconds;
            }
            return ret;
        }
    }

    // Windows 8 or later only
    //public class WebSocketWrapper
    //{
    //    private const int ReceiveChunkSize = 1024;
    //    private const int SendChunkSize = 1024;

    //    private readonly ClientWebSocket _ws;
    //    private readonly Uri _uri;
    //    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    //    private readonly CancellationToken _cancellationToken;

    //    private Action<WebSocketWrapper> _onConnected;
    //    private Action<string, WebSocketWrapper> _onMessage;
    //    private Action<WebSocketWrapper> _onDisconnected;

    //    protected WebSocketWrapper(string uri)
    //    {
    //        _ws = new ClientWebSocket();
    //        _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
    //        _uri = new Uri(uri);
    //        _cancellationToken = _cancellationTokenSource.Token;
    //    }

    //    /// <summary>
    //    /// Creates a new instance.
    //    /// </summary>
    //    /// <param name="uri">The URI of the WebSocket server.</param>
    //    /// <returns></returns>
    //    public static WebSocketWrapper Create(string uri)
    //    {
    //        return new WebSocketWrapper(uri);
    //    }

    //    /// <summary>
    //    /// Connects to the WebSocket server.
    //    /// </summary>
    //    /// <returns></returns>
    //    public WebSocketWrapper Connect()
    //    {
    //        ConnectAsync();
    //        return this;
    //    }

    //    /// <summary>
    //    /// Set the Action to call when the connection has been established.
    //    /// </summary>
    //    /// <param name="onConnect">The Action to call.</param>
    //    /// <returns></returns>
    //    public WebSocketWrapper OnConnect(Action<WebSocketWrapper> onConnect)
    //    {
    //        _onConnected = onConnect;
    //        return this;
    //    }

    //    /// <summary>
    //    /// Set the Action to call when the connection has been terminated.
    //    /// </summary>
    //    /// <param name="onDisconnect">The Action to call</param>
    //    /// <returns></returns>
    //    public WebSocketWrapper OnDisconnect(Action<WebSocketWrapper> onDisconnect)
    //    {
    //        _onDisconnected = onDisconnect;
    //        return this;
    //    }

    //    /// <summary>
    //    /// Set the Action to call when a messages has been received.
    //    /// </summary>
    //    /// <param name="onMessage">The Action to call.</param>
    //    /// <returns></returns>
    //    public WebSocketWrapper OnMessage(Action<string, WebSocketWrapper> onMessage)
    //    {
    //        _onMessage = onMessage;
    //        return this;
    //    }

    //    /// <summary>
    //    /// Send a message to the WebSocket server.
    //    /// </summary>
    //    /// <param name="message">The message to send</param>
    //    public void SendMessage(string message)
    //    {
    //        SendMessageAsync(message);
    //    }

    //    private async void SendMessageAsync(string message)
    //    {
    //        if (_ws.State != WebSocketState.Open)
    //        {
    //            throw new Exception("Connection is not open.");
    //        }

    //        var messageBuffer = Encoding.UTF8.GetBytes(message);
    //        var messagesCount = (int)Math.Ceiling((double)messageBuffer.Length / SendChunkSize);

    //        for (var i = 0; i < messagesCount; i++)
    //        {
    //            var offset = (SendChunkSize * i);
    //            var count = SendChunkSize;
    //            var lastMessage = ((i + 1) == messagesCount);

    //            if ((count * (i + 1)) > messageBuffer.Length)
    //            {
    //                count = messageBuffer.Length - offset;
    //            }

    //            await _ws.SendAsync(new ArraySegment<byte>(messageBuffer, offset, count), WebSocketMessageType.Text, lastMessage, _cancellationToken);
    //        }
    //    }

    //    private async void ConnectAsync()
    //    {
    //        await _ws.ConnectAsync(_uri, _cancellationToken);
    //        CallOnConnected();
    //        StartListen();
    //    }

    //    private async void StartListen()
    //    {
    //        var buffer = new byte[ReceiveChunkSize];

    //        try
    //        {
    //            while (_ws.State == WebSocketState.Open)
    //            {
    //                var stringResult = new StringBuilder();


    //                WebSocketReceiveResult result;
    //                do
    //                {
    //                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationToken);

    //                    if (result.MessageType == WebSocketMessageType.Close)
    //                    {
    //                        await
    //                            _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
    //                        CallOnDisconnected();
    //                    }
    //                    else
    //                    {
    //                        var str = Encoding.UTF8.GetString(buffer, 0, result.Count);
    //                        stringResult.Append(str);
    //                    }

    //                } while (!result.EndOfMessage);

    //                CallOnMessage(stringResult);

    //            }
    //        }
    //        catch (Exception)
    //        {
    //            CallOnDisconnected();
    //        }
    //        finally
    //        {
    //            _ws.Dispose();
    //        }
    //    }

    //    private void CallOnMessage(StringBuilder stringResult)
    //    {
    //        if (_onMessage != null)
    //            RunInTask(() => _onMessage(stringResult.ToString(), this));
    //    }

    //    private void CallOnDisconnected()
    //    {
    //        if (_onDisconnected != null)
    //            RunInTask(() => _onDisconnected(this));
    //    }

    //    private void CallOnConnected()
    //    {
    //        if (_onConnected != null)
    //            RunInTask(() => _onConnected(this));
    //    }

    //    private static void RunInTask(Action action)
    //    {
    //        Task.Factory.StartNew(action);
    //    }
    //}
}
