using System;
using System.Collections.Generic;
using System.IO;
using Fougerite;
using Fougerite.Events;
using UnityEngine;

namespace TpFriend
{
    public class TpFriend : Fougerite.Module
    {
        public IniParser Settings;
        public readonly List<Vector3> DefaultLocations = new List<Vector3>();
        public readonly List<string> CannotTeleport = new List<string>();
        public int MaxUses = 0;
        public int Cooldown = 1200000;
        public int RequestTimeout = 35;
        public int TeleportDelay = 10;
        public int TpCheck = 2;
        public string SysName = "[TpFriend]";
        public bool CheckIfPlayerIsNearStructure = false;
        public bool CheckIfPlayerIsOnDeployable = false;
        public bool CheckIfPlayerIsInShelter = false;
        
        public override string Name
        {
            get { return "TpFriend"; }
        }

        public override string Author
        {
            get { return "DreTaX"; }
        }

        public override string Description
        {
            get { return "TpFriend"; }
        }

        public override Version Version
        {
            get { return new Version("1.0"); }
        }

        public override void Initialize()
        {
            IniParser DefLocs = new IniParser(ModuleFolder + "\\DefaultLoc.ini");
            Util instance = Util.GetUtil();
            foreach (var x in DefLocs.EnumSection("DefaultLoc"))
            {
                try
                {
                    string value = DefLocs.GetSetting("DefaultLoc", x);
                    DefaultLocations.Add(instance.ConvertStringToVector3(value));
                }
                catch (Exception ex)
                {
                    Logger.LogError("[TpFriend] Failed to convert Vector " + ex);
                }
            }
            ReloadConfig();
            Hooks.OnCommand += OnCommand;
        }

        public override void DeInitialize()
        {
            Hooks.OnCommand -= OnCommand;
        }

        private Fougerite.Player GetPlayerByName(string name)
        {
            name = name.ToLower();
            foreach (var x in Server.GetServer().Players)
            {
                if (x.Name.ToLower().Equals(name))
                {
                    return x;
                }
            }

            return null;
        }

        public Fougerite.Player FindSimilarPlayer(Fougerite.Player Sender, object args)
        {
            int count = 0;
            Fougerite.Player Similar = null;
            string printablename = "";
            if (args as string[] != null)
            {
                string[] array = args as string[];
                printablename = string.Join(" ", array);
                var player = GetPlayerByName(printablename);
                if (player != null)
                {
                    return player;
                }

                foreach (var x in Server.GetServer().Players)
                {
                    foreach (var namepart in array)
                    {
                        if (x.Name.ToLower().Contains(namepart.ToLower()))
                        {
                            Similar = x;
                            count++;
                        }
                    }
                }
            }
            else
            {
                string name = ((string) args).ToLower();
                printablename = name;
                var player = GetPlayerByName(name);
                if (player != null)
                {
                    return player;
                }

                foreach (var x in Server.GetServer().Players)
                {
                    if (x.Name.ToLower().Contains(name))
                    {
                        Similar = x;
                        count++;
                    }
                }
            }

            if (count == 0)
            {
                Sender.MessageFrom(SysName, "Couldn't find [color#00FF00]" + printablename + "[/color]!");
            }
            else if (count > 1)
            {
                Sender.MessageFrom(SysName, "Found [color#FF0000]" + count
                                                                   + "[/color] players with similar name. [color#FF0000] Use more correct name![/color]");
            }
            else
            {
                return Similar;
            }

            return null;
        }

