using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;


/// <summary>
/// Used for overriding default HTTP Stream reading in HTTPRequest (ReadToEnd)
/// </summary>
public delegate string HTTPStreamHandler(StreamReader httpstream);

/// <summary>
/// Used for overriding default Header handling
/// </summary>
public delegate NameValueCollection HTTPHeaderHandler(WebHeaderCollection headers);

/// <summary>
/// Summary description for HTTPRequest.
/// </summary>
public class HTTPRequest
{
    public string OverrideHost = "";

    public string OverrideUserAgent = "";

    public string OverrideAccept = "";

    public Version OverrideProtocolVersion = null;

    /// <summary>
    /// Raw Response from web requests
    /// </summary>
    public HttpWebResponse HttpResponse;

    /// <summary>
    /// Username and password to be passed through via HTTP authentication
    /// </summary>
    public ICredentials Credentials;

    /// <summary>
    /// Used for overriding default HTTP Stream reading in HTTPRequest (ReadToEnd)
    /// </summary>
    public HTTPStreamHandler StreamHandler;

    /// <summary>
    /// Used for overriding default HTTP Header handling
    /// </summary>
    public HTTPHeaderHandler HeaderHandler;

    /// <summary>
    /// This is an advanced setting that needs to be set false for Rumbo, but no others.
    /// http://haacked.com/archive/2004/05/15/449.aspx for more info
    /// </summary>
    public bool Expect100Continue = true;

    /// <summary>
    /// This is an advanced setting that needs to be set false for MuchoViaje, but no others.
    /// </summary>
    public bool AllowAutomaticRedirect = true;

    /// <summary>
    /// Overridden in the case of Atrapalo, where it expects txt/xml
    /// </summary>
    public string ContentType = "application/x-www-form-urlencoded";

    /// <summary>
    /// Determines if the Url should be read with Latin or UTF8 encoding
    /// </summary>
    public Encoding Encoder = Encoding.GetEncoding("iso8859-1");

    /// <summary>
    /// The HTTP timeout, default 90 seconds
    /// </summary>
    public TimeSpan TimeOut = new TimeSpan(0, 1, 30);

    /// <summary>
    /// received cookies
    /// </summary>
    public CookieContainer RequestCookies;

    /// <summary>
    /// Referring website
    /// </summary>
    public string Referer;

    /// <summary>
    /// URL 
    /// </summary>
    public string URL = "";
    /// <summary>
    /// URI
    /// </summary>
    public Uri PageUri;

    /// <summary>
    /// Proxy Server for this request object
    /// </summary>
    private IWebProxy proxy;



