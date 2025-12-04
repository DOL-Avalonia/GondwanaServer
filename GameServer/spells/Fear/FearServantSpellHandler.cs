using System;
using System.Collections.Generic;
using System.Linq;

using DOL.AI.Brain;
using DOL.GS;
using DOL.GS.Effects;
using DOL.GS.PlayerClass;
using DOL.GS.ServerProperties;
using DOL.Language;

namespace DOL.GS.Spells
{
    [SpellHandler("FearServant")]
    public class FearServantSpellHandler : SpellHandler
    {
        private readonly ReaderWriterDictionary<GameNPC, PetFearBrain> _petFearBrains = new();
        private readonly ReaderWriterDictionary<GameNPC, FearBrain> _npcFearBrains = new();

        private const string PET_FEAR_FLAG = "FEAR_SERVANT_ACTIVE";
        private const string USE_PET_BRAIN = "FEAR_SERVANT_USE_PET_BRAIN";

        private static bool HasEffect(GameLiving target, string spellType)
            => SpellHandler.FindEffectOnTarget(target, spellType) != null;

        private static bool IsCharmedOrFriend(GameNPC npc)
            => HasEffect(npc, "Charm") || HasEffect(npc, "BeFriend") || (npc.Brain is FriendBrain);

        private static bool IsFollowingFriend(GameNPC npc) => npc.Brain is FollowingFriendMobBrain;

        private static bool IsTurret(GameNPC npc) => npc.Brain is TurretBrain;

        /// <summary>
        /// True if this NPC is a valid "servant" (player-owned/controlled) target.
        /// (What FearServant used to affect before.)
        /// </summary>
        private static bool IsEligibleServant(GameNPC npc)
        {
            if (npc.Brain is IllusionPetBrain) return true;
            if (IsTurret(npc)) return true;

            if (npc.Brain is ControlledNpcBrain) return true;
            if (npc.Brain is TheurgistPetBrain) return true;
            if (npc.Brain is NoveltyPetBrain) return true;

            if (IsCharmedOrFriend(npc)) return true;
            if (IsFollowingFriend(npc)) return true;

            return false;
        }

        public FearServantSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override void FinishSpellCast(GameLiving target, bool force = false)
        {
            ConsumePower(target);
            base.FinishSpellCast(target, force);
        }

        /// <summary>
        /// If AmnesiaChance == 0 → ONLY servants (previous behavior).
        /// If AmnesiaChance > 0 → ANY GameNPC (regular mobs too).
        /// </summary>
        public override IList<GameLiving> SelectTargets(GameObject castTarget, bool force = false)
        {
            bool amnesiaMode = Spell.AmnesiaChance > 0;

            return base.SelectTargets(castTarget, force)
                       .Where(t =>
                       {
                           var npc = t as GameNPC;
                           if (npc == null) return false;

                           return amnesiaMode
                               ? true
                               : IsEligibleServant(npc);
                       })
                       .ToList();
        }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            var npc = target as GameNPC;
            if (npc == null) return false;

            if (npc.Brain is IllusionPetBrain || IsTurret(npc))
            {
                if (npc.IsAlive)
                    npc.Die(m_caster);
                return true;
            }

            bool amnesiaMode = Spell.AmnesiaChance > 0;

            if (!amnesiaMode)
            {
                if (!IsEligibleServant(npc))
                    return false;

                if (npc.Level > Spell.Value)
                {
                    OnSpellResisted(target);
                    return true;
                }

                npc.TempProperties.setProperty(USE_PET_BRAIN, true);
                return base.ApplyEffectOnTarget(target, effectiveness);
            }

            if (IsCharmedOrFriend(npc) || IsFollowingFriend(npc))
            {
                TryCancelCharmOrBefriend(npc);
                TryReleaseOwnership(npc);
                npc.StopFollowing();
                npc.TempProperties.setProperty(USE_PET_BRAIN, false); // treat as normal NPC fear
            }

            else if (npc.Brain is ControlledNpcBrain || npc.Brain is TheurgistPetBrain || npc.Brain is NoveltyPetBrain)
            {
                npc.TempProperties.setProperty(USE_PET_BRAIN, true);
            }

