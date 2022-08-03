using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Fougerite;
using Fougerite.Concurrent;
using Fougerite.Permissions;
using UnityEngine;
using Random = System.Random;

namespace TpFriend
{
    public enum TeleportationEvent
    {
        FirstTeleport = 1,
        Timeout = 2,
        ReTeleported = 3,
        AutobanReset = 4,
        ExtraCheck = 5,
    }
    
    public class TpFriend : Module
    {
        public IniParser Settings;
        public readonly List<Vector3> DefaultLocations = new List<Vector3>();
        public readonly List<string> CannotTeleport = new List<string>();
        public readonly ConcurrentList<Fougerite.Player> Pending = new ConcurrentList<Fougerite.Player>();
        public bool PerformCeilingCheck;
        public float CeilingFallthroughDistanceCheck = 2.6f;
        public int MaxUses;
        public int Cooldown = 1200000;
        public int RequestTimeout = 35;
        public int TeleportDelay = 10;
        public int DoubleTeleportDelay = 2;
        public int TpCheck = 2;
        public string SysName = "[TpFriend]";
        public bool CheckIfPlayerIsNearStructure;
        public bool CheckIfPlayerIsOnDeployable;
        public bool CheckIfPlayerIsInShelter;
        public bool CheckPermissionsForDefaultCommands;
        
        public readonly Random Randomizer = new Random();
        
        public const string red = "[color #FF0000]";
        public const string green = "[color #009900]";
        public const string white = "[color #FFFFFF]";
        
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
            get { return new Version("1.1.1"); }
        }

        public override void Initialize()
        {
            DataStore.GetInstance().Flush("TpTimer");
            DataStore.GetInstance().Flush("tpfriendautoban");
            DataStore.GetInstance().Flush("tpfriendpending");
            DataStore.GetInstance().Flush("tpfriendpending2");
            DataStore.GetInstance().Flush("tpfriendcooldown");
            DataStore.GetInstance().Flush("tpfriendy");

            try
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
            }
            catch (Exception ex)
            {
                Logger.LogError("[TpFriend] Failed to read DefaultLoc.ini! " + ex);
            }