    /// <summary>
    /// Constructor
    /// </summary>
    public HTTPRequest()
    {
        RequestCookies = new CookieContainer();
        Referer = "http://www.google.com";
        PageUri = new Uri("about:blank");
        // Increase the connection limit for concurrent HTTP requests to the same server
        // http://blogs.msdn.com/jpsanders/archive/2009/05/20/understanding-maxservicepointidletime-and-defaultconnectionlimit.aspx           
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HTTPRequest"/> class.
    /// </summary>
    /// <param name="proxy">The proxy.</param>
    public HTTPRequest(IWebProxy proxy)
        : this()
    {
        this.proxy = proxy;
    }

    /// <summary>
    /// Get/set the proxy used for this HTTPRequest object
    /// </summary>
    public IWebProxy Proxy
    {
        set { proxy = value; }
    }

    /// <summary>
    /// Simple get request
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    public string Request(string url)
    {
        return GetHttp(url);
    }


    /// <summary>
    /// GET or POST request
    /// </summary>
    /// <param name="url">url</param>
    /// <param name="method">GET or POST</param>
    /// <param name="postdata">data to post</param>
    /// <returns>html</returns>
    public string Request(string url, string method, string postdata)
    {
        string pagesource = "";
        switch (method)
        {
            case "GET":
                pagesource = GetHttp(url);
                break;
            case "POST":
                pagesource = PostHttp(url, postdata, "POST");
                break;
            default:
                pagesource = PostHttp(url, postdata, method);
                break;
        }
        return pagesource;
    }

    /// <summary>
    /// Simple GET Request
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    private string GetHttp(string url)
    {
        HttpWebRequest httprequest;
        Uri responseUri;
        string bodytext = "";
        try
        {
            Uri requestUri = new Uri(url);
            httprequest = (HttpWebRequest)WebRequest.Create(requestUri);
            httprequest.AllowAutoRedirect = AllowAutomaticRedirect;
            httprequest.MaximumAutomaticRedirections = 10;
            httprequest.Method = "GET";
            httprequest.ProtocolVersion = new Version(1, 0);
            if (OverrideProtocolVersion != null) httprequest.ProtocolVersion = OverrideProtocolVersion;
            httprequest.Timeout = (int)TimeOut.TotalSeconds * 1000;
            Random random = new Random();
            int randomNumber = random.Next(100, 1000);
            httprequest.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/49.0." + randomNumber + ".87 Safari/537.36";
            if (OverrideUserAgent != "") httprequest.UserAgent = OverrideUserAgent;
            httprequest.KeepAlive = false;
            httprequest.SendChunked = false;
            httprequest.CookieContainer = RequestCookies;
            httprequest.Referer = Referer;
            httprequest.Headers.Add(HttpRequestHeader.AcceptLanguage, "en-gb");
            httprequest.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip, deflate");
            httprequest.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
            if (OverrideAccept != "")
            {
                httprequest.Accept = OverrideAccept;
            }
            httprequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            if (OverrideHost != "") httprequest.Host = OverrideHost;
            if (HeaderHandler != null)
            {
                httprequest.Headers.Add(HeaderHandler(httprequest.Headers));
            }
            if (Credentials != null)
            {
                // Shaves a second off NTLM authenticated requests
                httprequest.PreAuthenticate = true;
                httprequest.Credentials = Credentials;
            }

            if (proxy != null)
                httprequest.Proxy = proxy;



            string strError = "";
            try
            { 
                HttpResponse = (HttpWebResponse)httprequest.GetResponse();
            }
            catch (Exception e)
            {
                WebException wexError = e as WebException;
                if (wexError != null)
                {
                    WebResponse wrError = wexError.Response;
                    Stream stmError = wrError.GetResponseStream();
                    StreamReader srError = new StreamReader(stmError);
                    strError = srError.ReadToEnd();
                    srError.Close();

                }
            }
            if (HttpResponse == null)
            {
                return strError;
            }
            responseUri = HttpResponse.ResponseUri;
            Referer = responseUri.AbsoluteUri;
            URL = responseUri.AbsoluteUri;
            PageUri = responseUri;
            Stream responsestream = HttpResponse.GetResponseStream();
            StreamReader httpstream = new StreamReader(responsestream, Encoder);
            bodytext = StreamHandler == null ? httpstream.ReadToEnd() : StreamHandler(httpstream);
            httpstream.Close();
            HttpResponse.Close();
        }
        catch (Exception ex)
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Debug.Write(ex);
            }
        }

