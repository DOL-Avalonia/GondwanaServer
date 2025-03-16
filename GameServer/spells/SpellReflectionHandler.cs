﻿using DOL.AI.Brain;
using DOL.Events;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.Language;
using System;

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
            if (!(arguments is AttackedByEnemyEventArgs args))
            {
                return;
            }
            AttackData ad = args.AttackData;

            if (ad is { AttackType: AttackData.eAttackType.Spell or AttackData.eAttackType.DoT, AttackResult: GameLiving.eAttackResult.HitUnstyled or  GameLiving.eAttackResult.HitStyle } )
            {
                Spell spellToCast = ad.SpellHandler.Spell.Copy();
                SpellLine line = ad.SpellHandler.SpellLine;
                if (ad.SpellHandler.Parent != null && ad.SpellHandler.Parent is BomberSpellHandler bomber)
                {
                    spellToCast = bomber.Spell.Copy();
                    line = bomber.SpellLine;
                }

                int cost;
                GamePlayer player = ad.Target as GamePlayer;
                if (player != null && player.CharacterClass is Salvage)
                {
                    cost = ((spellToCast.Power * Spell.AmnesiaChance / 100) / 2) / Math.Max(1, (ad.Target.Level / ad.Attacker.Level));
                    spellToCast.CostPower = false;
                }
                else
                {
                    cost = (spellToCast.Power * Spell.AmnesiaChance / 100) / Math.Max(1, (ad.Target.Level / ad.Attacker.Level));
                    spellToCast.CostPower = true;
                    if (ad.Target.Mana < cost)
                        return;
                }
                spellToCast.Power = cost;

                double absorbPercent = Spell.LifeDrainReturn;

                int damageAbsorbed = (int)(0.01 * absorbPercent * (ad.Damage + ad.CriticalDamage));

                ad.Damage -= damageAbsorbed;
                if (player != null)
                    MessageToLiving(player, LanguageMgr.GetTranslation(player.Client, "SpellReflection.Self.Absorb", damageAbsorbed), eChatType.CT_Spell);
                if (ad.Attacker is GamePlayer attacker)
                    MessageToLiving(attacker, LanguageMgr.GetTranslation(attacker.Client, "SpellReflection.Target.Absorbs", damageAbsorbed), eChatType.CT_Spell);

                spellToCast.Damage = spellToCast.Damage * Spell.AmnesiaChance / 100;
                spellToCast.Value = spellToCast.Value * Spell.AmnesiaChance / 100;
                spellToCast.Duration = spellToCast.Duration * Spell.AmnesiaChance / 100;
                spellToCast.CastTime = 0;
                ushort ClientEffect = 0;

                switch (ad.DamageType)
                {
                    case eDamageType.Body:
                        ClientEffect = 6172;
                        break;
                    case eDamageType.Cold:
                        ClientEffect = 6057;
                        break;
                    case eDamageType.Energy:
                        ClientEffect = 6173;
                        break;
                    case eDamageType.Heat:
                        ClientEffect = 6171;
                        break;
                    case eDamageType.Matter:
                        ClientEffect = 6174;
                        break;
                    case eDamageType.Spirit:
                        ClientEffect = 6175;
                        break;
                    default:
                        ClientEffect = 6173;
                        break;
                }
                foreach (GamePlayer pl in ad.Target.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    pl.Out.SendSpellEffectAnimation(ad.Target, ad.Target, ClientEffect, 0, false, 1);
                }
                if (!Util.Chance((int)Spell.Value))
                    return;
                ISpellHandler spellhandler = ScriptMgr.CreateSpellHandler(ad.Target, spellToCast, line);
                if (spellhandler is BomberSpellHandler bomberspell)
                    bomberspell.ReduceSubSpellDamage = Spell.AmnesiaChance;
                spellhandler.CastSpell(ad.Attacker);

                if (Spell.HasSubSpell)
                    CastSubSpells(ad.Attacker);
            }
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
            string mainDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.SpellReflection.MainDescription1", Spell.Name, Spell.Value, Spell.AmnesiaChance, Spell.LifeDrainReturn);
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