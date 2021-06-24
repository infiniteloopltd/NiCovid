using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace SimplyBook
{
    class Program
    {
        static void Main(string[] args)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; 
            var lPharmacies = GetPharmacyMap();
            foreach (var pharmacyUrl in lPharmacies)
            {
                 var http = new HTTPRequest();
                var pharmacyHtml = http.Request(pharmacyUrl + "/v2/");
                const string csrfRegex = @"csrf_token...(?<CSRF>\w+)";
                var csrf = Regex.Match(pharmacyHtml, csrfRegex).Groups["CSRF"].Value;
                var queryUrl = pharmacyUrl +
                               "/v2/booking/working-days/?from=2021-06-28&to=2021-08-08&provider=any&service=1&location=&category=&booking_id=";
                const string strInfoRegex = @"ld.json.[>](?<Info>.*?)[<]/script";
                var info = Regex.Match(pharmacyHtml, strInfoRegex).Groups["Info"].Value;
                if (info == "")
                {
                    Console.WriteLine("Blocked?  " + pharmacyUrl);
                    continue;
                }
                var jInfo = JObject.Parse(info);
                HTTPHeaderHandler wicket = nvc =>
                {
                    var nvcSArgs = new NameValueCollection
                    {
                        {"X-Csrf-Token",csrf},
                        {"X-Requested-With", "XMLHttpRequest"}
                    };
                    return nvcSArgs;
                };
                http.HeaderHandler = wicket;

                var jsonServices = http.Request(pharmacyUrl + "/v2/service/");
                var jServices = JArray.Parse(jsonServices);
                if (jServices.Count == 0)
                {
                    Console.WriteLine("No services at  " + pharmacyUrl);
                    continue;
                }

           


                var jsonWorkingDays = http.Request(queryUrl);
                var jWorking = JArray.Parse(jsonWorkingDays);
                if (jWorking.Any(w => w["is_day_off"].ToString().ToLower() != "true"))
                {
                    // WOOP!
                    Console.WriteLine("Possible vacancy at " + pharmacyUrl);
                    if (jInfo["location"] != null && jInfo["location"]["addressLocality"] != null)
                    {
                        Console.WriteLine(" In " + jInfo["location"]["addressLocality"]);
                    }
                }
                else
                {
                    Console.WriteLine("No working days  " + pharmacyUrl);
                }

            }
        }



        public static List<string> GetPharmacyMap()
        {
            var html = File.ReadAllText("simplybook.html");
            const string strWebRegex = @"https://\w+.simplybook.cc";
            var lWeb = Regex.Matches(html, strWebRegex).Cast<Match>().Select(m => m.Value.Trim()).ToList();
            return lWeb;
        }

    }
}