        return bodytext;
    }

    /// <summary>
    /// Simple post
    /// </summary>
    /// <param name="url">url</param>
    /// <param name="postdata">data to post</param>
    /// <returns></returns>
    private string PostHttp(string url, string postdata, string verb)
    {
        Uri requestUri = new Uri(url);
        HttpWebRequest httprequest = (HttpWebRequest)WebRequest.Create(requestUri);    
        httprequest.AllowAutoRedirect = AllowAutomaticRedirect;
        httprequest.MaximumAutomaticRedirections = 10;
        httprequest.Method = verb;
        httprequest.ServicePoint.Expect100Continue = Expect100Continue;
        httprequest.ContentType = ContentType;
        httprequest.ContentLength = postdata.Length;
        httprequest.KeepAlive = false;
        httprequest.SendChunked = false;
        httprequest.CookieContainer = RequestCookies;
        httprequest.Referer = Referer;
        httprequest.Headers.Add(HttpRequestHeader.AcceptLanguage, "en-gb");
        httprequest.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip, deflate");
        httprequest.Accept = "application/json, text/javascript, */*; q=0.01"; // HACK 
        httprequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        if (Credentials != null)
        {
            // Shaves a second off NTLM authenticated requests
            httprequest.PreAuthenticate = true;
            httprequest.Credentials = Credentials;
        }
        if (OverrideHost != "") httprequest.Host = OverrideHost;
        if (HeaderHandler != null)
        {
            httprequest.Headers.Add(HeaderHandler(httprequest.Headers));
        }
        httprequest.Timeout = (int)TimeOut.TotalMilliseconds;
        httprequest.ProtocolVersion = new Version(1, 1);

        Random random = new Random();
        int randomNumber = random.Next(100, 1000);
        httprequest.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/49.0." + randomNumber + ".87 Safari/537.36";
        if (OverrideUserAgent != "") httprequest.UserAgent = OverrideUserAgent;
        if (proxy != null)
            httprequest.Proxy = proxy;

        if (verb != "HEAD")
        {
            var requestStream = httprequest.GetRequestStream();
            requestStream.Write(Encoding.ASCII.GetBytes(postdata), 0, postdata.Length);
            requestStream.Close();
        }

        string strError = "";
        try
        { 
            HttpResponse = httprequest.GetResponse() as HttpWebResponse;
        }
        catch (Exception e)
        {
            WebException wexError = e as WebException;
            if (wexError != null && wexError.Response != null)
            {
                WebResponse wrError = wexError.Response;
                Stream stmError = wrError.GetResponseStream();
                StreamReader srError = new StreamReader(stmError);
                strError = srError.ReadToEnd();
                srError.Close();
            }
        }
        if (HttpResponse == null)
        {
            return strError;
        }
        Uri responseURI = HttpResponse.ResponseUri;
        Referer = responseURI.AbsoluteUri;
        URL = responseURI.AbsoluteUri;
        PageUri = responseURI;
        Stream sResponse = HttpResponse.GetResponseStream();
        string bodytext = "";
        if (sResponse != null && sResponse.CanRead)
        {
            StreamReader httpstream = new StreamReader(sResponse, Encoder);
            bodytext = httpstream.ReadToEnd();
            httpstream.Close();
        }
        HttpResponse.Close();
        return bodytext;
    }

    /// <summary>
    /// Downloads a file, maintaining cookie security
    /// </summary>
    /// <param name="url">The URL.</param>
    /// <returns>A local FileName</returns>
    public byte[] DownloadFile(string url)
    {
        byte[] data = null;
        try
        {
            var requestUri = new Uri(url);
            var httprequest = (HttpWebRequest)WebRequest.Create(requestUri);
            httprequest.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
            httprequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            httprequest.AllowAutoRedirect = AllowAutomaticRedirect;
            httprequest.MaximumAutomaticRedirections = 10;
            httprequest.Method = "GET";
            httprequest.ProtocolVersion = HttpVersion.Version11;
            httprequest.Timeout = (int)TimeOut.TotalSeconds * 1000;
            Random random = new Random();
            int randomNumber = random.Next(100, 1000);
            httprequest.UserAgent = "Mozilla/5.0 (Windows NT 10.0; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/49.0." + randomNumber + ".87 Safari/537.36";
            if (OverrideUserAgent != "") httprequest.UserAgent = OverrideUserAgent;
            if (proxy != null)
                httprequest.Proxy = proxy;
            httprequest.KeepAlive = false;
            httprequest.SendChunked = false;
            httprequest.CookieContainer = RequestCookies;
            httprequest.Referer = Referer;
            httprequest.Headers.Add("Accept-Language", "en-gb");
            if (HeaderHandler != null)
            {
                httprequest.Headers.Add(HeaderHandler(httprequest.Headers));
            }

            HttpResponse = (HttpWebResponse)httprequest.GetResponse();
            var responseUri = HttpResponse.ResponseUri;
            Referer = responseUri.AbsoluteUri;
            URL = responseUri.AbsoluteUri;
            PageUri = responseUri;
            var responsestream = HttpResponse.GetResponseStream();
            var ms = new MemoryStream();
            if (responsestream != null) responsestream.CopyTo(ms);
            data = ms.ToArray();
            HttpResponse.Close();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.Write(ex);
        }
        return data;
    }

    /// <summary>
    /// Clear cookies
    /// </summary>
    public void ResetCookies()
    {
        RequestCookies = new CookieContainer();
    }
}

//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.296
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------



// 
// This source code was auto-generated by xsd, Version=4.0.30319.1.
// 
