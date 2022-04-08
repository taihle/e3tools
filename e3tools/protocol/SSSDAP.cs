// ------------------------------------------------------------------------------
// Copyright (c) 2016 - Allen Technologies www.allentek.com. All Rights Reserved.
// Author: Tai H. Le <tle@allentek.com>
// 
// Samsung TV - SDAP protocol
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
    public class SSSDAP
    {
        const string MSG_INFO = "<SDAP/1.0>SERVER 100 SYSTEM INFORMATION ACK=YES </SDAP/1.0>";
        const string MSG_TEST = "<SDAP/1.0>SERVER 100 SYSTEM TEST ACK=YES </SDAP/1.0>";
        const string MSG_NETWORK = "<SDAP/1.0>SERVER 100 SYSTEM NETWORK ACK=YES </SDAP/1.0>";
        const string MSG_STATUS_AP = "<SDAP/1.0>SERVER 100 SOFT_AP GET_STATUS ACK=YES </SDAP/1.0>";
        // const string MSG_DISK_SPACE = "<SDAP/1.0>SERVER 100 SYSTEM GET_DIR_SIZE DIR=directory_name ACK=YES </SDAP/1.0>";
        const string MSG_POWER_ON = "<SDAP/1.0>SERVER 100 SYSTEM POWERON </SDAP/1.0>";
        const string MSG_POWER_OFF = "<SDAP/1.0>SERVER 100 SYSTEM FULL_POWEROFF </SDAP/1.0>";
        const string MSG_REBOOT = "<SDAP/1.0>SERVER 100 REBOOT INSTANT </SDAP/1.0>";

        static Socket _udpSocket = null;

        public static void SendPowerOffMsg(string ip)
        {
            SendMsg(ip, SSSDAP.MSG_POWER_OFF);
        }

        public static void SendPowerOnMsg(string ip)
        {
            SendMsg(ip, SSSDAP.MSG_POWER_ON);
        }

        public static void SendRebootMsg(string ip)
        {
            SendMsg(ip, SSSDAP.MSG_REBOOT);
        }

        static void SendMsg(string ip, string msg)
        {
            if (null == _udpSocket)
            {
                _udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            }
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), 30002);
            byte[] data = Encoding.ASCII.GetBytes(msg);
            _udpSocket.SendTo(data, data.Length, SocketFlags.None, remoteEndPoint);
        }
    }
}
