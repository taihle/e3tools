// ------------------------------------------------------------------------------
// Copyright (c) 2015 - Allen Technologies Inc. allentek.com. All Rights Reserved.
// Author: Tai H. Le <tle@allentek.com>
// 
// STB/TV/Client Device registration dialog
// ------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Ati.VigoPC.WebServices.REST;

namespace e3tools
{
    /// <summary>
    /// Interaction logic for StbRegisterWindow.xaml
    /// </summary>
    public partial class StbRegisterWindow : Window
    {
        public StbRegisterWindow()
        {
            InitializeComponent();
            this.Stb = null;
        }

        public Stb Stb { get; set; }
        public WSInstalledSTB WSStb { get; set; }

        private async Task<WSInstalledSTB> LookupStb(Stb stb)
        {
            string macId = stb.MacId.ToLower();

            RSVigoStbClient stbClient = new RSVigoStbClient(Properties.Settings.Default.VigoServer);
            this.Title += " @ " + stbClient.VigoServer;

            stbClient.Switchport = macId;
            WSInstalledSTB stbi = await stbClient.CheckInAsync(stb.MacId, "E3.TOOLS");
            
            WSHospital h = CboHospitals.SelectedItem as WSHospital;
            WSNursingUnit n = CboWards.SelectedItem as WSNursingUnit;
            List<WSInstalledSTB> stbs;

            if (null == stbi)
            {                
                stbs = await App.gVigoUserClient.GetStbsByHospitalIdAsync(h.identity);
                stbi = stbs.FirstOrDefault(x => ((!string.IsNullOrEmpty(x.switchPort) && macId.Equals(x.switchPort)) || (!string.IsNullOrEmpty(x.macAddress) && macId.Equals(x.macAddress))));
            }

            if (null == stbi)
            {
                foreach (WSHospital hi in App.gVigoHospitals)
                {
                    if (hi.identity != h.identity)
                    {
                        stbs = await App.gVigoUserClient.GetStbsByHospitalIdAsync(hi.identity);
                        stbi = stbs.FirstOrDefault(x => ((!string.IsNullOrEmpty(x.switchPort) && macId.Equals(x.switchPort)) || (!string.IsNullOrEmpty(x.macAddress) && macId.Equals(x.macAddress))));
                        if (null != stbi) break;
                    }
                }
            }

            if (null != stbi)
            {
                h = App.gVigoHospitals.FirstOrDefault(x => x.identity == stbi.hospitalId || x.name == stbi.hospital);
                if (null != h)
                {
                    CboHospitals.SelectedItem = h;
                    n = h.nursingUnits.FirstOrDefault(x => x.name == stbi.nursingUnit);
                    CboWards.SelectedItem = n;
                }

                TxtStbRoom.Text = stbi.room;
                TxtStbBed.Text = stbi.bed;
                TxtSwitchPort.Text = stbi.switchPort;
                TxtMacId.Text = stbi.macAddress;
            }

            return stbi;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TxtStatus.Text = "Loading, please wait...";

            if (null == App.gVigoHospitals || App.gVigoHospitals.Count <= 0)
            {
                App.gVigoHospitals = await App.gVigoUserClient.GetHospitalsAsync();
            }

            CboHospitals.ItemsSource = App.gVigoHospitals;
            CboHospitals.Items.Refresh();
            WSHospital h = null;
            if (App.gVigoHospitals.Count > 0)
            {
                h = App.gVigoHospitals[0];
            }

            if (null != h)
            {
                CboHospitals.SelectedItem = h;
                PopulateWards(h);
            }

            List<WSSystemConfig> scs = await App.gVigoUserClient.GetSystemConfigurationsAsync();
            CboStbConfigs.ItemsSource = scs;
            if (scs.Count > 0) 
            {
                CboStbConfigs.SelectedItem = scs[0];
            }

            List<WSClientRelease> crs = await App.gVigoUserClient.GetClientReleasesAsync();
            CboClientReleases.ItemsSource = crs;
            if (crs.Count > 0)
            {
                CboClientReleases.SelectedItem = crs[0];
            }

            List<WSTvType> tvts = await App.gVigoUserClient.GetTvTypesAsync();
            CboTVTypes.ItemsSource = tvts;
            if (tvts.Count > 0)
            {
                CboTVTypes.SelectedItem = tvts[0];
            }

            this.WSStb = await LookupStb(this.Stb);
            if (null != this.WSStb)
            {
                WSSystemConfig sc = scs.FirstOrDefault(x => x.name == this.WSStb.systemConfiguration.name);
                if (null != sc)
                {
                    CboStbConfigs.SelectedItem = sc;
                }

                WSClientRelease cr = crs.FirstOrDefault(x => x.id == this.WSStb.clientRelease.id);
                if (null != sc)
                {
                    CboClientReleases.SelectedItem = cr;
                }

                WSTvType tvt = tvts.FirstOrDefault(x => x.identity.ToString() == this.WSStb.tvTypeCode);
                if (null != tvt)
                {
                    CboTVTypes.SelectedItem = tvt;
                }

                TxtStatus.Text = "Found: " + this.WSStb;
            }
            else
            {
                TxtSwitchPort.Text = this.Stb.MacId;
                TxtMacId.Text = this.Stb.MacId;
                TxtStatus.Text = "Not Found: " + this.Stb;
            }

            BtnOk.IsEnabled = true;
        }

