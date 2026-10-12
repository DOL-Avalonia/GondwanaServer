using System;
using System.Collections;
using DOL.Database;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.Language;
using System.Collections.Generic;

namespace DOL.GS.RealmAbilities
{
    /// <summary>
    /// Purge Ability, removes negative effects
    /// </summary>
    public class PurgeAbility : TimedRealmAbility
    {
        public PurgeAbility(DBAbility dba, int level) : base(dba, level) { }

        /// <summary>
        /// Action
        /// </summary>
        /// <param name="living"></param>
        public override void Execute(GameLiving living)
        {
            if (CheckPreconditions(living, DEAD | SITTING)) return;

            if (ServerProperties.Properties.USE_NEW_ACTIVES_RAS_SCALING)
            {
                int seconds = 0;
                switch (Level)
                {
                    case 1:
                        seconds = 5;
                        break;
                }

                if (seconds > 0)
                {
                    new PurgeTimer(living, this, seconds).Start(1);
                    DisableSkill(living);
                }
                else
                {
                    SendCastMessage(living);
                    if (RemoveNegativeEffects(living, this))
                    {
                        DisableSkill(living);
                    }
                }
            }
            else
            {
                if (Level < 2)
                {
                    new PurgeTimer(living, this, 5).Start(1);
                    DisableSkill(living);
                }
                else
                {
                    SendCastMessage(living);
                    if (RemoveNegativeEffects(living, this))
                    {
                        DisableSkill(living);
                    }
                }
            }
        }

        protected static bool RemoveNegativeEffects(GameLiving living, PurgeAbility purge)
        {
            bool removed = false;
            ArrayList effects = new ArrayList();


            GamePlayer player = (GamePlayer)living;

            if (player.CharacterClass.ID == (int)eCharacterClass.Necromancer)
            {
                NecromancerPet necroPet = (NecromancerPet)player.ControlledBrain.Body;
                lock (necroPet.EffectList)
                {
                    foreach (IGameEffect effect in necroPet.EffectList)
                    {
                        GameSpellEffect gsp = (GameSpellEffect)effect;

                        if (gsp == null)
                            continue;
                        if (gsp is GameSpellAndImmunityEffect { ImmunityState: true })
                            continue;
                        if (gsp.SpellHandler is { HasPositiveEffect: true }) // only enemy spells are affected
                            continue;
                        if (gsp.SpellHandler is { IsUnPurgeAble: true }) // do not purge unpurgeable effects
                            continue;

                        effects.Add(gsp);
                        removed = true;
                    }
                }
            }

            lock (living.EffectList)
            {
                foreach (IGameEffect effect in living.EffectList)
                {
                    GameSpellEffect gsp = effect as GameSpellEffect;
                    if (gsp == null)
                        continue;
                    if (gsp is GameSpellAndImmunityEffect { ImmunityState: true })
                        continue;
                    if (gsp.SpellHandler is { HasPositiveEffect: true }) // only enemy spells are affected
                        continue;
                    if (gsp.SpellHandler is { IsUnPurgeAble: true }) // do not purge unpurgeable effects
                        continue;
                    /*
                    if (gsp.SpellHandler is RvRResurrectionIllness)
                       continue;
                     */
                    //if (gsp.Spell.SpellType == "DesperateBowman")//Can't be purged
                    //continue;
                    gsp.CancelledByPurge = true;

                    effects.Add(gsp);
                    removed = true;
                }

                foreach (IGameEffect effect in effects)
                {
                    effect.Cancel(false);
                }
            }

            if (player != null)
            {
                foreach (GamePlayer rangePlayer in living.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    if (player.CharacterClass.ID == (int)eCharacterClass.Necromancer)
                    {
                        rangePlayer.Out.SendSpellEffectAnimation(player.ControlledBrain.Body,
                            player.ControlledBrain.Body, 7011, 0,
                            false, (byte)(removed ? 1 : 0));
                    }

                    rangePlayer.Out.SendSpellEffectAnimation(player, player, 7011, 0, false, (byte)(removed ? 1 : 0));
                }
                if (removed)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PurgeAbility.RemoveNegativeEffects.FallFromYou"), eChatType.CT_Advise, eChatLoc.CL_SystemWindow);
                }
                else
                {
                    player.DisableSkill(purge, 5);
                }
            }
            if (removed)
                player!.Stealth(false);
            return removed;
        }

        protected sealed class PurgeTimer : ECSGameTimerWrapperBase
        {
            private readonly GameLiving m_caster;
            private readonly PurgeAbility m_purge;
            private int m_counter;

            public PurgeTimer(GameLiving caster, PurgeAbility purge, int count) : base(caster)
            {
                m_caster = caster; m_purge = purge; m_counter = count;
            }

            protected override int OnTick(ECSGameTimer timer)
            {
                if (!m_caster.IsAlive)
                {
                    (m_caster as GamePlayer)?.DisableSkill(m_purge, 0);
                    return 0;
                }
                if (m_counter > 0)
                {
                    (m_caster as GamePlayer)?.Out.SendMessage(LanguageMgr.GetTranslation(((GamePlayer)m_caster).Client.Account.Language, "PurgeAbility.OnTick.PurgeActivate", m_counter), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                    m_counter--;
                    return 1000;
                }
                m_purge.SendCastMessage(m_caster);
                RemoveNegativeEffects(m_caster, m_purge);
                return 0;
            }
        }

        public override int GetReUseDelay(int level)
        {
            if (ServerProperties.Properties.USE_NEW_ACTIVES_RAS_SCALING)
            {
                switch (level)
                {
                    case 3:
                        return 600;
                    case 4:
                        return 450;
                    case 5:
                        return 300;
                    default:
                        return 900;
                }
            }
            else
            {
                return (level < 3) ? 900 : 300;
            }
        }

        public override void AddEffectsInfo(IList<string> list, GameClient client)
        {
            list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "PurgeAbility.AddEffectsInfo.Info1"));
            list.Add("");
            list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "PurgeAbility.AddEffectsInfo.Info2"));
            list.Add(LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "PurgeAbility.AddEffectsInfo.Info3"));
        }
    }
}