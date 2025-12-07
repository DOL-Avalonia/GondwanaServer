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
using DOL.Database;
using DOL.AI.Brain;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.Language;
using System.Linq;
using System.Numerics;

namespace DOL.GS.Spells
{
    [SpellHandler("Disease")]
    public class DiseaseSpellHandler : SpellHandler
    {
        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            ConsumePower(target);
            base.FinishSpellCast(target, force);
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);
            effect.Owner.TempProperties.setProperty("AmnesiaChance", Spell.AmnesiaChance);

            if (effect.Owner.Realm == 0 || Caster.Realm == 0)
            {
                effect.Owner.LastAttackedByEnemyTickPvE = effect.Owner.CurrentRegion.Time;
                Caster.LastAttackTickPvE = Caster.CurrentRegion.Time;
            }
            else
            {
                effect.Owner.LastAttackedByEnemyTickPvP = effect.Owner.CurrentRegion.Time;
                Caster.LastAttackTickPvP = Caster.CurrentRegion.Time;
            }

            GameSpellEffect mezz = FindEffectOnTarget(effect.Owner, "Mesmerize");
            if (mezz != null) mezz.Cancel(false);
            effect.Owner.Disease(true);

            // Slow effect, default to 15% if Value is 0
            double slowPercentage = Spell.Value != 0 ? Spell.Value / 100 : 0.15;
            effect.Owner.BuffBonusMultCategory1.Set((int)eProperty.MaxSpeed, this, 1.0 - slowPercentage);

            // Strength reduction, default to 7.5% if LifeDrainReturn is 0
            double strengthReduction = Spell.Damage != 0 ? Spell.Damage / 100 : 0.075;
            effect.Owner.BuffBonusMultCategory1.Set((int)eProperty.Strength, this, 1.0 - strengthReduction);

            SendUpdates(effect);

            string casterLanguage = (m_caster as GamePlayer)?.Client?.Account?.Language ?? "EN";
            GamePlayer ownerPlayer = effect.Owner as GamePlayer;

            // Handle translation for the effect owner
            if (ownerPlayer != null)
            {
                string message1 = string.IsNullOrEmpty(Spell.Message1) ? string.Empty : Spell.GetFormattedMessage1(ownerPlayer);
                MessageToLiving(effect.Owner, message1, eChatType.CT_Spell);
            }
            else
            {
                string message1 = string.IsNullOrEmpty(Spell.Message1) ? string.Empty : LanguageMgr.GetTranslation(casterLanguage, Spell.Message1, effect.Owner.GetName(0, false));
                MessageToLiving(effect.Owner, message1, eChatType.CT_Spell);
            }

