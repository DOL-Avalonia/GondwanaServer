/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */

using System;
using System.Collections;
using System.Collections.Generic;

using DOL.GS.Effects;
using DOL.GS;
using DOL.Events;
using DOL.GS.PacketHandler;
using DOL.Language;
using System.Numerics;
using log4net;
using DOL.GS.ServerProperties;

namespace DOL.GS.Spells
{
    /// <summary>
    /// Summary description for ReanimateCorpe.
    /// </summary>
    [SpellHandlerAttribute("ReanimateCorpse")]
    public class MonsterRez : ResurrectSpellHandler
    {
        // Constructor
        public MonsterRez(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line)
        {
        }

        protected override void ResurrectResponceHandler(GamePlayer player, byte response)
        {
            base.ResurrectResponceHandler(player, response);
            if (response == 1)
                ResurrectLiving(player);
        }

        protected override void ResurrectLiving(GameLiving living)
        {
            base.ResurrectLiving(living);

            SpellLine line = SkillBase.GetSpellLine("Summon Monster");
            Spell castSpell = SkillBase.GetSpellByID(14078);

            ISpellHandler spellhandler = ScriptMgr.CreateSpellHandler(m_caster, castSpell, line);
            spellhandler.StartSpell(living);
        }

        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            string description = LanguageMgr.GetTranslation(language, "SpellDescription.MonsterRez.MainDescription", Spell.ResurrectHealth, Spell.ResurrectMana);

            Spell currentSpell = Spell;
            bool firstSubspell = true;
            while (currentSpell.SubSpellID != 0)
            {
                currentSpell = SkillBase.GetSpellByID((int)currentSpell.SubSpellID);
                SpellLine subSpellLine = SkillBase.GetSpellLine(GlobalSpellsLines.Reserved_Spells);
                ISpellHandler subHandler = ScriptMgr.CreateSpellHandler(m_caster, currentSpell, subSpellLine);
                if (subHandler != null)
                {
                    if (firstSubspell)
                    {
                        description += "\n\n";
                        firstSubspell = false;
                    }
                    description += subHandler.GetDelveDescription(delveClient) + "\n";
                }
            }

