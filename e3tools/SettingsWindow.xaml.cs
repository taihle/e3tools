// ------------------------------------------------------------------------------
// Copyright (c) 2015 - Allen Technologies Inc. allentek.com. All Rights Reserved.
// Author: Tai H. Le <tle@allentek.com>
// 
// Settings Window/Dialog
// ------------------------------------------------------------------------------
using System.IO;
using System.Windows;

namespace e3tools
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            string s = CboBackendServers.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(s)) return;
            if (!Properties.Settings.Default.VigoServers.Contains(s))
            {
                Properties.Settings.Default.VigoServers.Add(s);
            }

            bool changed = (Properties.Settings.Default.VigoServer != s);

            string s1 = CboAppUpdateUrls.Text.Trim().ToLower();
            string s2 = CboZipTools.Text.Trim().ToLower();

            //string s2 = CboLogsPaths.Text.Trim();
            //if (string.IsNullOrEmpty(s2)) return;
            //if (!Properties.Settings.Default.LogsPaths.Contains(s2))
            //{
            //    Properties.Settings.Default.LogsPaths.Add(s2);
            //}

            //changed = changed || (Properties.Settings.Default.WSHostPort != s1) || (Properties.Settings.Default.LogsPath != s2);

            Properties.Settings.Default.AppUpdateUrl = s1;
            Properties.Settings.Default.ZipToolCmd = s2;
            Properties.Settings.Default.VigoServer = s;

            App.SaveGridViewColumns();

            this.DialogResult = changed;

            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LvViewColumns.ItemsSource = App.gDefaultGridViewColumns;

            CboBackendServers.ItemsSource = Properties.Settings.Default.VigoServers;
            CboBackendServers.Text = Properties.Settings.Default.VigoServer;

            CboAppUpdateUrls.Text = Properties.Settings.Default.AppUpdateUrl;
            CboZipTools.Text = Properties.Settings.Default.ZipToolCmd;

            // CboWSServers.ItemsSource = Properties.Settings.Default.WSHostPorts;
            // CboWSServers.Text = Properties.Settings.Default.WSHostPort;

            //CboLogsPaths.ItemsSource = Properties.Settings.Default.LogsPaths;
            //CboLogsPaths.Text = Properties.Settings.Default.LogsPath;
        }

        private void BtnAddViewColumn_Click(object sender, RoutedEventArgs e)
        {
            App.gDefaultGridViewColumns.Add(new SortableGridViewColumn("columnheader", "columnvalue"));
            LvViewColumns.Items.Refresh();
        }

        private void BtnEditViewColumn_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("TODO");
        }

        private void BtnDeleteViewColumn_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("TODO");
        }

        private void BtnMoveUpViewColumn_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("TODO");
        }

        private void BtnMoveDownViewColumn_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("TODO");
        }

        private void BtnResetViewColumn_Click(object sender, RoutedEventArgs e)
        {
            App.ResetGridViewColumns();
            LvViewColumns.Items.Refresh();
        }

        private void BtnCheckForUpdate_Click(object sender, RoutedEventArgs e)
        {
            string updateUrl = CboAppUpdateUrls.Text;
            if (string.IsNullOrEmpty(updateUrl))
            {
                Helper.ShowErrorMessage("Error: Please enter Software Update URL");
                CboAppUpdateUrls.Focus();
                return;
            }

            string zipToolCmd = CboZipTools.Text;
            if (!File.Exists(zipToolCmd))
            {
                Helper.ShowErrorMessage("Error: Please enter correct Zip Tool exe location!");
                CboZipTools.Focus();
                return;
            }

            Updater.CheckForSoftwareUpdateX(updateUrl, zipToolCmd, true);
        }
    }
}
