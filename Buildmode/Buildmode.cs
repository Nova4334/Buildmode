using Microsoft.Xna.Framework;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using Terraria.ID;
using System.Timers;
using TerrariaApi.Server;
using TShockAPI;

namespace Buildmode
{
    [ApiVersion(2, 1)]
    public class Buildmode : TerrariaPlugin
    {
        #region variables
        private static Timer _buffTimer;
		public static bool[] Enabled { get; private set; } = new bool[256];

		private static List<int> _active = new List<int>();

		private static List<int> _buildBuffs = new List<int>
		{
			BuffID.Builder,
			BuffID.Mining,
			BuffID.NightOwl,
			BuffID.Shine,
			BuffID.ObsidianSkin,
			BuffID.WaterWalking,
			BuffID.Gills,
			BuffID.Wet
		};
		private static byte[] _surface;
		private static byte[] _rock;

		private static byte[] _removeBg;

		private static double _time;
		private static bool _day;
        #endregion
        public Buildmode(Main game) : base(game)
		{
			Order = 1;
		}
		public override Version Version
		{
			get { return Assembly.GetExecutingAssembly().GetName().Version; }
		}
		public override string Author
		{
			get { return "nyan-ko & Nova4334"; }
		}
		public override string Name
		{
			get { return "Buildmode"; }
		}
		public override string Description
		{
			get { return "A useful tool for TShock FreeBuild servers."; }
		}
        public override void Initialize()
        {
            _buffTimer = new Timer(1000);
            _buffTimer.AutoReset = true;
            _buffTimer.Elapsed += Refresh;
            _buffTimer.Start();

            ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInitialize);
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
            ServerApi.Hooks.NetSendBytes.Register(this, OnSendBytes);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);

            Commands.ChatCommands.Add(new Command(
                "buildmode.toggle",
                Toggle,
                "buildmode",
                "bm"));

