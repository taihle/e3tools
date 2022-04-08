// ------------------------------------------------------------------------------
// Copyright (c) 2016 - Allen Technologies www.allentek.com. All Rights Reserved.
// Author: Tai H. Le <tle@allentek.com>
// 
// LG TV - RMS protocol
// ------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace e3tools
{
    public class LGRMS
    {
        static string RMS_REQUEST_MSG = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><rms><request><requestid>1</requestid><method>{METHOD}</method><parameters>{PARAMETERS}</parameters></request></rms>";
        static string RMS_PARAM_REQUEST = "<parameter><name>request</name><value><simpleType>{REQUEST}</simpleType></value></parameter>";
        static string RMS_PARAM_LIST = "<parameter><name>parameter</name><value><complexType><list>{PARAM_LIST}</list></complexType></value></parameter>";
        static string RMS_PARAM_SEND_RESULT_HTTP = "<parameter><name>send_result</name><value><simpleType>http</simpleType></value></parameter>";
        
        public static ProgressChangedEventHandler StatusCallback;

        static string GetRmsMsg(string method, string request, string param_list)
        {
            string ret = RMS_REQUEST_MSG.Replace("{METHOD}", method);
            if (!string.IsNullOrEmpty(param_list))
            {
                param_list = RMS_PARAM_LIST.Replace("{PARAM_LIST}", param_list);
            }
            if (!string.IsNullOrEmpty(request))
            {
                request = RMS_PARAM_REQUEST.Replace("{REQUEST}", request);
            }
            ret = ret.Replace("{PARAMETERS}", request + param_list + RMS_PARAM_SEND_RESULT_HTTP);
            return ret;
        }

        public static string GetRmsMsg_StartLog(string host, int port, string path)
        {
            return GetRmsMsg("control_log", "log_initialize", "<item name=\"ip\">" + host + "</item> <item name=\"port\">" + port + "</item> <item name=\"direction_path\">" + path + "</item>");
        }

        public static string GetRmsMsg_SetLogLevel(int level)
        {
            return GetRmsMsg("control_log", "set_log_level", "<item name=\"log_level\">" + level + "</item>");
        }

        public static string GetRmsMsg_StopLog()
        {
            return GetRmsMsg("control_log", "log_finalize", "");
        }

        public static string GetRmsMsg_Reboot()
        {
            return GetRmsMsg("control_tv", "system_action", "<item name=\"action_mode\">reboot</item>");
        }

        public static string GetRmsMsg_PowerOff()
        {
            return GetRmsMsg("control_tv", "system_action", "<item name=\"action_mode\">power_off</item>");
        }

        public static string GetRmsMsg_SystemInfo()
        {
            return GetRmsMsg("request_diagnosis", "", "");
        }

        static void OpenSocketAndSendData(BackgroundWorker bw, LGRMSDataArg arg)
        {            
            arg.Result += "CONNECTING...";
            bw.ReportProgress(0, arg);
            TcpClient client = null;
            try
            {
                client = new TcpClient(arg.Host, 9001);

                // Get a stream object for reading and writing
                NetworkStream stream = client.GetStream();

                arg.Result += "\nCONNECTED: to " + client.Client.RemoteEndPoint;
                if (bw.WorkerReportsProgress)
                {
                    bw.ReportProgress(0, arg);
                }

                if (stream.CanWrite)
                {
                    arg.Result += "\nSENDING: " + arg.Data;
                    if (bw.WorkerReportsProgress)
                    {
                        bw.ReportProgress(0, arg);
                    }
                    Byte[] bdata = System.Text.Encoding.ASCII.GetBytes(arg.Data);
                    stream.Write(bdata, 0, bdata.Length);

                    arg.Result += "\nSENT: " + bdata.Length + " bytes...";
                    if (bw.WorkerReportsProgress)
                    {
                        bw.ReportProgress(0, arg);
                    }
                }

                if (stream.CanRead)
                {
                    try
                    {
                        byte[] bytes = new byte[client.ReceiveBufferSize];
                        int k;
                        string response = "";
                        while ((k = stream.Read(bytes, 0, bytes.Length)) != 0)
                        {
                            string sdata = System.Text.Encoding.ASCII.GetString(bytes, 0, k);
                            response += sdata;                        
                        }

                        arg.Result += "\nRECEIVED: " + response;
                        if (bw.WorkerReportsProgress)
                        {
                            bw.ReportProgress(0, arg);
                        }
                        
                    }
                    catch (Exception e1)
                    {
                        arg.Result += "\nEXCEPTION: " + e1.Message;
                        if (bw.WorkerReportsProgress)
                        {
                            bw.ReportProgress(0, arg);
                        }
                    }
                }

                stream.Close();
                client.Close();
            }
            catch (SocketException e)
            {
                arg.Result += "\nEXCEPTION: " + e.Message;
                if (bw.WorkerReportsProgress)
                {
                    bw.ReportProgress(0, arg);
                }
            }
            finally
            {
                // Stop listening for new clients.
                if (null != client) client.Close();
            }
        }

        static Socket _udpSocket = null;
        public static void SendPowerOnMsg(string ip)
        {
            LGRMS.SendRemoteMsg(ip, "power_on");
        }

        public static void SendRemoteMsg(string ip, string msg)
        {
            if (null == _udpSocket)
            {
                _udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            }
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), 9311);
            byte[] data = Encoding.ASCII.GetBytes(msg);
            _udpSocket.SendTo(data, data.Length, SocketFlags.None, remoteEndPoint);
        }

        public static void SendPowerOffMsg(string ip)
        {
            SendMsgAsync(ip, LGRMS.GetRmsMsg_PowerOff());
        }

        public static void SendRebootMsg(string ip)
        {
            SendMsgAsync(ip, LGRMS.GetRmsMsg_Reboot());
        }

        public static void SendMsgAsync(string ip, string msg, Action<string> callback = null)
        {
            BackgroundWorker bw = new BackgroundWorker();
            bw.WorkerSupportsCancellation = true;
            bw.WorkerReportsProgress = true;
            if (null != StatusCallback)
            {
                bw.WorkerReportsProgress = true;
                bw.ProgressChanged += StatusCallback; // bw_ProgressChanged;
            }
            bw.RunWorkerCompleted += bw_RunWorkerCompleted;
            bw.DoWork += bw_DoWork;
            LGRMSDataArg data = new LGRMSDataArg() { Host = ip, Data = msg, Callback = callback };
            bw.RunWorkerAsync(data);
        }

        static void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            LGRMSDataArg arg = (e.Result as LGRMSDataArg);
            if (null != arg && null != arg.Callback)
            {
                arg.Callback(arg.Result);
            }
        }

        static void bw_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            LGRMSDataArg arg = e.UserState as LGRMSDataArg;
            if (null != arg)
            {
            }
        }

        static void bw_DoWork(object sender, DoWorkEventArgs e)
        {
            LGRMSDataArg arg = e.Argument as LGRMSDataArg;
            OpenSocketAndSendData((sender as BackgroundWorker), arg);
            Thread.Sleep(1000); // wait a bit
            e.Result = arg;
        }
    }

    public class LGRMSDataArg
    {
        public string Host { get; set; }
        public string Data { get; set; }
        public string Result { get; set; }
        public Action<string> Callback { get; set; }        
    }
}
