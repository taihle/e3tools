// ------------------------------------------------------------------------------
// Copyright (c) 2016 - Allen Technologies www.allentek.com. All Rights Reserved.
// Author: Tai H. Le <tle@allentek.com>
// 
// TV/Stb classes
// ------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;
using Ati.VigoPC.WebServices.REST;
using Renci.SshNet;
using System.Net;
using Newtonsoft.Json;
using System.Reflection;

namespace e3tools
{
    public class HostBase
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public string Ip { get; set; }
        public LogBase Logger { get; set; }

        public HostBase()
        {
            //this.Id  = 0;
            //this.Name = "";
            //this.Ip = "";
            //this.Logger = null;
        }

        public override string ToString()
        {
            return this.Name + "/" + this.Ip;
        }

        public virtual string GetLogFileName(DateTime dt)
        {
            return string.Empty;
        }

        public virtual string GetLogFilePath(DateTime dt)
        {
            return string.Empty;
        }

        public void Update(HostBase h)
        {
            this.Name = h.Name;
            this.Ip = h.Ip;
            this.Id = h.Id;
        }

        public XmlElement ToXml(XmlDocument xd, string name)
        {
            XmlElement xe = xd.CreateElement(name);
            xe.SetAttribute("id", this.Id.ToString());
            xe.SetAttribute("name", this.Name);
            xe.SetAttribute("ip", this.Ip);
            return xe;
        }

