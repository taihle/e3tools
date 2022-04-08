// ------------------------------------------------------------------------------
// Copyright (c) 2015 - Allen Technologies Inc. allentek.com. All Rights Reserved.
// Author: Tai H. Le <tle@allentek.com>
// 
// Handle web socket messages 
// ------------------------------------------------------------------------------
using Ati.VigoPC.WebServices.REST;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;

namespace e3lib
{
    public class JsonConvertSettings 
    {
        public static JsonSerializerSettings Instance = new JsonSerializerSettings
        {
            Error = delegate(object sender, ErrorEventArgs args)
            {
                // errors.Add(args.ErrorContext.Error.Message);
                args.ErrorContext.Handled = true;
            },
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore
        }; 
    }

    public class E3WSMessage
    {
        public string target_id { get; set; }
        public string source_id { get; set; }
        public string msg { get; set; }
        public object data { get; set; }

        public string ToJson()
        {
            return E3WSMessage.ToJson(this);
        }

        public static E3WSMessage FromJson(string txt)
        {
            return JsonConvert.DeserializeObject(txt, typeof(E3WSMessage), JsonConvertSettings.Instance) as E3WSMessage;
        }

        public static string ToJson(E3WSMessage obj)
        {
            return JsonConvert.SerializeObject(obj, JsonConvertSettings.Instance);
        }
    }

    public class E3WSConst
    {
        public static string WS_CLIENT_TYPE_REMOTE = "remote";
        public static string WS_CLIENT_TYPE_STB = "stb";
        public static string WS_CLIENT_TYPE_DISPLAY = "display";
    }

    public class E3WSClient
    {
        public string id { get; set; }
        public string type { get; set; } // of of the E3WSConst.WS_CLIENT_TYPE_XXX above
        public string status { get; set; }

        public string ToJson()
        {
            return E3WSClient.ToJson(this);
        }

        public static E3WSClient FromJson(string txt)
        {
            E3WSClient ret = null;
            try {
                ret = JsonConvert.DeserializeObject(txt, typeof(E3WSClient), JsonConvertSettings.Instance) as E3WSClient;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return ret;
        }

        public static string ToJson(E3WSClient obj)
        {
            return JsonConvert.SerializeObject(obj);
        }
    }

    public class E3WSStatusData
    {
        public string state { get; set; }
        public string power { get; set; }
        public int uptime { get; set; }
        public long idletime { get; set; }
        public string status { get; set; }
        public string stb { get; set; }

        private WSInstalledSTB _stb = null;
        public WSInstalledSTB InstalledStb 
        { 
            get 
            {
                if (_stb == null)
                {
                    ConvertStbData();
                }
                return _stb;
            }
        }

        private void ConvertStbData() {
            try
            {
                _stb = null;
                string txt = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(stb));
                _stb = JsonConvert.DeserializeObject(txt, typeof(WSInstalledSTB), JsonConvertSettings.Instance) as WSInstalledSTB;
            }
            catch(Exception){}
        }

        public string ToJson()
        {
            return E3WSStatusData.ToJson(this);
        }

        public static E3WSStatusData FromJson(string txt)
        {
            E3WSStatusData ret = null;
            try
            {
                ret = JsonConvert.DeserializeObject(txt, typeof(E3WSStatusData), JsonConvertSettings.Instance) as E3WSStatusData;
                if (null != ret) ret.ConvertStbData();
            }
            catch (Exception ex) 
            {
                Console.WriteLine(ex.Message);
            }
            return ret;
        }

        public static string ToJson(E3WSStatusData obj)
        {
            return JsonConvert.SerializeObject(obj, JsonConvertSettings.Instance);
        }
    }
}
