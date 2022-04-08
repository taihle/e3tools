// ------------------------------------------------------------------------------
// Copyright (c) 2016 - Allen Technologies www.allentek.com. All Rights Reserved.
// Author: Tai H. Le <tle@allentek.com>
// 
// Helper classes
// ------------------------------------------------------------------------------
using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Collections.Specialized;
using Newtonsoft.Json;
using System.Diagnostics;

namespace e3tools
{
    public class Helper
    {
        public static string SaveFileDialog(string title, string ext)
        {
            return SaveFileDialog(title, ext, string.Empty, string.Empty);
        }

        public static string SaveFileDialog(string title, string ext, string defaultFile, string defaultDir)
        {
            string ret = string.Empty;
            try
            {
                SaveFileDialog ofd = new SaveFileDialog();
                ofd.Filter = ext;
                ofd.Title = title;
                ofd.FileName = defaultFile;

                if (Directory.Exists(defaultDir))
                {
                    ofd.InitialDirectory = defaultDir;
                }

                if (ofd.ShowDialog() == true)
                {
                    ret = ofd.FileName;
                }
            }
            catch (Exception ex)
            {
                Helper.ShowErrorMessage(ex);
            }
            return ret;
        }

        public static string OpenFileDialog(string title, string ext)
        {
            return OpenFileDialog(title, ext, string.Empty, string.Empty);
        }

        public static string OpenFileDialog(string title, string ext, string defaultFile, string defaultDir = "")
        {
            string ret = string.Empty;
            try
            {
                OpenFileDialog ofd = new OpenFileDialog();
                ofd.Filter = ext;
                ofd.Title = title;

                if (File.Exists(defaultFile))
                {
                    ofd.FileName = defaultFile;
                }

                if (Directory.Exists(defaultDir))
                {
                    ofd.InitialDirectory = defaultDir;
                }

                if (ofd.ShowDialog() == true)
                {
                    ret = ofd.FileName;
                }
            }
            catch (Exception ex)
            {
                Helper.ShowErrorMessage(ex);
            }
            return ret;
        }

        public static string OpenFolderDialog(string title, string currentDirectory, Window owner)
        {
            var dlg = new CommonOpenFileDialog();
            dlg.Title = title;
            dlg.IsFolderPicker = true;
            dlg.InitialDirectory = currentDirectory;

            dlg.AddToMostRecentlyUsedList = false;
            dlg.AllowNonFileSystemItems = false;
            dlg.DefaultDirectory = currentDirectory;
            dlg.EnsureFileExists = true;
            dlg.EnsurePathExists = true;
            dlg.EnsureReadOnly = false;
            dlg.EnsureValidNames = true;
            dlg.Multiselect = false;
            dlg.ShowPlacesList = true;

            if (dlg.ShowDialog(owner) == CommonFileDialogResult.Ok)
            {
                return dlg.FileName;
            }

            return string.Empty;
        }

        public static string Utf8ToBase64(string utf8)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(utf8);
            return Convert.ToBase64String(bytes);
        }

        public static string Base64ToUtf8(string base64)
        {
            byte[] bytes = Convert.FromBase64String(base64);
            return Encoding.UTF8.GetString(bytes);
        }

        public static void ClearFolder(string FolderName)
        {
            DirectoryInfo dir = new DirectoryInfo(FolderName);

            foreach (FileInfo fi in dir.GetFiles())
            {
                fi.Delete();
            }

            foreach (DirectoryInfo di in dir.GetDirectories())
            {
                Helper.ClearFolder(di.FullName);
                di.Delete();
            }
        }

        public static int CopyFolder(string src, string dst)
        {
            int ret = -1;
            if (!Directory.Exists(src)) return ret;
            if (!Directory.Exists(dst))
            {
                Directory.CreateDirectory(dst);
            }            
            ClearFolder(dst);

            ret = 0;
            DirectoryInfo srcDir = new DirectoryInfo(src);
            foreach (FileInfo fi in srcDir.GetFiles())
            {
                fi.CopyTo(System.IO.Path.Combine(dst, fi.Name), true);
                ret++;
            }

            foreach (DirectoryInfo di in srcDir.GetDirectories())
            {
                ret += Helper.CopyFolder(di.FullName, System.IO.Path.Combine(dst, di.Name));
            }
            return ret;
        }

        public async static Task<int> CopyFolderAsync(string src, string dst, bool clearDst = true)
        {
            int ret = -1;
            if (!Directory.Exists(src)) return ret;
            if (!Directory.Exists(dst))
            {
                Directory.CreateDirectory(dst);
            }
            if (clearDst)
            {
                ClearFolder(dst);
            }

            ret = 0;
            DirectoryInfo srcDir = new DirectoryInfo(src);
            foreach (FileInfo fi in srcDir.GetFiles())
            {
                using (FileStream SourceStream = File.Open(fi.FullName, FileMode.Open))
                {
                    using (FileStream DestinationStream = File.Create(System.IO.Path.Combine(dst, fi.Name)))
                    {
                        await SourceStream.CopyToAsync(DestinationStream);
                    }
                }
                ret++;
            }

            foreach (DirectoryInfo di in srcDir.GetDirectories())
            {
                ret += await Helper.CopyFolderAsync(di.FullName, System.IO.Path.Combine(dst, di.Name), clearDst);
            }
            return ret;
        }

        public static void ShowErrorMessage(Exception ex, string title = "Error")
        {
            ShowErrorMessage(ex.Message, title);
        }

        public static void ShowErrorMessage(string msg, string title = "Error")
        {
            MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static string ExecuteZipTool(string args, string workingDir = "", string zipCmd = "")
        {
            string result = "";
            if (string.IsNullOrEmpty(zipCmd))
            {
                zipCmd = Properties.Settings.Default.ZipToolCmd;
            }

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                if (!string.IsNullOrEmpty(workingDir))
                {
                    psi.WorkingDirectory = workingDir;
                }
                psi.Arguments = args;
                psi.FileName = zipCmd;

                Process p = new Process();
                p.StartInfo = psi;
                p.Start();
                string output = p.StandardOutput.ReadToEnd();
                string error = p.StandardError.ReadToEnd();
                p.WaitForExit();

                result = output;

                if (!string.IsNullOrEmpty(error))
                {
                    result += "\n" + error;
                }
            }
            catch (Exception ex)
            {
                result += "\nException - " + ex.Message;
                if (!File.Exists(zipCmd))
                {
                    result += "\nPlease install or locate zip tool: " + zipCmd;
                }
            }

            return result;
        }
    }
}