            Commands.ChatCommands.Add(new Command(
                "buildmode.toggle",
                STime,
                "stime"));
        }
        public static void OnLeave(LeaveEventArgs args)
        {
            Enabled[args.Who] = false;
            _active.Remove(args.Who);
        }
        public static void Refresh(object unused, ElapsedEventArgs args)
        {
            foreach (var plr in _active)
            {
                if (Enabled[plr])
                {
                    BuildmodeEffects(plr);
                }
                else
                {
                    _active.Remove(plr);
                }
            }
        }
        public static void OnUpdate(EventArgs args)
        {
            _time++;

            if (!_day && _time > 32400)
            {
                _time = 0;
                _day = true;
            }
            else if (_day && _time > 54000)
            {
                _time = 0;
                _day = false;
            }
        }
        public static void OnPostInitialize(EventArgs args)
        {
            _surface = BitConverter.GetBytes((short)Main.worldSurface);
            _rock = BitConverter.GetBytes((short)Main.rockLayer);
            _removeBg = BitConverter.GetBytes((short)Main.maxTilesY);

            _time = Main.time;
            _day = Main.dayTime;
        }
        public static void OnSendBytes(SendBytesEventArgs args)
        {
            var plr = TShock.Players[args.Socket.Id];

            if (plr == null)
            {
                return;
            }

            int index = plr.Index;
            bool enabled = Enabled[index];

            var type = (PacketTypes)args.Buffer[2];
            switch (type)
            {
                case PacketTypes.WorldInfo:
                    {
                        byte[] surface;
                        byte[] rock;

                        int time;
                        bool day;

                        if (enabled)
                        {
                            surface = _removeBg;
                            rock = _removeBg;

                            time = 27000;
                            day = true;
                        }
                        else
                        {
                            surface = _surface;
                            rock = _rock;

                            time = (int)_time;
                            day = _day;
                        }

                        Buffer.BlockCopy(surface, 0, args.Buffer, 17, 2);
                        Buffer.BlockCopy(rock, 0, args.Buffer, 19, 2);

                        Buffer.BlockCopy(BitConverter.GetBytes(time), 0, args.Buffer, 3, 4);
                        args.Buffer[7] = (byte)(day ? 1 : 0);
                    }
                    break;
                case PacketTypes.TimeSet:
                    {
                        int time;
                        bool day;

                        if (enabled)
                        {
                            time = 27000;
                            day = true;
                        }
                        else
                        {
                            time = (int)_time;
                            day = _day;
                        }

                        args.Buffer[3] = (byte)(day ? 1 : 0);
                        Buffer.BlockCopy(BitConverter.GetBytes(time), 0, args.Buffer, 4, 4);
                    }
                    break;
            }
        }
        public static void Toggle(CommandArgs args)
        {
            if (args.Player == null)
            {
                return;
            }

            int index = args.Player.Index;

            Enabled[index] = !Enabled[index];

            if (Enabled[index])
            {
                _active.Add(index);
            }
            else
            {
                _active.Remove(index);
            }

            args.Player.SendData(PacketTypes.WorldInfo);

            //BuildmodeEffects(index);  probably not needed since bm effects happen every sec

            args.Player.SendSuccessMessage($"{(Enabled[index] ? "En" : "Dis")}abled buildmode.");
        }

        //Method that takes on the same parser as TShock, but rewrites it to work for the visual time defined by _time
        public static void STime(CommandArgs args)
        {
            if (args.Parameters.Count == 0)
            {
                double time = _time / 3600.0;
                time += 4.5;
                if (!_day)
                    time += 15.0;
                time = time % 24.0;
                args.Player.SendInfoMessage("The current time is {0}:{1:D2}.", (int)Math.Floor(time), (int)Math.Floor((time % 1.0) * 60.0));
                return;
            }
            switch (args.Parameters[0].ToLower())
            {
                case "day":
                    SetSTime(true, 0.0);
                    args.Player.SendSuccessMessage("You have changed the server time to 4:30 (4:30 AM).");
                    TSPlayer.All.SendMessage(string.Format("{0} has set the server time to 4:30.", args.Player.Name), Color.CornflowerBlue);
                    break;
                case "night":
                    SetSTime(false, 0.0);
                    args.Player.SendSuccessMessage("You have changed the server time to 19:30 (7:30 PM).");
                    TSPlayer.All.SendMessage(string.Format("{0} has set the server time to 19:30.", args.Player.Name), Color.CornflowerBlue);
                    break;
                case "noon":
                    SetSTime(true, 27000.0);
                    args.Player.SendSuccessMessage("You have changed the server time to 12:00 (12:00 PM).");
                    TSPlayer.All.SendMessage(string.Format("{0} has set the server time to 12:00.", args.Player.Name), Color.CornflowerBlue);
                    break;
                case "midnight":
                    SetSTime(false, 16200.0);
                    args.Player.SendSuccessMessage("You have changed the server time to 0:00 (12:00 AM).");
                    TSPlayer.All.SendMessage(string.Format("{0} has set the server time to 0:00.", args.Player.Name), Color.CornflowerBlue);
                    break;
                default:
                    string[] array = args.Parameters[0].Split(':');
                    if (array.Length != 2)
                    {
                        args.Player.SendErrorMessage("Invalid time string! Proper format: hh:mm, in 24-hour time.");
                        return;
                    }

                    int hours;
                    int minutes;
                    if (!int.TryParse(array[0], out hours) || hours < 0 || hours > 23
                        || !int.TryParse(array[1], out minutes) || minutes < 0 || minutes > 59)
                    {
                        args.Player.SendErrorMessage("Invalid time string! Proper format: hh:mm, in 24-hour time.");
                        return;
                    }

                    decimal time = hours + (minutes / 60.0m);
                    time -= 4.50m;
                    if (time < 0.00m)
                        time += 24.00m;

                    if (time >= 15.00m)
                    {
                        SetSTime(false, (double)((time - 15.00m) * 3600.0m));
                    }
                    else
                    {
                        SetSTime(true, (double)(time * 3600.0m));
                    }
                    args.Player.SendSuccessMessage(string.Format("You have changed the server time to {0}:{1:D2}.", hours, minutes));
                    TSPlayer.All.SendMessage(string.Format("{0} set the server time to {1}:{2:D2}.", args.Player.Name, hours, minutes), Color.CornflowerBlue);
                    break;
            }
        }
        private static void BuildmodeEffects(int player)
        {
            var plr = TShock.Players[player];

            _buildBuffs.ForEach(b => plr.SetBuff(b, 120));
        }
        private static void SetSTime(bool dayTime, double time)
        {
            _day = dayTime;
            _time = time;
            TSPlayer.All.SendData(PacketTypes.TimeSet, "", dayTime ? 1 : 0, (int)time, Main.sunModY, Main.moonModY);
        }
    }
}
