// ------------------------------------------------------------------------------
// Copyright (c) 2015 - Allen Technologies Inc. allentek.com. All Rights Reserved.
// Author: Tai H. Le <tle@allentek.com>
// 
// Common Input Dialog WPF
// ------------------------------------------------------------------------------
using System.Collections.Specialized;
using System.Windows;

namespace e3tools
{
    /// <summary>
    /// Interaction logic for InputWindow.xaml
    /// </summary>
    public partial class InputWindow : Window
    {
        public InputWindow()
        {
            InitializeComponent();
        }

        public InputWindow(string input, StringCollection defaultInputs)
            : this()
        {
            this.Input = input;
            if (null != defaultInputs)
            {
                this.CboInput.ItemsSource = defaultInputs;
            }
        }

        public InputWindow(string input) : 
            this()
        {
            this.Input = input;
        }

        public string Input
        {
            get { return CboInput.Text; }
            set { CboInput.Text = value; }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        // return null if user cancel the operation
        public static string ShowInput(string title, string input = "", StringCollection defaultInputs = null, Window owner = null)
        {
            InputWindow w = new InputWindow(input, defaultInputs);
            w.Title = title;
            if (null != owner)
            {
                w.Owner = owner;
            }
            else
            {
                w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            if (w.ShowDialog() == true)
            {
                return w.Input;
            }
            else
            {
                return null;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CboInput.Focus();
        }
    }
}
