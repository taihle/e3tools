// ------------------------------------------------------------------------------
// Copyright (c) 2021 - Allen Technologies Inc. allentek.com. All Rights Reserved.
// Author: Tai H. Le <tle@allentek.com>
// 
// Handle software update process
// ------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Windows;
using Ati.VigoPC.WebServices.REST;
using Newtonsoft.Json.Linq;


namespace e3tools
{
    public class Updater
    {
        class UpdateArgs
        {
            public Window OwnerWindow { get; set; }
            public bool Interactive { get; set; }
        }

        public static event RunWorkerCompletedEventHandler UpdateCompleted;

        public static void Check(Window ownerWindow = null, bool interactive = false)
        {
            BackgroundWorker bw = new BackgroundWorker();
            bw.DoWork += Bw_DoWork;
            bw.RunWorkerCompleted += Bw_RunWorkerCompleted;
            bw.RunWorkerAsync(new UpdateArgs() { OwnerWindow = ownerWindow, Interactive = interactive });
        }

        private static void Bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            UpdateCompleted?.Invoke(sender, e);
        }

        private static void Bw_DoWork(object sender, DoWorkEventArgs e)
        {
            UpdateArgs args = e.Argument as UpdateArgs;
            e.Result = CheckForSoftwareUpdate(args.OwnerWindow, args.Interactive);
        }

        public static string CheckForSoftwareUpdate(Window ownerWindow = null, bool interactive = false)
        {
            string updateUrl = Properties.Settings.Default.AppUpdateUrl;
            if (string.IsNullOrEmpty(updateUrl))
            {
                if (interactive)
                {
                    updateUrl = InputWindow.ShowInput("Enter URL", updateUrl, new StringCollection(), ownerWindow);
                }
            }

            if (string.IsNullOrEmpty(updateUrl))
            {
                return "Software Update Error: Invalid update URL! Please check/set in the Settings.";
            }

            string zipToolCmd = Properties.Settings.Default.ZipToolCmd;
            if (string.IsNullOrEmpty(zipToolCmd))
            {
                if (interactive)
                {
                    zipToolCmd = InputWindow.ShowInput("Enter Zip Tool Cmd", zipToolCmd, new StringCollection(), ownerWindow);
                }
            }

            if (string.IsNullOrEmpty(zipToolCmd))
            {
                return "Software Update Error: Invalid ZipToolCmd! Please check/set in the Settings.";
            }

            return CheckForSoftwareUpdateX(updateUrl, zipToolCmd, interactive);
        }

        public static string DownloadAndUpdate(WebClient webClient, string zipFileName, string updateUrl, string zipToolCmd, bool interactive = false)
        {
            string ret = "";
            try
            {
                string zipFileFromServer = updateUrl + zipFileName;
                string sourcePath = AppDomain.CurrentDomain.BaseDirectory;
                string updatedZipFile = Path.Combine(sourcePath, zipFileName);

                webClient.DownloadFile(zipFileFromServer, updatedZipFile);

                if (File.Exists(updatedZipFile))
                {
                    Application.Current.Shutdown();
                    Process.Start(Path.Combine(sourcePath, "e3tools_update.bat"), zipFileName + " " + zipToolCmd);
                }
                else
                {
                    ret = "Error - Failed to download file " + zipFileName + "\nFrom" + zipFileFromServer;
                    if (interactive)
                    {
                        Helper.ShowErrorMessage(ret, "Software Update Error");
                    }
                }
            }
            catch (Exception ex)
            {
                ret = "Error Updating Software - " + ex.Message;
                if (interactive)
                {
                    Helper.ShowErrorMessage(ret, "Software Update Error");
                }
            }

            return ret;
        }

        public static string CheckForSoftwareUpdateX(string updateUrl, string zipToolCmd, bool interactive = false)
        {
            string ret = "";
            try
            {
                if (!updateUrl.EndsWith("/")) updateUrl += "/";

                using (var webClient = new System.Net.WebClient())
                {
                    var jsonString = webClient.DownloadString(updateUrl + "e3tools.json");
                    JObject updateVersion = JObject.Parse(jsonString);
                    if (null == updateVersion || null == updateVersion["version"] || null == updateVersion["zip"])
                    {
                        ret = "Error - Failed to pull update info.\nCheck settings for Software Update URL:\n" + updateUrl;
                        if (interactive)
                        {
                            Helper.ShowErrorMessage(ret, "Software Update Check Error");
                        }
                    }
                    else
                    {
                        string version = updateVersion["version"].ToString();
                        Assembly a = Assembly.GetExecutingAssembly();
                        string myVersion = FileVersionInfo.GetVersionInfo(a.Location).FileVersion;
                        Version newVersion = new Version(version);
                        Version currVersion = new Version(myVersion);
                        if (newVersion.CompareTo(currVersion) > 0)
                        {
                            if (MessageBox.Show("Update is available!\nProceed?", "Software Update", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                            {
                                string zipFileName = updateVersion["zip"].ToString();
                                ret = DownloadAndUpdate(webClient, zipFileName, updateUrl, zipToolCmd, interactive);
                            }
                        }
                        else if (interactive)
                        {
                            MessageBox.Show("Current version " + myVersion + " is the latest!", "Software Update");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ret = ex.Message + "\n\nCheck settings for Software Update URL:\n" + updateUrl;
                if (interactive)
                {
                    Helper.ShowErrorMessage(ret, "Software Update Check Error");
                }
            }

            return ret;
        }
    }
}
