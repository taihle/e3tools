// ------------------------------------------------------------------------------
// Copyright (c) 2015 - Allen Technologies Inc. allentek.com. All Rights Reserved.
// Author: Tai H. Le <tle@allentek.com>
// 
// Web Service Helper
// ------------------------------------------------------------------------------
using System;
using System.Threading; 
using System.Net; 
using System.IO; 

namespace Ati.VigoPC.WebServices.REST
{
    public class WebRequestHelper
    {
        private void button1_Click(object sender, EventArgs e)
        {
            Thread t = new Thread(MakeRequest); 
            t.IsBackground = true; 
            t.Start();
        }

        private void MakeRequest()
        {
            string uri = string.Empty;
            WebRequest request = HttpWebRequest.Create(uri);
            request.Method = "GET";
            object data = new object();
            RequestState state = new RequestState(request, data, uri);
            IAsyncResult result = request.BeginGetResponse(new AsyncCallback(UpdateItem), state);
            ThreadPool.RegisterWaitForSingleObject(result.AsyncWaitHandle, new WaitOrTimerCallback(ScanTimeoutCallback), state, (30 * 1000), true);
        }

        private void UpdateItem(IAsyncResult result)
        {
            String str;
            RequestState state = (RequestState)result.AsyncState;
            WebRequest request = (WebRequest)state.Request;
            HttpWebResponse response = (HttpWebResponse)request.EndGetResponse(result);
            Stream s = (Stream)response.GetResponseStream();
            StreamReader readStream = new StreamReader(s);
            string dataString = readStream.ReadToEnd();
            response.Close();
            s.Close();
            readStream.Close();
            string lastMod = String.Empty;
            if (response.Headers["last-modified"] != null)
            {
                lastMod = response.Headers["last-modified"];
            }
            str = "Read: " + state.SiteUrl + ": " + response.ContentLength.ToString() + " bytes. Last-Mod: " + lastMod; 
            Thread.Sleep(400); 
            // Invoke(t_DelegateAddString, new Object[] { str });
        }

        private static void ScanTimeoutCallback(object state, bool timedOut)
        {
            if (timedOut)
            {
                RequestState reqState = (RequestState)state;
                if (reqState != null) reqState.Request.Abort();
                // MessageBox.Show("aborted- timeout");
            }
        }
    }

    class RequestState
    {
        public WebRequest Request; // holds the request 
        public object Data; // store any data in this 
        public string SiteUrl; // holds the UrlString to match up results (Database lookup, etc). 

        public RequestState(WebRequest request, object data, string siteUrl)
        {
            this.Request = request; this.Data = data; this.SiteUrl = siteUrl;
        }
    }
}

