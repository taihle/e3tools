// ------------------------------------------------------------------------------
// Copyright (c) 2015 - Allen Technologies Inc. allentek.com. All Rights Reserved.
// Author: Tai H. Le <tle@allentek.com>
// 
// Web Service Data Object Definitions 
// ------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Newtonsoft.Json;

namespace Ati.VigoPC.WebServices.REST
{
    /// <summary>
    /// Based class for all REST client
    /// </summary>
    public class WSDataObjectHelper
    {
        private static JsonSerializerSettings _jss = null;

        private static JsonSerializerSettings JsonSerializerSettings
        {
            get
            {
                if (null == _jss)
                {
                    _jss = new JsonSerializerSettings();
                    _jss.NullValueHandling = NullValueHandling.Ignore;
                }
                return _jss;
            }
        }

        public static object Copy(object source)
        {
            string data = JsonConvert.SerializeObject(source, Formatting.None, WSDataObjectHelper.JsonSerializerSettings);
            object copy = JsonConvert.DeserializeObject(data, source.GetType(), WSDataObjectHelper.JsonSerializerSettings);
            return copy;
        }

        public static T FromContentData<T>(string data)
        {
            try
            {
                return (T)JsonConvert.DeserializeObject(data, typeof(T), WSDataObjectHelper.JsonSerializerSettings);
            }
            catch(Exception ex)
            {
                // Logger.Error("FromContentData(): Error - " + ex.Message + "\nDATA:\n" + data, ex);
            }
            return default(T);
        }

        public static string ToContentData(object o)
        {
            try
            {
                return JsonConvert.SerializeObject(o, WSDataObjectHelper.JsonSerializerSettings);
            }
            catch (Exception ex)
            {
                // Logger.Error("FromContentData(): Error - " + ex.Message + "\nDATA:\n" + data, ex);
            }
            return string.Empty;
        }

        public static List<T> ArrayFromContentData<T>(string data)
        {
            T[] ary = WSDataObjectHelper.FromContentData<T[]>(data);
            if (null != ary)
            {
                return new List<T>(ary);
            }
            return new List<T>();
        }

        public static List<T> ListFromContentData<T>(string data)
        {
            queryResults qr = WSDataObjectHelper.FromContentData<queryResults>(data);
            if (null == qr)
            {
                return ArrayFromContentData<T>(data);
            }

            List<T> ret = new List<T>();
            foreach (object obj in qr.rows)
            {
                try
                {
                    string edata = obj.ToString();
                    T p = WSDataObjectHelper.FromContentData<T>(edata);
                    if (null != p)
                    {
                        ret.Add(p);
                    }
                }
                catch
                {
                }
            }
            return ret;
        }
    }

    public class SurveyQuestionTypes
    {
        public static string DISPLAY = "DISPLAY";
        public static string SINGLE = "SINGLE";
        public static string MULTIPLE = "MULTI";
        public static string TEXT = "TEXT";
    }
}
