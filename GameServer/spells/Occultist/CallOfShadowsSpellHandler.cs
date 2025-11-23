using Discord;
using DOL.Database;
using DOL.Events;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.GS.Spells;
using DOL.Language;
using System;
using static DOL.GS.Spells.SpellHandler.OccultistForms;

namespace DOL.GS.Spells
{
    public class CallOfShadowsSpellEffect : GameSpellEffect
    {
        public CallOfShadowsSpellEffect(ISpellHandler handler, int duration, int pulseFreq)
            : base(handler, duration, pulseFreq) { }

        public CallOfShadowsSpellEffect(ISpellHandler handler, int duration, int pulseFreq, double effectiveness)
            : base(handler, duration, pulseFreq, effectiveness) { }
    }

    /// </summary>
    [SpellHandler("CallOfShadows")]
    public class CallOfShadowsSpellHandler : AbstractMorphSpellHandler
    {
        private const string FLAG_ACTIVE = "COS_ACTIVE";
        private const string FLAG_UNINTERRUPTIBLE = "COS_UNINTERRUPTIBLE";

        private const int MODEL = 1695;
        private const int DISEASE_ID = 25296;
        private const int DISEASE_PCT = 50;

        private const int DEC_SPELL_DMG_PCT = 10;
        private const int DEC_ABSORB_PCT = 5;
        private const float DEC_REGEN_MULT = 5.5f;

        private const int CHT_VALUE_CORE = 30;
        private const int CHT_PER_LVL_AF = 4;
        private const int CHT_SEC_RES_PCT = 15;
        private const double CHT_ABSORB_PCT_DBL = CHT_VALUE_CORE * (2.0 / 3.0);
        private const int CHT_ABSORB_PCT = (int)(CHT_ABSORB_PCT_DBL * 2 + 1) / 2; // Rounding in constexpr
        private const int SPI_STEALTH_DET = 20;

        // ---- Global booster ----
        private const double BOOST = 1.10;

        // Snapshots we apply/undo
        private int _spellDmgBonus;
        private int _absBonus;
        private float _regenMult;
        private int _hpFlat;
        private int _wsPct;
        private int _secResPct;
        private int _afFlat;
        private int _tempParryLevel;
        private int _stealthDet;

        public CallOfShadowsSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line)
        {
            Priority = 700;
            OverwritesMorphs = true;
        }

        public override ushort GetModelFor(GameLiving living) => MODEL;

        protected override GameSpellEffect CreateSpellEffect(GameLiving target, double effectiveness)
            => new GameSpellEffect(this, CalculateEffectDuration(target, effectiveness), 0);

        public override bool PreventsApplication(GameSpellEffect self, GameSpellEffect other)
        {
            return base.PreventsApplication(self, other);
        }

        private void SetFormProperties(GameLiving target, bool apply)
        {
            // Flags: uninterruptible (no move-cast flag set)
            if (apply)
            {
                target.TempProperties.setProperty(FLAG_ACTIVE, true);
                target.TempProperties.setProperty(FLAG_UNINTERRUPTIBLE, true);

                // Pet “spirit-like” template swap
                target.TempProperties.setProperty(KEY_COS, true);
            }
            else
            {
                target.TempProperties.removeProperty(FLAG_ACTIVE);
                target.TempProperties.removeProperty(FLAG_UNINTERRUPTIBLE);

                target.TempProperties.removeProperty(KEY_COS);
            }
        }

        private void ApplyFormEffects(GameLiving target, bool apply)
        {
            var mult = (sbyte)(apply ? 1 : -1);

            // Decrepit bonuses:
            target.SpecBuffBonusCategory[(int)eProperty.SpellDamage] += mult * _spellDmgBonus;
            target.SpecBuffBonusCategory[(int)eProperty.DotDamageBonus] += mult * _spellDmgBonus;
            target.SpecBuffBonusCategory[(int)eProperty.ArmorAbsorption] += mult * _absBonus;

            // Regen (multiplicative bucket)
            if (Math.Abs(_regenMult) > 0.0001f)
            {
                if (apply)
                    target.BuffBonusMultCategory1.Set((int)eProperty.HealthRegenerationRate, this, _regenMult);
                else
                    target.BuffBonusMultCategory1.Remove((int)eProperty.HealthRegenerationRate, this);
            }

            // Chtonic bonuses:
            var hpBonus = mult * _hpFlat;
            target.OtherBuffBonus[(int)eProperty.ArmorFactor] += mult * _afFlat;
            target.BaseBuffBonusCategory[(int)eProperty.MaxHealth] += hpBonus;
            if (hpBonus != 0 && apply)
                target.Health += hpBonus;
            target.SpecBuffBonusCategory[(int)eProperty.WeaponSkill] += mult * _wsPct;
            target.SpecBuffBonusCategory[(int)eProperty.Resist_Heat] += mult * _secResPct;
            target.SpecBuffBonusCategory[(int)eProperty.Resist_Cold] += mult * _secResPct;
            target.SpecBuffBonusCategory[(int)eProperty.Resist_Matter] += mult * _secResPct;
            target.SpecBuffBonusCategory[(int)eProperty.Resist_Body] += mult * _secResPct;
            target.SpecBuffBonusCategory[(int)eProperty.Resist_Spirit] += mult * _secResPct;
            target.SpecBuffBonusCategory[(int)eProperty.Resist_Energy] += mult * _secResPct;

            // Spirit Bonus:
            target.BaseBuffBonusCategory[(int)eProperty.StealthDetectionBonus] += mult * _stealthDet;

            // Parry specialization (temporary) + parry chance
            if (target is GamePlayer pl)
            {
                new RegionTimerAction<GamePlayer>(pl, p =>
                {
                    _tempParryLevel = GS.CharacterClassOccultist.ModTempParry(p, apply, _tempParryLevel);
                    return 0;
                }).Start(1);
            }

            if (apply)
            {
                GameEventMgr.AddHandler(target, GameLivingEvent.AttackedByEnemy, OnAttackedByEnemy);
                GameEventMgr.AddHandler(target, GameLivingEvent.AttackedByEnemy, ClearInterruptIfCasting);
            }
            else
            {
                GameEventMgr.RemoveHandler(target, GameLivingEvent.AttackedByEnemy, OnAttackedByEnemy);
                GameEventMgr.RemoveHandler(target, GameLivingEvent.AttackedByEnemy, ClearInterruptIfCasting);
            }
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);

