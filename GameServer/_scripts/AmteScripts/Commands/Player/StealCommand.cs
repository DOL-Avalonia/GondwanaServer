﻿using DOL.Database;
using DOL.Events;
using DOL.GS.Finance;
using DOL.GS.PacketHandler;
using DOL.GS.SkillHandler;
using DOL.Language;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DOL.GS.Commands
{
    public class StealCommandHandlerBase : AbstractCommandHandler, ICommandHandler
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public const int MIN_VOL_TIME = 5; // Secondes
        public const int MAX_VOL_TIME = 15;
        public const string PLAYER_STEALER = "vol_player_stealer";
        public const string TARGET_STOLE = "vol_target_stole";
        public const string PLAYER_VOL_TIMER = "player_vol_timer";

        [ScriptLoadedEvent]
        public static void ScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            GameEventMgr.AddHandler(GamePlayerEvent.Moving,
                new DOLEventHandler(EventPlayerMove));
        }

        [ScriptUnloadedEvent]
        public static void ScriptUnloaded(DOLEvent e, object sender, EventArgs args)
        {
            GameEventMgr.RemoveHandler(GamePlayerEvent.Moving,
                new DOLEventHandler(EventPlayerMove));
        }

        public static void EventPlayerMove(DOLEvent d, object sender, EventArgs e)
        {
            GamePlayer player = sender as GamePlayer;

            if (player != null)
            {
                GamePlayer Source = player.TempProperties.getProperty<object>(PLAYER_STEALER, null) as GamePlayer;
                if (Source != null)
                {
                    if (!CanVol(Source, player))
                        CancelVol(Source);
                }
                else
                {
                    RegionTimer Timer = player.TempProperties.getProperty<object>(PLAYER_VOL_TIMER, null) as RegionTimer;
                    if (Timer != null)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "Commands.Players.Vol.Move"),
                            eChatType.CT_Important, eChatLoc.CL_SystemWindow);

                        Timer.Stop();

                        player.Out.SendCloseTimerWindow();
                        player.TempProperties.removeProperty(PLAYER_VOL_TIMER);
                    }
                }
            }
        }

        public static bool CanVol(GamePlayer stealer, GamePlayer target)
        {
            if (target == null || (target is GameNPC))
            {
                return false;
            }

            if (stealer == target)
            {
                return false;
            }

            if (!stealer.IsStealthed)
            {
                return false;
            }

            if (stealer.GuildID == target.GuildID && stealer.GuildID != string.Empty)
            {
                return false;
            }

            if (stealer.Group != null && stealer.Group.IsInTheGroup(target as GameLiving))
            {
                return false;
            }

            if (stealer.Level < 25 || target.Level < 20)
            {
                return false;
            }

            return true;
        }

        public static StealResult Vol(GamePlayer stealer, GamePlayer target)
        {
            var result = new StealResult();
            int deltaLevel = Math.Abs(stealer.Level - target.Level);
            bool shouldTryToSteal = false;
            if (deltaLevel > 10)
            {
                if (target.Level > stealer.Level)
                {
                    result.Status = deltaLevel >= 20 ? StealResultStatus.STEALTHLOST : StealResultStatus.FAILED;

                }
                else
                {
                    shouldTryToSteal = true;
                }
            }
            else
            {
                shouldTryToSteal = true;
            }

            if (shouldTryToSteal)
            {
                int specLevel = stealer.GetBaseSpecLevel("Stealth");
                float chance = (specLevel * 100) / stealer.Level;
                var rand = new Random(DateTime.Now.Millisecond);
                float resist = target.GetModified(eProperty.RobberyResist) / 100.0f;
                float bonusChance = stealer.GetModified(eProperty.RobberyChanceBonus);
                chance += bonusChance;
                chance *= 1.0f - resist;

                if (rand.Next(1, 101) > chance)
                {
                    result.Status = StealResultStatus.FAILED;
                    return result;
                }

                result.Status = rand.Next(1, 101) <= 70 ? StealResultStatus.SUCCESS_MONEY : StealResultStatus.SUSSCES_ITEM;

                if (result.Status == StealResultStatus.SUCCESS_MONEY)
                {
                    var moneyPerc = rand.Next(10, 41);
                    result.Money = ((target.GetCurrentMoney() * moneyPerc) / 100);
                }
            }

            return result;
        }

        public static void CancelVol(GamePlayer Player)
        {
            CancelVol(Player,
                Player.TempProperties.getProperty<object>(PLAYER_VOL_TIMER, null) as RegionTimer);
        }

        private static void DisableRobbing(GamePlayer player, int duration)
        {
            int reduction = player.GetModified(eProperty.RobberyDelayReduction);
            int dur = reduction == 0 ? duration : (int)(duration * ((100.0f - reduction) / 100.0f));

            player.TempProperties.setProperty(VolAbilityHandler.DISABLE_PROPERTY, player.CurrentRegion.Time + dur);
            player.DisableSkill(SkillBase.GetAbility(Abilities.Vol), dur);
        }

        public static void CancelVol(GamePlayer Player, RegionTimer Timer)
        {
            DisableRobbing(Player, VolAbilityHandler.DISABLE_DURATION);
            
            Timer.Stop();

            Player.Out.SendCloseTimerWindow();

            (Timer.Properties.getProperty<object>(TARGET_STOLE, null) as GamePlayer).TempProperties.removeProperty(PLAYER_STEALER);

            Player.TempProperties.removeProperty(PLAYER_VOL_TIMER);
        }

        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer Player = client.Player;

            if (!Player.HasAbility(Abilities.Vol))
            {
                //Les autres classes n'ont pas à savoir l'existance de ceci.
                Player.Out.SendMessage(LanguageMgr.GetTranslation(Player.Client.Account.Language, "Commands.Players.Vol.UnknownCommand"),
                                eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (!Player.IsAllowToVolInThisArea)
            {
                Player.Out.SendMessage(LanguageMgr.GetTranslation(Player.Client.Account.Language, "Commands.Players.Vol.Area"),
                eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (!Player.IsWithinRadius(Player.TargetObject, WorldMgr.GIVE_ITEM_DISTANCE))
            {
                Player.Out.SendMessage(LanguageMgr.GetTranslation(Player.Client.Account.Language, "Commands.Players.Vol.Distance"),
                                eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (Player.IsMezzed)
            {
                Player.Out.SendMessage(LanguageMgr.GetTranslation(Player.Client.Account.Language, "Commands.Players.Vol.Hypnotized"),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (Player.IsStunned)
            {
                Player.Out.SendMessage(LanguageMgr.GetTranslation(Player.Client.Account.Language, "Commands.Players.Vol.Stunned"),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            if (Player.PlayerAfkMessage != null)
            {
                Player.Out.SendMessage(LanguageMgr.GetTranslation(Player.Client.Account.Language, "Commands.Players.Vol.AFK"),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            if (!Player.IsAlive)
            {
                Player.Out.SendMessage(LanguageMgr.GetTranslation(Player.Client.Account.Language, "Commands.Players.Vol.Dead"),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (Player.TempProperties.getProperty<object>(PLAYER_VOL_TIMER, null) != null)
            {
                Player.Out.SendMessage(LanguageMgr.GetTranslation(Player.Client.Account.Language, "Commands.Players.Vol.AlreadyStealing"),
                    eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                return;
            }

            var targetPlayer = Player.TargetObject as GamePlayer;

            if (targetPlayer != null)
            {
                if (targetPlayer.PlayerAfkMessage != null)
                {
                    Player.Out.SendMessage(LanguageMgr.GetTranslation(Player.Client.Account.Language, "Commands.Players.Vol.TargetAFK"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }
            }

            long VolChangeTick = Player.TempProperties.getProperty<long>(
                VolAbilityHandler.DISABLE_PROPERTY, 0L);
            long ChangeTime = Player.CurrentRegion.Time - VolChangeTick;
            if (ChangeTime < VolAbilityHandler.DISABLE_DURATION && Player.Client.Account.PrivLevel < 3) //Allow Admin
            {
                Player.Out.SendMessage(LanguageMgr.GetTranslation(Player.Client.Account.Language, "Commands.Players.Vol.Time", ((VolAbilityHandler.DISABLE_DURATION - ChangeTime) / 1000).ToString()),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            GamePlayer Target = Player.TargetObject as GamePlayer;
            if (Target == null && args.Length >= 2)
            {
                Target = WorldMgr.GetClientByPlayerName(args[1],
                    false, true).Player;
            }

            if (CanVol(Player, Target))
            {
                int VolTime = Util.Random(MIN_VOL_TIME, MAX_VOL_TIME);

                string TargetRealName = Target.GetName(Target);
                Player.Out.SendMessage(LanguageMgr.GetTranslation(Player.Client.Account.Language, "Commands.Players.Vol.Steal", TargetRealName),
                    eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                Player.Out.SendTimerWindow(LanguageMgr.GetTranslation(Player.Client.Account.Language, "Commands.Players.Vol.StealWindow", TargetRealName), VolTime);

                RegionTimer Timer = new RegionTimer(Player);
                Timer.Callback = new RegionTimerCallback(VolTarget);
                Timer.Properties.setProperty(PLAYER_STEALER, Player);
                Timer.Properties.setProperty(TARGET_STOLE, Target);
                Timer.Start(VolTime * 1000);

                Target.TempProperties.setProperty(PLAYER_STEALER, Player);
                Player.TempProperties.setProperty(PLAYER_VOL_TIMER, Timer);

                DisableRobbing(Player, VolAbilityHandler.DISABLE_DURATION);
            }
            else
            {
                Player.Out.SendMessage(LanguageMgr.GetTranslation(Player.Client.Account.Language, "Commands.Players.Vol.CantSteal"),
                    eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }
        }

        public int VolTarget(RegionTimer Timer)
        {
            GamePlayer stealer = (GamePlayer)Timer.Properties.getProperty<object>(PLAYER_STEALER, null);
            GamePlayer target = (GamePlayer)Timer.Properties.getProperty<object>(TARGET_STOLE, null);
            var mezzerId = target.TempProperties.getProperty<string>(GamePlayer.PLAYER_MEZZED_BY_OTHER_PLAYER_ID, null);
            if (mezzerId != null)
            {
                stealer.Reputation--;
                stealer.Out.SendMessage(LanguageMgr.GetTranslation(stealer.Client.Account.Language, "Commands.Players.Vol.StealSleeping"), eChatType.CT_YouDied, eChatLoc.CL_SystemWindow);
                stealer.SaveIntoDatabase();

                Random rand = new Random(DateTime.UtcNow.Millisecond);
                if (rand.Next() > 50)
                {
                    var mezzerClient = WorldMgr.GetClientByPlayerID(mezzerId, true, true);

                    if (mezzerClient != null)
                    {
                        mezzerClient.Player.Reputation--;
                        mezzerClient.Out.SendMessage(LanguageMgr.GetTranslation(mezzerClient.Account.Language, "Commands.Players.Vol.StealSleapingPartner"), eChatType.CT_YouDied, eChatLoc.CL_SystemWindow);
                        mezzerClient.Player.SaveIntoDatabase();
                    }
                }
            }

            StealResult result = Vol(stealer, target);
            if (result.Status == StealResultStatus.STEALTHLOST)
            {
                stealer.Out.SendMessage(LanguageMgr.GetTranslation(stealer.Client.Account.Language, "Commands.Players.Vol.Fail"),
                    eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                stealer.Stealth(false);
            }
            else if (result.Status == StealResultStatus.FAILED)
            {
                stealer.Out.SendMessage(LanguageMgr.GetTranslation(stealer.Client.Account.Language, "Commands.Players.Vol.Fail"),
                    eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }
            else
            {
                PerformVolAction(stealer, target, result);
            }

            Random newRand = new Random(DateTime.UtcNow.Millisecond);
            if (newRand.Next() > 30)
            {
                stealer.Reputation--;
                stealer.Out.SendMessage(LanguageMgr.GetTranslation(stealer.Client.Account.Language, "Commands.Players.Vol.LostRep"), eChatType.CT_YouDied, eChatLoc.CL_SystemWindow);
                stealer.SaveIntoDatabase();
            }

            CancelVol(stealer, Timer);

            return 0;
        }

        private void PerformVolAction(GamePlayer stealer, GamePlayer target, StealResult vol)
        {
            if (vol.Status == StealResultStatus.SUCCESS_MONEY)
            {
                stealer.AddMoney(Currency.Copper.Mint(vol.Money));
                target.RemoveMoney(Currency.Copper.Mint(vol.Money));
                target.Out.SendMessage(LanguageMgr.GetTranslation(target.Client.Account.Language, "Commands.Players.Vol.BeStealed", Money.GetString(vol.Money)), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                stealer.Out.SendMessage(LanguageMgr.GetTranslation(stealer.Client.Account.Language, "Commands.Players.Vol.StealGain", Money.GetString(vol.Money)), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                TaskManager.UpdateTaskProgress(stealer, "SuccessfulPvPThefts", 1);
            }
            else if (vol.Status == StealResultStatus.SUSSCES_ITEM)
            {

                if (!stealer.Inventory.IsSlotsFree(1, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack))
                {
                    stealer.Out.SendMessage(LanguageMgr.GetTranslation(stealer.Client.Account.Language, "Commands.Players.Vol.FullInventory"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    var items = new List<InventoryItem>();
                    foreach (var item in target.Inventory.AllItems)
                    {
                        if (item.IsDropable && item.IsTradable)
                        {
                            items.Add(item);
                        }
                    }

                    int stealableItems = items.Count();
                    if (stealableItems < 1)
                    {
                        stealer.Out.SendMessage(LanguageMgr.GetTranslation(stealer.Client.Account.Language, "Commands.Players.Vol.NothingToSteal"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    }
                    else
                    {
                        var slot = stealer.Inventory.FindFirstEmptySlot(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack);

                        if (slot == eInventorySlot.Invalid)
                        {
                            stealer.Out.SendMessage(LanguageMgr.GetTranslation(stealer.Client.Account.Language, "Commands.Players.Vol.FullBag"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        }
                        else
                        {
                            int index = new Random(DateTime.Now.Millisecond).Next(0, stealableItems - 1);
                            var item = items.ElementAt(index);
                            target.Inventory.RemoveItem(item);
                            stealer.Inventory.AddItem(slot, item);

                            stealer.Out.SendMessage(LanguageMgr.GetTranslation(stealer.Client.Account.Language, "Commands.Players.Vol.StealItem", item.Name, stealer.GetPersonalizedName(target)), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                            target.Out.SendMessage(LanguageMgr.GetTranslation(target.Client.Account.Language, "Commands.Players.Vol.BeStealedItem", item.Name, target.Name), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                            TaskManager.UpdateTaskProgress(stealer, "SuccessfulPvPThefts", 1);
                        }
                    }
                }
            }
        }
    }


    public enum StealResultStatus
    {
        SUCCESS_MONEY,
        SUSSCES_ITEM,
        FAILED,
        STEALTHLOST
    }

    public class StealResult
    {
        public StealResultStatus Status { get; set; }

        public long Money { get; set; }

    }

    [CmdAttribute(
        "&steal",
        ePrivLevel.Player,
        "Commands.Players.Steal.Description",
        "Commands.Players.Steal.Usage")]
    public class StealCommandHandler : StealCommandHandlerBase
    {
    }
    
    [CmdAttribute(
        "&vol",
        ePrivLevel.Player,
        "Commands.Players.Vol.Description",
        "Commands.Players.Vol.Usage")]
    public class VolCommandHandler : StealCommandHandlerBase
    {
    }
}
