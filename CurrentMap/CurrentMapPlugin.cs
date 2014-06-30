using ManiaNet.DedicatedServer.XmlRpc.Methods;
using ManiaNet.DedicatedServer.XmlRpc.Structs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace ManiaNet.DedicatedServer.Controller.Plugins.CurrentMap
{
    [RegisterPlugin("CurrentMap", author: "zocka", name: "Current Map")]
    public class CurrentMapPlugin : ControllerPlugin
    {
        private ServerController controller;

        private Dictionary<string, string> currentMap = new Dictionary<string, string>();
        private string mxMessage = "$7f0>> Visit $<$z@Model.Name$> on $l[@Model.Url]Mania-Exchange.com$l";

        private string testML = @"<manialink version=""1""><label text=""hallo"" textcolor=""00f"" /></manialink>";

        private string testML2 = @"<manialink version=""1""><label text=""hallo2"" textcolor=""0f0"" posn=""0 10 0"" /></manialink>";

        private string widget = @"<frame posn=""110 90 5"">
        <!-- <quad bgcolor=""444C"" sizen=""50 20"" /> -->
        <label text=""@Model.Name"" textsize=""2"" scale=""0.9"" posn=""3 -3 1"" sizen=""44 4"" style=""TextValueSmallSm"" />

        <quad sizen=""3 3"" posn=""3 -7 1"" image=""file://@Model.Country"" />
        <label text=""@Model.Author"" textsize=""2"" scale=""0.9"" posn=""7 -7 1"" sizen=""44 4"" style=""TextCardSmallScores2"" />

        <quad style=""MedalsBig"" substyle=""MedalNadeo"" sizen=""3 3"" posn=""3 -11 1"" />
        <label text=""@Model.Time"" textsize=""2"" scale=""0.9"" posn=""7 -11 1"" sizen=""44 4"" style=""TextCardSmallScores2"" />
        <frame hidden=""@Model.HasMXRecord"">
            <quad image=""http://www.mania-exchange.com/Content/images/planet_mx_logo.png"" sizen=""3 3"" posn=""3 -15 1"" />
            <label text=""@Model.MXTime"" textsize=""2"" scale=""0.9"" posn=""7 -15 1"" sizen=""44 4"" style=""TextCardSmallScores2"" />
        </frame>
    </frame>";

        public override bool Load(ServerController controller)
        {
            Action<ManiaPlanetPlayerChat> unloadAction = playerChatCall => controller.CallMethod(new SendHideManialinkPage(), 1000);
            controller.RegisterCommand("unload", unloadAction);
            Action<ManiaPlanetPlayerChat> testMLAction = playerChatCall => controller.CallMethod(new SendDisplayManialinkPage(testML, 0, false), 1000);
            controller.RegisterCommand("testml", testMLAction);
            Action<ManiaPlanetPlayerChat> testMLAction2 = playerChatCall => controller.CallMethod(new SendDisplayManialinkPage(testML2, 1000, false), 1000);
            controller.RegisterCommand("testml2", testMLAction2);

            this.controller = controller;
            this.controller.BeginMap += controller_BeginMap;
            this.controller.PlayerCheckpoint += controller_PlayerCheckpoint;
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
            currentMap = getCurrentMap(methodCall.Map);
            Console.WriteLine("Received mapname " + methodCall.Map.Name);
            if (currentMap.ContainsKey("MX"))
                sender.CallMethod(new ChatSendServerMessage(mxMessage.Replace("@Model.Name", currentMap["Name"]).Replace("@Model.MX", currentMap["MX"])), 1000);
            if (currentMap.ContainsKey("Name"))
            {
                string widgetRendered = widget.Replace("@Model.Name", currentMap["Name"])
                                              .Replace("@Model.Country", currentMap["Country"])
                                              .Replace("@Model.Author", currentMap["Author"])
                                              .Replace("@Model.HasMXRecord", (currentMap.ContainsKey("MXTime") && currentMap["MXTime"] != null) ? "0" : "1")
                                              .Replace("@Model.Time", currentMap["Time"]);

                clientManialinks["*"] = widgetRendered;
            }
        }

        private void controller_EndMap(ServerController sender, ManiaPlanetEndMap methodCall)
        {
            clientManialinks.Remove("*");
        }

        private void controller_PlayerCheckpoint(ServerController sender, TrackManiaPlayerCheckpoint methodCall)
        {
            string msg = "CP #" + methodCall.CheckpointIndex.ToString() + ": " + methodCall.TimeOrScore.ToString();
            Console.WriteLine(msg);
            sender.CallMethod(new ChatSendToLogin(msg, methodCall.PlayerLogin), 1000);
        }

        private void displayWidget()
        {
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
                            return null;
                        result = JsonConvert.DeserializeObject<Dictionary<string, string>>(json.Substring(1, json.Length - 2));
                        result.Add("url", "tm.mania-exchange.com/tracks/" + result["TrackID"]);
                    }
                    success = true;
                    //var json = new WebClient().DownloadString(apiUrl);
                }
                catch { }
            }
            while (!success);

            return result;
        }

        private void onMapStart(MapInfoStruct mapData)
        {
            Dictionary<string, string> mx = mxLookup(mapData.UId);
            if (mx != null)
            {
                //mxMessage = Razor.Parse(mxMessage, new { Map = mapData.Name, mxLink = mx["url"] });
                controller.CallMethod(new ChatSendServerMessage(mxMessage), 1000);
            }
            displayWidget();
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