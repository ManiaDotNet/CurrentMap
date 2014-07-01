using ManiaNet.DedicatedServer.XmlRpc.Methods;
using ManiaNet.DedicatedServer.XmlRpc.Structs;
using Newtonsoft.Json;
using RazorEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;

namespace ManiaNet.DedicatedServer.Controller.Plugins.CurrentMap
{
    [RegisterPlugin("CurrentMap", author: "zocka", name: "Current Map")]
    public class CurrentMapPlugin : ControllerPlugin
    {
        private readonly string widget = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("ManiaNet.DedicatedServer.Controller.Plugins.CurrentMap.Widget.csxml")).ReadToEnd();
        private Dictionary<string, string> currentMap = new Dictionary<string, string>();
        private string mxMessage = "$7f0>> Visit $<$z@Model.Name$> on $l[@Model.Url]Mania-Exchange$l!";

        public override bool Load(ServerController controller)
        {
            controller.BeginMap += controller_BeginMap;

            GetCurrentMapInfo getCurrentMapInfoCall = new GetCurrentMapInfo();
            if (controller.CallMethod(getCurrentMapInfoCall, 1000) && !getCurrentMapInfoCall.HadFault)
                displayWidget(controller, getCurrentMapInfoCall.ReturnValue);

            return true;
        }

        public override void Run()
        {
        }

        public override bool Unload()
        {
            return true;
        }

        private void controller_BeginMap(ServerController sender, ManiaPlanetBeginMap methodCall)
        {
            displayWidget(sender, methodCall.Map);
        }

        private void controller_EndMap(ServerController sender, ManiaPlanetEndMap methodCall)
        {
            clientManialinks.Clear();
        }

        private void displayWidget(ServerController controller, MapInfoStruct map)
        {
            currentMap = getCurrentMap(map);

            if (currentMap.ContainsKey("Name"))
            {
                if (currentMap.ContainsKey("MX"))
                    controller.CallMethod(new ChatSendServerMessage(mxMessage.Replace("@Model.Name", currentMap["Name"]).Replace("@Model.Url", currentMap["MX"])), 1000);

                clientManialinks["*"] = WebUtility.HtmlDecode(Razor.Parse(widget, currentMap, "currentMapWidget"));
                onClientManialinksChanged();
            }
        }

        private Dictionary<string, string> getCurrentMap(MapInfoStruct map)
        {
            Dictionary<string, string> data = new Dictionary<string, string>();
            if (map != null)
            {
                Dictionary<string, string> mx = mxLookup(map.UId);
                //Dictionary<string, string> mx = null;
                Dictionary<string, string> author = userLookup(map.Author);
                data.Add("Author", author["nickname"]);
                data.Add("Name", map.Name);
                data.Add("Time", Tools.FormatMilliseconds(map.AuthorTime));
                data.Add("Country", ManiaPlanet.Nations.GetNation(author["path"]).AvatarName);
                if (mx != null)
                {
                    data.Add("MX", mx["url"]);
                    if (!string.IsNullOrEmpty(mx["ReplayWRTime"]))
                        data.Add("MXTime", mx["ReplayWRTime"]);
                }
            }
            else
            {
                Console.WriteLine("received map struct was null");
            }
            return data;
        }

        private Dictionary<string, string> mxLookup(string uid)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            bool success = false;

            do
            {
                try
                {
                    string apiUrl = "http://api.mania-exchange.com/tm/tracks/" + uid;
                    using (WebClient client = new WebClient())
                    {
                        client.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/535.2 (KHTML, like Gecko) Chrome/15.0.874.121 Safari/535.2";
                        string json = client.DownloadString(apiUrl);

                        if (string.IsNullOrWhiteSpace(json))
                            return result;

                        result = JsonConvert.DeserializeObject<Dictionary<string, string>>(json.Substring(1, json.Length - 2));
                        result.Add("url", "tm.mania-exchange.com/tracks/" + result["TrackID"]);
                    }
                    success = true;
                }
                catch { }
            }
            while (!success);

            return result;
        }

        private Dictionary<string, string> userLookup(string account)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            bool success = false;

            do
            {
                try
                {
                    string apiUrl = "http://prprod.de/mpnicks.php?player=" + account;
                    var json = new WebClient().DownloadString(apiUrl);

                    if (string.IsNullOrWhiteSpace(json))
                    {
                        result.Add("nickname", account);
                        result.Add("path", string.Empty);
                    }
                    else
                    {
                        result = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    }

                    success = true;
                }
                catch { }
            }
            while (!success);

            return result;
        }
    }
}