        private async void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TxtStatus.Text = "Processing, please wait...";

                if (null == this.WSStb)
                {
                    this.WSStb = new WSInstalledSTB();
                }
                WSHospital h = CboHospitals.SelectedItem as WSHospital;
                WSNursingUnit n = CboWards.SelectedItem as WSNursingUnit;
                WSSystemConfig sc = CboStbConfigs.SelectedItem as WSSystemConfig;
                WSClientRelease cr = CboClientReleases.SelectedItem as WSClientRelease;
                WSTvType tvt = CboTVTypes.SelectedItem as WSTvType;

                this.WSStb.hospital = h.name;
                this.WSStb.hospitalId = h.identity;
                this.WSStb.nursingUnit = n.identity.ToString();
                this.WSStb.room = TxtStbRoom.Text;
                this.WSStb.bed = TxtStbBed.Text;
                this.WSStb.systemConfiguration = sc;
                this.WSStb.clientRelease = cr;
                this.WSStb.tvType = tvt.type;
                this.WSStb.tvTypeCode = tvt.identity.ToString();

                WSInstalledSTB ret = await App.gVigoUserClient.UpdateStbAsync(this.WSStb);

                if (null != ret)
                {
                    TxtStatus.Text = "Ok";
                }
                else
                {
                    TxtStatus.Text = "Error: " + App.gVigoUserClient.Error;
                }
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Error: " + ex.Message;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void CboHospitals_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PopulateWards(CboHospitals.SelectedItem as WSHospital);
        }

        void PopulateWards(WSHospital h)
        {
            CboWards.ItemsSource = null;
            if (null == h || null == h.nursingUnits) return;
            CboWards.ItemsSource = h.nursingUnits;
            if (h.nursingUnits.Count() > 0)
            {
                CboWards.SelectedItem = h.nursingUnits[0];
            }
        }

        Dictionary<long, List<WSInstalledSTB>> _stbsByHospitalId = new Dictionary<long, List<WSInstalledSTB>>();

        private async void BtnFindStbByIP_Click(object sender, RoutedEventArgs e)
        {
            string txt = InputWindow.ShowInput("Find STB by IP");
            if (null == txt || txt.Length <= 0)
            {
                return;
            }

            List<WSInstalledSTB> ret = new List<WSInstalledSTB>();

            foreach (WSHospital h in App.gVigoHospitals)
            {
                TxtStatus.Text = "Searching STB in " + h.name + "... " + ret.Count;

                List<WSInstalledSTB> stbs = null;
                if (_stbsByHospitalId.ContainsKey(h.identity))
                {
                    stbs = _stbsByHospitalId[h.identity];
                }
                else
                {
                    stbs = await App.gVigoUserClient.GetStbsByHospitalIdAsync(h.identity);
                    _stbsByHospitalId.Add(h.identity, stbs);
                }

                WSInstalledSTB stb = stbs.FirstOrDefault(x => !string.IsNullOrEmpty(x.ipAddress) && txt.Equals(x.ipAddress));
                if (null != stb) 
                {
                    ret.Add(stb);
                }
            }

            string msg = "Found " + ret.Count + " STB(s) for IP: " + txt;
            if (ret.Count > 0)
            {
                foreach (WSInstalledSTB stb in ret)
                {
                    msg += "\n" + stb.room + "/" + stb.bed + "\t" + stb.switchPort + "\t" + stb.hospital + "/" + stb.nursingUnit;
                }
            }
            MessageBox.Show(msg);
        }
    }
}
