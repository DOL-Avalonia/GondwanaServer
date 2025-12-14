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
using System.Collections.Generic;
using DOL.Database;
using DOL.Events;
using DOL.GS.Effects;
using DOL.GS.Spells;
using DOL.Language;

namespace DOL.GS.RealmAbilities
{
    /// <summary>
    /// Minion Rescue RA
    /// </summary>
    public class CallOfDarknessAbility : RR5RealmAbility
    {
        private static DBSpell _dbspell;
        private Spell _spell;
        private SpellLine _spellline;
        private GamePlayer _player;

        public const int DURATION = 60 * 1000;

        public CallOfDarknessAbility(DBAbility dba, int level) : base(dba, level) { }

        private void BuildSpell()
        {
            _spellline = new SpellLine("RAs", "RealmAbilities", "RealmAbilities", true);

            if (_dbspell == null)
            {
                _dbspell = new DBSpell
                {
                    SpellID = 8888,
                    TooltipId = 8888,
                    Name = "Call of Darkness",
                    Icon = 7051,
                    ClientEffect = 15184,
                    Target = "self",
                    Type = "CallOfDarkness",
                    Duration = 60,
                    CastTime = 0,
                    MoveCast = false,
                    Uninterruptible = false,
                    Range = 0,
                };
                SkillBase.AddScriptedSpell(_spellline.KeyName, new Spell(_dbspell, 0));
            }

            _spell = new Spell(_dbspell, 0);
        }

        /// <summary>
        /// Action
        /// </summary>
        /// <param name="living"></param>
        public override void Execute(GameLiving living)
        {
            if (CheckPreconditions(living, DEAD | SITTING | MEZZED | STUNNED)) return;
            
            BuildSpell();

            var spellHandler = new CallOfDarknessSpellHandler(living, _spell, _spellline);
            if (_spell.CastTime > 0)
            {
                spellHandler.CastingCompleteEvent += (spell) =>
                {
                    if (((SpellHandler)spell).Status is SpellHandler.eStatus.Success or SpellHandler.eStatus.Failure)
                    {
                        DisableSkill(living);
                    }
                };
                spellHandler.StartSpell(living);
            }
            else
            {
                if (spellHandler.StartSpell(living))
                    DisableSkill(living);
            }
        }

        public override int GetReUseDelay(int level)
        {
            return 900;
        }

        public static void AddDelveInfos(IList<string> list, GameClient client)
        {
            var language = client?.Account?.Language ?? ServerProperties.Properties.SERV_LANGUAGE;
            list.Add(LanguageMgr.GetTranslation(language, "CallOfDarknessAbility.AddEffectsInfo.Info1"));
        }

        public override void AddEffectsInfo(IList<string> list, GameClient client)
        {
            var language = client?.Account?.Language ?? ServerProperties.Properties.SERV_LANGUAGE;
            AddDelveInfos(list, client);
            list.Add("");
            list.Add(LanguageMgr.GetTranslation(language, "CallOfDarknessAbility.AddEffectsInfo.Info2"));
            list.Add(LanguageMgr.GetTranslation(language, "CallOfDarknessAbility.AddEffectsInfo.Info3"));
            list.Add(LanguageMgr.GetTranslation(language, "CallOfDarknessAbility.AddEffectsInfo.Info4"));
        }
    }

    [SpellHandlerAttribute("CallOfDarkness")]
    public class CallOfDarknessSpellHandler : SpellHandler
    {
        /// <inheritdoc />
        public CallOfDarknessSpellHandler(GameLiving caster, Spell spell, SpellLine spellLine) : base(caster, spell, spellLine)
        {
        }

        /// <inheritdoc />
        public override PlayerXEffect GetSavedEffect(GameSpellEffect effect)
        {
            // Remove when player quits
            return null;
        }

        /// <inheritdoc />
        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            if (!base.ApplyEffectOnTarget(target, effectiveness))
                return false;

            foreach (GamePlayer p in target.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                p.Out.SendSpellEffectAnimation(target, target, 7051, 0, false, 1);
            }
            return true;
        }

        /// <inheritdoc />
        protected override int CalculateEffectDuration(GameLiving target, double effectiveness)
        {
            return RealmAbilities.CallOfDarknessAbility.DURATION;
        }

        /// <inheritdoc />
        public override string GetDelveDescription(GameClient delveClient)
        {
            var list = new List<string>();
            CallOfDarknessAbility.AddDelveInfos(list, delveClient);
            return string.Join("\n", list);
        }
    }
}