        public void ReloadConfig()
        {
            try
            {
                if (!File.Exists(ModuleFolder + "\\Settings.ini"))
                {
                    File.Create(ModuleFolder + "\\Settings.ini");
                    Settings = new IniParser(ModuleFolder + "\\Settings.ini");
                    Settings.AddSetting("Settings", "MaxUses", MaxUses.ToString());
                    Settings.AddSetting("Settings", "Cooldown", Cooldown.ToString());
                    Settings.AddSetting("Settings", "RequestTimeout", RequestTimeout.ToString());
                    Settings.AddSetting("Settings", "TeleportDelay", TeleportDelay.ToString());
                    Settings.AddSetting("Settings", "TpCheck", TpCheck.ToString());
                    Settings.AddSetting("Settings", "SysName", SysName);
                    Settings.AddSetting("Settings", "CheckIfPlayerIsNearStructure", CheckIfPlayerIsNearStructure.ToString());
                    Settings.AddSetting("Settings", "CheckIfPlayerIsOnDeployable", CheckIfPlayerIsOnDeployable.ToString());
                    Settings.AddSetting("Settings", "CheckIfPlayerIsInShelter", CheckIfPlayerIsInShelter.ToString());
                    Settings.AddSetting("Settings", "CannotTeleportTo", "HGIG,");
                    Settings.Save();
                }
                Settings = new IniParser(ModuleFolder + "\\Settings.ini");
                MaxUses = int.Parse(Settings.GetSetting("Settings", "MaxUses"));
                Cooldown = int.Parse(Settings.GetSetting("Settings", "Cooldown"));
                RequestTimeout = int.Parse(Settings.GetSetting("Settings", "RequestTimeout"));
                TeleportDelay = int.Parse(Settings.GetSetting("Settings", "TeleportDelay"));
                TpCheck = int.Parse(Settings.GetSetting("Settings", "TpCheck"));
                SysName = Settings.GetSetting("Settings", "SysName");
                CheckIfPlayerIsNearStructure = Settings.GetBoolSetting("Settings", "CheckIfPlayerIsNearStructure");
                CheckIfPlayerIsOnDeployable = Settings.GetBoolSetting("Settings", "CheckIfPlayerIsOnDeployable");
                CheckIfPlayerIsInShelter = Settings.GetBoolSetting("Settings", "CheckIfPlayerIsInShelter");
                var CantTP = Settings.GetSetting("Settings", "CannotTeleportTo");
                CannotTeleport.Clear();
                foreach (var x in CantTP.Split(','))
                {
                    if (!string.IsNullOrEmpty(x))
                    {
                        CannotTeleport.Add(x);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("TpFriend] Failed to read config! " + ex);
            }
        }


        public void OnCommand(Fougerite.Player player, string cmd, string[] args)
        {
            if (cmd == "tpa")
            {
                if (args.Length == 0)
                {
                    player.MessageFrom(SysName, "Teleport Usage:");
                    player.MessageFrom(SysName, "TpFriend V" + Version + " by " + Author);
                    player.MessageFrom(SysName, "\"/tpa [PlayerName]\" to request a teleport.");
                    player.MessageFrom(SysName, "\"/tpaccept\" to accept a requested teleport.");
                    player.MessageFrom(SysName, "\"/tpdeny\" to deny a request.");
                    player.MessageFrom(SysName, "\"/tpcount\" to see how many requests you have remaining.");
                    player.MessageFrom(SysName, "\"/tpcancel\" to cancel your own request.");
                }
                else
                {
                    Fougerite.Player playertor = FindSimilarPlayer(player, args);
                    if (playertor == null)
                    {
                        return;
                    }

                    if (playertor == player)
                    {
                        player.MessageFrom(SysName, "Cannot teleport to yourself!");
                        return;
                    }

                    foreach (var x in CannotTeleport)
                    {
                        if (DataStore.GetInstance().ContainsKey(x, playertor.SteamID) || DataStore.GetInstance().ContainsKey(x, playertor.UID))
                        {
                            player.MessageFrom(SysName, "You cannot teleport to this guy currently!");
                            return;
                        }
                    }
                }
            }
        }

        private TpFriendTE CreateParallelTimer(int timeoutDelay, Dictionary<string, object> args)
        {
            TpFriendTE timedEvent = new TpFriendTE(timeoutDelay);
            timedEvent.Args = args;
            timedEvent.OnFire += Callback;
            return timedEvent;
        }

        private void Callback(TpFriendTE e)
        {
            e.Kill();
            var data = e.Args;
        }
    }
}