            ReloadConfig();
            Hooks.OnCommand += OnCommand;
            Hooks.OnPlayerDisconnected += OnPlayerDisconnected;
        }

        public override void DeInitialize()
        {
            DefaultLocations.Clear();
            Hooks.OnCommand -= OnCommand;
            Hooks.OnPlayerDisconnected -= OnPlayerDisconnected;
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
            if (args is string[] array)
            {
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

            switch (count)
            {
                case 0:
                    Sender.MessageFrom(SysName, "Couldn't find [color#00FF00]" + printablename + "[/color]!");
                    break;
                case > 1:
                    Sender.MessageFrom(SysName, "Found [color#FF0000]" + count 
                                                                       + "[/color] players with similar name. [color#FF0000] Use more correct name![/color]");
                    break;
                default:
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
                    File.Create(ModuleFolder + "\\Settings.ini").Dispose();
                    Settings = new IniParser(ModuleFolder + "\\Settings.ini");
                    Settings.AddSetting("Settings", "MaxUses", MaxUses.ToString());
                    Settings.AddSetting("Settings", "Cooldown", Cooldown.ToString());
                    Settings.AddSetting("Settings", "RequestTimeout", RequestTimeout.ToString());
                    Settings.AddSetting("Settings", "TeleportDelay", TeleportDelay.ToString());
                    Settings.AddSetting("Settings", "DoubleTeleportDelay", DoubleTeleportDelay.ToString());
                    Settings.AddSetting("Settings", "TpCheck", TpCheck.ToString());
                    Settings.AddSetting("Settings", "PerformCeilingCheck", PerformCeilingCheck.ToString());
                    Settings.AddSetting("Settings", "CeilingFallthroughDistanceCheck", CeilingFallthroughDistanceCheck.ToString());
                    Settings.AddSetting("Settings", "SysName", SysName);
                    Settings.AddSetting("Settings", "CheckIfPlayerIsNearStructure", CheckIfPlayerIsNearStructure.ToString());
                    Settings.AddSetting("Settings", "CheckIfPlayerIsOnDeployable", CheckIfPlayerIsOnDeployable.ToString());
                    Settings.AddSetting("Settings", "CheckIfPlayerIsInShelter", CheckIfPlayerIsInShelter.ToString());
                    Settings.AddSetting("Settings", "CheckPermissionsForDefaultCommands", CheckPermissionsForDefaultCommands.ToString());
                    Settings.AddSetting("Settings", "CannotTeleportTo", "HGIG,");
                    Settings.Save();
                }
                else
                {
                    Settings = new IniParser(ModuleFolder + "\\Settings.ini");
                }
                
                PerformCeilingCheck = Settings.GetBoolSetting("Settings", "PerformCeilingCheck");
                CeilingFallthroughDistanceCheck =
                    float.Parse(Settings.GetSetting("Settings", "CeilingFallthroughDistanceCheck"));
                MaxUses = int.Parse(Settings.GetSetting("Settings", "MaxUses"));
                Cooldown = int.Parse(Settings.GetSetting("Settings", "Cooldown")) / 60000 * 60;
                RequestTimeout = int.Parse(Settings.GetSetting("Settings", "RequestTimeout"));
                TeleportDelay = int.Parse(Settings.GetSetting("Settings", "TeleportDelay"));
                DoubleTeleportDelay = int.Parse(Settings.GetSetting("Settings", "DoubleTeleportDelay"));
                TpCheck = int.Parse(Settings.GetSetting("Settings", "TpCheck"));
                SysName = Settings.GetSetting("Settings", "SysName");
                CheckIfPlayerIsNearStructure = Settings.GetBoolSetting("Settings", "CheckIfPlayerIsNearStructure");
                CheckIfPlayerIsOnDeployable = Settings.GetBoolSetting("Settings", "CheckIfPlayerIsOnDeployable");
                CheckIfPlayerIsInShelter = Settings.GetBoolSetting("Settings", "CheckIfPlayerIsInShelter");
                CheckPermissionsForDefaultCommands = Settings.GetBoolSetting("Settings", "CheckPermissionsForDefaultCommands");
                string CantTP = Settings.GetSetting("Settings", "CannotTeleportTo");
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
            ulong id = player.UID;
            string name = player.Name;
            switch (cmd)
            {
                case "tpa" when args.Length == 0:
                {
                    player.MessageFrom(SysName, "Teleport Usage:");
                    player.MessageFrom(SysName, "TpFriend V" + Version + " by " + Author);
                    player.MessageFrom(SysName, "\"/tpa [PlayerName]\" to request a teleport.");
                    player.MessageFrom(SysName, "\"/tpaccept\" to accept a requested teleport.");
                    player.MessageFrom(SysName, "\"/tpdeny\" to deny a request.");
                    player.MessageFrom(SysName, "\"/tpcount\" to see how many requests you have remaining.");
                    player.MessageFrom(SysName, "\"/tpcancel\" to cancel your own request.");
                    player.MessageFrom(SysName, "\"/tpreload\" to reload the config.");
                    break;
                }
                case "tpa":
                {
                    if (CheckPermissionsForDefaultCommands &&
                        (!player.Admin || !PermissionSystem.GetPermissionSystem()
                            .PlayerHasPermission(player, "tpfriend.tpa")))
                    {
                        player.MessageFrom(SysName, "You don't have permission to do this!");
                        return;
                    }
                    
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

                    ulong idt = playertor.UID;
                    string namet = playertor.Name;
                    object ttime = DataStore.GetInstance().Get("tpfriendcooldown", id);
                    double time = 0;
                    if (ttime == null || !double.TryParse(Convert.ToString(ttime, CultureInfo.InvariantCulture),
                            NumberStyles.Any, NumberFormatInfo.InvariantInfo, out time))
                    {
                        DataStore.GetInstance().Add("tpfriendcooldown", id, 0);
                    }
                    
                    object usedtp = DataStore.GetInstance().Get("tpfriendusedtp", id);
                    double calc = (TimeSpan.FromTicks(DateTime.Now.Ticks).TotalSeconds - time);
                    
                    if (calc >= Cooldown || time == 0)
                    {
                        if (usedtp == null)
                        {
                            DataStore.GetInstance().Add("tpfriendusedtp", id, 0);
                            usedtp = 0;
                        }

                        if (MaxUses > 0)
                        {
                            if (usedtp is int usedtpInt && MaxUses <= usedtpInt)
                            {
                                player.MessageFrom(SysName, "Reached max number of teleport requests!");
                                return;
                            }
                        }

                        if (DataStore.GetInstance().Get("tpfriendpending2", idt) != null)
                        {
                            player.MessageFrom(SysName, "This player is pending a request. Wait a bit.");
                            return;
                        }

                        if (DataStore.GetInstance().Get("tpfriendpending", id) != null)
                        {
                            player.MessageFrom(SysName, "You are pending a request. Wait a bit or cancel It");
                            return;
                        }

                        playertor.MessageFrom(SysName, "Teleport request from " + name + " to accept write /tpaccept");
                        player.MessageFrom(SysName, "Teleport request sent to " + namet);

                        DataStore.GetInstance().Add("tpfriendcooldown", id, TimeSpan.FromTicks(DateTime.Now.Ticks).TotalSeconds);
                        DataStore.GetInstance().Add("tpfriendpending", id, idt);
                        DataStore.GetInstance().Add("tpfriendpending2", idt, id);
                        
                        Pending.Add(player);
                        Pending.Add(playertor);
                        
                        Dictionary<string, object> Data = new Dictionary<string, object>(3);
                        Data["Player"] = player;
                        Data["PlayerTo"] = playertor;
                        Data["Event"] = TeleportationEvent.Timeout;
                        CreateParallelTimer(RequestTimeout * 1000, Data).Start();
                    }
                    else
                    {
                        player.MessageFrom(SysName, "You have to wait before teleporting again!");
                        double done = Math.Round(calc);
                        double done2 = Math.Round((double) Cooldown, 2);
                        player.MessageFrom(SysName, "Time Remaining: " + done + " / " + done2 + " seconds");
                    }

                    break;
                }
                case "tpreload":
                {
                    if (player.Admin || PermissionSystem.GetPermissionSystem().PlayerHasPermission(player, "tpfriend.tpreload"))
                    {
                        ReloadConfig();
                        player.MessageFrom(SysName, "Config reloaded!");
                    }

                    break;
                }
                case "tpaccept":
                {
                    if (CheckPermissionsForDefaultCommands &&
                        (!player.Admin || !PermissionSystem.GetPermissionSystem()
                            .PlayerHasPermission(player, "tpfriend.tpaccept")))
                    {
                        player.MessageFrom(SysName, "You don't have permission to do this!");
                        return;
                    }
                    
                    object ppending = DataStore.GetInstance().Get("tpfriendpending2", id);
                    if (ppending is ulong ppendingUid)
                    {
                        Fougerite.Player PlayerFrom = Server.GetServer().FindPlayer(ppendingUid);
                        if (PlayerFrom != null)
                        {
                            RemovePlayerFromPending(player);
                            RemovePlayerFromPending(PlayerFrom);

                            object playertpuse = DataStore.GetInstance().Get("tpfriendusedtp", ppendingUid) ?? 0;
                            int playerTpUseInt = playertpuse is int castInt ? castInt : 0;

                            if (MaxUses > 0)
                            {
                                playerTpUseInt++;
                                DataStore.GetInstance().Add("tpfriendusedtp", ppendingUid, playerTpUseInt);
                                PlayerFrom.MessageFrom(SysName, "Teleport requests used " + playerTpUseInt + " / "
                                                                + MaxUses);
                            }
                            else
                            {
                                PlayerFrom.MessageFrom(SysName, "You have unlimited requests remaining!");
                            }

                            ulong idt = PlayerFrom.UID;
                            if (TeleportDelay > 0)
                            {
                                PlayerFrom.MessageFrom(SysName, "Teleporting you in: " + TeleportDelay + " second(s)");
                                Dictionary<string, object> Data2 = new Dictionary<string, object>(3);
                                Data2["Player"] = PlayerFrom;
                                Data2["PlayerTo"] = PlayerFrom;
                                Data2["Event"] = TeleportationEvent.FirstTeleport;
                                CreateParallelTimer(TeleportDelay * 1000, Data2).Start();
                            }
                            else
                            {
                                if (CheckIfPlayerIsInShelter)
                                {
                                    if (player.IsInShelter)
                                    {
                                        DataStore.GetInstance().Add("tpfriendcooldown", id, 0);
                                        PlayerFrom.MessageFrom(SysName, "Your target player is in a shelter, can't teleport!");
                                        player.MessageFrom(SysName, "You are in a shelter, can't teleport the player!");
                                        return;
                                    }
                                }

                                if (CheckIfPlayerIsOnDeployable)
                                {
                                    if (player.IsOnDeployable)
                                    {
                                        DataStore.GetInstance().Add("tpfriendcooldown", id, 0);
                                        PlayerFrom.MessageFrom(SysName, "Your target player is on a Deployable, can't teleport!");
                                        player.MessageFrom(SysName, "You are on a Deployable, can't teleport the player!");
                                        return;
                                    }
                                }

                                if (CheckIfPlayerIsNearStructure)
                                {
                                    if (player.IsNearStructure)
                                    {
                                        DataStore.GetInstance().Add("tpfriendcooldown", id, 0);
                                        PlayerFrom.MessageFrom(SysName, "Your player is near a house, can't teleport!");
                                        player.MessageFrom(SysName, "You are near a house, can't teleport!");
                                        return;
                                    }
                                }

                                DataStore.GetInstance().Add("tpfriendautoban", PlayerFrom.SteamID, "using");
                                DataStore.GetInstance().Add("tpfriendy", idt, player.Y);
                                PlayerFrom.TeleportTo(player.Location);
                                PlayerFrom.MessageFrom(SysName, "Teleported!");

                                if (!PerformCeilingCheck)
                                {
                                    Dictionary<string, object> Data3 = new Dictionary<string, object>(3);
                                    Data3["Player"] = PlayerFrom;
                                    Data3["PlayerTo"] = PlayerFrom;
                                    Data3["Event"] = TeleportationEvent.AutobanReset;
                                    CreateParallelTimer(500, Data3).Start(); 
                                }
                                //DataStore.GetInstance().Add("tpfriendautoban", idt, "none");

                                Dictionary<string, object> Data2 = new Dictionary<string, object>(3);
                                Data2["Player"] = PlayerFrom;
                                Data2["PlayerTo"] = PlayerFrom;
                                Data2["Event"] = TeleportationEvent.ReTeleported;
                                CreateParallelTimer(DoubleTeleportDelay * 1000, Data2).Start();
                            }

                            DataStore.GetInstance().Remove("tpfriendpending", idt);
                            DataStore.GetInstance().Remove("tpfriendpending2", id);
                            player.MessageFrom(SysName, "Teleport Request Accepted!");
                        }
                        else
                        {
                            RemovePlayerFromPending(player);
                            player.MessageFrom(SysName, "Player isn't online!");
                        }
                    }
                    else
                    {
                        // Sanity removal check
                        DataStore.GetInstance().Remove("tpfriendpending2", id);
                    }

                    break;
                }
                case "tpdeny":
                {
                    if (CheckPermissionsForDefaultCommands &&
                        (!player.Admin || !PermissionSystem.GetPermissionSystem()
                            .PlayerHasPermission(player, "tpfriend.tpdeny")))
                    {
                        player.MessageFrom(SysName, "You don't have permission to do this!");
                        return;
                    }
                    
                    object pending = DataStore.GetInstance().Get("tpfriendpending2", id);
                    if (pending is ulong ppendingUid)
                    {
                        Fougerite.Player PlayerFrom = Server.GetServer().FindPlayer(ppendingUid);
                        if (PlayerFrom != null)
                        {
                            PlayerFrom.MessageFrom(SysName, red + "Your request was denied!");
                            RemovePlayerFromPending(PlayerFrom);
                        }

                        RemovePlayerFromPending(player);
                        DataStore.GetInstance().Remove("tpfriendpending", ppendingUid);
                        DataStore.GetInstance().Add("tpfriendcooldown", ppendingUid, 0);
                        DataStore.GetInstance().Remove("tpfriendpending2", id);
                        player.MessageFrom(SysName, "Request denied!");
                    }
                    else
                    {
                        player.MessageFrom(SysName, "No request to deny.");
                        // Sanity removal check
                        DataStore.GetInstance().Remove("tpfriendpending2", id);
                    }

                    break;
                }
                case "tpcancel":
                {
                    if (CheckPermissionsForDefaultCommands &&
                        (!player.Admin || !PermissionSystem.GetPermissionSystem()
                            .PlayerHasPermission(player, "tpfriend.tpcancel")))
                    {
                        player.MessageFrom(SysName, "You don't have permission to do this!");
                        return;
                    }
                    
                    object pending = DataStore.GetInstance().Get("tpfriendpending2", id);
                    if (pending is ulong ppendingUid)
                    {
                        Fougerite.Player PlayerTo = Server.GetServer().FindPlayer(ppendingUid);
                        if (PlayerTo != null)
                        {
                            PlayerTo.MessageFrom(SysName, red + player.Name + " Cancelled the request!");
                            RemovePlayerFromPending(PlayerTo);
                        }
                        RemovePlayerFromPending(player);

                        DataStore.GetInstance().Remove("tpfriendpending", id);
                        DataStore.GetInstance().Add("tpfriendcooldown", id, 0);
                        DataStore.GetInstance().Remove("tpfriendpending2", ppendingUid);
                        player.MessageFrom(SysName, "Request Cancelled!");
                    }
                    else
                    {
                        player.MessageFrom(SysName, "No request to cancel.");
                        // Sanity removal check
                        DataStore.GetInstance().Remove("tpfriendpending2", id);
                    }

                    break;
                }
                case "tpcount" when MaxUses > 0:
                {
                    if (CheckPermissionsForDefaultCommands &&
                        (!player.Admin || !PermissionSystem.GetPermissionSystem()
                            .PlayerHasPermission(player, "tpfriend.tpcount")))
                    {
                        player.MessageFrom(SysName, "You don't have permission to do this!");
                        return;
                    }
                    
                    object uses = DataStore.GetInstance().Get("tpfriendusedtp", id) ?? 0;

                    player.MessageFrom(SysName, "Teleport requests used " + (int) uses + " / " + MaxUses);
                    break;
                }
                case "tpcount":
                {
                    if (CheckPermissionsForDefaultCommands &&
                        (!player.Admin || !PermissionSystem.GetPermissionSystem()
                            .PlayerHasPermission(player, "tpfriend.tpcount")))
                    {
                        player.MessageFrom(SysName, "You don't have permission to do this!");
                        return;
                    }
                    
                    player.MessageFrom(SysName, "You have unlimited requests remaining!");
                    break;
                }
                case "tpresettime":
                {
                    if (player.Admin || PermissionSystem.GetPermissionSystem().PlayerHasPermission(player, "tpfriend.tpresettime"))
                    {
                        if (args.Length == 0)
                        {
                            DataStore.GetInstance().Add("tpfriendcooldown", id, 0);
                            player.Message("Reset!");
                        }
                        else
                        {
                            Fougerite.Player playertor = FindSimilarPlayer(player, args);
                            if (playertor == null)
                            {
                                return;
                            }
                            
                            DataStore.GetInstance().Add("tpfriendcooldown", playertor.UID, 0);
                            player.Message("Reset!");
                        }
                    }

                    break;
                }
                case "clearuses":
                {
                    if (player.Admin || PermissionSystem.GetPermissionSystem().PlayerHasPermission(player, "tpfriend.clearuses"))
                    {
                        DataStore.GetInstance().Flush("tpfriendusedtp");
                        player.MessageFrom(SysName, "Flushed!");
                    }

                    break;
                }
                case "clearusesof":
                {
                    if (player.Admin || PermissionSystem.GetPermissionSystem().PlayerHasPermission(player, "tpfriend.clearusesof"))
                    {
                        if (args.Length == 0)
                        {
                            DataStore.GetInstance().Remove("tpfriendusedtp", id);
                            player.Message("Reset!");
                        }
                        else
                        {
                            Fougerite.Player playertor = FindSimilarPlayer(player, args);
                            if (playertor == null)
                            {
                                return;
                            }
                            
                            DataStore.GetInstance().Remove("tpfriendusedtp", playertor.UID);
                            player.Message("Reset!");
                        }
                    }

                    break;
                }
            }
        }
        
        private void OnPlayerDisconnected(Fougerite.Player player)
        {
            if (Pending.Contains(player))
            {
                Pending.Remove(player);
            }
        }

        private void RemovePlayerFromPending(Fougerite.Player player)
        {
            if (Pending.Contains(player))
            {
                Pending.Remove(player);
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
            Dictionary<string, object> Data = e.Args;

            Fougerite.Player PlayerFrom = (Fougerite.Player) Data["Player"];
            Fougerite.Player PlayerTo = (Fougerite.Player) Data["PlayerTo"];
            if (!PlayerTo.IsOnline || !PlayerFrom.IsOnline)
            {
                DataStore.GetInstance().Add("tpfriendautoban", PlayerFrom.SteamID, "none");
                if (Pending.Contains(PlayerFrom))
                {
                    Pending.Remove(PlayerFrom);
                }
                if (Pending.Contains(PlayerTo))
                {
                    Pending.Remove(PlayerTo);
                }
                
                return;
            }
            
            TeleportationEvent evt = (TeleportationEvent) Data["Event"];
            DataStore.GetInstance().Add("tpfriendautoban", PlayerFrom.SteamID, "using");

            if (evt == TeleportationEvent.FirstTeleport)
            {
                if (CheckIfPlayerIsInShelter)
                {
                    if (PlayerTo.IsInShelter)
                    {
                        DataStore.GetInstance().Add("tpfriendcooldown", PlayerFrom.UID, 0);
                        PlayerFrom.MessageFrom(SysName, "Your player is in a shelter, can't teleport!");
                        PlayerTo.MessageFrom(SysName, "You are in a shelter, can't teleport!");
                        return;
                    }
                }

                if (CheckIfPlayerIsOnDeployable)
                {
                    if (PlayerTo.IsOnDeployable)
                    {
                        DataStore.GetInstance().Add("tpfriendcooldown", PlayerFrom.UID, 0);
                        PlayerFrom.MessageFrom(SysName, "Your player is in on a Deployable, can't teleport!");
                        PlayerTo.MessageFrom(SysName, "You are on a Deployable, can't teleport!");
                        return;
                    }
                }

                if (CheckIfPlayerIsNearStructure)
                {
                    if (PlayerTo.IsNearStructure)
                    {
                        DataStore.GetInstance().Add("tpfriendcooldown", PlayerFrom.UID, 0);
                        PlayerFrom.MessageFrom(SysName, "Your player is near a house, can't teleport!");
                        PlayerTo.MessageFrom(SysName, "You are near a house, can't teleport!");
                        return;
                    }
                }

                PlayerFrom.TeleportTo(PlayerTo.Location, false);
                PlayerFrom.MessageFrom(SysName, "You have been teleported to your friend");
                Dictionary<string, object> Data2 = new Dictionary<string, object>(3);
                Data2["Player"] = PlayerFrom;
                Data2["PlayerTo"] = PlayerFrom;
                Data2["Event"] = TeleportationEvent.ReTeleported;
                CreateParallelTimer(DoubleTeleportDelay * 1000, Data2).Start();
            }
            // Autokill
            else if (evt == TeleportationEvent.Timeout)
            {
                if (!Pending.Contains(PlayerFrom) || !Pending.Contains(PlayerTo))
                {
                    DataStore.GetInstance().Add("tpfriendautoban", PlayerFrom.SteamID, "none");
                    if (Pending.Contains(PlayerFrom))
                    {
                        Pending.Remove(PlayerFrom);
                    }

                    if (Pending.Contains(PlayerTo))
                    {
                        Pending.Remove(PlayerTo);
                    }

                    return;
                }

                Pending.Remove(PlayerFrom);
                Pending.Remove(PlayerTo);

                object oispend = DataStore.GetInstance().Get("tpfriendpending", PlayerFrom.UID);
                object oispend2 = DataStore.GetInstance().Get("tpfriendpending2", PlayerTo.UID);

                if (oispend != null && oispend2 != null)
                {
                    DataStore.GetInstance().Remove("tpfriendpending", PlayerFrom.UID);
                    DataStore.GetInstance().Remove("tpfriendpending2", PlayerTo.UID);
                    
                    DataStore.GetInstance().Add("tpfriendcooldown", PlayerFrom.UID, 0);
                    DataStore.GetInstance().Add("tpfriendautoban", PlayerFrom.SteamID, "none");
                    
                    PlayerFrom.MessageFrom(SysName, "Teleport request timed out");
                    PlayerTo.MessageFrom(SysName, "Teleport request timed out");
                }
            }
            else if (evt == TeleportationEvent.ReTeleported)
            {
                PlayerFrom.TeleportTo(PlayerTo.Location);
                PlayerFrom.MessageFrom(SysName, "You have been teleported to your friend again.");
                DataStore.GetInstance().Add("tpfriendy", PlayerFrom.UID, PlayerTo.Y);

                if (PerformCeilingCheck)
                {
                    Dictionary<string, object> Data2 = new Dictionary<string, object>(3);
                    Data2["Player"] = PlayerFrom;
                    Data2["PlayerTo"] = PlayerFrom;
                    Data2["Event"] = TeleportationEvent.ExtraCheck;
                    CreateParallelTimer(2000, Data2).Start(); 
                }
            }
            else if (evt == TeleportationEvent.AutobanReset)
            {
                DataStore.GetInstance().Add("tpfriendautoban", PlayerFrom.SteamID, "none");
            }
            else if (evt == TeleportationEvent.ExtraCheck)
            {
                float y = PlayerFrom.Y;
                float oy = (float) DataStore.GetInstance().Get("tpfriendy", PlayerFrom.UID);
                if (oy - y > CeilingFallthroughDistanceCheck)
                {
                    Server.GetServer().BroadcastFrom(SysName, PlayerFrom.Name + red
                                                                               + " tried to fall through a house via tpa. Teleported away.");
                    Logger.LogWarning("[TpFriend Teleportation Ceiling Fallthrough] " + PlayerFrom.Name + " - " +
                                      PlayerFrom.SteamID + " - " + PlayerFrom.IP
                                      + " - " + PlayerFrom.Location);

                    PlayerFrom.TeleportTo(DefaultLocations[Randomizer.Next(1, DefaultLocations.Count)]);
                    DataStore.GetInstance().Remove("tpfriendy", PlayerFrom.UID);
                }
                
                Dictionary<string, object> Data2 = new Dictionary<string, object>(3);
                Data2["Player"] = PlayerFrom;
                Data2["PlayerTo"] = PlayerFrom;
                Data2["Event"] = TeleportationEvent.AutobanReset;
                CreateParallelTimer(2000, Data2).Start();
            }
        }
    }
}