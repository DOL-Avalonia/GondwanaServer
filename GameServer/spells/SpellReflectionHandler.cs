using DOL.AI.Brain;
using DOL.Events;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.GS.PlayerClass;
using DOL.Language;
using System;
using System.Diagnostics;
using System.Linq.Expressions;

namespace DOL.GS.Spells
{
    [SpellHandler("SpellReflection")]
    public class SpellReflectionHandler : SpellHandler
    {
        public SpellReflectionHandler(GameLiving caster, Spell spell, SpellLine spellLine) : base(caster, spell, spellLine)
        {
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            GameLiving living = effect.Owner;

            GameEventMgr.AddHandler(living, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(EventHandler));
            if (Caster is GamePlayer casterPlayer)
            {
                MessageToLiving(casterPlayer, LanguageMgr.GetTranslation(casterPlayer.Client, "SpellReflection.Self.Message"), eChatType.CT_Spell);

                foreach (GamePlayer nearbyPlayer in casterPlayer.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    if (nearbyPlayer != casterPlayer)
                    {
                        nearbyPlayer.Out.SendMessage(LanguageMgr.GetTranslation(nearbyPlayer.Client, "SpellReflection.Others.Message", nearbyPlayer.GetPersonalizedName(casterPlayer)), eChatType.CT_Spell, eChatLoc.CL_SystemWindow);
                    }
                }
            }
            SendEffectAnimation(effect.Owner, 0, false, 1);
        }

        private void EventHandler(DOLEvent e, object sender, EventArgs arguments)
        {
            if (arguments is not AttackedByEnemyEventArgs args)
            {
                return;
            }
            
            AttackData ad = args.AttackData;
            if (ad is not { AttackType: AttackData.eAttackType.Spell, AttackResult: GameLiving.eAttackResult.HitUnstyled or GameLiving.eAttackResult.HitStyle })
                return;

            var attacker = ad.Attacker;
            var defender = ad.Target;
            Spell spellToCast = ad.SpellHandler.Spell.Copy();
            SpellLine line = ad.SpellHandler.SpellLine;
            if (ad.SpellHandler.Parent is BomberSpellHandler bomber)
            {
                spellToCast = bomber.Spell.Copy();
                line = bomber.SpellLine;
            }

            int power;
            double powerCalc;
            double levelRatio = (double)defender.Level / attacker.Level;
            GamePlayer? defenderPlayer = defender as GamePlayer;
            spellToCast.PowerType = Spell.ePowerType.Mana; // Force spells to use mana at this point
            powerCalc = spellToCast.Power * Spell.AmnesiaChance / 100.0;
            powerCalc /= Math.Max(1.0, levelRatio);
            if (defenderPlayer.CharacterClass.PowerType is Spell.ePowerType.Endurance)
            {
                spellToCast.PowerType = Spell.ePowerType.Endurance;
                powerCalc *= 1.5; // +50% usage of endurance compared to mana
            }
            power = (int)Math.Round(powerCalc);
            
            switch (spellToCast.PowerType)
            {
                case Spell.ePowerType.None:
                    power = 0;
                    break;
                
                case Spell.ePowerType.Mana:
                    if (ad.Target.Mana < power)
                        return;
                    break;

                case Spell.ePowerType.Endurance:
                    if (ad.Target.Endurance < power)
                        return;
                    break;

                default:
                    throw new NotImplementedException($"Unimplemented power type {Spell.PowerType} when reflecting Spell {Spell.Name} ({Spell.ID})");
            }
            
            spellToCast.Power = power;

            double absorbPercent = Spell.LifeDrainReturn;
            int damageAbsorbed = (int)(0.01 * absorbPercent * (ad.Damage + ad.CriticalDamage));

            ad.Damage -= damageAbsorbed;

            if (damageAbsorbed > 0)
            {
                if (defenderPlayer != null)
                    MessageToLiving(defenderPlayer, LanguageMgr.GetTranslation(defenderPlayer.Client, "SpellReflection.Self.Absorb", damageAbsorbed), eChatType.CT_Spell);

                if (ad.Attacker is GamePlayer playerAttacker)
                    MessageToLiving(playerAttacker, LanguageMgr.GetTranslation(playerAttacker.Client, "SpellReflection.Target.Absorbs", damageAbsorbed), eChatType.CT_Spell);
            }

            spellToCast.Damage = spellToCast.Damage * Spell.AmnesiaChance / 100;
            spellToCast.Value = spellToCast.Value * Spell.AmnesiaChance / 100;
            spellToCast.Duration = spellToCast.Duration * Spell.AmnesiaChance / 100;
            spellToCast.CastTime = 0;

            ushort ClientEffect = ad.DamageType switch
            {
                eDamageType.Body => 6172,
                eDamageType.Cold => 6057,
                eDamageType.Energy => 6173,
                eDamageType.Heat => 6171,
                eDamageType.Matter => 6174,
                eDamageType.Spirit => 6175,
                _ => 6173
            };

            foreach (GamePlayer pl in ad.Target.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                pl.Out.SendSpellEffectAnimation(ad.Target, ad.Target, ClientEffect, 0, false, 1);
            }

            if (ad.SpellHandler.Spell.Target is "ground" || (ad.SpellHandler.Target != ad.Target && ad.SpellHandler.Spell.Radius > 0))
            {
                // Don't reflect if being the secondary target of an AOE
                // TODO: Does this check work well when an AOE spell was redirected?
                return;
            }

            if (Spell.Value < 100 && !Util.Chance((int)Spell.Value))
                return;

            ISpellHandler spellhandler = ScriptMgr.CreateSpellHandler(ad.Target, spellToCast, line);
            if (spellhandler is BomberSpellHandler bomberspell)
                bomberspell.ReduceSubSpellDamage = Spell.AmnesiaChance;

            spellhandler.StartSpell(ad.Attacker, false);
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            GameLiving living = effect.Owner;

            GameEventMgr.RemoveHandler(effect.Owner, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(EventHandler));
            return base.OnEffectExpires(effect, noMessages);
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            int recastSeconds = Spell.RecastDelay / 1000;
            string mainDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.SpellReflection.MainDescription1", Spell.Value, Spell.AmnesiaChance, Spell.LifeDrainReturn);
            string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.SpellReflection.MainDescription2");

            if (Spell.RecastDelay > 0)
            {
                string thirdDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return mainDesc + "\n\n" + secondDesc + "\n\n" + thirdDesc;
            }

            return mainDesc + "\n\n" + secondDesc;
        }
    }
}