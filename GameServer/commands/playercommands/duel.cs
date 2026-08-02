using System;
using DOL.Events;
using DOL.Language;
using DOL.GS.PacketHandler;
using DOL.GS.Scripts;
using System.Linq;
using DOL.GS.Spells;
using System.Numerics;

namespace DOL.GS.Commands
{
    [CmdAttribute(
         "&duel",
         ePrivLevel.Player,
         "Commands.Players.Duel.Description",
         "Commands.Players.Duel.Usage")]
    public class DuelCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        private const string DUEL_STARTER_WEAK = "DuelStarter";
        private const string CHALLENGE_TARGET_WEAK = "DuelTarget";
        private const string REMATCH_KEY_PREFIX = GameDuel.REMATCH_KEY_PREFIX;

        public void OnCommand(GameClient client, string[] args)
        {
            bool inHousing = client.Player.CurrentRegionID == ServerRules.AmtenaelRules.HousingRegionID;
            bool inPvP = client.Player.IsInPvP;
            bool inRvR = client.Player.IsInRvR;
            bool inDungeon = client.Player.CurrentRegion.IsDungeon;

            var player = client?.Player;
            if (player == null) return;

            if (IsSpammingCommand(player, "duel"))
                return;

            if (player.TempProperties.getProperty<bool>("ArenaParticipant", false) || player.TempProperties.getProperty<bool>("ArenaQueued", false))
            {
                player.Out.SendMessage("You cannot duel while participating in an Arena Contest.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (inHousing)
            {
                DisplayMessage(client, LanguageMgr.GetTranslation(client!.Account.Language, "Commands.Players.Duel.InHousing"));
                return;
            }
            if (inPvP || inRvR)
            {
                DisplayMessage(client, LanguageMgr.GetTranslation(client!.Account.Language, "Commands.Players.Duel.InPvP&RvR"));
                return;
            }
            if (inDungeon)
            {
                DisplayMessage(client, LanguageMgr.GetTranslation(client!.Account.Language, "Commands.Players.Duel.InDungeon"));
                return;
            }

            // If user simply types /duel, treat it as /duel challenge on current target
            if (args.Length == 1)
            {
                TrySendInvite(player);
                return;
            }

            switch (args[1].ToLower())
            {
                case "challenge":
                    TrySendInvite(player);
                    return;

                case "accept":
                    {
                        WeakRef weak;
                        GamePlayer duelStarter;

                        lock (player.TempProperties)
                            weak = player.TempProperties.getProperty<object>(DUEL_STARTER_WEAK, null) as WeakRef;

                        if (weak == null || (duelStarter = weak.Target as GamePlayer) == null)
                        {
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Duel.ConsideringDuel"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        if (!CheckDuelStart(player, duelStarter))
                        {
                            // Clean pending flags if we cannot start anymore
                            lock (player.TempProperties) player.TempProperties.removeProperty(DUEL_STARTER_WEAK);
                            lock (duelStarter.TempProperties) duelStarter.TempProperties.removeProperty(CHALLENGE_TARGET_WEAK);
                            return;
                        }

                        player.DuelStart(duelStarter);

                        duelStarter.Out.SendMessage(LanguageMgr.GetTranslation(duelStarter.Client, "Commands.Players.Duel.TargetAccept", duelStarter.GetPersonalizedName(player)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Duel.YouAccept"), eChatType.CT_System, eChatLoc.CL_SystemWindow);

                        lock (player.TempProperties) player.TempProperties.removeProperty(DUEL_STARTER_WEAK);
                        lock (duelStarter.TempProperties) duelStarter.TempProperties.removeProperty(CHALLENGE_TARGET_WEAK);
                        return;
                    }

                case "decline":
                    {
                        WeakRef weak;
                        GamePlayer duelStarter;

                        lock (player.TempProperties)
                        {
                            weak = player.TempProperties.getProperty<object>(DUEL_STARTER_WEAK, null) as WeakRef;
                            player.TempProperties.removeProperty(DUEL_STARTER_WEAK);
                        }

                        if (weak == null || (duelStarter = weak.Target as GamePlayer) == null)
                        {
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Duel.NotInDuel"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        lock (duelStarter.TempProperties)
                            duelStarter.TempProperties.removeProperty(CHALLENGE_TARGET_WEAK);

                        duelStarter.Out.SendMessage(LanguageMgr.GetTranslation(duelStarter.Client, "Commands.Players.Duel.TargetDeclines", duelStarter.GetPersonalizedName(player)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Duel.YouDecline", duelStarter.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }

                case "cancel":
                    {
                        WeakRef weak;
                        GamePlayer duelTarget;

                        lock (player.TempProperties)
                        {
                            weak = player.TempProperties.getProperty<object>(CHALLENGE_TARGET_WEAK, null) as WeakRef;
                            player.TempProperties.removeProperty(CHALLENGE_TARGET_WEAK);
                        }

                        if (weak == null || (duelTarget = weak.Target as GamePlayer) == null)
                        {
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Duel.YouHaventChallenged"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        lock (duelTarget.TempProperties)
                            duelTarget.TempProperties.removeProperty(DUEL_STARTER_WEAK);

                        // FIX: use the challenger (player) name personalized for the target
                        duelTarget.Out.SendMessage(
                            LanguageMgr.GetTranslation(duelTarget.Client, "Commands.Players.Duel.TargetCancel", player.GetPersonalizedName(duelTarget)),
                            eChatType.CT_System, eChatLoc.CL_SystemWindow);

                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Duel.YouCancel"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }

                case "surrender":
                    {
                        GamePlayer target = player.DuelTarget;
                        if (target == null)
                        {
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Duel.NotInDuel"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        player.DuelStop();

                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Duel.YouSurrender", player.GetPersonalizedName(target)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        target.Out.SendMessage(LanguageMgr.GetTranslation(target.Client, "Commands.Players.Duel.TargetSurrender", target.GetPersonalizedName(player)), eChatType.CT_System, eChatLoc.CL_SystemWindow);

                        foreach (GamePlayer p in player.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
                        {
                            if (p != player && p != target)
                                p.MessageFromArea(player, LanguageMgr.GetTranslation(p.Client, "Commands.Players.Duel.PlayerVsPlayer", p.GetPersonalizedName(player), p.GetPersonalizedName(target)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        return;
                    }
            }

            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "Commands.Players.Duel.DuelOptions"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }

        /// <summary>
        /// NEW: Send invite via CustomDialog when starter types plain /duel or /duel challenge
        /// </summary>
        private void TrySendInvite(GamePlayer starter)
        {
            GamePlayer target = starter.TargetObject as GamePlayer;

            if (target == null || target == starter)
            {
                starter.Out.SendMessage(LanguageMgr.GetTranslation(starter.Client, "Commands.Players.Duel.NeedTarget"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            if (!CheckDuelStart(starter, target))
                return;

            // Check pending on both sides
            WeakRef weak;
            GamePlayer other;

            lock (starter.TempProperties)
            {
                weak = starter.TempProperties.getProperty<object>(CHALLENGE_TARGET_WEAK, null) as WeakRef;
                if (weak != null && (other = weak.Target as GamePlayer) != null)
                {
                    starter.Out.SendMessage(LanguageMgr.GetTranslation(starter.Client, "Commands.Players.Duel.YouAlreadyChallenging", starter.GetPersonalizedName(other)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }
                weak = starter.TempProperties.getProperty<object>(DUEL_STARTER_WEAK, null) as WeakRef;
                if (weak != null && (other = weak.Target as GamePlayer) != null)
                {
                    starter.Out.SendMessage(LanguageMgr.GetTranslation(starter.Client, "Commands.Players.Duel.YouAlreadyConsidering", starter.GetPersonalizedName(other)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }
            }

            lock (target.TempProperties)
            {
                if (target.TempProperties.getProperty<object>(DUEL_STARTER_WEAK, null) != null)
                {
                    starter.Out.SendMessage(LanguageMgr.GetTranslation(starter.Client, "Commands.Players.Duel.TargetAlreadyConsidering", starter.GetPersonalizedName(target)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }
                if (target.TempProperties.getProperty<object>(CHALLENGE_TARGET_WEAK, null) != null)
                {
                    starter.Out.SendMessage(LanguageMgr.GetTranslation(starter.Client, "Commands.Players.Duel.TargetAlreadyChallenging", starter.GetPersonalizedName(target)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return;
                }

                target.TempProperties.setProperty(DUEL_STARTER_WEAK, new WeakRef(starter));
            }

            lock (starter.TempProperties)
                starter.TempProperties.setProperty(CHALLENGE_TARGET_WEAK, new WeakRef(target));

            // Notify both
            starter.Out.SendMessage(LanguageMgr.GetTranslation(starter.Client, "Commands.Players.Duel.YouChallenge", starter.GetPersonalizedName(target)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            target.Out.SendMessage(LanguageMgr.GetTranslation(target.Client, "Commands.Players.Duel.ChallengesYou", starter.GetPersonalizedName(target)), eChatType.CT_System, eChatLoc.CL_SystemWindow);

            // Custom dialog on target (OK = Accept, Cancel = Decline)
            string dialogText = LanguageMgr.GetTranslation(target.Client, "Commands.Players.Duel.Dialog.InvitePrompt1", starter.GetPersonalizedName(starter)) + " " + LanguageMgr.GetTranslation(target.Client, "Commands.Players.Duel.Dialog.InvitePrompt2");
            target.Out.SendCustomDialog(dialogText, new CustomDialogResponse(DuelInviteResponse));
        }

        /// <summary>
        /// Dialog callback on the TARGET player
        /// </summary>
        private void DuelInviteResponse(GamePlayer target, byte response)
        {
            WeakRef weak;
            GamePlayer starter;

            lock (target.TempProperties)
                weak = target.TempProperties.getProperty<object>(DUEL_STARTER_WEAK, null) as WeakRef;

            if (weak == null || (starter = weak.Target as GamePlayer) == null)
            {
                target.Out.SendMessage(LanguageMgr.GetTranslation(target.Client, "Commands.Players.Duel.ConsideringDuel"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            // Decline (Cancel)
            if (response != 0x01)
            {
                lock (target.TempProperties) target.TempProperties.removeProperty(DUEL_STARTER_WEAK);
                lock (starter.TempProperties) starter.TempProperties.removeProperty(CHALLENGE_TARGET_WEAK);

                starter.Out.SendMessage(LanguageMgr.GetTranslation(starter.Client, "Commands.Players.Duel.TargetDeclines", starter.GetPersonalizedName(target)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                target.Out.SendMessage(LanguageMgr.GetTranslation(target.Client, "Commands.Players.Duel.YouDecline", starter.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            // Accept (OK)
            if (!CheckDuelStart(target, starter))
            {
                // Clean pending flags if we cannot start anymore
                lock (target.TempProperties) target.TempProperties.removeProperty(DUEL_STARTER_WEAK);
                lock (starter.TempProperties) starter.TempProperties.removeProperty(CHALLENGE_TARGET_WEAK);
                return;
            }

            target.DuelStart(starter);

            starter.Out.SendMessage(LanguageMgr.GetTranslation(starter.Client, "Commands.Players.Duel.TargetAccept", starter.GetPersonalizedName(target)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            target.Out.SendMessage(LanguageMgr.GetTranslation(target.Client, "Commands.Players.Duel.YouAccept"), eChatType.CT_System, eChatLoc.CL_SystemWindow);

            lock (target.TempProperties) target.TempProperties.removeProperty(DUEL_STARTER_WEAK);
            lock (starter.TempProperties) starter.TempProperties.removeProperty(CHALLENGE_TARGET_WEAK);
        }

        /// <summary>
        /// Checks if a duel can be started between 2 players at this moment
        /// </summary>
        private static bool CheckDuelStart(GamePlayer actionSource, GamePlayer actionTarget)
        {
            bool srcSafe = actionSource.CurrentAreas.Cast<AbstractArea>().Any(a => a.IsSafeArea);
            bool tarSafe = actionTarget.CurrentAreas.Cast<AbstractArea>().Any(a => a.IsSafeArea);

            var unsafeRegions = ServerRules.AmtenaelRules.UnsafeRegions;
            bool IsPvPTimerOn = unsafeRegions != null && (Array.IndexOf(unsafeRegions, actionSource.CurrentRegionID) >= 0 || Array.IndexOf(unsafeRegions, actionTarget.CurrentRegionID) >= 0);

            if (IsOnRematchCooldown(actionSource, actionTarget, out var remain))
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.RematchCooldown", FormatTime(remain), actionSource.GetPersonalizedName(actionTarget)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (!IsPvPTimerOn)
            {
                if (actionSource.IsInvulnerableToAttack)
                {
                    actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.PvPImmunitySelf"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }
                if (actionTarget.IsInvulnerableToAttack)
                {
                    actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.PvPImmunityTarget", actionSource.GetPersonalizedName(actionTarget)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    return false;
                }
            }

            // Is Arena Participant
            if (actionTarget.TempProperties.getProperty<bool>("ArenaParticipant", false) || actionTarget.TempProperties.getProperty<bool>("ArenaQueued", false))
            {
                actionSource.Out.SendMessage($"{actionTarget.Name} is busy participating in an Arena Contest.", eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            // SafeArea
            if (srcSafe)
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.SafeZone"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (tarSafe)
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.TargetSafeZone", actionSource.GetPersonalizedName(actionTarget)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            // Mounted
            if (actionSource.IsRiding || actionSource.IsOnHorse)
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.MountedSelf"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (actionTarget.IsRiding || actionTarget.IsOnHorse)
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.MountedTarget", actionSource.GetPersonalizedName(actionTarget)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            // Jail
            if (JailMgr.IsPrisoner(actionSource))
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.JailedSelf"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (JailMgr.IsPrisoner(actionTarget))
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.JailedTarget", actionSource.GetPersonalizedName(actionTarget)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            // Stunned
            if (actionSource.IsStunned)
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.StunnedSelf"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (actionTarget.IsStunned)
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.StunnedTarget", actionSource.GetPersonalizedName(actionTarget)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            // Dead
            if (!actionSource.IsAlive)
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.DeadSelf"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (!actionTarget.IsAlive)
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.DeadTarget", actionSource.GetPersonalizedName(actionTarget)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            // Crafting
            if (actionSource.IsCrafting)
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.CraftingSelf"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (actionTarget.IsCrafting)
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.CraftingTarget", actionSource.GetPersonalizedName(actionTarget)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            // Stealing
            if (actionSource.TempProperties.getProperty<object>(StealCommandHandlerBase.PLAYER_VOL_TIMER, null) != null)
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.StealingSelf"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (actionTarget.TempProperties.getProperty<object>(StealCommandHandlerBase.PLAYER_VOL_TIMER, null) != null)
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.StealingTarget", actionSource.GetPersonalizedName(actionTarget)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            // Linkdead / not fully in world
            if (actionSource.Client == null || actionTarget.Client == null || actionSource.Client.ClientState != GameClient.eClientState.Playing || actionTarget.Client.ClientState != GameClient.eClientState.Playing)
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.Linkdead"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            // AFK
            if (actionSource.PlayerAfkMessage != null)
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.AFKSelf"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (actionTarget.PlayerAfkMessage != null)
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.AFKTarget", actionSource.GetPersonalizedName(actionTarget)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            // Monster Rez or Occultist Bringer of Deat & Call Of Shadows
            if (SpellHandler.FindEffectOnTarget(actionSource, "SummonMonster") != null || SpellHandler.FindEffectOnTarget(actionSource, "CallOfShadows") != null || SpellHandler.FindEffectOnTarget(actionSource, "BringerOfDeath") != null)
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.CantThisFormSelf"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (SpellHandler.FindEffectOnTarget(actionTarget, "SummonMonster") != null || SpellHandler.FindEffectOnTarget(actionTarget, "CallOfShadows") != null || SpellHandler.FindEffectOnTarget(actionTarget, "BringerOfDeath") != null)
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.CantThisFormTarget", actionSource.GetPersonalizedName(actionTarget)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            // Warlock morph / speed-decrease enchantment
            var wsdSrc = SpellHandler.FindEffectOnTarget(actionSource, "WarlockSpeedDecrease");
            if (wsdSrc != null)
            {
                int rm = wsdSrc.Spell?.ResurrectMana ?? 0;
                string appearance = LanguageMgr.GetWarlockMorphAppearance(actionSource.Client.Account.Language, rm);
                actionSource.Out.SendMessage(
                    LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.WarlockMorphSelf", appearance),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            var wsdTar = SpellHandler.FindEffectOnTarget(actionTarget, "WarlockSpeedDecrease");
            if (wsdTar != null)
            {
                int rm = wsdTar.Spell?.ResurrectMana ?? 0;
                string appearance = LanguageMgr.GetWarlockMorphAppearance(actionSource.Client.Account.Language, rm);
                actionSource.Out.SendMessage(
                    LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.WarlockMorphTarget", actionSource.GetPersonalizedName(actionTarget), appearance),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            if (!GameServer.ServerRules.IsSameRealm(actionSource, actionTarget, true))
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.EnemyRealm"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (actionSource.DuelTarget != null)
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.YouInDuel"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (actionTarget.DuelTarget != null)
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.TargetInDuel", actionSource.GetPersonalizedName(actionTarget)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (actionTarget.InCombat)
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.TargetInCombat", actionSource.GetPersonalizedName(actionTarget)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (actionSource.InCombat)
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.YouInCombat"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (actionTarget.Group != null)
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.TargetInGroup", actionSource.GetPersonalizedName(actionTarget)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (actionSource.Group != null)
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.YouInGroup"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (actionSource.Health < actionSource.MaxHealth)
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.YouHealth"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }
            if (actionTarget.Health < actionTarget.MaxHealth)
            {
                actionSource.Out.SendMessage(LanguageMgr.GetTranslation(actionSource.Client, "Commands.Players.Duel.TargetHealth"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return false;
            }

            return true;
        }

        private static bool IsOnRematchCooldown(GamePlayer a, GamePlayer b, out int remainingSeconds)
        {
            remainingSeconds = 0;
            int cool = ServerProperties.Properties.DUEL_REMATCH_COOLDOWN_SECONDS;
            if (cool <= 0) return false;

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long untilA, untilB;

            lock (a.TempProperties)
                untilA = a.TempProperties.getProperty<long>(REMATCH_KEY_PREFIX + b.InternalID, 0L);
            lock (b.TempProperties)
                untilB = b.TempProperties.getProperty<long>(REMATCH_KEY_PREFIX + a.InternalID, 0L);

            long until = Math.Max(untilA, untilB);
            if (until > now)
            {
                remainingSeconds = (int)(until - now);
                return true;
            }
            return false;
        }

        private static string FormatTime(int seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"m\:ss");
        }
    }
}
