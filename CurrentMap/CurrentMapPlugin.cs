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

        private string widget = @"<manialink version=""1"">
    <frame posn=""110 90 5"">
        <!-- <quad bgcolor=""444C"" sizen=""50 20"" /> -->
        <label text=""@Model.Name"" textsize=""2"" scale=""0.9"" posn=""3 -3 1"" sizen=""44 4"" style=""TextValueSmallSm"" />

        <quad sizen=""3 3"" posn=""3 -7 1"" image=""file://Skins/Avatars/Flags/<% Model.Country %>.dds"" />
        <label text=""@Model.Author"" textsize=""2"" scale=""0.9"" posn=""7 -7 1"" sizen=""44 4"" style=""TextCardSmallScores2"" />

        <quad style=""MedalsBig"" substyle=""MedalNadeo"" sizen=""3 3"" posn=""3 -11 1"" />
        <label text=""@Model.Time"" textsize=""2"" scale=""0.9"" posn=""7 -11 1"" sizen=""44 4"" style=""TextCardSmallScores2"" />
    </frame>
</manialink>";

        public override bool Load(ServerController controller)
        {
            this.controller = controller;
            this.controller.BeginMap += controller_BeginMap;
            this.controller.PlayerCheckpoint += controller_PlayerCheckpoint;
            this.controller.BeginMatch += controller_BeginMatch;
            this.controller.PlayerChat += controller_PlayerChat;
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
        }

        private void controller_BeginMatch(ServerController sender, ManiaPlanetBeginMatch methodCall)
        {
            GetCurrentMapInfo call = new GetCurrentMapInfo();

            Console.WriteLine(sender.CallMethod(call, 3000).ToString()); //False

            if (!call.HadFault)
            {
                if (call.ReturnValue != null)
                {
                    currentMap = getCurrentMap(call.ReturnValue);
                    Console.WriteLine("Received mapname " + call.ReturnValue.Name);
                }
                else
                {
                    Console.WriteLine("GetCurrentMapInfo returned null");
                }
            }
            else
            {
                Console.WriteLine("GetCurrentMapInfo had a fault");
            }
            if (currentMap.ContainsKey("MX"))
                sender.CallMethod(new ChatSendServerMessage(mxMessage.Replace("@Model.Name", currentMap["Name"]).Replace("@Model.MX", currentMap["MX"])), 1000);
            if (currentMap.ContainsKey("name"))
                Console.WriteLine(currentMap["name"] + " by " + currentMap["Author"]);
        }

        private void controller_PlayerChat(ServerController sender, ManiaPlanetPlayerChat methodCall)
        {
            // test without beginning match
            controller_BeginMatch(sender, null);
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
                Dictionary<string, string> author = userLookup(map.Author);
                data.Add("Author", author["nickname"]);
                data.Add("Name", map.Name);
                data.Add("Time", Tools.FormatMilliseconds(map.AuthorTime));
                data.Add("Country", "other");
                if (mx != null)
                {
                    data.Add("MX", mx["url"]);
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
            try
            {
                Dictionary<string, string> result = new Dictionary<string, string>();
                string apiUrl = "http://api.mania-exchange.com/tm/tracks/" + uid;
                var json = new WebClient().DownloadString(apiUrl);
                if (string.IsNullOrWhiteSpace(json))
                    return null;
                result = JsonConvert.DeserializeObject<Dictionary<string, string>>(json.Substring(1, json.Length - 2));
                result.Add("url", "tm.mania-exchange.com/tracks/" + result["TrackID"]);
                return result;
            }
            catch { return null; }
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
            string apiUrl = "http://prprod.de/mpnicks.php?player=" + account;
            var json = new WebClient().DownloadString(apiUrl);
            if (string.IsNullOrWhiteSpace(json))
            {
                result.Add("nickname", account);
            }
            else
            {
                result = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            }
            return result;
        }
    }
}