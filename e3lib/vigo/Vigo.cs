// ------------------------------------------------------------------------------
// Copyright (c) 2015 - Allen Technologies Inc. allentek.com. All Rights Reserved.
// Author: Tai H. Le <tle@allentek.com>
// 
// Web Service REST API Client 
// ------------------------------------------------------------------------------
using Ati.VigoPC.WebServices.REST;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace e3tools
{
    public class RSVigoClient
    {
        #region Private vars

        private string _vigoServer = "https://vpn-portal.allentek.net"; // Properties.Settings.Default.VigoServer;
        private string _vigoServerResourcePrefix = "/ws/json";
        string _username = string.Empty;
        string _password = string.Empty;
        private RestClient _restClient = null;
        string _error = string.Empty;
        string _responseRawData = string.Empty;
        List<Parameter> _cookies = new List<Parameter>();
        #endregion

        #region Public Properties

        public List<Parameter> Cookies
        {
            get { return _cookies; }
            set { _cookies = value; }
        }

        public string Error
        {
            get { return _error; }
            set { _error = value; }
        }

        public string Username
        {
            get { return _username; }
            set { _username = value; }
        }

        public string Password
        {
            get { return _password; }
            set { _password = value; }
        }

        public string VigoServer
        {
            get { return _vigoServer; }
            set
            {
                try
                {
                    Uri uri = new Uri(value);

                    string prefix = uri.AbsolutePath.TrimEnd("/".ToCharArray());
                    if (!string.IsNullOrEmpty(prefix))
                    {
                        _vigoServerResourcePrefix = prefix;
                        value = value.Replace(_vigoServerResourcePrefix, "");
                    }
                }
                catch
                {
                }

                if (!_vigoServer.Equals(value))
                {
                    _vigoServer = value;
                    _restClient = null; // reset and open with new url next time
                }
            }
        }

        public string VigoServerResourcePrefix
        {
            get { return _vigoServerResourcePrefix; }
            set { _vigoServerResourcePrefix = value; }
        }

        public RestClient RestClient
        {
            get
            {
                if (null == _restClient)
                {
                    _restClient = new RestClient(_vigoServer);
                    _restClient.Authenticator = new HttpBasicAuthenticator(this.Username, this.Password);
                }
                return _restClient;
            }

            set
            {
                _restClient = value;
                this.Cookies.Clear();
            }
        }

        public string ResponseRawData
        {
            get { return _responseRawData; }
            set { _responseRawData = value; }
        }

        public void PrepareSessionCookies(ref RestRequest request)
        {
            PrepareSessionCookies(ref request, _cookies);
        }

        public void PrepareSessionCookies(ref RestRequest request, List<Parameter> param)
        {
            if (param.Count > 0)
            {
                foreach (Parameter p in param)
                {
                    request.AddParameter(p);
                }
            }
        }

        public void UpdateSessionCookies(RestResponse response)
        {
            UpdateSessionCookies(response, ref _cookies);
        }

        public void UpdateSessionCookies(RestResponse response, ref List<Parameter> param)
        {
            param.Clear();
            param.AddRange(response.Cookies);
        }

        #endregion

        #region Init

        public RSVigoClient()
        {
        }

        public RSVigoClient(string vigoServerUrl)
        {
            this.VigoServer = vigoServerUrl;
        }

        #endregion

        #region Supported Methods
        protected void ResetResponseData()
        {
            this.Error = string.Empty;
            this.ResponseRawData = string.Empty;
        }

        bool _reportWebServiceStatusCode = false; // RegistryHelper.GetAppFlag("ReportWebServiceStatusCode", false);
        protected void FetchResponseError(RestResponse response)
        {
            if (null == response)
            {
                this.Error = "Internal Error: RestResponse object is null!";
                return;
            }

            if (response.StatusCode == HttpStatusCode.NotFound ||
                response.StatusCode == HttpStatusCode.BadGateway ||
                response.StatusCode == HttpStatusCode.GatewayTimeout ||
                response.StatusCode == HttpStatusCode.RequestTimeout ||
                response.StatusCode == HttpStatusCode.ServiceUnavailable)
            {
                this.Error = "Vigo Server is not available. Please try again later.";
            }
            else
            {
                this.Error = ParseErrorMessage(response);
            }

            if (_reportWebServiceStatusCode)
            {
                this.Error += "\n(StatusCode: " + response.StatusCode + ")";
            }

            this.RestClient = null;
        }

        string ParseErrorMessage(RestResponse response)
        {
            string content = response.StatusDescription;
            try
            {
                if (!string.IsNullOrEmpty(response.Content))
                {
                    content = response.Content;
                    int h1StartPos = content.IndexOf("<h1>", StringComparison.CurrentCultureIgnoreCase);
                    if (h1StartPos >= 0)
                    {
                        h1StartPos += 4;
                        int h1EndPos = content.IndexOf("</h1>", h1StartPos, StringComparison.CurrentCultureIgnoreCase);
                        if (h1EndPos > h1StartPos)
                        {
                            return content.Substring(h1StartPos, h1EndPos - h1StartPos);
                        }
                    }
                }
            }
            catch
            {
            }
            return content;
        }
        #endregion

        #region Public methods
        public string RunGenericRequest(string url, NameValueCollection paramList, string method)
        {
            try
            {
                Method m = Method.GET;
                if (method.Equals("POST", StringComparison.CurrentCultureIgnoreCase)) m = Method.POST;

                RestRequest request = new RestRequest(m);
                if (null != paramList && paramList.Count > 0)
                {
                    if (m == Method.POST)
                    {
                        request.Resource = this.VigoServerResourcePrefix + url;
                        foreach (string k in paramList.Keys)
                        {
                            string[] values = paramList.GetValues(k);
                            foreach (string v in values)
                            {
                                request.AddParameter(k, v);
                            }
                        }
                    }
                    else
                    {
                        StringBuilder sb = new StringBuilder(this.VigoServerResourcePrefix + url + "?");
                        foreach (string k in paramList.Keys)
                        {
                            string[] values = paramList.GetValues(k);
                            foreach (string v in values)
                            {
                                sb.Append(k + "=" + v + "&");
                            }
                        }
                        request.Resource = sb.ToString();
                    }
                }
                else
                {
                    request.Resource = this.VigoServerResourcePrefix + url;
                }

                RestResponse response = this.RestClient.Execute(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    _responseRawData = response.Content;
                    return _responseRawData;
                }
                else
                {
                    FetchResponseError(response);
                }
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
            }
            return null;
        }
        #endregion
    }

    public partial class RSVigoStbClient : RSVigoClient
    {
        #region Events and Handlers
        // public event VigoNotificationsReceived VigoNotificationsReceivedHandler;
        #endregion

        #region Private Members
        private string _switchport = string.Empty; // "123.456.789.000:001";
        private WSInstalledSTB _stb = null;
        #endregion

        #region Public Properties
        public string Switchport
        {
            get { return _switchport; }
            set
            {
                if (!_switchport.Equals(value))
                {
                    _switchport = value;
                    SetAuthenticationData();
                }
            }
        }

        public WSInstalledSTB Stb
        {
            get { return _stb; }
        }

        // http://aus-devdb11.corp.allentek.com/ws/json/stbClient/occupiedBedsCount
        public int OccupiedBedsCount
        {
            get
            {
                int ret = 0;
                try
                {
                    ResetResponseData();
                    RestRequest request = new RestRequest(Method.GET);
                    string query = base.VigoServerResourcePrefix + "/stbClient/occupiedBedsCount";
                    request.Resource = query;
                    RestResponse response = this.RestClient.Execute(request);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        string data = response.Content;
                        this.ResponseRawData = data;
                        ret = Convert.ToInt32(data);
                    }
                    else
                    {
                        FetchResponseError(response);
                    }
                }
                catch (Exception ex)
                {
                    this.Error = "Exception: " + ex.Message;
                }
                return ret;
            }
        }

        #endregion

        #region Private Functions
        private void SetAuthenticationData()
        {
            base.Username = "vigo_client";
            base.Password = "7pLuPnrx_hi;af" + "---" + this.Switchport.ToLower();
            base.RestClient = null;
        }
        #endregion

        #region Init
        public RSVigoStbClient()
        {
            SetAuthenticationData();
        }

        public RSVigoStbClient(string vigoServerHost)
            : base(vigoServerHost)
        {
            SetAuthenticationData();
        }
        #endregion

        #region Common: Time, IsServerAlive
        public DateTime DateTime
        {
            get
            {
                try
                {
                    ResetResponseData();
                    RestRequest request = new RestRequest(Method.GET);
                    request.Resource = base.VigoServerResourcePrefix + "/stbClient/time";
                    RestResponse response = this.RestClient.Execute(request);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        string data = response.Content;
                        this.ResponseRawData = data;
                        WSTimeResponse time = WSDataObjectHelper.FromContentData<WSTimeResponse>(data);
                        return DateTime.Parse(time.dateTime);
                    }
                    else
                    {
                        FetchResponseError(response);
                    }
                }
                catch
                {
                }
                return DateTime.MinValue;
            }
        }

        public bool IsServerAlive
        {
            get
            {
                DateTime dt = this.DateTime;
                return (dt != DateTime.MinValue);
            }
        }
        #endregion

        #region CheckIn

        public Task<WSInstalledSTB> CheckInAsync(string mac, string version)
        {
            var tcs = new TaskCompletionSource<WSInstalledSTB>();
            ResetResponseData();
            this._stb = null;
            RestRequest request = new RestRequest(Method.POST);
            request.Resource = base.VigoServerResourcePrefix + "/stbClient/checkIn";
            request.AddParameter("macAddress", mac);
            request.AddParameter("stbType", "SmartTV");
            if (!string.IsNullOrEmpty(version))
            {
                request.AddParameter("clientVersion", version);
            }

            this.RestClient.ExecuteAsync(request, (RestResponse response) =>
            {
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    this.UpdateSessionCookies(response);
                    this.Error = string.Empty;
                    string data = response.Content;
                    this.ResponseRawData = data;
                    this._stb = WSDataObjectHelper.FromContentData<WSInstalledSTB>(data);
                    tcs.TrySetResult(this._stb);
                }
                else
                {
                    FetchResponseError(response);
                    tcs.TrySetResult(null);
                }
            });

            return tcs.Task;
        }

        #endregion

        #region Parameters
        public Task<WSDynamicParameter> GetParameterAsync(string name)
        {
            var tcs = new TaskCompletionSource<WSDynamicParameter>();
            ResetResponseData();
            RestRequest request = new RestRequest(Method.GET);
            request.Resource = base.VigoServerResourcePrefix + "/stbClient/parameter/" + name;

            this.RestClient.ExecuteAsync(request, (RestResponse response) =>
            {
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    string data = response.Content;
                    this.ResponseRawData = data;
                    tcs.TrySetResult(WSDataObjectHelper.FromContentData<WSDynamicParameter>(data));
                }
                else
                {
                    FetchResponseError(response);
                    tcs.TrySetResult(null);
                }
            });

            return tcs.Task;
        }

        public WSDynamicParameter GetParameter(string name)
        {
            ResetResponseData();
            RestRequest request = new RestRequest(Method.GET);
            request.Resource = base.VigoServerResourcePrefix + "/stbClient/parameter/" + name;

            RestResponse response = this.RestClient.Execute(request);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string data = response.Content;
                this.ResponseRawData = data;
                return WSDataObjectHelper.FromContentData<WSDynamicParameter>(data);
            }
            else
            {
                FetchResponseError(response);
            }
            return null;
        }
        #endregion

        #region TVIR
        public string GetTVIR()
        {
            if (null == _stb)
            {
                this.Error = "Please check in first!";
                return null;
            }

            ResetResponseData();
            RestRequest request = new RestRequest(Method.GET);
            request.Resource = base.VigoServerResourcePrefix + "/stbClient/tvir";

            RestResponse response = this.RestClient.Execute(request);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                this.ResponseRawData = response.Content;
                return this.ResponseRawData;
            }
            else
            {
                FetchResponseError(response);
            }

            return string.Empty;
        }
        #endregion

        #region CareInfo Methods
        public WSCareInfo2 GetCareInfo2()
        {
            if (null == _stb)
            {
                this.Error = "Please check in first!";
                return null;
            }

            ResetResponseData();
            RestRequest request = new RestRequest(Method.GET);
            request.Resource = base.VigoServerResourcePrefix + @"/stbClient/patientSmartBoard";

            RestResponse response = this.RestClient.Execute(request);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                this.ResponseRawData = response.Content;
                return WSDataObjectHelper.FromContentData<WSCareInfo2>(this.ResponseRawData);
            }
            else
            {
                FetchResponseError(response);
            }
            return null;
        }

        public WSCareInfo GetCareInfo()
        {
            if (null == _stb)
            {
                this.Error = "Please check in first!";
                return null;
            }

            ResetResponseData();
            RestRequest request = new RestRequest(Method.GET);
            request.Resource = base.VigoServerResourcePrefix + "/stbClient/careInfo";

            RestResponse response = this.RestClient.Execute(request);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                this.ResponseRawData = response.Content;
                return WSDataObjectHelper.FromContentData<WSCareInfo>(this.ResponseRawData);
            }
            else
            {
                FetchResponseError(response);
            }
            return null;
        }
        #endregion

        #region ChannelLineup
        public List<WSChannel> GetChannelLineup()
        {
            ResetResponseData();
            if (null == _stb)
            {
                this.Error = "Please check in first!";
                return null;
            }

            RestRequest request = new RestRequest(Method.GET);
            request.Resource = base.VigoServerResourcePrefix + "/stbClient/channelLineup";

            RestResponse response = this.RestClient.Execute(request);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                this.ResponseRawData = response.Content;
                WSChannel[] channels = WSDataObjectHelper.FromContentData<WSChannel[]>(this.ResponseRawData);
                return new List<WSChannel>(channels);
            }
            else
            {
                FetchResponseError(response);
            }
            return null;
        }

        public Task<List<WSChannel>> GetChannelLineupAsync()
        {
            var tcs = new TaskCompletionSource<List<WSChannel>>();
            try
            {
                ResetResponseData();
                if (null == _stb)
                {
                    this.Error = "Please check in first!";
                    tcs.TrySetResult(null);
                }
                else
                {
                    RestRequest request = new RestRequest(Method.GET);
                    request.Resource = base.VigoServerResourcePrefix + "/stbClient/channelLineup";

                    this.RestClient.ExecuteAsync(request, (RestResponse response) =>
                    {
                        if (response.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            this.ResponseRawData = response.Content;
                            tcs.TrySetResult(WSDataObjectHelper.ListFromContentData<WSChannel>(this.ResponseRawData));
                        }
                        else
                        {
                            FetchResponseError(response);
                            tcs.TrySetResult(null);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
                tcs.TrySetResult(null);
            }
            return tcs.Task;
        }
        #endregion

        #region SiteWideAlert
        public List<WSSitewideAlert> GetSitewideAlerts()
        {
            if (null == _stb)
            {
                this.Error = "Please check in first!";
                return null;
            }

            ResetResponseData();
            RestRequest request = new RestRequest(Method.GET);
            request.Resource = base.VigoServerResourcePrefix + "/stbClient/emergencyAlerts";

            RestResponse response = this.RestClient.Execute(request);

            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                this.ResponseRawData = response.Content;
                return WSDataObjectHelper.ArrayFromContentData<WSSitewideAlert>(this.ResponseRawData);
            }
            else
            {
                FetchResponseError(response);
            }
            return null;
        }

        #endregion

        #region Content
        public Task<WSContent> GetContentByNameAsync(string name)
        {
            var tcs = new TaskCompletionSource<WSContent>();
            ResetResponseData();
            RestRequest request = new RestRequest(Method.GET);
            request.Resource = base.VigoServerResourcePrefix + "/content/byName/" + name;

            this.RestClient.ExecuteAsync(request, (RestResponse response) =>
            {
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    string data = response.Content;
                    this.ResponseRawData = data;
                    tcs.TrySetResult(WSDataObjectHelper.FromContentData<WSContent>(data));
                }
                else
                {
                    FetchResponseError(response);
                    tcs.TrySetResult(null);
                }
            });
            return tcs.Task;
        }

        public Task<WSBinaryContent> DownloadContentByNameAsync(string name)
        {
            var tcs = new TaskCompletionSource<WSBinaryContent>();
            ResetResponseData();
            RestRequest request = new RestRequest(Method.GET);
            request.Resource = base.VigoServerResourcePrefix + "/content/download/byName/" + name + "/en";
            this.RestClient.ExecuteAsync(request, (RestResponse response) =>
            {
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    string data = response.Content;
                    this.ResponseRawData = data;
                    tcs.TrySetResult(WSDataObjectHelper.FromContentData<WSBinaryContent>(data));
                }
                else
                {
                    FetchResponseError(response);
                    tcs.TrySetResult(null);
                }
            });
            return tcs.Task;
        }

        #endregion
    }

    public class RSVigoUserClient : RSVigoClient
    {
        #region Init
        public RSVigoUserClient()
        {
        }

        public RSVigoUserClient(string vigoServerHost)
            : base(vigoServerHost)
        {
        }
        #endregion

        #region userClient
        public WSUser Login(string username, string password)
        {
            try
            {
                ResetResponseData();

                base.Username = username;
                base.Password = password;
                base.RestClient = null;

                RestRequest request = new RestRequest(Method.POST);
                request.Resource = base.VigoServerResourcePrefix + "/userClient/auth";

                RestResponse response = this.RestClient.Execute(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    this.ResponseRawData = response.Content;
                    return WSDataObjectHelper.FromContentData<WSUser>(this.ResponseRawData);
                }
                else
                {
                    FetchResponseError(response);
                }
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
            }
            return null;
        }

        public Task<WSUser> LoginAsync(string username, string password)
        {
            var tcs = new TaskCompletionSource<WSUser>();
            try
            {
                ResetResponseData();

                base.Username = username;
                base.Password = password;
                base.RestClient = null;

                RestRequest request = new RestRequest(Method.POST);
                request.Resource = base.VigoServerResourcePrefix + "/userClient/auth";

                this.RestClient.ExecuteAsync(request, (RestResponse response) => { 
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        this.ResponseRawData = response.Content;
                        tcs.TrySetResult(WSDataObjectHelper.FromContentData<WSUser>(this.ResponseRawData));
                    }
                    else
                    {
                        FetchResponseError(response);
                        tcs.TrySetResult(null);
                    }
                });
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
                tcs.TrySetResult(null);
            }
            return tcs.Task;
        }

        public WSUser GetCurrentUser()
        {
            try
            {
                ResetResponseData();
                RestRequest request = new RestRequest(Method.GET);
                request.Resource = base.VigoServerResourcePrefix + "/userClient/currentUser";
                RestResponse response = this.RestClient.Execute(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    this.ResponseRawData = response.Content;
                    return WSDataObjectHelper.FromContentData<WSUser>(this.ResponseRawData);
                }
                else
                {
                    FetchResponseError(response);
                }
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
            }
            return null;
        }

        #endregion

        #region admin
        public List<WSStbEvent> GetStbEvents(long hospitalId, long stbId, DateTime startDate, DateTime endDate, string eventCategory, string eventName)
        {
            return GetStbEvents(hospitalId, stbId, startDate, endDate, eventCategory, eventName, 1000, 1);
        }

        public List<WSStbEvent> GetStbEvents(long hospitalId, long stbId, DateTime startDate, DateTime endDate,
            string eventCategory, string eventName, int rowsPerPage, int pageNum)
        {
            try
            {
                ResetResponseData();
                RestRequest request = new RestRequest(Method.GET);
                StringBuilder sb = new StringBuilder(base.VigoServerResourcePrefix + "/admin/stbEvents?"); //sidx=stbTimestamp&sord=desc");
                sb.Append("&rows=" + rowsPerPage);
                sb.Append("&page=" + pageNum);
                sb.Append("&hospitalId=" + hospitalId);
                sb.Append("&startDate=" + startDate.ToString("MM/dd/yyyy"));
                sb.Append("&endDate=" + endDate.ToString("MM/dd/yyyy"));
                if (stbId > 0) sb.Append("&stbId=" + stbId);
                if (!string.IsNullOrEmpty(eventCategory)) sb.Append("&category=" + eventCategory);
                if (!string.IsNullOrEmpty(eventName)) sb.Append("&eventName=" + eventName);

                request.Resource = sb.ToString();

                RestResponse response = this.RestClient.Execute(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    this.ResponseRawData = response.Content;
                    return WSDataObjectHelper.ListFromContentData<WSStbEvent>(this.ResponseRawData);
                }
                else
                {
                    FetchResponseError(response);
                }
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
            }
            return null;
        }
        #endregion

        #region hospital
        public List<WSHospital> GetHospitals()
        {
            try
            {
                ResetResponseData();
                RestRequest request = new RestRequest(Method.GET);
                request.Resource = base.VigoServerResourcePrefix + "/hospitals?rows=500&page=1";

                RestResponse response = this.RestClient.Execute(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    this.ResponseRawData = response.Content;
                    return WSDataObjectHelper.ListFromContentData<WSHospital>(this.ResponseRawData);
                }
                else
                {
                    FetchResponseError(response);
                }
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
            }
            return null;
        }

        public Task<List<WSHospital>> GetHospitalsAsync()
        {
            var tcs = new TaskCompletionSource<List<WSHospital>>();
            try
            {
                ResetResponseData();
                RestRequest request = new RestRequest(Method.GET);
                request.Resource = base.VigoServerResourcePrefix + "/hospitals?rows=500&page=1";

                this.RestClient.ExecuteAsync(request, (RestResponse response) =>
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        this.ResponseRawData = response.Content;
                        tcs.TrySetResult(WSDataObjectHelper.ListFromContentData<WSHospital>(this.ResponseRawData));
                    }
                    else
                    {
                        FetchResponseError(response);
                        tcs.TrySetResult(null);
                    }
                });
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
                tcs.TrySetResult(null);
            }
            return tcs.Task;
        }

        public List<WSSafetyAlert> GetSafetyAlerts(long hospitalId)
        {
            try
            {
                ResetResponseData();
                RestRequest request = new RestRequest(Method.GET);
                request.Resource = base.VigoServerResourcePrefix + "/hospital/" + hospitalId + "/safetyAlerts?rows=500&page=1";

                RestResponse response = this.RestClient.Execute(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    this.ResponseRawData = response.Content;
                    return WSDataObjectHelper.ListFromContentData<WSSafetyAlert>(this.ResponseRawData);
                }
                else
                {
                    FetchResponseError(response);
                }
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
            }
            return null;
        }

        public Task<List<WSChannel>> GetChannelLineupAsync(long hospitalId, long nursingUnitId = -1)
        {
            var tcs = new TaskCompletionSource<List<WSChannel>>();
            try
            {
                ResetResponseData();
                RestRequest request = new RestRequest(Method.GET);
                string url = base.VigoServerResourcePrefix + "/hospital/" + hospitalId + "/channelLineup?rows=500&page=1&sidx=name";
                if (nursingUnitId > 0)
                {
                    url += url + "&nursingUnitId=" + nursingUnitId;
                }
                request.Resource = url;
                this.RestClient.ExecuteAsync(request, (RestResponse response) =>
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        this.ResponseRawData = response.Content;
                        tcs.TrySetResult(WSDataObjectHelper.ListFromContentData<WSChannel>(this.ResponseRawData));
                    }
                    else
                    {
                        FetchResponseError(response);
                        tcs.TrySetResult(null);
                    }
                });
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
                tcs.TrySetResult(null);
            }
            return tcs.Task;
        }

        public List<WSChannel> GetChannelLineup(long hospitalId, long nursingUnitId = -1)
        {
            try
            {
                ResetResponseData();
                RestRequest request = new RestRequest(Method.GET);
                string url = base.VigoServerResourcePrefix + "/hospital/" + hospitalId + "/channelLineup?rows=500&page=1";
                if (nursingUnitId > 0) 
                {
                    url += url + "&nursingUnitId=" + nursingUnitId;
                }
                request.Resource = url;
                RestResponse response = this.RestClient.Execute(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    this.ResponseRawData = response.Content;
                    return WSDataObjectHelper.ListFromContentData<WSChannel>(this.ResponseRawData);
                }
                else
                {
                    FetchResponseError(response);
                }
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
            }
            return null;
        }
        #endregion

        #region stbs
        public WSInstalledSTB GetStbById(long stbId)
        {
            try
            {
                ResetResponseData();
                RestRequest request = new RestRequest(Method.GET);
                request.Resource = this.VigoServerResourcePrefix + "/stb/" + stbId;
                RestResponse response = this.RestClient.Execute(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    this.ResponseRawData = response.Content;
                    return WSDataObjectHelper.FromContentData<WSInstalledSTB>(this.ResponseRawData);
                }
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
            }
            return null;
        }

        public List<WSInstalledSTB> GetStbs(string query)
        {
            try
            {
                ResetResponseData();
                RestRequest request = new RestRequest(Method.GET);
                string service = this.VigoServerResourcePrefix + "/stbs?searchAcrossHospitals=false&sidx=room&rows=500&page=1&hideAtiInternal=false";
                if (!string.IsNullOrEmpty(query)) service += "&" + query;
                request.Resource = service;

                RestResponse response = this.RestClient.Execute(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    this.ResponseRawData = response.Content;
                    List<WSInstalledSTB> ret = WSDataObjectHelper.ListFromContentData<WSInstalledSTB>(this.ResponseRawData);
                    return ret;
                }
                else
                {
                    FetchResponseError(response);
                }
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
            }
            return null;
        }

        public Task<List<WSInstalledSTB>> GetStbsAsync(string query)
        {
            var tcs = new TaskCompletionSource<List<WSInstalledSTB>>();
            try
            {
                ResetResponseData();
                RestRequest request = new RestRequest(Method.GET);
                string service = this.VigoServerResourcePrefix + "/stbs?searchAcrossHospitals=false&sidx=room&rows=500&page=1&hideAtiInternal=false";
                if (!string.IsNullOrEmpty(query)) service += "&" + query;
                request.Resource = service;

                this.RestClient.ExecuteAsync(request, (RestResponse response) =>
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        this.ResponseRawData = response.Content;
                        tcs.TrySetResult(WSDataObjectHelper.ListFromContentData<WSInstalledSTB>(this.ResponseRawData));
                    }
                    else
                    {
                        FetchResponseError(response);
                        tcs.TrySetResult(null);
                    }
                });
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
                tcs.TrySetResult(null);
            }
            return tcs.Task;
        }

        public List<WSInstalledSTB> GetStbsByHospitalId(long hospitalId)
        {
            return GetStbs(hospitalId, 1000, 1);
        }

        public Task<List<WSInstalledSTB>> GetStbsByHospitalIdAsync(long hospitalId)
        {
            return GetStbsAsync(hospitalId, 1000, 1);
        }

        public List<WSInstalledSTB> GetStbs(long hospitalId, int rowsPerPage, int pageNum)
        {
            try
            {
                ResetResponseData();
                RestRequest request = new RestRequest(Method.GET);
                StringBuilder sb = new StringBuilder(base.VigoServerResourcePrefix + "/stbs?hospitalId=" + hospitalId);
                sb.Append("&rows=" + rowsPerPage);
                sb.Append("&page=" + pageNum);
                sb.Append("&hideAtiInternal=false");
                request.Resource = sb.ToString();

                RestResponse response = this.RestClient.Execute(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    this.ResponseRawData = response.Content;
                    return WSDataObjectHelper.ListFromContentData<WSInstalledSTB>(this.ResponseRawData);
                }
                else
                {
                    FetchResponseError(response);
                }
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
            }
            return null;
        }

        public List<WSAccount> GetAccounts(long hospitalId, int maxCount)
        {
            try
            {
                ResetResponseData();
                RestRequest request = new RestRequest(Method.GET);
                string service = this.VigoServerResourcePrefix + "/accounts?rows=" + maxCount + "&page=1&hospitalId=" + hospitalId;
                request.Resource = service;
                RestResponse response = this.RestClient.Execute(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    this.ResponseRawData = response.Content;
                    List<WSAccount> ret = WSDataObjectHelper.ListFromContentData<WSAccount>(this.ResponseRawData);
                    return ret;
                }
                else
                {
                    FetchResponseError(response);
                }
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
            }
            return null;
        }

        public async void UpdateStbsWithPatientAccountInfoAsync(List<WSInstalledSTB> stbs)
        {
            foreach (WSInstalledSTB stb in stbs)
            {
                long paid = stb.patientAccountId;
                if (paid > 0)
                {
                    WSPatient p = await GetPatientDataAsync(paid);
                    if (null != p)
                    {
                        // why do we swap these 2 fields
                        stb.patientMRN = p.adtAccountNumber;
                        stb.patientADTAccountNumber = p.adtMedicalRecordNumber;
                        stb.patientFirstName = p.firstName;
                        stb.patientLastName = p.lastName;
                    }
                }
            }
        }

        public Task<List<WSInstalledSTB>> GetStbsAsync(long hospitalId, int rowsPerPage, int pageNum)
        {
            var tcs = new TaskCompletionSource<List<WSInstalledSTB>>();
            try
            {
                ResetResponseData();
                RestRequest request = new RestRequest(Method.GET);
                StringBuilder sb = new StringBuilder(base.VigoServerResourcePrefix + "/stbs?hospitalId=" + hospitalId);
                sb.Append("&rows=" + rowsPerPage);
                sb.Append("&page=" + pageNum);
                sb.Append("&includePatientStatus=true");
                sb.Append("&hideAtiInternal=false");
                request.Resource = sb.ToString();

                this.RestClient.ExecuteAsync(request, (RestResponse response) =>
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        this.ResponseRawData = response.Content;
                        List<WSInstalledSTB> ret = WSDataObjectHelper.ListFromContentData<WSInstalledSTB>(this.ResponseRawData);
                        UpdateStbsWithPatientAccountInfoAsync(ret);
                        tcs.TrySetResult(ret);
                    }
                    else
                    {
                        FetchResponseError(response);
                        tcs.TrySetResult(null);
                    }
                });
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
                tcs.TrySetResult(null);
            }
            return tcs.Task;
        }

        public Task<List<WSSystemConfig>> GetSystemConfigurationsAsync()
        {
            var tcs = new TaskCompletionSource<List<WSSystemConfig>>();
            try
            {
                ResetResponseData();
                RestRequest request = new RestRequest(Method.GET);
                request.Resource = base.VigoServerResourcePrefix + "/stbs/systemConfigurations";

                this.RestClient.ExecuteAsync(request, (RestResponse response) =>
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        this.ResponseRawData = response.Content;
                        tcs.TrySetResult(WSDataObjectHelper.ListFromContentData<WSSystemConfig>(this.ResponseRawData));
                    }
                    else
                    {
                        FetchResponseError(response);
                        tcs.TrySetResult(null);
                    }
                });
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
                tcs.TrySetResult(null);
            }
            return tcs.Task;
        }

        public Task<List<WSClientRelease>> GetClientReleasesAsync()
        {
            var tcs = new TaskCompletionSource<List<WSClientRelease>>();
            try
            {
                ResetResponseData();
                RestRequest request = new RestRequest(Method.GET);
                request.Resource = base.VigoServerResourcePrefix + "/stbs/clientReleases";

                this.RestClient.ExecuteAsync(request, (RestResponse response) =>
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        this.ResponseRawData = response.Content;
                        tcs.TrySetResult(WSDataObjectHelper.ListFromContentData<WSClientRelease>(this.ResponseRawData));
                    }
                    else
                    {
                        FetchResponseError(response);
                        tcs.TrySetResult(null);
                    }
                });
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
                tcs.TrySetResult(null);
            }
            return tcs.Task;
        }

        public string SendStbRebootCommand(long stbId, string reason)
        {
            try
            {
                ResetResponseData();
                RestRequest request = new RestRequest(Method.POST);
                StringBuilder sb = new StringBuilder(base.VigoServerResourcePrefix + "/stb/reboot/" + stbId);
                request.Resource = sb.ToString();
                request.AddParameter("reason", reason);

                RestResponse response = this.RestClient.Execute(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    this.ResponseRawData = response.Content;
                    return this.ResponseRawData;
                }
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
            }
            return null;
        }

        public string SendStbPingCommand(long stbId)
        {
            try
            {
                ResetResponseData();
                RestRequest request = new RestRequest(Method.POST);
                StringBuilder sb = new StringBuilder(base.VigoServerResourcePrefix + "/stb/ping/" + stbId);
                request.Resource = sb.ToString();
                RestResponse response = this.RestClient.Execute(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    this.ResponseRawData = response.Content;
                    WSPingResponse r = WSDataObjectHelper.FromContentData<WSPingResponse>(this.ResponseRawData);
                    if (null != r)
                    {
                        return r.message;
                    }
                }
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
            }
            return null;
        }

        public DateTime GetLastStbHeartBeat(WSInstalledSTB stb)
        {
            DateTime ret = DateTime.MinValue;
            if (null != stb.lastBeat)
            {
                ret = stb.lastBeat;
            }

            if (ret.Equals(DateTime.MinValue))
            {
                try
                {
                    List<WSStbEvent> es = this.GetStbEvents("&stbId=" + stb.identity + "&category=SYSTEM&eventName=STARTUP&hospitalId=" + stb.hospitalId);
                    if (es.Count > 0)
                    {
                        ret = es[0].stbTimestamp;
                    }
                }
                catch
                {
                }
            }
            return ret;
        }

        public List<WSStbEvent> GetStbEvents(string query)
        {
            List<WSStbEvent> ret = new List<WSStbEvent>();
            try
            {
                ResetResponseData();
                RestRequest request = new RestRequest(Method.GET);
                StringBuilder sb = new StringBuilder(base.VigoServerResourcePrefix + "/admin/stbEvents?startDate=1%2F1%2F2010&endDate=12%2F31%2F2010&_search=true&nd=1284610939805&rows=1&page=1&sidx=stbTimestamp&sord=desc");
                if (!string.IsNullOrEmpty(query)) sb.Append("&" + query);
                request.Resource = sb.ToString();
                RestResponse response = this.RestClient.Execute(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    this.ResponseRawData = response.Content;
                    ret = WSDataObjectHelper.ListFromContentData<WSStbEvent>(this.ResponseRawData);
                }
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
            }
            return ret;
        }

        public List<WSTvType> GetTvTypes()
        {
            try
            {
                ResetResponseData();
                RestRequest request = new RestRequest(Method.GET);
                request.Resource = base.VigoServerResourcePrefix + "/stbs/tvTypes";

                RestResponse response = this.RestClient.Execute(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    this.ResponseRawData = response.Content;
                    return WSDataObjectHelper.ListFromContentData<WSTvType>(this.ResponseRawData);
                }
                else
                {
                    FetchResponseError(response);
                }
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
            }
            return null;
        }

        public Task<List<WSTvType>> GetTvTypesAsync()
        {
            var tcs = new TaskCompletionSource<List<WSTvType>>();
            try
            {
                ResetResponseData();
                RestRequest request = new RestRequest(Method.GET);
                request.Resource = base.VigoServerResourcePrefix + "/stbs/tvTypes";

                this.RestClient.ExecuteAsync(request, (RestResponse response) =>
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        this.ResponseRawData = response.Content;
                        tcs.TrySetResult(WSDataObjectHelper.ListFromContentData<WSTvType>(this.ResponseRawData));
                    }
                    else
                    {
                        FetchResponseError(response);
                        tcs.TrySetResult(null);
                    }
                });
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
                tcs.TrySetResult(null);
            }
            return tcs.Task;
        }

        public List<WSInstalledSTBType> GetStbTypes()
        {
            try
            {
                ResetResponseData();
                RestRequest request = new RestRequest(Method.GET);
                request.Resource = base.VigoServerResourcePrefix + "/stbs/stbTypes";

                RestResponse response = this.RestClient.Execute(request);
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    this.ResponseRawData = response.Content;
                    return WSDataObjectHelper.ListFromContentData<WSInstalledSTBType>(this.ResponseRawData);
                }
                else
                {
                    FetchResponseError(response);
                }
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
            }
            return null;
        }

        /*
        identity (no documentation provided)  form  
        switchPort (no documentation provided)  form  
        tvType (no documentation provided)  form  
        room (no documentation provided)  form  
        bed (no documentation provided)  form  
        adtRoom (no documentation provided)  form  
        adtBed (no documentation provided)  form  
        nursingUnit (no documentation provided)  form  
        disabled (no documentation provided)  form false 
        clientVersion (no documentation provided)  form  
        useOutboundConnect (no documentation provided)  form  
        oper (no documentation provided)  form 
        */
        public Task<WSInstalledSTB> UpdateStbAsync(WSInstalledSTB stb)
        {
            var tcs = new TaskCompletionSource<WSInstalledSTB>();
            try
            {
                RestRequest request = new RestRequest(Method.POST);
                request.Resource = base.VigoServerResourcePrefix + "/stb/update"; // "/stb/update";
                if (stb.identity > 0)
                {
                    request.AddParameter("identity", stb.identity);
                    request.AddParameter("oper", "edit");
                }
                else
                {
                    request.AddParameter("oper", "add");
                }

                request.AddParameter("switchPort", stb.switchPort);
                request.AddParameter("tvType", stb.tvTypeCode);
                request.AddParameter("room", stb.room);
                request.AddParameter("bed", stb.bed);
                request.AddParameter("nursingUnit", stb.nursingUnit);
                request.AddParameter("systemConfigurationId", stb.systemConfiguration.id);
                request.AddParameter("clientReleaseId", stb.clientRelease.id);

                if (!string.IsNullOrEmpty(stb.adtRoom))
                {
                    request.AddParameter("adtRoom", stb.adtRoom);
                }
                if (!string.IsNullOrEmpty(stb.adtBed))
                {
                    request.AddParameter("adtBed", stb.adtBed);
                }

                request.AddParameter("useOutboundConnect", "false");

                this.RestClient.ExecuteAsync(request, (RestResponse response) =>
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        this.ResponseRawData = response.Content;
                        tcs.TrySetResult(WSDataObjectHelper.FromContentData<WSInstalledSTB>(this.ResponseRawData));
                    }
                    else
                    {
                        FetchResponseError(response);
                        tcs.TrySetResult(null);
                    }
                });
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
                tcs.TrySetResult(null);
            }
            return tcs.Task;
        }

        #endregion

        #region accounts
        /// <summary>
        /// /account/{accountId}/{hospitalId}/updatePatientBoard
        /// </summary>
        /// <param name="careInfo"></param>
        /// <returns></returns>
        public WSCareInfo2 UpdateCareInfo2(WSCareInfo2 careInfo)
        {
            RestRequest request = new RestRequest(Method.POST);
            request.Resource = base.VigoServerResourcePrefix + "/account/" + careInfo.accountId + "/" + careInfo.hospitalId + "/updatePatientBoard";
            if (null != careInfo.fields)
            {
                foreach (WSField f in careInfo.fields)
                {
                    string fieldDescription = f.id + "^^" + (
                        (f.fieldType.Equals("SingleList", StringComparison.CurrentCultureIgnoreCase) || f.fieldType.Equals("MultiList", StringComparison.CurrentCultureIgnoreCase)) ? "-" : f.fieldValue);
                    request.AddParameter("fieldDescriptions", fieldDescription);
                }
            }

            ResetResponseData();
            RestResponse response = this.RestClient.Execute(request);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                this.ResponseRawData = response.Content;
                careInfo = WSDataObjectHelper.FromContentData<WSCareInfo2>(this.ResponseRawData);
                this.Error = string.Empty;
            }
            else
            {
                FetchResponseError(response);
            }
            return careInfo;
        }

        public WSCareInfo2 GetCareInfoConfig(long hospitalId)
        {
            WSCareInfo2 careInfo = null;

            RestRequest request = new RestRequest(Method.GET);
            request.Resource = base.VigoServerResourcePrefix + "/hospital/" + hospitalId + "/hospitalConfiguration";

            ResetResponseData();
            RestResponse response = this.RestClient.Execute(request);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                this.ResponseRawData = response.Content;
                careInfo = WSDataObjectHelper.FromContentData<WSCareInfo2>(this.ResponseRawData);
            }
            else
            {
                FetchResponseError(response);
            }
            return careInfo;
        }

        public WSCareInfo UpdateCareInfo(WSCareInfo careInfo)
        {
            this.ResponseRawData = string.Empty;

            RestRequest request = new RestRequest(Method.POST);
            request.Resource = base.VigoServerResourcePrefix + "/account/" + careInfo.accountId + "/updateCareInfo";
            request.AddParameter("doctorName", careInfo.doctorName);
            request.AddParameter("nurseName", careInfo.nurseName);
            request.AddParameter("nurseName2", careInfo.nurse2Name);
            request.AddParameter("notes", careInfo.notes);
            request.AddParameter("activities", careInfo.activities);
            request.AddParameter("hospitalIdFilter", careInfo.hospitalId);
            if (null != careInfo.safetyAlerts)
            {
                foreach (WSSafetyAlert sa in careInfo.safetyAlerts)
                {
                    request.AddParameter("safetyAlerts", sa.id);
                }
            }

            RestResponse response = this.RestClient.Execute(request);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                this.ResponseRawData = response.Content;
                careInfo = WSDataObjectHelper.FromContentData<WSCareInfo>(this.ResponseRawData);
            }
            else
            {
                FetchResponseError(response);
            }
            return careInfo;
        }

        public WSPatient GetPatientData(long accountId)
        {
            this.ResponseRawData = string.Empty;
            WSPatient ret = null;
            RestRequest request = new RestRequest(Method.GET);
            request.Resource = base.VigoServerResourcePrefix + "/account/" + accountId + "/patient";
            RestResponse response = this.RestClient.Execute(request);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                this.ResponseRawData = response.Content;
                ret = WSDataObjectHelper.FromContentData<WSPatient>(this.ResponseRawData);
            }
            else
            {
                FetchResponseError(response);
            }
            return ret;
        }

        public Task<WSPatient> GetPatientDataAsync(long accountId)
        {
            var tcs = new TaskCompletionSource<WSPatient>();
            try
            {
                this.ResponseRawData = string.Empty;
                WSPatient ret = null;
                RestRequest request = new RestRequest(Method.GET);
                request.Resource = base.VigoServerResourcePrefix + "/account/" + accountId + "/patient";
                this.RestClient.ExecuteAsync(request, (RestResponse response) =>
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        this.ResponseRawData = response.Content;
                        tcs.TrySetResult(WSDataObjectHelper.FromContentData<WSPatient>(this.ResponseRawData));
                    }
                    else
                    {
                        FetchResponseError(response);
                        tcs.TrySetResult(null);
                    }
                });
            }
            catch (Exception ex)
            {
                this.Error = ex.Message;
                tcs.TrySetResult(null);
            }
            return tcs.Task;
        }

        public WSPatient GetTabletPatientData(string macId)
        {
            this.ResponseRawData = string.Empty;
            WSPatient ret = null;
            RestRequest request = new RestRequest(Method.GET);
            request.Resource = base.VigoServerResourcePrefix + "/tabletClient/getPatient?macAddress=" + macId;
            RestResponse response = this.RestClient.Execute(request);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                this.ResponseRawData = response.Content;
                ret = WSDataObjectHelper.FromContentData<WSPatient>(this.ResponseRawData);
            }
            else
            {
                FetchResponseError(response);
            }
            return ret;
        }

        public string GetPatientEMRData(string hospitalName, string patientMrn)
        {
            this.ResponseRawData = string.Empty;
            RestRequest request = new RestRequest(Method.GET);
            request.Resource = base.VigoServerResourcePrefix + "/medical/" + patientMrn + "?hospitalName=" + hospitalName;
            RestResponse response = this.RestClient.Execute(request);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                this.ResponseRawData = response.Content;
            }
            else
            {
                FetchResponseError(response);
            }
            return this.ResponseRawData;
        }

        public Task<string> GetPatientEMRDataAsync(string hospitalName, string patientMrn)
        {
            var tcs = new TaskCompletionSource<string>();
            ResetResponseData();
            RestRequest request = new RestRequest(Method.GET);
            request.Resource = base.VigoServerResourcePrefix + "/medical/" + patientMrn + "?hospitalName=" + hospitalName;
            this.RestClient.ExecuteAsync(request, (RestResponse response) =>
            {
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    string data = response.Content;
                    this.ResponseRawData = data;
                    tcs.TrySetResult(data);
                }
                else
                {
                    FetchResponseError(response);
                    tcs.TrySetResult("{\"status\":\"ERROR\",\"message\":\"" + response.StatusCode + "\"}");
                }
            });
            return tcs.Task;
        }

        #endregion

        #region contents
        public Task<WSBinaryContent> DownloadContentByNameAsync(string name)
        {
            var tcs = new TaskCompletionSource<WSBinaryContent>();
            ResetResponseData();
            RestRequest request = new RestRequest(Method.GET);
            request.Resource = base.VigoServerResourcePrefix + "/content/download/byName/" + name + "/en";
            this.RestClient.ExecuteAsync(request, (RestResponse response) =>
            {
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    string data = response.Content;
                    this.ResponseRawData = data;
                    tcs.TrySetResult(WSDataObjectHelper.FromContentData<WSBinaryContent>(data));
                }
                else
                {
                    FetchResponseError(response);
                    tcs.TrySetResult(null);
                }
            });
            return tcs.Task;
        }
        #endregion
    }
}
