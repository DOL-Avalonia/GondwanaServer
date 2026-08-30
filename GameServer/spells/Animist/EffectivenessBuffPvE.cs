using DOL.AI.Brain;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using System;

namespace DOL.GS.Spells
{
    [SpellHandlerAttribute("EffectivenessBuffPvE")]
    public class EffectivenessBuffPvESpellHandler : SpellHandler
    {
        public override bool HasPositiveEffect => true;

        public EffectivenessBuffPvESpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        /// <summary>
        /// Perform validation checks before allowing the player to cast.
        /// Enforces strictly PvE-only restrictions and custom Pet targeting rules.
        /// </summary>
        public override bool CheckBeginCast(GameLiving selectedTarget, bool quiet)
        {
            if (Caster is GamePlayer player)
            {
                if (player.IsInPvP ||
                    player.IsInRvR ||
                    player.isInBG ||
                    GameServer.ServerRules.IsInPvPArea(player) ||
                    player.TempProperties.getProperty<bool>("ArenaParticipant", false) ||
                    player.DuelTarget != null)
                {
                    if (!quiet)
                    {
                        MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellDescription.EffectivenessBuffPvE.MainDescription3"), eChatType.CT_Important);
                    }
                    return false;
                }
            }

            if (Spell.LifeDrainReturn == 1 && Spell.Target.ToLower() == "pet")
            {
                GameLiving actualTarget = selectedTarget;
                if (actualTarget == null || !(Caster.IsControlledNPC(actualTarget as GameNPC) || (actualTarget as GameNPC)?.GetLivingOwner() == Caster))
                {
                    if (Caster.ControlledBrain != null && Caster.ControlledBrain.Body != null)
                        actualTarget = Caster.ControlledBrain.Body;
                }

                if (actualTarget is GameNPC npcTarget)
                {
                    GameLiving owner = npcTarget.GetLivingOwner() ?? (npcTarget as GamePet)?.Owner;
                    bool isPet = owner != null || npcTarget is GamePet || npcTarget.Brain is IControlledBrain;
                    if (isPet)
                    {
                        if (!(npcTarget is TurretPet))
                        {
                            if (!quiet) MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.EffectivenessBuffPvE.OnlyTurrets"), eChatType.CT_SpellResisted);
                            return false;
                        }
                    }
                }
            }

            return base.CheckBeginCast(selectedTarget, quiet);
        }

        /// <summary>
        /// Applies the effect to the target.
        /// Custom feature: If Spell.LifeDrainReturn == 1, it filters out all pets EXCEPT the caster's own Animist Turrets.
        /// </summary>
        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            if (Spell.LifeDrainReturn == 1 && target is GameNPC npcTarget)
            {
                GameLiving owner = npcTarget.GetLivingOwner() ?? (npcTarget as GamePet)?.Owner;
                bool isPet = owner != null || npcTarget is GamePet || npcTarget.Brain is IControlledBrain;

                if (isPet)
                {
                    if (!(npcTarget is TurretPet))
                        return false;
                }
            }

            return base.ApplyEffectOnTarget(target, effectiveness);
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);

            effect.Owner.BaseBuffBonusCategory[eProperty.LivingEffectiveness] += (int)Spell.Value;

            if (effect.Owner is GamePlayer player)
            {
                player.Out.SendUpdateWeaponAndArmorStats();
                player.Out.SendStatusUpdate();
            }
        }

        /// <summary>
        /// When the applied effect expires and is removed from the target.
        /// </summary>
        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            int immunity = base.OnEffectExpires(effect, noMessages);
            effect.Owner.BaseBuffBonusCategory[eProperty.LivingEffectiveness] -= (int)Spell.Value;

            if (effect.Owner is GamePlayer player)
            {
                player.Out.SendUpdateWeaponAndArmorStats();
                player.Out.SendStatusUpdate();
            }

            return immunity;
        }

        /// <summary>
        /// Generate the custom tool-tip description delving details.
        /// </summary>
        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;
            string mainDesc;

            if (Spell.Radius > 0)
            {
                mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.EffectivenessBuffPvE.MainDescription1", Spell.Radius, Spell.Value);
            }
            else
            {
                mainDesc = LanguageMgr.GetTranslation(language, "SpellDescription.EffectivenessBuffPvE.MainDescription2", Spell.Value);
            }

            string pveDesc = LanguageMgr.GetTranslation(language, "SpellDescription.EffectivenessBuffPvE.MainDescription3");
            string description = mainDesc + "\n\n" + pveDesc;

            if (Spell.RecastDelay > 0)
            {
                string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return description + "\n\n" + secondDesc;
            }

            return description;
        }
    }
}