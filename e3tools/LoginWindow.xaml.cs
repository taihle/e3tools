// ------------------------------------------------------------------------------
// Copyright (c) 2015 - Allen Technologies Inc. allentek.com. All Rights Reserved.
// Author: Tai H. Le <tle@allentek.com>
// 
// User Login Window/Dialog
// ------------------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using System.Windows;

namespace e3tools
{
    /// <summary>
    /// Interaction logic for LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        async Task<bool> VigoLogin(string username, string password)
        {
            if (null == App.gVigoUserClient)
            {
                App.gVigoUserClient = new RSVigoUserClient(Properties.Settings.Default.VigoServer);
            }

            if (null == App.gVigoUser)
            {
                App.gVigoUser = await App.gVigoUserClient.LoginAsync(username, password);
            }

            return (null != App.gVigoUser);
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            BtnLogin.IsEnabled = false;
            TxtStatus.Text = "Loggin in, please wait...";
            string username = TxtUsername.Text;
            string password = TxtPassword.Password;
            bool ret = await VigoLogin(username, password);
            if (ret)
            {
                if (ChkRememberMe.IsChecked.Value)
                {
                    Properties.Settings.Default.VigoUsername = username;
                    Properties.Settings.Default.VigoPassword = SimpleAES.Instance.Encrypt(password);
                }
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                BtnLogin.IsEnabled = true;
                TxtStatus.Text = "Error: " + App.gVigoUserClient.Error;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                this.DialogResult = false;
                this.Close();
            }
            catch (Exception ex) { }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TxtStatus.Text = "Server: " + Properties.Settings.Default.VigoServer;
            if (!string.IsNullOrEmpty(Properties.Settings.Default.VigoUsername))
            {
                TxtUsername.Text = Properties.Settings.Default.VigoUsername;
                TxtPassword.Password = SimpleAES.Instance.Decrypt(Properties.Settings.Default.VigoPassword);
            }
        }
    }
}