        public HostBase FromXml(XmlElement x)
        {
            try
            {
                this.Name = x.Attributes["name"].Value;
                if (x.HasAttribute("id")) this.Id = Convert.ToInt64(x.Attributes["id"].Value);
                if (x.HasAttribute("ip")) this.Ip = x.Attributes["ip"].Value;
                return this;
            }
            catch
            {
            }
            return null;
        }
    }

    public class Stb : HostBase
    {
        public const string TVTYPE_LG = "lg";
        public const string TVTYPE_SAMSUNG = "ss";

        public string MacId { get; set; }
        public string Type { get; set; }
        public string Notes { get; set; }
        public Server Server { get; set; }
        public string Status { get; set; }
        public string Uptime { get; set; }
        public string Idletime { get; set; }
        public int HighLightedBrushIndex { get; set; }

        private WSInstalledSTB _stb = null;
        [ExpandableObject]
        public WSInstalledSTB InstalledStb 
        { 
            get { return _stb; } 
            set {
                if (null == value) return;
                if (null == this._stb) this._stb = value;
                else { updateStbProperties(value); }
                this._stb.ParseHWInfoFromOldVersion();
            }
        }

        void updateStbProperties(WSInstalledSTB stb) 
        {
            try {
                PropertyInfo[] propInfos = stb.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (PropertyInfo pi in propInfos)
                {
                    object v = pi.GetValue(stb);
                    if (null != v)
                    {
                        if (pi.Name == "lastBeat")
                        {
                            if (v.ToString() != "1/1/0001 12:00:00 AM")
                            {
                                pi.SetValue(this._stb, v);
                            }
                        }
                        else
                        {
                            pi.SetValue(this._stb, v);
                        }
                    }
                }
            }
            catch(Exception ex)
            {
            }
        }

        public Stb(long id = 0, WSInstalledSTB istb = null)
        {
            this.Type = TVTYPE_LG;
            this.Id = id;
            this.Status = string.Empty;
            this.HighLightedBrushIndex = -1;
            this.InstalledStb = istb;
        }

        public override string GetLogFileName(DateTime dt)
        {
            return this.MacId.Trim().Replace(":", "") + "_" + dt.ToString("yyyy-MM-dd") + ".log";
        }

        public override string GetLogFilePath(DateTime dt)
        {
            // return "/var/www/html/procentric/log/logs/" + this.GetLogFileName(dt);
            return this.Server.LogsPath + this.GetLogFileName(dt);
        }

        public void PowerOff()
        {
            if (this.Type == Stb.TVTYPE_LG)
            {
                LGRMS.SendPowerOffMsg(this.Ip);
            }
            else if (this.Type == Stb.TVTYPE_SAMSUNG)
            {
                SSSDAP.SendPowerOffMsg(this.Ip);
            }
        }

        public void PowerOn()
        {
            if (this.Type == Stb.TVTYPE_SAMSUNG)
            {
                SSSDAP.SendPowerOnMsg(this.Ip);
            }
            else
            {
                LGRMS.SendPowerOnMsg(this.Ip);
            }
        }

        public void Reboot()
        {
            if (this.Type == Stb.TVTYPE_LG)
            {
                LGRMS.SendRebootMsg(this.Ip);
            }
            else if (this.Type == Stb.TVTYPE_SAMSUNG)
            {
                SSSDAP.SendRebootMsg(this.Ip);
            }
        }

        public void DisableCPUPTCLog(Action<string> done = null)
        {
            if (this.Type == Stb.TVTYPE_LG)
            {
                LGRMS.SendMsgAsync(this.Ip, LGRMS.GetRmsMsg_StopLog(), done1 => {
                    if (null != done) done(done1);
                });
            }
        }

        public void EnableCPUPTCLog(Action<string> done = null)
        {
            if (this.Type == Stb.TVTYPE_LG)
            {
                LGRMS.SendMsgAsync(this.Ip, LGRMS.GetRmsMsg_StopLog(), done1 => {
                    if (done1.Contains("EXCEPTION:"))
                    {
                        if (null != done) done(done1);
                    }
                    else
                    {
                        LGRMS.SendMsgAsync(this.Ip, LGRMS.GetRmsMsg_StartLog(this.Ip, 23, "telnet"), done2 =>
                        {
                            if (done2.Contains("EXCEPTION:"))
                            {
                                if (null != done) done(done1 + "\n" + done2);
                            }
                            else
                            {
                                LGRMS.SendMsgAsync(this.Ip, LGRMS.GetRmsMsg_SetLogLevel(1), done3 =>
                                {
                                    if (null != done) done(done1 + "\n" + done2 + "\n" + done3);
                                });
                            }
                        });
                    }
                });
            }
        }

        void SendStartLogDone(object sender, RunWorkerCompletedEventArgs e)
        {
            
        }

        public override bool Equals(object obj)
        {
            Stb stb = obj as Stb;
            if (null == stb) return false;
            if (stb == this) return true;
            return ((null != stb.Name && stb.Name.Equals(this.Name, StringComparison.CurrentCultureIgnoreCase))
                && (null != stb.Ip && stb.Ip.Equals(this.Ip, StringComparison.CurrentCultureIgnoreCase))
                && (null != stb.MacId && stb.MacId.Equals(this.MacId, StringComparison.CurrentCultureIgnoreCase))
                && (null != stb.Type && stb.Type.Equals(this.Type, StringComparison.CurrentCultureIgnoreCase)));
        }

        public Stb Copy()
        {
            return new Stb() { Name = this.Name, Ip = this.Ip, MacId = this.MacId, Type = this.Type, Notes = this.Notes };
        }

        public void Update(Stb stb)
        {
            base.Update(stb);
            this.MacId = stb.MacId;
            this.Type = stb.Type;
            this.Notes = stb.Notes;
        }

        public override string ToString()
        {
            return this.Name + "/" + this.MacId + "/" + this.Ip;
        }

        public XmlElement ToXml(XmlDocument xd)
        {
            XmlElement x = base.ToXml(xd, "stb");
            x.SetAttribute("macid", this.MacId);
            x.SetAttribute("type", this.Type);
            x.SetAttribute("status", this.Status);            
            x.SetAttribute("notes", this.Notes);
            x.SetAttribute("hiliColor", this.HighLightedBrushIndex.ToString());
            return x;
        }

        public Stb FromXml(XmlElement x)
        {
            try
            {
                if (null != base.FromXml(x))
                {
                    if (x.HasAttribute("macid")) this.MacId = x.Attributes["macid"].Value;
                    if (x.HasAttribute("type")) this.Type = x.Attributes["type"].Value;
                    if (x.HasAttribute("status")) this.Notes = x.Attributes["status"].Value;
                    if (x.HasAttribute("notes")) this.Notes = x.Attributes["notes"].Value;
                    if (x.HasAttribute("hiliColor")) this.HighLightedBrushIndex = Convert.ToInt32(x.Attributes["hiliColor"].Value);
                    return this;
                }
            }
            catch
            {
            }
            return null;
        }

        public static List<Stb> FromXmls(XmlNode x)
        {
            List<Stb> stbs = new List<Stb>();
            if (null != x)
            {
                foreach (XmlElement xe in x.ChildNodes)
                {
                    Stb stb = new Stb();
                    if (stb.FromXml(xe) != null)
                    {
                        stbs.Add(stb);
                    }
                }
            }
            return stbs;
        }

        public static List<Stb> Load(string s)
        {
            List<Stb> stbs = new List<Stb>();
            try
            {
                XmlDocument xd = new XmlDocument();
                xd.LoadXml(s);
                stbs = FromXmls(xd.DocumentElement);
            }
            catch
            {
            }
            return stbs;
        }

        public static XmlNode ToXml(List<Stb> stbs, XmlDocument xd)
        {
            XmlElement root = xd.CreateElement("stbs");
            foreach (Stb stb in stbs)
            {
                root.AppendChild(stb.ToXml(xd));
            }
            return root;
        }

        public static string Save(List<Stb> stbs)
        {
            XmlDocument xd = new XmlDocument();
            XmlNode root = ToXml(stbs, xd);
            xd.AppendChild(root);
            StringBuilder sb = new StringBuilder();
            xd.Save(new StringWriter(sb));
            return sb.ToString();
        }
    }

    public class Server : HostBase
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public WSHospital Hospital { get; set; }
        public bool E3DevicesOnly { get; set; }
        public string LogsPath { get; set; }
        public List<Stb> Stbs { get; private set; }
        public string IconUrl { get; set; }
        public string Versions { get; set; }
        public string Procentric { get; set; }
        public string Offlines { get; set; }
        public string WSHost { get; set; }
        public int WSPort { get; set; }

        public Server()
        {
            this.E3DevicesOnly = true;
            this.Stbs = new List<Stb>();
            this.LogsPath = "/opt/e3/e3ws/logs/";
            this.WSHost = "";
            this.WSPort = 8889;

            this.Name = "";
            this.Ip = "";
            this.Username = "";
            this.Password = "";
            this.Id = 0;
        }

        public Server(WSHospital h) : this()
        {
            this.Hospital = h;
            this.Name = h.name;
            this.Ip = "";
            this.Username = "";
            this.Password = "";
            this.Id = h.identity;
        }

        public Server(string name, string host, string user, string pass, long id = 0)
            : this()
        {
            this.Name = name;
            this.Ip = host;
            this.Username = user;
            this.Password = pass;
            this.Id = id;
        }

        public void SortStbs()
        {
            this.Stbs = this.Stbs.OrderBy(x => x.Name).ToList();
        }

        public override string GetLogFileName(DateTime dt)
        {
            return "access_log";
        }

        public override string GetLogFilePath(DateTime dt)
        {
            return "/var/log/httpd/" + this.GetLogFileName(dt);
        }

        public override bool Equals(object obj)
        {
            Server s = obj as Server;
            if (null == s) return false;
            if (s == this) return true;
            try
            {
                return (s.Name.Equals(this.Name, StringComparison.CurrentCultureIgnoreCase)
                    && s.Ip.Equals(this.Ip, StringComparison.CurrentCultureIgnoreCase)
                    && s.Username.Equals(this.Username)
                    && s.Password.Equals(this.Password));
            }
            catch { return false; }
        }

        public Server Copy()
        {
            return new Server(this.Name, this.Ip, this.Username, this.Password);
        }

        public void Update(Server s)
        {
            base.Update(s);
            this.Username = s.Username;
            this.Password = s.Password;
            this.E3DevicesOnly = s.E3DevicesOnly;
            this.LogsPath = s.LogsPath;
            this.Versions = s.Versions;
            this.Offlines = s.Offlines;
            this.WSHost = s.WSHost;
            this.WSPort = s.WSPort;
        }

        public XmlElement ToXml(XmlDocument xd)
        {
            XmlElement xe = base.ToXml(xd, "server");
            xe.SetAttribute("username", this.Username);
            xe.SetAttribute("password", SimpleAES.Instance.Encrypt(this.Password, this.Name));
            xe.SetAttribute("e3only", this.E3DevicesOnly.ToString());
            xe.SetAttribute("logspath", this.LogsPath);
            xe.SetAttribute("iconurl", this.IconUrl);
            xe.SetAttribute("versions", this.Versions);
            xe.SetAttribute("offlines", this.Offlines);
            xe.SetAttribute("wshost", this.WSHost);
            xe.SetAttribute("wsport", this.WSPort.ToString());
            this.SortStbs();
            xe.AppendChild(Stb.ToXml(this.Stbs, xd));
            return xe;
        }

        public Server FromXml(XmlElement x)
        {
            try
            {
                if (null != base.FromXml(x))
                {
                    this.Username = x.Attributes["username"].Value;
                    this.Password = SimpleAES.Instance.Decrypt(x.Attributes["password"].Value, this.Name);
                    if (x.Attributes["e3only"] != null)
                    {
                        this.E3DevicesOnly = Convert.ToBoolean(x.Attributes["e3only"].Value);
                    }
                    if (x.Attributes["logspath"] != null)
                    {
                        this.LogsPath = x.Attributes["logspath"].Value;
                    }
                    if (x.Attributes["iconurl"] != null)
                    {
                        this.IconUrl = x.Attributes["iconurl"].Value;
                    }
                    if (x.Attributes["versions"] != null)
                    {
                        this.Versions = x.Attributes["versions"].Value;
                    }
                    if (x.Attributes["offlines"] != null)
                    {
                        this.Offlines = x.Attributes["offlines"].Value;
                    }
                    if (x.Attributes["wshost"] != null)
                    {
                        this.WSHost = x.Attributes["wshost"].Value;
                    }

                    if (x.Attributes["wsport"] != null)
                    {
                        this.WSPort = Convert.ToInt32(x.Attributes["wsport"].Value);
                    }

                    this.Stbs = Stb.FromXmls(x.FirstChild);
                    foreach (Stb stb in this.Stbs)
                    {
                        stb.Server = this;
                    }
                    return this;
                }
            }
            catch(Exception ex)
            {
                // TODO
            }
            return null;
        }

        public static List<Server> FromXmls(XmlNode x)
        {
            List<Server> servers = new List<Server>();
            foreach (XmlElement xe in x.ChildNodes)
            {
                Server server = new Server();
                if (server.FromXml(xe) != null)
                {
                    servers.Add(server);
                }
            }
            return servers;
        }

        public static List<Server> Load(string s)
        {
            List<Server> servers = new List<Server>();
            try
            {
                XmlDocument xd = new XmlDocument();
                xd.LoadXml(s);
                servers = FromXmls(xd.DocumentElement);
            }
            catch
            {
            }
            return servers;
        }

        public static XmlNode ToXml(List<Server> servers, XmlDocument xd)
        {
            XmlElement root = xd.CreateElement("servers");
            foreach (Server server in servers)
            {
                root.AppendChild(server.ToXml(xd));
            }
            return root;
        }

        public static string Save(List<Server> servers)
        {
            XmlDocument xd = new XmlDocument();
            XmlNode root = ToXml(servers, xd);
            xd.AppendChild(root);
            StringBuilder sb = new StringBuilder();
            xd.Save(new StringWriter(sb));
            return sb.ToString();
        }

        public async Task<List<Stb>> RefreshStbs()
        {
            if (null == App.gVigoHospitals || App.gVigoHospitals.Count <= 0)
            {
                App.gVigoHospitals = await App.gVigoUserClient.GetHospitalsAsync();
            }

            if (null == this.Hospital)
            {
                this.Hospital = App.gVigoHospitals.FirstOrDefault(x => x.identity == this.Id);
            }

            List<Stb> prevStbs = new List<Stb>();
            prevStbs.AddRange(this.Stbs);

            this.Stbs.Clear();

            if (null == this.Hospital) return this.Stbs;

            List<WSInstalledSTB> stbs = await App.gVigoUserClient.GetStbsByHospitalIdAsync(this.Hospital.identity);
            IEnumerable<WSInstalledSTB> stbsx = stbs;
            if (this.E3DevicesOnly)
            {
                stbsx = stbs.Where(x => x.macAddress == x.switchPort);
            }

            foreach (WSInstalledSTB istb in stbsx)
            {
                Stb stb = new Stb(istb.identity, istb) {Name = istb.room + "/" + istb.bed, Ip = istb.ipAddress, MacId = istb.macAddress, Server = this };
                stb.Type = Stb.TVTYPE_LG;
                if (!string.IsNullOrEmpty(istb.clientVersion)) 
                {
                    //if (istb.clientVersion.Contains("LY770") || istb.clientVersion.Contains("LX770") || istb.clientVersion.Contains("STB-3000")) 
                    //{
                    //    stb.Type = Stb.TVTYPE_LG;
                    //}
                    //else 
                    if (istb.clientVersion.Contains("/HG") || istb.clientVersion.Contains("/T-M"))
                    {
                        stb.Type = Stb.TVTYPE_SAMSUNG;
                    }
                }

                Stb prevStb = prevStbs.FirstOrDefault(x => x.Id == stb.Id);
                if (null != prevStb)
                {
                    stb.HighLightedBrushIndex = prevStb.HighLightedBrushIndex;
                    stb.Logger = prevStb.Logger;
                    if (null != stb.Logger)
                    {
                        stb.Logger.Host = stb;
                    }
                }
                this.Stbs.Add(stb);
            }

            prevStbs.Clear();

            return this.Stbs;
        }

        private string ReadUrl(string url, string key = "")
        {
            try
            {
                WebRequest request = WebRequest.Create(url);
                request.Timeout = 10000;
                WebResponse response = request.GetResponse();
                using(StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string content = reader.ReadToEnd();
                    if (!string.IsNullOrEmpty(key))
                    {
                        dynamic results = JsonConvert.DeserializeObject<dynamic>(content);
                        return results[key].Value;
                    }
                    else
                    {
                        return content;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ReadUrl(): exception - " + ex.Message);
            }
            return "";
        }

        public Task<string> RefreshInfo()
        {
            var tcs = new TaskCompletionSource<string>();
            try
            {   
                if (string.IsNullOrEmpty(this.Ip))
                {
                    this.Versions = "ERR: No Host/IP";
                    tcs.TrySetResult("ERR: No Host/IP");
                }
                else
                {
                    string v = ReadUrl("http://" + this.Ip + "/e3/config/config.json", "version");
                    string v2 = ReadUrl("http://" + this.Ip + "/e3sstv/config/config.json", "version");
                    if (!string.IsNullOrEmpty(v2) && (v.IndexOf(v2) < 0))
                    {
                        if (!string.IsNullOrEmpty(v)) v += ",";
                        v += v2;
                    }
                    this.Versions = v;

                    v = ReadUrl("http://" + this.Ip + "/procentric/lgtv/e3zip.ver", "version");
                    v += "/" + ReadUrl("http://" + this.Ip + "/procentric/sstv/e3_ss_offline.ver");
                    v += "/" + ReadUrl("http://" + this.Ip + "/procentric/sstz/e3_sstz_offline.ver");

                    v += "/" + ReadUrl("http://" + this.Ip + "/e3offline/e3_lgtv_offline.ver", "version");
                    v += "/" + ReadUrl("http://" + this.Ip + "/e3offline/e3_ss_offline.ver");
                    v += "/" + ReadUrl("http://" + this.Ip + "/e3offline/e3_sstz_offline.ver");

                    this.Offlines = v;

                    tcs.TrySetResult("OK");
                }                
            }
            catch (Exception ex)
            {
                tcs.TrySetResult("ERR: " + ex.Message);
            }
            return tcs.Task;
        }

        public Task<string> TestFtpLoginTask(string ip = null, string username = null, string password = null)
        {
            var tcs = new TaskCompletionSource<string>();
            tcs.TrySetResult(this.TestFtpLogin(ip, username, password));
            return tcs.Task;
        }

        public string TestFtpLogin(string ip = null, string username = null, string password = null)
        {
            string ret = "OK";
            if (string.IsNullOrEmpty(username)) username = this.Username;
            if (string.IsNullOrEmpty(password)) password = this.Password;

            try
            {
                using (SftpClient sftp = new SftpClient(ip, username, password))
                {
                    sftp.Connect();
                    sftp.Disconnect();
                }
            }
            catch (Exception ex)
            {
                ret = "ERR: " + ex.Message;
            }

            return ret;
        }
    }

}
