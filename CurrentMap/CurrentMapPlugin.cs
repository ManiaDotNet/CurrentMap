using System;
using System.Collections.Generic;
using System.Linq;
using ManiaNet.DedicatedServer.XmlRpc.MethodCalls;
using ManiaNet.DedicatedServer.XmlRpc.Structs;
using System.Net;
using Newtonsoft.Json;
using XmlRpc.MethodCalls;
using XmlRpc.Types.Structs;
using XmlRpc.Types;
using RazorEngine;

namespace ManiaNet.DedicatedServer.Controller.Plugins.CurrentMap
{
    [RegisterPlugin("CurrentMap", author: "zocka", name: "Current Map")]
    class CurrentMapPlugin: ControllerPlugin
    {
        private ServerController controller;

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
            return true;
        }

        public override void Run()
        {
        }

        public override bool Unload()
        {
            return true;
        }

        private void currentMap()
        {
            MethodCall<XmlRpcStruct<MapInfoStruct>, MapInfoStruct> call = new GetCurrentMapInfo();
            controller.CallMethod(call, 1000);
            if (!call.HadFault)
            {
                MapInfoStruct result = call.ReturnValue;
                Dictionary<string, string> mx = mxLookup(result.UId);
                if (mx != null)
                {
                    var data = new { Author = result.Author, Name = result.Name, Time = result.AuthorTime, Country = "GER", MX = mx["url"] };
                }
                else
                {
                    var data = new { Author = result.Author, Name = result.Name, Time = result.AuthorTime, Country = "GER" };
                }
            }
            
        }
        private void onMapStart(MapInfoStruct mapData)
        {
            Dictionary<string, string> mx = mxLookup(mapData.UId);
            if (mx != null)
            {
                mxMessage = Razor.Parse(mxMessage, new { Map = mapData.Name, mxLink = mx["url"] });
                controller.CallMethod(new ChatSendServerMessage(mxMessage), 1000);
            }
            displayWidget();
        }

        private void displayWidget()
        {
            
        }

        private Dictionary<string, string> mxLookup(string uid)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            string apiUrl = "http://api.mania-exchange.com/tm/tracks/" + uid;
            var json = new WebClient().DownloadString(apiUrl);
            if (json == "")
                return null;
            result = JsonConvert.DeserializeObject<Dictionary<string, string>>(json.Substring(1, json.Length - 2));
            result.Add("url", "tm.mania-exchange.com/tracks/" + result["TrackID"]);
            return result;
        }
    }
}
