using DOL.AI.Brain;
using DOL.GS.PacketHandler;
using Google.Protobuf.WellKnownTypes;
using System;
using System.Collections;
using System.Collections.Generic;

namespace DOL.GS.Effects
{

    public class ChargeEffect : StaticEffect, IGameEffect
    {
        protected const String delveString = "Grants unbreakable speed 3 for 15 second duration. Grants immunity to roots, stun, snare and mesmerize spells. Target will still take damage from snare/root spells that do damage.";
        GameLiving m_living;
        protected long m_startTick;
        private ECSGameTimer m_expireTimer;

        public override void Start(GameLiving living)
        {
            m_living = living;
            //Send messages
            if (m_living is GamePlayer player)
            {
                player.Out.SendMessage("You begin to charge wildly!", eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
            }
            else if (m_living?.GetLivingOwner() is GamePlayer owner)
            {
                owner.Out.SendMessage("The " + m_living.Name + " charges its prey!", eChatType.CT_Say, eChatLoc.CL_SystemWindow);
            }
            else
                return;

            m_startTick = living.CurrentRegion.Time;
            foreach (GamePlayer t_player in living.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                t_player.Out.SendSpellEffectAnimation(living, living, 7035, 0, false, 1);
            }

            //sets player into combat mode
            living.LastAttackTickPvP = m_startTick;
            ArrayList speedSpells = new ArrayList();
            lock (living.EffectList)
            {
                foreach (IGameEffect effect in living.EffectList)
                {
                    if (effect is GameSpellEffect == false) continue;
                    if ((effect as GameSpellEffect)!.Spell.SpellType == "SpeedEnhancement")
                        speedSpells.Add(effect);
                }
            }
            foreach (GameSpellEffect spell in speedSpells)
                spell.Cancel(false);
            m_living.BuffBonusMultCategory1.Set((int)eProperty.MaxSpeed, this, PropertyCalc.MaxSpeedCalculator.SPEED3);
            m_living.TempProperties.setProperty("Charging", true);
            if (m_living is GamePlayer)
                ((GamePlayer)m_living).Out.SendUpdateMaxSpeed();
            StartTimers();
            m_living.EffectList.Add(this);
        }

        public override bool Cancel(bool playerCancel, bool force = false)
        {
            m_living.TempProperties.removeProperty("Charging");
            m_living.EffectList.Remove(this);
            m_living.BuffBonusMultCategory1.Remove((int)eProperty.MaxSpeed, this);
            //Send messages
            if (m_living is GamePlayer player)
            {
                player.Out.SendUpdateMaxSpeed();
                player.Out.SendMessage("You no longer seem so crazy!", eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
            else if (m_living?.GetLivingOwner() is GamePlayer owner)
            {
                owner.Out.SendMessage("The " + m_living.Name + " ceases its charge!", eChatType.CT_Say, eChatLoc.CL_SystemWindow);
            }
            StopTimers();
            return true;
        }

        private void StartTimers()
        {
            StopTimers();
            m_expireTimer = new ECSGameTimer(m_living, ExpireCallback, RealmAbilities.ChargeAbility.DURATION * 1000);
        }

        private void StopTimers()
        {
            m_expireTimer?.Stop();
            m_expireTimer = null;
        }

        private int ExpireCallback(ECSGameTimer timer)
        {
            Cancel(false);
            return 0;
        }

        // Name of the effect
        public override string Name { get { return "Charge"; } }

        /// <summary>
        /// Remaining time of the effect in milliseconds
        /// </summary>
        public override Int32 RemainingTime
        {
            get
            {
                ECSGameTimer timer = m_expireTimer;
                return timer == null || !timer.IsAlive ? 0 : timer.TimeUntilElapsed;
            }
        }

        // Icon to show on players, can be id
        public override ushort Icon
        {
            get
            {
                if (m_living is GameNPC) return 411;
                else return 3034;
            }
        }

        // Delve Info
        public override IList<string> DelveInfo
        {
            get
            {
                var delveInfoList = new List<string>(4);
                delveInfoList.Add(delveString);

                int seconds = (int)(RemainingTime / 1000);
                if (seconds > 0)
                {
                    delveInfoList.Add(" "); //empty line
                    if (seconds > 60)
                        delveInfoList.Add("- " + seconds / 60 + ":" + (seconds % 60).ToString("00") + " minutes remaining.");
                    else
                        delveInfoList.Add("- " + seconds + " seconds remaining.");
                }

                return delveInfoList;
            }
        }
    }
}
