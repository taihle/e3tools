// ------------------------------------------------------------------------------
// Copyright (c) 2015 - Allen Technologies Inc. allentek.com. All Rights Reserved.
// Author: Tai H. Le <tle@allentek.com>
// 
// Window to create offline data
// ------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Ati.VigoPC.WebServices.REST;
using System.IO;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Newtonsoft.Json;
using System.Collections.Specialized;
using Newtonsoft.Json.Linq;

namespace e3tools
{
    /// <summary>
    /// Interaction logic for CreateZipLoaderWindow.xaml
    /// </summary>
    public partial class CreateZipLoaderWindow : Window
    {
        public OfflineZipDocument _document = new OfflineZipDocument();

        public CreateZipLoaderWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeData(Properties.Settings.Default.OfflineZipDocument);
        }

        private async void InitializeData(string json)
        { 
            try
            {
                _document.FromJsonStr(json);

                if (null == App.gVigoHospitals || App.gVigoHospitals.Count <= 0)
                {
                    App.gVigoHospitals = await App.gVigoUserClient.GetHospitalsAsync();
                }

                _document.UpdateHospitals(App.gVigoHospitals);
                CboHospitals.ItemsSource = _document.Hospitals;
                CboHospitals.Items.Refresh();

                SelectedHospital sh = _document.FindHospital(Properties.Settings.Default.ZipSelectedHospitalId);
                if (null != sh)
                {
                    CboHospitals.SelectedItem = sh;
                }

                if (!string.IsNullOrEmpty(Properties.Settings.Default.ZipOutputLocation))
                {
                    CboOutputLocation.Text = Properties.Settings.Default.ZipOutputLocation;
                }

                if (!string.IsNullOrEmpty(Properties.Settings.Default.ZipToolCmd))
                {
                    CboZipToolCmd.Text = Properties.Settings.Default.ZipToolCmd;
                }

                UpdateHospitalsTabHeaderCount();
            }
            catch(Exception ex)
            {
                if (null == App.gVigoHospitals || App.gVigoHospitals.Count <= 0)
                {
                    this.Close();
                    App.Login(this.Owner);
                }
                else
                {
                    Helper.ShowErrorMessage(ex);
                    this.Close();
                }
            }
        }

        void UpdateHospitalsTabHeaderCount()
        {
            TabHospitals.Header = "Hospitals (" + _document.Hospitals.FindAll(x => x.Selected).Count + "/" + _document.Hospitals.Count + ")";
        }

        void UpdatePlatformsTabHeaderCount()
        {
            SelectedHospital sh = CboHospitals.SelectedItem as SelectedHospital;
            if (null == sh) return;
            TabPlatforms.Header = "Platforms (" + sh.Platforms.FindAll(x => x.Selected).Count + "/" + sh.Platforms.Count + ")";
        }

        private void CboHospitals_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.TvChannelsList.ItemsSource = null;
            this.TvChannelsList.Items.Refresh();

            CboSelectedWards.ItemsSource = null;
            CboSelectedWards.Items.Refresh();

            TxtOfflineConfigEditor.Text = "";
            BtnVersion.Content = "";

            SelectedHospital sh = CboHospitals.SelectedItem as SelectedHospital;
            if (null == sh) return;

            WSHospital h = sh.wsHospital;
            if (null == h) return;

            string imageDir, contentDir;
            foreach (WSNursingUnit ni in h.nursingUnits)
            {
                string tvchannelsFile = getChannelsFilePath(h, ni, out contentDir, out imageDir);
                ni.channelListDownloaded = (File.Exists(tvchannelsFile));
            }

            CboSelectedWards.ItemsSource = h.nursingUnits;
            CboSelectedWards.Items.Refresh();

            if (CboSelectedWards.Items.Count > 0)
            {
                WSNursingUnit n = h.nursingUnits[0];
                foreach (WSNursingUnit ni in h.nursingUnits)
                {
                    if (ni.identity == sh.DefaultChannelWardId)
                    {
                        n = ni;
                        break;
                    }
                }
                CboSelectedWards.SelectedItem = n;
            }

            TxtOfflineConfigEditor.Text = sh.OfflineConfig;
            BtnVersion.Content = sh.Version;

            sh.UpdatePlatforms();
            CboPlatforms.ItemsSource = sh.Platforms;
            CboPlatforms.SelectedItem = sh.Platforms.Find(x => x.Selected);