            foreach (GamePlayer player in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
            {
                if (!(effect.Owner == player))
                {
                    string personalizedTargetName = player.GetPersonalizedName(effect.Owner);

                    string message2 = string.IsNullOrEmpty(Spell.Message2) ? string.Empty : Spell.GetFormattedMessage2(player, personalizedTargetName);
                    player.MessageFromArea(effect.Owner, message2, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
            }

            effect.Owner.StartInterruptTimer(effect.Owner.SpellInterruptDuration, AttackData.eAttackType.Spell, Caster);
            if (effect.Owner is GameNPC npc)
            {
                IOldAggressiveBrain aggroBrain = npc.Brain as IOldAggressiveBrain;
                if (aggroBrain != null)
                    aggroBrain.AddToAggroList(Caster, 1);
            }
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            base.OnEffectExpires(effect, noMessages);
            effect.Owner.Disease(false);
            effect.Owner.BuffBonusMultCategory1.Remove((int)eProperty.MaxSpeed, this);
            effect.Owner.BuffBonusMultCategory1.Remove((int)eProperty.Strength, this);

            if (!noMessages)
            {
                string casterLanguage = (m_caster as GamePlayer)?.Client?.Account?.Language ?? "EN";
                GamePlayer ownerPlayer = effect.Owner as GamePlayer;

                // Handle translation for the effect owner when it expires
                if (ownerPlayer != null)
                {
                    string message3 = string.IsNullOrEmpty(Spell.Message3) ? string.Empty : Spell.GetFormattedMessage3(ownerPlayer);
                    MessageToLiving(effect.Owner, message3, eChatType.CT_SpellExpires);
                }
                else
                {
                    string message3 = string.IsNullOrEmpty(Spell.Message3) ? string.Empty : LanguageMgr.GetTranslation(casterLanguage, Spell.Message3, effect.Owner.GetName(0, false));
                    MessageToLiving(effect.Owner, message3, eChatType.CT_SpellExpires);
                }

                foreach (GamePlayer player in effect.Owner.GetPlayersInRadius(WorldMgr.INFO_DISTANCE))
                {
                    if (!(effect.Owner == player))
                    {
                        string personalizedTargetName = player.GetPersonalizedName(effect.Owner);

                        string message4 = string.IsNullOrEmpty(Spell.Message4) ? string.Empty : Spell.GetFormattedMessage4(player, personalizedTargetName);
                        player.MessageFromArea(effect.Owner, message4, eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
                    }
                }
            }

            SendUpdates(effect);

            return 0;
        }

        protected override int CalculateEffectDuration(GameLiving target, double effectiveness)
        {
            double duration = base.CalculateEffectDuration(target, effectiveness);
            duration -= duration * target.GetResist(Spell.DamageType) * 0.01;

            if (duration < 1)
                duration = 1;
            else if (duration > (Spell.Duration * 4))
                duration = (Spell.Duration * 4);
            return (int)duration;
        }

        protected virtual void SendUpdates(GameSpellEffect effect)
        {
            effect.Owner.UpdateMaxSpeed();
            if (effect.Owner is GamePlayer player)
            {
                player.Out.SendCharStatsUpdate();
                player.Out.SendUpdateWeaponAndArmorStats();
                player.Group?.UpdateMember(effect.Owner, true, true);
            }
        }

        public override PlayerXEffect GetSavedEffect(GameSpellEffect e)
        {
            if ( //VaNaTiC-> this cannot work, cause PulsingSpellEffect is derived from object and only implements IConcEffect
                 //e is PulsingSpellEffect ||
                 //VaNaTiC<-
                Spell.Pulse != 0 || Spell.Concentration != 0 || e.RemainingTime < 1)
                return null;
            PlayerXEffect eff = new PlayerXEffect();
            eff.Var1 = Spell.ID;
            eff.Duration = e.RemainingTime;
            eff.IsHandler = true;
            eff.SpellLine = SpellLine.KeyName;
            return eff;
        }

        public override void OnEffectRestored(GameSpellEffect effect, int[] vars)
        {
            effect.Owner.Disease(true);
            double slowPercentage = Spell.Value != 0 ? Spell.Value / 100 : 0.15;
            effect.Owner.BuffBonusMultCategory1.Set((int)eProperty.MaxSpeed, this, 1.0 - slowPercentage);

            double strengthReduction = Spell.Damage != 0 ? Spell.Damage / 100 : 0.075;
            effect.Owner.BuffBonusMultCategory1.Set((int)eProperty.Strength, this, 1.0 - strengthReduction);
        }

        public override int OnRestoredEffectExpires(GameSpellEffect effect, int[] vars, bool noMessages)
        {
            return this.OnEffectExpires(effect, noMessages);
        }

        public DiseaseSpellHandler(GameLiving caster, Spell spell, SpellLine spellLine) : base(caster, spell, spellLine) { }

        public override string GetDelveDescription(GameClient delveClient)
        {
            double slowPercent = Spell.Value != 0 ? Spell.Value : 15;
            double strengthPercent = Spell.Damage != 0 ? Spell.Damage : 7.5;
            int healInhibit = Spell.AmnesiaChance != 0 ? Spell.AmnesiaChance : 50;

            return LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disease.MainDescription", slowPercent, strengthPercent.ToString("0.#"), healInhibit);
        }
    }
}