            if (Spell.Radius > 0 || Spell.Frequency > 0 || Spell.Duration > 0)
            {
                string damageTypeName = LanguageMgr.GetDamageOfType(delveClient, Spell.DamageType);
                double SpellDamage = Spell.Damage;
                description += LanguageMgr.GetTranslation(language, "SpellDescription.MonsterRez.DamageMain", damageTypeName, SpellDamage);

                if (Spell.Radius > 0)
                {
                    description += LanguageMgr.GetTranslation(language, "SpellDescription.MonsterRez.DamageRadius");
                }

                if (Spell.Frequency > 0)
                {
                    int freqSeconds = Spell.Frequency / 1000;
                    description += LanguageMgr.GetTranslation(language, "SpellDescription.MonsterRez.DamageFrequency", freqSeconds);
                }

                if (Spell.Duration > 0)
                {
                    int durSeconds = Spell.Duration / 1000;
                    description += LanguageMgr.GetTranslation(language, "SpellDescription.MonsterRez.DamageDuration", durSeconds);
                }

                description += ".";
            }
            return description;
        }
    }

    /// <summary>
    /// Summary description for ReanimateCorpe.
    /// </summary>
    [SpellHandlerAttribute("SummonMonster")]
    public class SummonMonster : AbstractMorphSpellHandler
    {
        private SpellLine m_monsterspellline = null;
        private GamePlayer m_owner = null;
        public override bool IgnoreForRiding => true;

        public SpellLine MonsterSpellLine
        {
            get
            {
                if (m_monsterspellline == null)
                    m_monsterspellline = SkillBase.GetSpellLine("Summon Monster");

                return m_monsterspellline;
            }
        }

        public Spell MonsterSpellDoT
        {
            get
            {
                return (SkillBase.GetSpellByID(14077));
            }
        }

        public Spell MonsterSpellDisease
        {
            get
            {
                return (SkillBase.GetSpellByID(14079));
            }
        }

        protected override GameSpellEffect CreateSpellEffect(GameLiving target, double effectiveness)
        {
            return new GameSpellEffect(this, Spell.Duration, Spell.Frequency, effectiveness);
        }

        /// <inheritdoc />
        public override ushort GetModelFor(GameLiving living)
        {
            return (ushort)Spell.Value;
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            if (!(effect.Owner is GamePlayer))
                return;

            GamePlayer player = effect.Owner as GamePlayer;
            m_owner = player;

            player!.BaseBuffBonusCategory[(int)eProperty.MagicAbsorption] += (int)Spell.LifeDrainReturn;
            player.BaseBuffBonusCategory[(int)eProperty.ArmorAbsorption] += (int)Spell.LifeDrainReturn;
            player.Out.SendCharStatsUpdate();
            player.Health = player.MaxHealth;
            GameEventMgr.AddHandler(player, GameLivingEvent.Dying, new DOLEventHandler(EventRaised));
            GameEventMgr.AddHandler(player, GamePlayerEvent.Linkdeath, new DOLEventHandler(EventRaised));
            GameEventMgr.AddHandler(player, GamePlayerEvent.Quit, new DOLEventHandler(EventRaised));
            GameEventMgr.AddHandler(player, GamePlayerEvent.RegionChanged, new DOLEventHandler(EventRaised));

            base.OnEffectStart(effect);
        }

        public override void OnEffectPulse(GameSpellEffect effect)
        {

            if (effect.Owner is GamePlayer)
            {
                GamePlayer player = effect.Owner as GamePlayer;
                player!.CastSpell(this.MonsterSpellDoT, this.MonsterSpellLine);
                player.CastSpell(this.MonsterSpellDisease, this.MonsterSpellLine);
            }

            base.OnEffectPulse(effect);
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            if (!(effect.Owner is GamePlayer))
                return 0;

            GamePlayer player = effect.Owner as GamePlayer;

            player!.BaseBuffBonusCategory[(int)eProperty.MagicAbsorption] -= (int)Spell.LifeDrainReturn;
            player.BaseBuffBonusCategory[(int)eProperty.ArmorAbsorption] -= (int)Spell.LifeDrainReturn;
            player.Out.SendCharStatsUpdate();

            int leftHealth = Convert.ToInt32(player.MaxHealth * 0.10);
            player.Health = leftHealth;

            GameEventMgr.RemoveHandler(player, GameLivingEvent.Dying, new DOLEventHandler(EventRaised));
            GameEventMgr.RemoveHandler(player, GamePlayerEvent.Linkdeath, new DOLEventHandler(EventRaised));
            GameEventMgr.RemoveHandler(player, GamePlayerEvent.Quit, new DOLEventHandler(EventRaised));
            GameEventMgr.RemoveHandler(player, GamePlayerEvent.RegionChanged, new DOLEventHandler(EventRaised));

            return base.OnEffectExpires(effect, noMessages);
        }

        public void EventRaised(DOLEvent e, object sender, EventArgs arguments)
        {
            GamePlayer player = sender as GamePlayer; //attacker
            if (player == null) return;
            GameSpellEffect effect = SpellHandler.FindEffectOnTarget(player, "SummonMonster");
            if (effect != null)
                effect.Cancel(false);
        }

        // Constructor
        public SummonMonster(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line)
        {
            Priority = 1000;
        }
        
        public override void OnBetterThan(GameLiving target, GameSpellEffect oldEffect, GameSpellEffect newEffect)
        {
            SpellHandler attempt = (SpellHandler)newEffect.SpellHandler;
            if (attempt.Caster.GetController() is GamePlayer player)
                player.SendTranslatedMessage("SpellHandler.SummonMonster.Target.Resist", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow, player.GetPersonalizedName(target));
            attempt.SendSpellResistAnimation(target);
        }
        
        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.SummonMonster.MainDescription");
        }

    }

    /// <summary>
    /// Summary description for MonsterDoT.
    /// </summary>
    [SpellHandlerAttribute("MonsterDoT")]
    public class MonsterDoT : DirectDamageSpellHandler
    {
        public override IList<GameLiving> SelectTargets(GameObject castTarget, bool force = false)
        {
            var list = new List<GameLiving>(8);
            GameLiving target = castTarget as GameLiving;

            //if (target == null || Spell.Range == 0)
            //	target = Caster;

            foreach (GamePlayer player in target!.GetPlayersInRadius(false, (ushort)Spell.Radius))
            {
                if (GameServer.ServerRules.IsAllowedToAttack(Caster, player, true))
                {
                    list.Add(player);
                }
            }
            foreach (GameNPC npc in target.GetNPCsInRadius(false, (ushort)Spell.Radius))
            {
                if (GameServer.ServerRules.IsAllowedToAttack(Caster, npc, true))
                {
                    list.Add(npc);
                }
            }

            return list;
        }


        // Constructor
        public MonsterDoT(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }

    /// <summary>
    /// Summary description for MonsterDoT.
    /// </summary>
    [SpellHandlerAttribute("MonsterDisease")]
    public class MonsterDisease : DiseaseSpellHandler
    {
        public override IList<GameLiving> SelectTargets(GameObject castTarget, bool force = false)
        {
            var list = new List<GameLiving>(8);
            GameLiving target = castTarget as GameLiving;

            if (target == null || Spell.Range == 0)
                target = Caster;

            foreach (GamePlayer player in target.GetPlayersInRadius(false, (ushort)Spell.Radius))
            {
                if (GameServer.ServerRules.IsAllowedToAttack(Caster, player, true))
                {
                    list.Add(player);
                }
            }
            foreach (GameNPC npc in target.GetNPCsInRadius(false, (ushort)Spell.Radius))
            {
                if (GameServer.ServerRules.IsAllowedToAttack(Caster, npc, true))
                {
                    list.Add(npc);
                }
            }

            return list;
        }

        // Constructor
        public MonsterDisease(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

    }
}