            else
            {
                npc.TempProperties.setProperty(USE_PET_BRAIN, false);
            }

            // level resist (applies to all non-instant-kill cases)
            if (npc.Level > Spell.Value)
            {
                OnSpellResisted(target);
                return true;
            }

            return base.ApplyEffectOnTarget(target, effectiveness);
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            var npc = effect.Owner as GameNPC;
            if (npc == null) { base.OnEffectStart(effect); return; }

            bool usePetBrain = npc.TempProperties.getProperty<bool>(USE_PET_BRAIN, false);

            if (usePetBrain)
            {
                npc.TempProperties.setProperty(PET_FEAR_FLAG, true);
                var petBrain = new PetFearBrain(m_caster);
                _petFearBrains.AddOrReplace(npc, petBrain);
                npc.AddBrain(petBrain);
                petBrain.Think();
            }
            else
            {
                var fearBrain = new FearBrain();
                _npcFearBrains.AddOrReplace(npc, fearBrain);
                npc.AddBrain(fearBrain);
                fearBrain.Think();
            }

            base.OnEffectStart(effect);
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            var npc = effect.Owner as GameNPC;

            if (npc != null)
            {
                if (_petFearBrains.TryRemove(npc, out var petBrain))
                {
                    petBrain.RemoveEffect();
                    npc.RemoveBrain(petBrain);
                    npc.TempProperties.removeProperty(PET_FEAR_FLAG);
                }
                else if (_npcFearBrains.TryRemove(npc, out var fearBrain))
                {
                    fearBrain.RemoveEffect();
                    npc.RemoveBrain(fearBrain);
                }

                npc.TempProperties.removeProperty(USE_PET_BRAIN);

                if (npc.Brain == null)
                    npc.AddBrain(new StandardMobBrain());
            }

            return base.OnEffectExpires(effect, noMessages);
        }

        public override void OnSpellResisted(GameLiving target)
        {
            SendSpellResistAnimation(target);
            SendSpellResistMessages(target);
            StartSpellResistLastAttackTimer(target);
        }

        private static void TryCancelCharmOrBefriend(GameNPC npc)
        {
            (SpellHandler.FindEffectOnTarget(npc, "Charm") as GameSpellEffect)?.Cancel(false);
            (SpellHandler.FindEffectOnTarget(npc, "BeFriend") as GameSpellEffect)?.Cancel(false);
        }

        private static void TryReleaseOwnership(GameNPC npc)
        {
            if (npc.Owner != null)
            {
                DOL.Events.GameEventMgr.Notify(DOL.Events.GameLivingEvent.PetReleased, npc);
                npc.Owner = null;
            }
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            int recastSeconds = Spell.RecastDelay / 1000;

            string main;
            string secondDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.FearServant.PetDescription");

            if (Spell.AmnesiaChance > 0)
            {
                if (Spell.Radius > 0)
                {
                    main = LanguageMgr.GetTranslation(language, "SpellDescription.FearServant.AreaTarget", Spell.Value, Spell.Radius);
                }
                else
                {
                    main = LanguageMgr.GetTranslation(language, "SpellDescription.FearServant.SingleTarget", Spell.Value);
                }
            }
            else
            {
                if (Spell.Radius > 0)
                {
                    main = LanguageMgr.GetTranslation(language, "SpellDescription.FearServant.AreaTarget&Npc", Spell.Value, Spell.Radius);
                }
                else
                {
                    main = LanguageMgr.GetTranslation(language, "SpellDescription.FearServant.SingleTarget&Npc", Spell.Value);
                }
            }

            if (Spell.RecastDelay > 0)
            {
                string thirdDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                return main + "\n\n" + secondDesc + "\n\n" + thirdDesc;
            }

            switch (Spell.ID)
            {
                case 25259:
                case 25260:
                case 25261:
                case 25264:
                case 25265:
                    {
                        var viewer = delveClient?.Player;
                        if (viewer?.CharacterClass is ClassOccultist)
                        {
                            main += "\n\n" + "Can't be cast under Chtonic form.";
                        }
                        break;
                    }
            }
            return main + "\n\n" + secondDesc;
        }
    }
}