            UpdatePlatformsTabHeaderCount();
        }

        bool _cancelGenerateProcess = false;
        private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (BtnGenerate.Content.ToString() == "Cancel")
                {
                    _cancelGenerateProcess = true;
                    return;
                }

                string msg = "";

                List<SelectedHospital> selectedHospitals = _document.Hospitals.FindAll(x => x.Selected);
                List<SelectedHospital> invalidVersions = selectedHospitals.FindAll(x => string.IsNullOrEmpty(x.Version));
                if (null != invalidVersions && invalidVersions.Count > 0)
                {
                    msg = "";
                    foreach (SelectedHospital sp in invalidVersions)
                    {
                        msg += "\n" + sp.wsHospital.name;
                    }
                    Helper.ShowErrorMessage("Please specify version for the following hospital(s):\n" + msg, "Generate Offline Zip Error");
                    return;
                }

                TxtGenerateStatus.Text = "";

                int total = _document.CalculateTotalGeneratedZips(ref msg);
                if (total <= 0)
                {
                    Helper.ShowErrorMessage("Please select a hospital and a platform to begin with.", "Generate Offline Zip Error");
                    return;
                }

                string outputLocation = CboOutputLocation.Text;
                if (string.IsNullOrEmpty(outputLocation))
                {
                    Helper.ShowErrorMessage("Please select an output location to begin with.", "Generate Offline Zip Error");
                    return;
                }

                msg = "You are about to create " + total + " offline zips for the following hospital(s):" + msg + "\n\nOut output to: " +  outputLocation + "\n\nContimue?";
                MessageBoxResult mbr = MessageBox.Show(msg, "Generate Offline Zip Data", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (mbr == MessageBoxResult.No) return;

                BtnGenerate.Content = "Cancel";
                PbTotalProgress.Value = 0;
                GrdProgress.Visibility = Visibility.Visible;

                string ret = string.Empty;

                if (!Directory.Exists(outputLocation))
                {
                    TxtProgress.Text = "Creating directory: " + outputLocation;
                    Directory.CreateDirectory(outputLocation);
                }

                int processing_count = 0;

                foreach (SelectedHospital sh in selectedHospitals)
                {
                    if (_cancelGenerateProcess) break;

                    List<SelectedPlatform> selectedPlatforms = sh.Platforms.FindAll(x => x.Selected);
                    foreach (SelectedPlatform sp in selectedPlatforms)
                    {
                        if (_cancelGenerateProcess) break;
                        
                        E3Platform p = sp.e3Platform;

                        processing_count += 1;
                        TxtProgress.Text = "Generating [" + p.Name + "] offline zip for [" + sh.wsHospital.name + "]:";

                        string sourcePath = Path.Combine(p.RootDir, "zipsrc");
                        if (!Directory.Exists(sourcePath))
                        {
                            Directory.CreateDirectory(sourcePath);
                            ret = Helper.ExecuteZipTool(" x -aoa -y -bb3 -o" + sourcePath + " " + p.ZipFile, sourcePath);
                            TxtGenerateStatus.Text += "\n" + ret;
                        }

                        // copy code/template to  
                        string outputLocationPlatform = Path.Combine(outputLocation, p.PrefixDir);
                        string outputLocationPlatformZipsrc = Path.Combine(outputLocationPlatform, "zipsrc");

                        TxtProgress.Text = "Copying src data to " + outputLocationPlatformZipsrc;
                        int copiedCount = await Helper.CopyFolderAsync(sourcePath, outputLocationPlatformZipsrc);
                        TxtGenerateStatus.Text += "\nCopied " + copiedCount + " files/dirs [" + sourcePath + "] --> [" + outputLocationPlatformZipsrc + "]";

                        TxtGenerateStatus.Text += "\n" + TxtProgress.Text;
                        string cacheDir = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "offlinedata", sh.Id.ToString(), "cache");
                        string tvchannelsFile = Path.Combine(cacheDir, "content", "tvchannels");
                        if (!File.Exists(tvchannelsFile))
                        {
                            TxtProgress.Text = "Downloading TV Channels Lineup for [" + sh.wsHospital.name + "]:";
                            TxtGenerateStatus.Text += "\n" + TxtProgress.Text;
                            await PopulateTvChannelsList(sh.wsHospital);
                        }

                        string version_name = sh.Version + "_" + p.ShortName + "_" + sh.wsHospital.abbreviation.ToUpper();

                        string zipFile = p.ZipName + "_v" + version_name + "_" + sh.Id + "_" + sh.wsHospital.name.Replace(" ", "-") + ".zip";
                        string zipsrcCacheDir = Path.Combine(outputLocationPlatformZipsrc, p.CacheDir);

                        TxtProgress.Text = "Creating zip data [" + zipFile + "]... " + processing_count + " of " + total;

                        copiedCount = await Helper.CopyFolderAsync(cacheDir, zipsrcCacheDir, false);
                        TxtGenerateStatus.Text += "\nCopied " + copiedCount + " files/dirs [" + cacheDir + "] --> [" + zipsrcCacheDir + "]";

                        // update offline config file
                        string cfg_data_str = "";
                        string offlineConfigFile = Path.Combine(outputLocationPlatformZipsrc, p.RelativePathConfigFile);
                        if (string.IsNullOrEmpty(cfg_data_str) && File.Exists(offlineConfigFile))
                        {
                            cfg_data_str = File.ReadAllText(offlineConfigFile);
                        }

                        cfg_data_str = UpdateOfflineConfigJsonText(cfg_data_str, sh, sp);

                        if (!string.IsNullOrEmpty(cfg_data_str))
                        {
                            File.WriteAllText(offlineConfigFile, cfg_data_str);
                            TxtGenerateStatus.Text += "\nUpdated offline config data:\n" + cfg_data_str;
                        }

                        // update offline version file
                        string versionFile = Path.Combine(outputLocationPlatformZipsrc, p.RelativePathVersionFile);
                        if (File.Exists(versionFile))
                        {
                            string version_data_str = File.ReadAllText(versionFile);
                            version_data_str = UpdateOfflineVersionText(version_data_str, sh, sp);

                            if (sp.Id == 1 && !string.IsNullOrEmpty(cfg_data_str)) // special LG version+config file
                            {
                                JObject version_data = JObject.Parse(version_data_str);
                                JObject cfg_data = JObject.Parse(cfg_data_str);
                                if (null != version_data && null != cfg_data)
                                {
                                    version_data.Merge(cfg_data, new JsonMergeSettings
                                    {
                                        MergeArrayHandling = MergeArrayHandling.Union
                                    });
                                    version_data_str = version_data.ToString();
                                }                        
                            }

                            File.WriteAllText(versionFile, version_data_str);
                            TxtGenerateStatus.Text += "\nUpdated offline version data:\n" + version_data_str;
                        }

                        zipFile = Path.Combine(outputLocationPlatform, zipFile);
                        TxtGenerateStatus.Text += "\n\n" + Helper.ExecuteZipTool("a -r \"" + zipFile + "\"", outputLocationPlatformZipsrc);

                        if (File.Exists(versionFile))
                        {
                            string version_data = File.ReadAllText(versionFile);
                            versionFile = zipFile.Replace(".zip", ".ver");
                            File.WriteAllText(versionFile, version_data);
                            TxtGenerateStatus.Text += "\nVersion:\n" + version_data;
                        }

                        PbTotalProgress.Value = (int)(processing_count * 100 / total);
                        TxtTotalProgress.Text = "Total Progress (" + PbTotalProgress.Value + "%):";
                    }
                }

                TxtProgress.Text = "Completed.";
                if (_cancelGenerateProcess)
                {
                    TxtProgress.Text += " Cancelled by user!";
                }
                TxtGenerateStatus.Text += "\n\n" + TxtProgress.Text;
            }
            catch (Exception ex)
            {
                TxtGenerateStatus.Text += "\nException: " + ex.Message;
            }

            _cancelGenerateProcess = false;
            BtnGenerate.Content = "Generate";
            GrdProgress.Visibility = Visibility.Collapsed;
        }

        void p_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            SetStatusText(e.Data);
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void BtnRefreshTvChannels_Click(object sender, RoutedEventArgs e)
        {
            SelectedHospital sh = CboHospitals.SelectedItem as SelectedHospital;
            if (null == sh) return;
            await PopulateTvChannelsList(sh.wsHospital, CboSelectedWards.SelectedItem as WSNursingUnit, true);
        }

        private string getChannelsFilePath(WSHospital h, WSNursingUnit n, out string contentDir, out string imageDir)
        {
            string tempCachetDir = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "offlinedata", h.identity.ToString(), "cache");
            if (!Directory.Exists(tempCachetDir))
            {
                Directory.CreateDirectory(tempCachetDir);
            }

            contentDir = Path.Combine(tempCachetDir, "content");
            if (!Directory.Exists(contentDir))
            {
                Directory.CreateDirectory(contentDir);
            }

            imageDir = Path.Combine(tempCachetDir, "image");
            if (!Directory.Exists(imageDir))
            {
                Directory.CreateDirectory(imageDir);
            }

            string tvchannels_file_name = "tvchannels";
            if (null != n)
            {
                tvchannels_file_name += "_" + n.identity.ToString();
            }

            string tvchannelsFile = Path.Combine(contentDir, tvchannels_file_name);

            return tvchannelsFile;
        }

        private async Task<bool> PopulateTvChannelsList(WSHospital h, WSNursingUnit n = null, bool downloadPrompt = false)
        {
            if (null == h) return false;

            List<WSChannel> chList = null;

            string imageDir, contentDir;
            string tvchannelsFile = getChannelsFilePath(h, n, out contentDir, out imageDir);

            MessageBoxResult ret = MessageBoxResult.None;

            if (File.Exists(tvchannelsFile))
            {
                if (downloadPrompt)
                {
                    ret = MessageBox.Show("Download from server?", "Refresh?", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                    if (ret == MessageBoxResult.Cancel)
                    {
                        return false;
                    }
                }
                else
                {
                    ret = MessageBoxResult.No;
                }
            }

            this.TvChannelsList.ItemsSource = null;
            this.TvChannelsList.Items.Refresh();

            if (ret == MessageBoxResult.No)
            {
                string data = File.ReadAllText(tvchannelsFile);
                string utf8 = Helper.Base64ToUtf8(data);
                chList = WSDataObjectHelper.ArrayFromContentData<WSChannel>(utf8);
            }
            else
            {
                this.GridStbInfo.IsEnabled = false;
                chList = await DownloadChannelsList(h, n, tvchannelsFile, imageDir, contentDir);
            }

            if (chList != null)
            {
                foreach (WSChannel ch in chList)
                {
                    ch.tvLogoUrl = Path.Combine(imageDir, ch.channelNumber.ToString());
                }
                this.TvChannelsList.ItemsSource = chList;
            }

            SetStatusText("Total: " + this.TvChannelsList.Items.Count, 1);
            this.GridStbInfo.IsEnabled = true;
            return true;
        }

        public async void SetStatusText(string txt, int delay = 0)
        {
            TxtStatus.Text = txt;
            if (delay > 0)
            {
                await Task.Delay(delay * 1000);
            }
        }

        private async Task<List<WSChannel>> DownloadChannelsList(WSHospital h, WSNursingUnit n, string tvchannelsFile, string imageDir, string contentDir)
        {

            List<WSChannel> chList = new List<WSChannel>();

            string msg = "Processing channels list for " + h.name;
            if (null != n)
            {
                msg += ", " + n.name;
            }

            UpdateDownloadChannelsListProgressStatusText(msg);
            RSVigoStbClient stbClient = new RSVigoStbClient(Properties.Settings.Default.VigoServer);
            WSInstalledSTB stb = await StbCheckIn(stbClient, h, n);
            if (null != stb)
            {
                if (stb.hospitalId != h.identity)
                {
                    UpdateDownloadChannelsListProgressStatusText("Error check-in stb account to this hospital...");
                    return chList;
                }

                UpdateDownloadChannelsListProgressStatusText("Downloading channels list...");
                chList = await stbClient.GetChannelLineupAsync();

                if (chList != null)
                {
                    File.WriteAllText(Path.Combine(contentDir, "stb.json"), JsonConvert.SerializeObject(stb, Formatting.Indented));
                    string data = stbClient.ResponseRawData;
                    File.WriteAllText(tvchannelsFile + ".json", data);
                    string binary_data = Helper.Utf8ToBase64(data);
                    File.WriteAllText(tvchannelsFile, binary_data);

                    string default_channels_file = h.identity.ToString();
                    if (null != n)
                    {
                        default_channels_file += "_" + n.identity.ToString();
                    }

                    if (tvchannelsFile.IndexOf(default_channels_file) >= 0)
                    {
                        default_channels_file = Path.Combine(contentDir, "tvchannels");
                        File.WriteAllText(default_channels_file + ".json", data);
                        File.WriteAllText(default_channels_file, binary_data);
                    }

                    int i = 0;
                    foreach (WSChannel ch in chList)
                    {
                        i++;
                        if (!string.IsNullOrEmpty(ch.tvLogo))
                        {
                            UpdateDownloadChannelsListProgressStatusText((i * 100 / chList.Count) + "%, downloading TV logo for [" + ch.channelNumber + " - " + ch.alias + "] ...");
                            WSBinaryContent imageContent = await stbClient.DownloadContentByNameAsync(ch.tvLogo);
                            if (null != imageContent && imageContent.objectType == "ImageContent" && null != imageContent.binaryContent)
                            {
                                byte[] bytes = System.Convert.FromBase64String(imageContent.binaryContent);
                                string imageFile = Path.Combine(imageDir, ch.channelNumber.ToString());
                                File.WriteAllBytes(imageFile, bytes);
                            }
                        }
                    }
                }
            }

            UpdateDownloadChannelsListProgressStatusText("Done!");

            return chList;
        }

        private void UpdateDownloadChannelsListProgressStatusText(string txt)
        {
            SetStatusText(txt);
            if (GrdProgress.Visibility == Visibility.Visible)
            {
                TxtProgress.Text = txt;
            }
        }

        private async Task<WSInstalledSTB> StbCheckIn(RSVigoStbClient stbClient, WSHospital h, WSNursingUnit n)
        {
            string mac_id = "nextgen:dev-pc";
            stbClient.Switchport = mac_id;
            WSInstalledSTB stb = await stbClient.CheckInAsync(mac_id, "E3.TOOLS");
            long nursingUnitsIdentity = -1;
            if (null != n)
            {
                nursingUnitsIdentity = n.identity;
            }

            if (null == stb
                || stb.hospitalId != h.identity
                || (nursingUnitsIdentity != -1 && stb.nursingUnitId != nursingUnitsIdentity))
            {
                if (null == stb)
                {
                    stb = new WSInstalledSTB();
                }

                stb.hospital = h.name;
                stb.hospitalId = h.identity;
                stb.nursingUnit = h.defaultChannelListWardId.ToString();
                if (nursingUnitsIdentity != -1)
                {
                    stb.nursingUnit = nursingUnitsIdentity.ToString();
                }

                stb.room = "NEXTGEN";
                stb.bed = "DEV-PC";
                stb.switchPort = mac_id;

                WSInstalledSTB ret = await App.gVigoUserClient.UpdateStbAsync(stb);

                if (null != ret)
                {
                    stb = ret;
                }
                stbClient.Switchport = mac_id;
                stb = await stbClient.CheckInAsync(mac_id, "E3.TOOLS");
            }

            return stb;
        }

        private List<string> _VIEWABLE_FILE_EXTS = new List<string>() { ".ver", ".json", ".txt" };
        private long _VIEWABLE_FILE_SIZE = 1000000;

        private void TvCode_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            SelectedHospital sh = (CboHospitals.SelectedItem as SelectedHospital);
            SelectedPlatform sp = (CboPlatforms.SelectedItem as SelectedPlatform);

            FileSystemInfo fsi = e.NewValue as FileSystemInfo;
            if (null == fsi) return;

            TxtFileEditorTitle.Text = fsi.FullName.Replace(TxtCodeFolderTitle.Text, "");
            TxtFileEditorTitle.Tag = fsi.FullName;
            TxtFileEditor.Text = "";
            SetStatusText("");

            if (fsi is FileInfo)
            {
                FileInfo fsii = fsi as FileInfo;

                if (fsii.Length <= _VIEWABLE_FILE_SIZE && _VIEWABLE_FILE_EXTS.Contains(fsii.Extension.ToLower()))
                {
                    string txt = File.ReadAllText(fsi.FullName);

                    if (null != sp && fsi.FullName.ToLower().Contains(sp.e3Platform.RelativePathConfigFile.ToLower()))
                    {
                        txt = UpdateOfflineConfigJsonText(txt, sh, sp);
                    }

                    if (null != sp && fsi.FullName.ToLower().Contains(sp.e3Platform.RelativePathVersionFile.ToLower()))
                    {
                        txt = UpdateOfflineVersionText(txt, sh, sp);
                    }

                    TxtFileEditor.Text = txt;
                }
                else
                {
                    SetStatusText(fsi.Name + " - Unsupported format or file data too large to load...");
                }
                
            }
            else if (fsi is DirectoryInfo)
            {
                DisplayFileSystemInfo(fsi as DirectoryInfo, fsi.Name);
            }
        }

        private string UpdateOfflineConfigJsonText(string txt, SelectedHospital sh, SelectedPlatform sp)
        {
            string ret = txt;
            if (null != sh && !string.IsNullOrEmpty(sh.OfflineConfig))
            {
                ret = sh.OfflineConfig;
            }
            return UpdateOfflineVersionText(ret, sh, sp);
        }

        private const string STR_VERSION = "VERSION";
        private const string STR_HOSPITAL_SHORT_NAME = "HOSPITAL-SHORT-NAME";
        private const string STR_PLATFORM_SHORT_NAME = "PLATFORM-SHORT-NAME";

        private string UpdateOfflineVersionText(string txt, SelectedHospital sh, SelectedPlatform sp)
        {
            string ret = txt;
            if (!string.IsNullOrEmpty(ret))
            {
                if (null != sh && !string.IsNullOrEmpty(sh.Version))
                {
                    ret = ret.Replace(STR_VERSION, sh.Version);
                }
                if (null != sh && null != sh.wsHospital && !string.IsNullOrEmpty(sh.wsHospital.abbreviation))
                {
                    ret = ret.Replace(STR_HOSPITAL_SHORT_NAME, sh.wsHospital.abbreviation.ToUpper());
                }
                if (null != sp && null != sp.e3Platform)
                {
                    ret = ret.Replace(STR_PLATFORM_SHORT_NAME, sp.e3Platform.ShortName);
                }
            }
            return ret;
        }

        private string ParseOfflineVersionFromText(string txt)
        {
            string ret = string.Empty;
            try
            {
                if (!string.IsNullOrEmpty(txt))
                {
                    ret = txt;
                    JObject version_data = null;
                    try { version_data = JObject.Parse(txt); } catch (Exception) { };
                    if (null != version_data && null != version_data["version"])
                    {
                        ret = version_data.ToString();
                    }

                    if (ret.IndexOf("-") > 0)
                    {
                        ret = ret.Split("-".ToCharArray())[0];
                    }
                }
            }
            catch (Exception ex)
            {
                Helper.ShowErrorMessage(ex);
            }
            return ret;
        }

        private void SelectHospitals(bool selected, ComboBox cbo)
        {
            SelectedHospital currentSelected = cbo.SelectedItem as SelectedHospital;
            foreach (SelectedHospital sh in _document.Hospitals)
            {
                sh.Selected = selected;
            }
            cbo.ItemsSource = null;
            cbo.Items.Refresh();
            cbo.ItemsSource = _document.Hospitals;
            cbo.Items.Refresh();
            if (null != currentSelected)
            {
                cbo.SelectedItem = _document.Hospitals.Find(x => x.Id == currentSelected.Id);
            }
            else
            {
                cbo.SelectedItem = _document.Hospitals[0];
            }
            UpdateHospitalsTabHeaderCount();
        }

        private void BtnSelectedHospitalsAll_Click(object sender, RoutedEventArgs e)
        {
            SelectHospitals(true, CboHospitals);
        }

        private void BtnSelectedHospitalsNone_Click(object sender, RoutedEventArgs e)
        {
            SelectHospitals(false, CboHospitals);
        }

        private void SelectPlatforms(bool selected, ComboBox cbo)
        {
            SelectedHospital sh = CboHospitals.SelectedItem as SelectedHospital;
            if (null == sh) return;
            SelectedPlatform currentSelected = cbo.SelectedItem as SelectedPlatform;
            cbo.ItemsSource = null;
            cbo.Items.Refresh();
            foreach (SelectedPlatform sp in sh.Platforms)
            {
                sp.Selected = selected;
            }
            cbo.ItemsSource = sh.Platforms;
            cbo.Items.Refresh();
            currentSelected = sh.Platforms.Find(x => (null != currentSelected && currentSelected.Id == x.Id));
            if (null == cbo.SelectedItem) currentSelected = sh.Platforms[0];
            cbo.SelectedItem = currentSelected;
            UpdatePlatformsTabHeaderCount();
        }

        private void BtnSelectedPlatformsAll_Click(object sender, RoutedEventArgs e)
        {
            SelectPlatforms(true, CboPlatforms);
        }

        private void BtnSelectedPlatformsNone_Click(object sender, RoutedEventArgs e)
        {
            SelectPlatforms(false, CboPlatforms);
        }

        private async void CboPlatforms_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedPlatform sp = CboPlatforms.SelectedItem as SelectedPlatform;
            if (null == sp) return;
            UnzipCodeTemplate(sp.e3Platform);
        }

        void UnzipCodeTemplate(E3Platform p, bool reload = false)
        {
            if (null == p) return;

            string zipsrc = Path.Combine(p.RootDir, "zipsrc");
            if (!Directory.Exists(zipsrc))
            {
                Directory.CreateDirectory(zipsrc);
            }

            if (reload || !File.Exists(p.ConfigFile))
            {
                SetStatusText("Unzipping offline data template. Please wait...", 1);
                Helper.ClearFolder(zipsrc);
                string zipFilePath = p.ZipFile;
                Helper.ExecuteZipTool(" x -aoa -y -bb3 -o" + zipsrc + " " + zipFilePath, zipsrc);
            }

            DirectoryInfo di = new DirectoryInfo(zipsrc);
            TxtCodeFolderTitle.Text = di.FullName;
            TvCode.ItemsSource = di.GetFileSystemInfos();
            DisplayFileSystemInfo(di);
        }

        void DisplayFileSystemInfo(DirectoryInfo di, string folder_name = "") 
        {
            int d = 0, f = 0; long s = 0;
            if (!string.IsNullOrEmpty(folder_name)) folder_name += " - ";
            SetStatusText(folder_name + CalculateFileSystemInfo(di, ref f, ref d, ref s));
        }

        string CalculateFileSystemInfo(DirectoryInfo idi, ref int totalFiles, ref int totalDirs, ref long totalSize)
        {
            totalFiles = 0;
            totalSize = 0;
            totalDirs = 0;
            if (null != idi)
            {
                FileInfo[] files = idi.GetFiles();
                foreach(FileInfo fi in files)
                {
                    totalFiles += 1;
                    totalSize += fi.Length;
                }

                DirectoryInfo[] dirs = idi.GetDirectories();
                foreach(DirectoryInfo di in dirs)
                {
                    totalDirs += 1;
                    long diSize = 0;
                    int diFiles = 0;
                    int diDirs = 0;
                    CalculateFileSystemInfo(di, ref diFiles, ref diDirs, ref diSize);
                    totalDirs += diDirs;
                    totalFiles += diFiles;
                    totalSize += diSize;
                }
            }
            return "Total: " + totalDirs + " folders, " + totalFiles + " files, " + totalSize + " bytes";
        }

        void UnzipCodeTemplateFromZip(E3Platform p, string zipFilePath)
        {
            if (null == p) return;

            try
            {
                SetStatusText("Loading offline data from zip file: " + zipFilePath + ", please wait...", 1);
                TvCode.ItemsSource = null;

                string zipsrc = Path.Combine(p.RootDir, "zipsrc");
                if (!Directory.Exists(zipsrc))
                {
                    Directory.CreateDirectory(zipsrc);
                }

                Helper.ClearFolder(zipsrc);
                Helper.ExecuteZipTool(" x -aoa -y -bb3 -o" + zipsrc + " " + zipFilePath, zipsrc);

                DirectoryInfo di = new DirectoryInfo(zipsrc);
                TxtCodeFolderTitle.Text = di.FullName;
                TvCode.ItemsSource = di.GetFileSystemInfos();

                var version_file = Path.Combine(zipsrc, p.RelativePathVersionFile);
                string offline_version = File.ReadAllText(version_file);
                offline_version = ParseOfflineVersionFromText(offline_version);
                if (!string.IsNullOrEmpty(offline_version))
                {
                    BtnVersion.Content = offline_version;
                }

                DisplayFileSystemInfo(di);
            }
            catch (Exception ex)
            {
                Helper.ShowErrorMessage(ex);
                SetStatusText("Error - " + ex.Message);
            }
        }

        private void BtnBrowseOutputLocation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = Helper.OpenFolderDialog("Select Output Location", CboOutputLocation.Text, this);
                if (!string.IsNullOrEmpty(path))
                {
                    CboOutputLocation.Text = path;
                }
            }
            catch (Exception ex)
            {
                Helper.ShowErrorMessage(ex);
            }
        }

        private void BtnBrowseZipToolExe_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = Helper.OpenFileDialog("Select Zip Tool", "E3 offline data project (*.json)|*.json|All (*.*)|*.*", CboZipToolCmd.Text);
                if (File.Exists(path))
                {
                    if (CboOutputLocation.Text != path)
                    {
                        CboOutputLocation.Text = path;
                        Properties.Settings.Default.ZipToolCmd = path;
                    }
                }
            }
            catch (Exception ex)
            {
                Helper.ShowErrorMessage(ex);
            }
        }

        private async void CboSelectedWards_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectedHospital sh = CboHospitals.SelectedItem as SelectedHospital;
            if (null == sh) return;

            WSHospital h = sh.wsHospital;
            if (null == h) return;

            WSNursingUnit n = CboSelectedWards.SelectedItem as WSNursingUnit;
            if (null == n) return;

            ChkDefaultChannelListWard.IsChecked = (n.identity == sh.DefaultChannelWardId);

            if (n.generateChannelList || (n.identity == sh.DefaultChannelWardId))
            {
                await PopulateTvChannelsList(h, n);
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            SelectedHospital sh = (CboHospitals.SelectedItem as SelectedHospital);
            if (null != sh) Properties.Settings.Default.ZipSelectedHospitalId = sh.Id;

            Properties.Settings.Default.OfflineZipDocument = _document.ToJsonStr();

            // saving output path
            UpdateAndSaveCboValue(CboOutputLocation, "ZipOutputLocation", "ZipOutputLocations");

            // saving output path
            UpdateAndSaveCboValue(CboZipToolCmd, "ZipToolCmd", "ZipToolCmds");

            Properties.Settings.Default.Save();
        }

        void UpdateAndSaveCboValue(ComboBox cbo, string propName, string collectionName)
        {
            string path = cbo.Text.Trim();
            if (!string.IsNullOrEmpty(path))
            {
                path = path.ToLower();
                Properties.Settings.Default[propName] = path;

                if (null == Properties.Settings.Default[collectionName])
                {
                    Properties.Settings.Default[collectionName] = new System.Collections.Specialized.StringCollection();
                }

                StringCollection collection = Properties.Settings.Default[collectionName] as StringCollection;
                if (!collection.Contains(path))
                {
                    collection.Insert(0, path);
                }
                else if (collection.Count > 1 && collection[0] != path)
                {
                    collection.Remove(path);
                    collection.Insert(0, path);
                }
            }
        }

        private async void BtnAutoDetectInstalledPlatforms_Click(object sender, RoutedEventArgs e)
        {
            SelectedHospital sh = CboHospitals.SelectedItem as SelectedHospital;
            if (null == sh) return;

            BtnAutoDetectInstalledPlatforms.IsEnabled = false;
           
            List<WSInstalledSTB> stbs = await App.gVigoUserClient.GetStbsByHospitalIdAsync(sh.Id);
            foreach (SelectedPlatform sp in sh.Platforms)
            {
                List<WSInstalledSTB> stbsi = stbs.FindAll(x => (sp.ContainsModel(x.hwModel) || sp.ContainsModel(x.clientVersion)));
                sp.Selected = (null != stbsi && stbsi.Count > 0);
                if (sp.Selected)
                {
                    sp.InstalledModels.Clear();
                    foreach(WSInstalledSTB stb in stbsi)
                    {
                        stb.ParseHWInfoFromOldVersion();
                        if (!string.IsNullOrEmpty(stb.hwModel))
                        {
                            if (!sp.InstalledModels.Contains(stb.hwModel))
                            {
                                sp.InstalledModels.Add(stb.hwModel);
                            }
                        }
                    }
                }
            }

            CboPlatforms.ItemsSource = null;
            CboPlatforms.Items.Refresh();
            CboPlatforms.ItemsSource = sh.Platforms;
            CboPlatforms.Items.Refresh();
            if (CboPlatforms.Items.Count > 0)
            {
                CboPlatforms.SelectedItem = sh.Platforms.Find(x => x.Selected);
            }

            UpdatePlatformsTabHeaderCount();

            List<SelectedPlatform> selectedPlatforms = sh.Platforms.FindAll(x => x.Selected);
            string msg = "Found " + selectedPlatforms.Count + " installed platform(s).";
            foreach(SelectedPlatform sp in selectedPlatforms)
            {
                msg += "\n\n" + sp.e3Platform.Name + ": " + sp.InstalledModels.Count + " model(s)";
                foreach(string s in sp.InstalledModels)
                {
                    msg += "\n  - " + s;
                }
            }

            MessageBox.Show(msg, "Auto Detect Platforms");

            BtnAutoDetectInstalledPlatforms.IsEnabled = true;
        }

        private void BtnRefreshCodeTemplate_Click(object sender, RoutedEventArgs e)
        {
            SelectedPlatform sp = CboPlatforms.SelectedItem as SelectedPlatform;
            if (null == sp) return;
            UnzipCodeTemplate(sp.e3Platform, true);
        }

        private void BtnOpenCodeTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = Helper.OpenFileDialog("Select old zip File", "E3 offline zip (*.zip)|*.zip|All (*.*)|*.*");
                if (!string.IsNullOrEmpty(path))
                {
                    SelectedPlatform sp = CboPlatforms.SelectedItem as SelectedPlatform;
                    if (null == sp) return;
                    UnzipCodeTemplateFromZip(sp.e3Platform, path);
                }
            }
            catch (Exception ex)
            {
                Helper.ShowErrorMessage(ex, "Open Offline Zip Error");
            }
        }

        private void BtnOpenOutputFolder_Click(object sender, RoutedEventArgs e)
        {
            string outputFolder = CboOutputLocation.Text;
            System.Diagnostics.Process.Start(outputFolder);
        }

        private void BtnCodeFolder_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(TxtCodeFolderTitle.Text);
        }

        private void BtnCodeFolderDeleteItem_Click(object sender, RoutedEventArgs e)
        {
            FileSystemInfo fsi = TvCode.SelectedItem as FileSystemInfo;
            if (null == fsi) return;
            string title = "Delete file/folder from data template";
            string msg = "Delete " + (fsi is FileInfo ? "file" : "folder") + " " + fsi.FullName + "?";
            if (MessageBox.Show(msg, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {   
                if (fsi is DirectoryInfo)
                {
                    Helper.ClearFolder(fsi.FullName);
                }
                try { fsi.Delete(); } catch (Exception ex) { Helper.ShowErrorMessage(ex);  }
                DirectoryInfo di = new DirectoryInfo(TxtCodeFolderTitle.Text);
                TvCode.ItemsSource = di.GetFileSystemInfos();
                DisplayFileSystemInfo(di);
            }
        }        

        private void BtnFileEditorSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string txt = TxtFileEditor.Text.Trim();
                if (string.IsNullOrEmpty(txt))
                {
                    string fileName = TxtFileEditorTitle.Tag.ToString();
                    File.WriteAllText(fileName, txt);
                }
            }
            catch (Exception ex)
            {
                Helper.ShowErrorMessage(ex);
            }
        }

        private void TvChannelsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            WSChannel ch = (TvChannelsList.SelectedItem as WSChannel);
            ProgGridChannel.DataContext = ch;
        }

        private void ChkDefaultChannelListWard_Click(object sender, RoutedEventArgs e)
        {
            SelectedHospital sh = (CboHospitals.SelectedItem as SelectedHospital);
            if (null == sh) return;

            if (ChkDefaultChannelListWard.IsChecked == true)
            {
                WSNursingUnit n = (CboSelectedWards.SelectedItem as WSNursingUnit);
                if (null != n)
                {
                    sh.DefaultChannelWardId = n.identity;
                }
            }
            else
            {
                sh.DefaultChannelWardId = 0;
            }
        }

        private void BtnClearTvChannels_Click(object sender, RoutedEventArgs e)
        {
            WSHospital h = (CboHospitals.SelectedItem as WSHospital);
            WSNursingUnit n = (CboSelectedWards.SelectedItem as WSNursingUnit);
            if (null == h || null == n) return;
            string imageDir, contentDir;
            string tvchannelsFile = getChannelsFilePath(h, n, out contentDir, out imageDir);
            if (File.Exists(tvchannelsFile))
            {
                File.Delete(tvchannelsFile);
            }
            n.channelListDownloaded = File.Exists(tvchannelsFile);
            this.TvChannelsList.ItemsSource = null;
            this.TvChannelsList.Items.Refresh();
        }

        private void BtnOfflineConfigSave_Click(object sender, RoutedEventArgs e)
        {
            SelectedHospital sh = (CboHospitals.SelectedItem as SelectedHospital);
            if (null == sh) return;

            TxtOfflineConfigEditor.Text = TxtOfflineConfigEditor.Text.Trim();
            sh.OfflineConfig = TxtOfflineConfigEditor.Text;
        }

        private void BtnOfflineConfigClear_Click(object sender, RoutedEventArgs e)
        {
            TxtOfflineConfigEditor.Text = "";
        }

        private void BtnVersion_Click(object sender, RoutedEventArgs e)
        {
            string ver = "";
            if (null != BtnVersion.Content) ver = BtnVersion.Content.ToString();
            string new_ver = InputWindow.ShowInput("Please enter version # (for example: 3.0.3): ", ver);
            if (!string.IsNullOrEmpty(new_ver) && new_ver != ver)
            {
                BtnVersion.Content = new_ver;
                SelectedHospital sh = (CboHospitals.SelectedItem as SelectedHospital);
                if (null != sh) sh.Version = new_ver;
            }
        }

        private void ChkHospitalCheckBox_Click(object sender, RoutedEventArgs e)
        {
            UpdateHospitalsTabHeaderCount();
        }

        private void ChkPlatformCheckBox_Click(object sender, RoutedEventArgs e)
        {
            UpdatePlatformsTabHeaderCount();
        }

        private void MnuNew_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult mbr = MessageBox.Show("Please save/export your work before start new project.\nContinue?", "New Offline Data Project", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (mbr == MessageBoxResult.No) return;
            InitializeData("[]");
        }

        private void MnuImport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = Helper.OpenFileDialog("Import from File", "E3 offline data project (*.json)|*.json|All (*.*)|*.*");
                if (!string.IsNullOrEmpty(path))
                {
                    string json = File.ReadAllText(path);
                    InitializeData(json);
                }
            }
            catch (Exception ex)
            {
                Helper.ShowErrorMessage(ex, "Import Project Error");
            }
        }

        private void MnuExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = Helper.SaveFileDialog("Export to File", "E3 data file (*.json)|*.json|All (*.*)|*.*");
                if (!string.IsNullOrEmpty(path))
                {
                    File.WriteAllText(path, _document.ToJsonStr());
                }
            }
            catch (Exception ex)
            {
                Helper.ShowErrorMessage(ex, "Import Project Error");
            }
        }

        private void MnuClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void TabData_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SetStatusText("");
        }
    }

    public class E3Platform
    {
        public int Id { get; set; }
        public string RootDir { get; set; }
        public string PrefixDir { get; set; }
        public string Name { get; set; }
        public string ZipName { get; set; }
        public List<string> Models { get; set; }

        public E3Platform(int id, string name, string zipname, string prefix, List<string> models) 
        {
            this.Id = id;
            this.Name = name;
            this.ZipName = zipname;
            this.PrefixDir = prefix;
            this.RootDir = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "offlinedata", this.PrefixDir);
            this.Models = models;
        }

        virtual public string ZipFile 
        {
            get { return Path.Combine(this.RootDir, this.ZipName + ".zip"); }
        }

        virtual public string RelativePathVersionFile
        {
            get { return this.ZipName + ".ver"; }
        }

        virtual public string VersionFile 
        {
            get { return Path.Combine(this.RootDir, this.RelativePathVersionFile); }
        }

        virtual public string RelativePathConfigFile
        {
            get { return Path.Combine(this.CacheDir, "config_offline.json"); }
        }

        virtual public string ConfigFile
        {
            get { return Path.Combine(this.RootDir, "zipsrc", this.RelativePathConfigFile); }
        }

        virtual public string CacheDir
        {
            get { return @"e3\cache"; }
        }

        virtual public string ShortName
        {
            get { return this.PrefixDir.ToUpper(); }
        }

        public override string ToString()
        {
            return this.Id + " - " + this.Name;
        }
    }

    public class LGPlatform : E3Platform
    {
        public LGPlatform(int id, string name, string zipname, string prefix, List<string> models) : base(id, name, zipname, prefix, models)
        {
        }

        public override string CacheDir
        {
            get { return "cache"; }
        }

        public override string RelativePathVersionFile
        {
            get { return Path.Combine("config", "config.json"); }
        }

        public override string RelativePathConfigFile
        {
            get { return Path.Combine(this.CacheDir, "config_offline.json"); }
        }

        public override string VersionFile
        {
            get { return Path.Combine(this.RootDir, "zipsrc", this.RelativePathVersionFile); }
        }
    }

    public static class E3PlatformList
    {
        static List<E3Platform> _items = new List<E3Platform>();
        public static List<E3Platform> Items 
        {
            get 
            {
                if (_items.Count <= 0)
                {
                    _items.Add(new E3Platform(3, "Samsung TV Tizen (xxNF693)", "e3_sstz_offline", "sstz", new List<string>() { "NF693" }));
                    _items.Add(new E3Platform(2, "Samsung TV Orsay (xxNC693/xxNE593)", "e3_ss_offline", "sstv", new List<string>() { "NC693", "NE593" }));
                    _items.Add(new LGPlatform(1, "LG TV/STB", "e3_lgtv_offline", "lgtv", new List<string>() { "LY", "LX", "LU", "LV", "UV", "STB-" }));
                }
                return _items;
            }
        }
    }

    public class GetFileSystemInfosConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is DirectoryInfo)
                {
                    return ((DirectoryInfo)value).GetFileSystemInfos();
                }
            }
            catch { }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    #region HeaderToImageConverter

    [ValueConversion(typeof(string), typeof(bool))]
    public class HeaderToImageConverter : IValueConverter
    {
        public static HeaderToImageConverter Instance =
            new HeaderToImageConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DirectoryInfo)
            {
                Uri uri = new Uri("pack://application:,,,/resources/folder.png");
                BitmapImage source = new BitmapImage(uri);
                return source;
            }
            else
            {
                Uri uri = new Uri
                ("pack://application:,,,/resources/file.png");
                BitmapImage source = new BitmapImage(uri);
                return source;
            }
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("Cannot convert back");
        }
    }

    #endregion // HeaderToImageConverter

    public class UriToCachedImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return null;

            try
            {
                if (!string.IsNullOrEmpty(value.ToString()))
                {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.UriSource = new Uri(value.ToString());
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.EndInit();
                    return bi;
                }
            }
            catch (Exception) { }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException("Two way conversion is not supported.");
        }
    }

    public class SelectableItem : INotifyPropertyChanged
    {
        protected bool _selected = false;

        public event PropertyChangedEventHandler PropertyChanged;

        public bool Selected
        {
            get { return _selected; }
            set
            {
                if (_selected != value)
                {
                    _selected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Selected"));
                }
            }
        }
    }

    public class SelectedPlatform : SelectableItem
    {
        public int Id { get; set; }
        public List<string> InstalledModels { get; set; }

        [JsonIgnore]
        public E3Platform e3Platform { get; set; }

        public SelectedPlatform()
        {
            this.InstalledModels = new List<string>();
        }

        public SelectedPlatform(E3Platform p)
        {
            this.e3Platform = p;
            this.Id = p.Id;
            this.InstalledModels = new List<string>();
        }

        public bool ContainsModel(string model)
        {
            if (string.IsNullOrEmpty(model)) return false;

            foreach(string s in this.e3Platform.Models)
            {
                if (model.IndexOf(s) >= 0) return true;
            }

            return false;
        }

        public override string ToString()
        {
            if (null != this.e3Platform) return e3Platform.ToString();
            else return this.Id.ToString();
        }
    }

    public class SelectedHospital : SelectableItem
    {
        public long Id { get; set; }
        public long DefaultChannelWardId { get; set; }
        public string OfflineConfig { get; set; }
        public string Version { get; set; }
        public List<SelectedPlatform> Platforms { get; set; }        

        [JsonIgnore]
        public WSHospital wsHospital { get; set; }

        public SelectedHospital()
        {
            this.Platforms = new List<SelectedPlatform>();
            this.Version = "3.0.2";
        }

        public SelectedHospital(WSHospital h)
        {
            this.wsHospital = h;
            this.Id = h.identity;
            this.Platforms = new List<SelectedPlatform>();
            this.Version = "3.0.2";
        }

        public void UpdatePlatforms()
        {
            foreach (E3Platform p in E3PlatformList.Items)
            {
                SelectedPlatform sp = this.Platforms.Find(x => x.Id == p.Id);
                if (null == sp)
                {
                    sp = new SelectedPlatform(p);
                    sp.PropertyChanged += Sp_PropertyChanged;
                    this.Platforms.Add(sp);
                }
                else
                {
                    sp.e3Platform = p;
                }
            }
        }

        private void Sp_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // TODO
        }
    }

    public class OfflineZipDocument : INotifyPropertyChanged
    {
        // public Dictionary<long, SelectedHospital> Hospitals { get; private set; }
        public List<SelectedHospital> Hospitals { get; private set; }

        public OfflineZipDocument()
        {
            this.Hospitals = new List<SelectedHospital>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public int CalculateTotalGeneratedZips(ref string msg)
        {
            int ret = 0;
            msg = "";
            List<SelectedHospital> shs = this.Hospitals.FindAll(x => x.Selected);
            if (null != shs && shs.Count > 0)
            {
                foreach (SelectedHospital sh in shs)
                {
                    sh.UpdatePlatforms();
                    List<SelectedPlatform> sps = sh.Platforms.FindAll(x => x.Selected);
                    if (null != sps && sps.Count > 0)
                    {
                        msg += "\n" + sh.wsHospital.name + " (v" + sh.Version + "), " + sps.Count + ": ";
                        foreach (SelectedPlatform sp in sps)
                        {
                            msg += sp.e3Platform.ShortName + ", ";
                            ret += 1;
                        }
                    }
                }
            }
            return ret;
        }

        public SelectedHospital FindHospital(long id)
        {
            return this.Hospitals.Find(x => x.Id == id);
        }

        public void UpdateHospitals(List<WSHospital> lst)
        {
            foreach (WSHospital h in lst)
            {
                SelectedHospital sh = this.FindHospital(h.identity);
                if (null == sh)
                {
                    sh = new SelectedHospital(h);
                    sh.PropertyChanged += Sh_PropertyChanged;
                    this.Hospitals.Add(sh);
                }
                else
                {
                    sh.wsHospital = h;
                }
            }
        }

        public string ToJsonStr()
        {
            string ret = JsonConvert.SerializeObject(this.Hospitals, Formatting.Indented);
            return ret;
        }

        public void FromJsonStr(string s)
        {
            this.Hospitals.Clear();
            if (!string.IsNullOrEmpty(s))
            {
                List<SelectedHospital> lst = JsonConvert.DeserializeObject<List<SelectedHospital>>(s);
                if (null != lst && lst.Count > 0)
                {
                    this.Hospitals = lst;
                    foreach(SelectedHospital sh in lst)
                    {
                        sh.PropertyChanged += Sh_PropertyChanged;
                    }
                }
            }
        }

        private void Sh_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(sender, new PropertyChangedEventArgs("HospitalsSelected"));
        }
    }
}

