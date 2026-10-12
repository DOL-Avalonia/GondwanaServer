using DOL.Database;
using DOL.Events;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;
using DOL.Language;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;

namespace DOL.GS.RealmAbilities
{
    public class PerfectRecoveryAbility : TimedRealmAbility
    {
        public PerfectRecoveryAbility(DBAbility dba, int level) : base(dba, level) { }
        private Int32 m_resurrectValue = 5;
        private const String RESURRECT_CASTER_PROPERTY = "RESURRECT_CASTER";
        private readonly Dictionary<GameLiving, ECSGameTimer> m_resTimersByLiving = new();
        private readonly object m_resTimersLock = new();

        public override void Execute(GameLiving living)
        {
            if (CheckPreconditions(living, DEAD | SITTING | MEZZED | STUNNED))
                return;
            GamePlayer player = living as GamePlayer;

            if (player == null)
                return;

            GamePlayer targetPlayer = null;
            bool isGoodTarget = true;

            if (player.TargetObject == null)
            {
                isGoodTarget = false;
            }
            else
            {
                targetPlayer = player.TargetObject as GamePlayer;

                if (targetPlayer == null ||
                    targetPlayer.IsAlive ||
                    GameServer.ServerRules.IsSameRealm(living, player.TargetObject as GameLiving, true) == false)
                {
                    isGoodTarget = false;
                }
            }

            if (isGoodTarget == false)
            {
                player.Out.SendMessage("You have to target a dead member of your realm!", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                return;
            }

            if (ServerProperties.Properties.USE_NEW_ACTIVES_RAS_SCALING)
            {
                switch (Level)
                {
                    case 1: m_resurrectValue = 10; break;
                    case 2: m_resurrectValue = 25; break;
                    case 3: m_resurrectValue = 50; break;
                    case 4: m_resurrectValue = 75; break;
                    case 5: m_resurrectValue = 100; break;
                }
            }
            else
            {
                switch (Level)
                {
                    case 2: m_resurrectValue = 50; break;
                    case 3: m_resurrectValue = 100; break;
                }
            }

            GameLiving resurrectionCaster = targetPlayer!.TempProperties.getProperty<object>(RESURRECT_CASTER_PROPERTY, null) as GameLiving;
            if (resurrectionCaster != null)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "SpellHandler.ResurrectSpellHandler.AlreadyConsideringResurrect"), eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                return;
            }
            if (!player.IsWithinRadius(targetPlayer, (int)(1500 * player.GetModified(eProperty.SpellRange) * 0.01)))

            {
                player.Out.SendMessage("You are too far away from your target to use this ability!", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow);
                return;
            }
            if (targetPlayer != null)
            {
                SendCasterSpellEffectAndCastMessage(living, 7019, true);
                DisableSkill(living);
                //Lifeflight:
                //don't rez just yet
                //ResurrectLiving(targetPlayer, player);
                //we need to add a dialogue response to the rez, copying from the rez spellhandler

                targetPlayer.TempProperties.setProperty(RESURRECT_CASTER_PROPERTY, living);
                ECSGameTimer resurrectExpiredTimer = new ECSGameTimer(targetPlayer, ResurrectExpiredCallback, 15000);
                lock (m_resTimersLock) m_resTimersByLiving[targetPlayer] = resurrectExpiredTimer;

                //send resurrect dialog
                targetPlayer.Out.SendCustomDialog(LanguageMgr.GetTranslation(targetPlayer.Client, "SpellHandler.ResurrectSpellHandler.ResurrectDialog", targetPlayer.GetPersonalizedName(living), m_resurrectValue), new CustomDialogResponse(ResurrectResponceHandler));

            }
        }

