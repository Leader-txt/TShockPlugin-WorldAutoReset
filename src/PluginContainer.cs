using Microsoft.Xna.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.Utilities;
using Terraria.WorldBuilding;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;

namespace WorldReset
{
    [ApiVersion(2, 1)]
    public class PluginContainer : TerrariaPlugin
    {
        enum Status
        {
            Available = 0,
            Generating = 1,
            Cleaning = 2
        }

        private GenerationProgress generationProgress;
        private Status status = Status.Available;
        private int Count = 30;

        public PluginContainer(Main game) : base(game) { }
        public override string Name => "WorldReset";

        private string GetProgress()
        {
            return string.Format("{0:0.0%} - " + generationProgress.Message + " - {1:0.0%}", generationProgress.TotalProgress, generationProgress.Value);
        }

        private void OnServerConnect(ConnectEventArgs args)
        {
            switch (status)
            {
                case Status.Available:
                    return;
                case Status.Cleaning:
                    NetMessage.SendData(2, args.Who, -1, NetworkText.FromLiteral("重置数据中，请稍后"));
                    args.Handled = true;
                    return;
                case Status.Generating:
                    NetMessage.SendData(2, args.Who, -1, NetworkText.FromLiteral("生成地形中，请稍后:" + GetProgress()));
                    args.Handled = true;
                    return;
            }
        }
        private void OnNpcKilled(NpcKilledEventArgs args)
        {
            if (args.npc.type != NPCID.MoonLordCore) return;
            if (--Count > 0)
            {
                TShock.Utils.Broadcast($"离重置地图还需要击败{Count}次月球领主", 0, 255, 255);
                return;
            }

            //Kick all players
            status = Status.Cleaning;
            Main.WorldFileMetadata = null;
            foreach (var player in TShock.Players)
                if (player != null)
                //player.Kick("服务器正在重置，请稍后进入", true);
                {
                    //TShock.Utils.Kick(player, "服务器正在重置，请稍后进入", true); 
                    player.Disconnect("服务器正在重置，请稍后进入,重置进度:");
                }

            //Reset World Map
            Main.gameMenu = true;
            //Main.serverGenLock = true;
            Main.autoGen = true;
            generationProgress = new GenerationProgress();
            WorldGen.CreateNewWorld(generationProgress);
            status = Status.Generating;

            while (Main.autoGen )//Main.serverGenLock)
            {
                TShock.Log.ConsoleInfo(GetProgress());
                Thread.Sleep(100);
            }

            //Reload world map
            status = Status.Cleaning;
            Main.rand = new UnifiedRandom((int) DateTime.Now.Ticks);
            WorldFile.LoadWorld(false);
            Main.dayTime = WorldFile._tempDayTime;
            Main.time = WorldFile._tempTime;
            Main.raining = WorldFile._tempRaining;
            Main.rainTime = WorldFile._tempRainTime;
            Main.maxRaining = WorldFile._tempMaxRain;
            Main.cloudAlpha = WorldFile._tempMaxRain;
            Main.moonPhase = WorldFile._tempMoonPhase;
            Main.bloodMoon = WorldFile._tempBloodMoon;
            Main.eclipse = WorldFile._tempEclipse;

            //Reset player data
            TShock.DB.Query("DELETE FROM tsCharacter", Array.Empty<object>());

            //Reset status to playing
            Main.gameMenu = false;
            generationProgress = null;
            status = Status.Available;
        }

        private void OnWorldSave(WorldSaveEventArgs args)
        {
            args.Handled = status != Status.Available && Main.WorldFileMetadata == null;
        }

        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command("reset.admin", reset, "reset"));
            //ServerApi.Hooks.NpcKilled.Register(this, OnNpcKilled);
            ServerApi.Hooks.ServerConnect.Register(this, OnServerConnect);
            ServerApi.Hooks.WorldSave.Register(this, OnWorldSave, int.MaxValue);
        }

        private void reset(CommandArgs args)
        {
            if(args.Parameters.Count != 0)
            {
                int second = int.Parse(args.Parameters[0]);
                for (int i = 0; i < second; i++)
                {
                    TShock.Utils.Broadcast("服务器即将重置，倒计时:" + (second - i), Color.Red);
                    Thread.Sleep(1000);
                }
            }
            //Kick all players
            status = Status.Cleaning;
            Main.WorldFileMetadata = null;
            foreach (var player in TShock.Players)
                if (player != null)
                //player.Kick("服务器正在重置，请稍后进入", true);
                {
                    //TShock.Utils.Kick(player, "服务器正在重置，请稍后进入", true); 
                    player.Disconnect("服务器正在重置，请稍后进入");
                }

            //Reset World Map
            Main.gameMenu = true;
            //Main.serverGenLock = true;
            generationProgress = new GenerationProgress();
            Task task=WorldGen.CreateNewWorld(generationProgress);
            status = Status.Generating;

            while (!task.IsCompleted)//Main.serverGenLock)
            {
                TShock.Log.ConsoleInfo(GetProgress());
                Thread.Sleep(100);
            }
            //Reload world map
            status = Status.Cleaning;
            Main.rand = new UnifiedRandom((int)DateTime.Now.Ticks);
            WorldFile.LoadWorld(false);
            Main.dayTime = WorldFile._tempDayTime;
            Main.time = WorldFile._tempTime;
            Main.raining = WorldFile._tempRaining;
            Main.rainTime = WorldFile._tempRainTime;
            Main.maxRaining = WorldFile._tempMaxRain;
            Main.cloudAlpha = WorldFile._tempMaxRain;
            Main.moonPhase = WorldFile._tempMoonPhase;
            Main.bloodMoon = WorldFile._tempBloodMoon;
            Main.eclipse = WorldFile._tempEclipse;

            //Reset player data
            TShock.DB.Query("DELETE FROM tsCharacter", Array.Empty<object>());

            //Reset status to playing
            Main.gameMenu = false;
            generationProgress = null;
            status = Status.Available;
        }
    }
}