            // Note: THIS WILL NOT BE ABLE TO HANDLE A RADIUS ON THIS SPELL, HITTING SEVERAL TARGETS AT ONCE.

            var o = effect.Owner;
            var gp = o as GamePlayer;

            // Decrepit:
            _spellDmgBonus = (int)Math.Round(DEC_SPELL_DMG_PCT * BOOST);
            int decrepitAbs = (int)Math.Round(DEC_ABSORB_PCT * BOOST);
            _regenMult = (float)(DEC_REGEN_MULT * BOOST);

            // Chtonic:
            _wsPct = (int)Math.Round(CHT_VALUE_CORE * BOOST);
            _secResPct = (int)Math.Round(CHT_SEC_RES_PCT * BOOST);
            int chtAbs = (int)Math.Round(CHT_ABSORB_PCT * BOOST);
            // HP flat from % of current MaxHealth: 30% -> 33%
            _hpFlat = (int)Math.Round(o.MaxHealth * (CHT_VALUE_CORE / 100.0) * BOOST);
            // AF flat from per-level and %AF component
            int afPerLevel = (int)Math.Round(CHT_PER_LVL_AF * BOOST);
            _afFlat = (int)Math.Round(BOOST * ChtonicShapeShift.GetAFBonus(effect.Owner, CHT_VALUE_CORE, afPerLevel));

            // Spirit:
            _stealthDet = (int)Math.Round(SPI_STEALTH_DET * BOOST);

            // Total absorb pool (stack Decrepit + Chtonic flavors)
            _absBonus = decrepitAbs + chtAbs;
            
            _tempParryLevel = (int)o.Level;

            SetFormProperties(o, true);
            ApplyFormEffects(o, true);

            if (o is GamePlayer cosOwner)
                SetOccultistPetForm(cosOwner, true);

            if (gp != null)
            {
                gp.Out.SendUpdateWeaponAndArmorStats();
                gp.Out.SendCharStatsUpdate();
                gp.Out.SendCharResistsUpdate();
                gp.UpdatePlayerStatus();
                gp.Out.SendUpdatePlayer();
            }
            else
            {
                o.UpdateHealthManaEndu();
            }
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            var o = effect.Owner;
            var gp = o as GamePlayer;

            SetFormProperties(o, false);
            ApplyFormEffects(o, false);

            // Pets: flip back to base templates
            if (o is GamePlayer cosOwner)
                SetOccultistPetForm(cosOwner, false);

            if (gp != null)
            {
                gp.Out.SendUpdateWeaponAndArmorStats();
                gp.Out.SendCharStatsUpdate();
                gp.Out.SendCharResistsUpdate();
                gp.UpdatePlayerStatus();
                gp.Out.SendUpdatePlayer();
            }
            else
            {
                o.UpdateHealthManaEndu();
            }

            return base.OnEffectExpires(effect, noMessages);
        }

        private void OnAttackedByEnemy(DOLEvent e, object sender, EventArgs args)
        {
            if (args is not AttackedByEnemyEventArgs { AttackData: { } ad })
                return;

            if (!ad.IsSuccessfulHit || (!ad.IsMeleeAttack && !ad.IsRangedAttack))
                return;

            if ((ad.Damage + ad.CriticalDamage) <= 0)
                return;

            if (!Util.Chance(DISEASE_PCT))
                return;

            var owner = (GameLiving)sender;

            var disease = SkillBase.GetSpellByID(DISEASE_ID) ?? new Spell(new DBSpell
            {
                SpellID = DISEASE_ID,
                Name = "Shadow Rot",
                Description = "A rotting disease that slows, weakens, and inhibits heals.",
                Type = "Disease",
                Target = "enemy",
                Damage = 30,
                DamageType = (int)eDamageType.Body,
                Duration = 20000
            }, 50);

            var line = SkillBase.GetSpellLine(GlobalSpellsLines.Reserved_Spells);
            ScriptMgr.CreateSpellHandler(owner, disease, line)?.StartSpell(ad.Attacker);
        }

        private void ClearInterruptIfCasting(DOLEvent e, object sender, EventArgs args)
        {
            if (sender is not GamePlayer gp) return;
            if (!gp.IsCasting) return;
            if (!gp.TempProperties.getProperty(FLAG_ACTIVE, false)) return;

            gp.DisabledCastingTimeout = 0;
        }
    }
}
