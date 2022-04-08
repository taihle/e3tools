// ------------------------------------------------------------------------------
// Copyright (c) 2015 - Allen Technologies Inc. allentek.com. All Rights Reserved.
// Author: Tai H. Le <tle@allentek.com>
// 
// E3 tools to manage client STB/Devices
// ------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using Ati.VigoPC.WebServices.REST;

namespace e3tools
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static RSVigoUserClient gVigoUserClient = null;
        public static WSUser gVigoUser = null;
        public static List<WSHospital> gVigoHospitals = null;
        public static List<Server> gServers = new List<Server>();
        public static List<SortableGridViewColumn> gDefaultGridViewColumns = new List<SortableGridViewColumn>();

        App()
        {
            if (e3tools.Properties.Settings.Default.UpgradeRequired)
            {
                e3tools.Properties.Settings.Default.Upgrade();
                e3tools.Properties.Settings.Default.UpgradeRequired = false;
                e3tools.Properties.Settings.Default.Save();
            }

            LoadGridViewColumns();
        }

        public static void InitServersData()
        {
            try
            {
                string filename = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\e3tools.xml";
                string data = string.Empty;
                if (File.Exists(filename))
                {
                    data = File.ReadAllText(filename);
                }
                else
                {
                    data = e3tools.Properties.Settings.Default.Servers;
                    File.WriteAllText(filename, data);
                }
                gServers = Server.Load(data);
                gServers.Sort((x, y) => x.Name.CompareTo(y.Name));
            }
            catch (Exception ex)
            {
            }
        }

        public static void SaveServersData(string data)
        {
            try
            {
                string filename = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\e3tools.xml";
                File.WriteAllText(filename, data);
            }
            catch (Exception ex)
            {
                e3tools.Properties.Settings.Default.Servers = data;
                e3tools.Properties.Settings.Default.Save();
            }
        }

        void LoadGridViewColumns()
        {
            try
            {
                string s = e3tools.Properties.Settings.Default.GridViewColumns;
                if (string.IsNullOrEmpty(s))
                {
                    s = e3tools.Properties.Settings.Default.GridViewColumnsDefault;
                }
                LoadGridViewColumns(Newtonsoft.Json.Linq.JArray.Parse(s));
            }
            catch (Exception ex)
            {
                ResetGridViewColumns();
            }
        }

        static void LoadGridViewColumns(dynamic data)
        {
            gDefaultGridViewColumns.Clear();
            try
            {
                foreach (dynamic c in data)
                {
                    SortableGridViewColumn col = new SortableGridViewColumn(c.header.ToString(), c.sort.ToString());
                    if (null != c.visible && c.visible == false) col.IsVisible = false;
                    gDefaultGridViewColumns.Add(col);
                }
            }
            catch (Exception ex)
            {
                gDefaultGridViewColumns.Add(new SortableGridViewColumn("Room/Bed", "Name"));
            }
        }

        public static void ResetGridViewColumns()
        {            
            try
            {
                dynamic data = Newtonsoft.Json.Linq.JArray.Parse(e3tools.Properties.Settings.Default.GridViewColumnsDefault);
                LoadGridViewColumns(data);
            }
            catch (Exception ex)
            {
                gDefaultGridViewColumns.Add(new SortableGridViewColumn("Room/Bed", "Name"));
            }
        }

        public static void SaveGridViewColumns()
        {
            string s = "";
            foreach (SortableGridViewColumn i in App.gDefaultGridViewColumns.OrderBy(x => x.Index))
            {
                if (s.Length > 0) s += ",";
                s += "{\"header\":\"" + i.Header + "\", \"sort\":\"" + i.SortPropertyName + "\"";
                if (i.IsVisible == false)
                {
                    s += ",\"visible\":\"false\"";
                }
                s += "}";
            }
            s = "[" + s + "]";
            e3tools.Properties.Settings.Default.GridViewColumns = s;
            e3tools.Properties.Settings.Default.Save();
        }

        public static bool Login(Window o = null)
        {
            LoginWindow w = new LoginWindow();
            if (null != o) w.Owner = o;
            else w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return (true == w.ShowDialog());
        }

        private void Application_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Helper.ShowErrorMessage(e.Exception, "Application Error");
            this.Shutdown(-1);
        }
    }
}