        //Lifeflight add
        /// <summary>
        /// Resurrects target if it accepts
        /// </summary>
        /// <param name="player"></param>
        /// <param name="response"></param>
        protected virtual void ResurrectResponceHandler(GamePlayer player, byte response)
        {
            //DOLConsole.WriteLine("resurrect responce: " + response);
            ECSGameTimer resurrectExpiredTimer = null;
            lock (m_resTimersLock)
                if (m_resTimersByLiving.Remove(player, out ECSGameTimer t)) t.Stop();

            if (resurrectExpiredTimer != null)
            {
                resurrectExpiredTimer.Stop();
            }

            GameLiving rezzer = (GameLiving)player.TempProperties.getProperty<object>(RESURRECT_CASTER_PROPERTY, null);
            if (!player.IsAlive)
            {
                if (rezzer == null)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "SpellHandler.ResurrectSpellHandler.NoResurrectAttempt"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    if (response == 1)
                    {
                        ResurrectLiving(player, rezzer); //accepted

                    }
                    else
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "SpellHandler.ResurrectSpellHandler.ResurrectDeclined"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        //Dont need to refund anything with PR
                        //m_caster.Mana += CalculateNeededPower(player);
                        //but we do need to give them PR back
                        //Lifeflight: Seems like the best way to do this is to send a 0 duration to DisableSkill, which will enable to ability
                        (rezzer as GameLiving).DisableSkill(this, 0);

                    }
                }
            }
            player.TempProperties.removeProperty(RESURRECT_CASTER_PROPERTY);
        }

        //Lifeflight add
        /// <summary>
        /// Cancels resurrection after some time
        /// </summary>
        /// <param name="callingTimer"></param>
        /// <returns></returns>
        protected virtual int ResurrectExpiredCallback(ECSGameTimer callingTimer)
        {
            if (callingTimer.Owner is not GamePlayer player)
                return 0;
            lock (m_resTimersLock)
                m_resTimersByLiving.Remove(player);

            player.TempProperties.removeProperty(RESURRECT_CASTER_PROPERTY);
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client, "SpellHandler.ResurrectSpellHandler.RezExpired"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            return 0;
        }

        public void ResurrectLiving(GamePlayer resurrectedPlayer, GameLiving rezzer)
        {
            if (rezzer.ObjectState != GameObject.eObjectState.Active)
                return;
            if (rezzer.CurrentRegionID != resurrectedPlayer.CurrentRegionID)
                return;
            resurrectedPlayer.Health = (int)(resurrectedPlayer.MaxHealth * m_resurrectValue / 100);
            resurrectedPlayer.Mana = (int)(resurrectedPlayer.MaxMana * m_resurrectValue / 100);
            resurrectedPlayer.Endurance = (int)(resurrectedPlayer.MaxEndurance * m_resurrectValue / 100); //no endurance after any rez
            resurrectedPlayer.MoveTo(rezzer.Position);

            GameLiving living = resurrectedPlayer as GameLiving;
            ECSGameTimer resurrectExpiredTimer = null;
            lock (m_resTimersLock)
                if (m_resTimersByLiving.Remove(living, out ECSGameTimer t)) t.Stop();

            if (resurrectExpiredTimer != null)
            {
                resurrectExpiredTimer.Stop();
            }

            resurrectedPlayer.StopReleaseTimer();
            resurrectedPlayer.Out.SendPlayerRevive(resurrectedPlayer);
            resurrectedPlayer.UpdatePlayerStatus();

            GameSpellEffect effect = SpellHandler.FindEffectOnTarget(resurrectedPlayer, "PveResurrectionIllness");
            if (effect != null)
                effect.Cancel(false);
            GameSpellEffect effecttwo = SpellHandler.FindEffectOnTarget(resurrectedPlayer, "RvrResurrectionIllness");
            if (effecttwo != null)
                effecttwo.Cancel(false);
            resurrectedPlayer.Out.SendMessage(LanguageMgr.GetTranslation(resurrectedPlayer.Client, "SpellHandler.ResurrectSpellHandler.ResurrectedBy", resurrectedPlayer.GetPersonalizedName(rezzer)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            //Lifeflight: this should make it so players who have been ressurected don't take damage for 5 seconds
            RezDmgImmunityEffect rezImmune = new RezDmgImmunityEffect();
            rezImmune.Start(resurrectedPlayer);

            //Lifeflight: We need to reward rez RPs
            GamePlayer casterPlayer = rezzer as GamePlayer;
            if (casterPlayer != null)
            {
                long rezRps = resurrectedPlayer.LastDeathRealmPoints * (m_resurrectValue + 50) / 1000;
                if (rezRps > 0)
                {
                    casterPlayer.GainRealmPoints(rezRps);
                }
                else
                {
                    casterPlayer.Out.SendMessage(LanguageMgr.GetTranslation(casterPlayer.Client, "SpellHandler.ResurrectSpellHandler.NoRealmPoints1"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                    casterPlayer.Out.SendMessage(LanguageMgr.GetTranslation(casterPlayer.Client, "SpellHandler.ResurrectSpellHandler.NoRealmPoints2"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                }
            }

        }
        public override int GetReUseDelay(int level)
        {
            return 300;
        }
    }
}

