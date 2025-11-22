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
using Discord;
using DOL.GS.PacketHandler;
using DOL.Language;
using System.Collections.Generic;
using DOL.AI.Brain;
using DOL.Database;
using DOL.GS.Effects;
using DOL.GS;
using DOL.GS.Geometry;
using DOL.GS.Scripts;
using DOL.GS.Styles;
using DOL.GS.Utils;
using System;
using log4net;
using System.Collections;
using System.Numerics;
using Vector = DOL.GS.Geometry.Vector;
using System.Drawing;
using DOL.GS.ServerProperties;
using System.Text;

namespace DOL.GS.Spells
{
    [SpellHandler("IllusionSpell")]
    public class IllusionSpell : AbstractMorphSpellHandler
    {
        private List<IllusionPet> illusionPets = new List<IllusionPet>();

        public IEnumerable<IllusionPet> Illusions => illusionPets;

        public IllusionSpell(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line)
        {
            OverwritesMorphs = false;
        }

        public override void OnEffectStart(GameSpellEffect effect)
        {
            base.OnEffectStart(effect);
            CreateIllusionPets(effect.Owner as GamePlayer);
        }

        protected override (string target, ushort radius, bool modified) GetModifiedTarget(GameObject castTarget)
        {
            var (target, radius, modified) = base.GetModifiedTarget(castTarget);
            radius = 0;
            return (target, radius, modified);
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            foreach (IllusionPet pet in illusionPets)
            {
                if (pet.IsAlive)
                {
                    foreach (GamePlayer player in pet.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        player.Out.SendSpellEffectAnimation(pet, pet, 7202, 0, false, 1);
                    }
                    pet.Die(Caster);
                }
            }
            illusionPets.Clear();
            return base.OnEffectExpires(effect, noMessages);
        }

        public override bool PreventsApplication(GameSpellEffect self, GameSpellEffect other)
        {
            // Illusion never prevents applications.
            return false;
        }

        public override void OnBetterThan(GameLiving target, GameSpellEffect oldEffect, GameSpellEffect newEffect)
        {
            SpellHandler attempt = (SpellHandler)newEffect.SpellHandler;
            if (attempt.Caster.GetController() is GamePlayer player)
                player.SendTranslatedMessage("SpellHandler.IllusionSpell.Target.Resist", eChatType.CT_SpellResisted, eChatLoc.CL_SystemWindow, player.GetPersonalizedName(target));
            attempt.SendSpellResistAnimation(target);
        }

        public override bool IsOverwritable(GameSpellEffect compare)
        {
            if (compare.SpellHandler is not IllusionSpell)
                return false;

            return base.IsOverwritable(compare);
        }

        /// <inheritdoc />
        public override bool ShouldOverwriteOldEffect(GameSpellEffect oldeffect, GameSpellEffect neweffect)
        {
            if (oldeffect.SpellHandler is IllusionSpell oldIllusion && neweffect.SpellHandler is IllusionSpell newIllusion)
            {
                if (newIllusion.Priority >= Priority)
                    return true;
                if (oldIllusion.Spell.LifeDrainReturn != 0 && newIllusion.Spell.LifeDrainReturn == 0) // unmorph
                    return false;
                if (newIllusion.Spell.Value < oldIllusion.Spell.Value) // less clones
                    return false;
                if (newIllusion.Spell.Damage < oldIllusion.Spell.Damage) // less damage
                    return false;
                return neweffect.Duration > oldeffect.RemainingTime;
            }

            if (neweffect.SpellHandler is AbstractMorphSpellHandler otherMorph)
            {
                // For illusions, we actually want to be replaced by any not-lower morph
                if (otherMorph.Priority >= Priority)
                    return true;
            }

            return base.ShouldOverwriteOldEffect(oldeffect, neweffect);
        }

        public enum eSpawnType
        {
            Random = 0,
            Circle,
            Line
        }

        private void CreateIllusionPets(GamePlayer target)
        {
            int numPets = (int)Spell.Value;
            if (numPets < 1) numPets = 1;
            
            GameNpcInventoryTemplate npcInventory = new GameNpcInventoryTemplate();
            InventoryItem item;
            foreach (eInventorySlot slot in PlayerCloner.itemsToCopy)
            {
                item = target.Inventory.GetItem(slot);
                if (item != null)
                {
                    // keep clothing manager uses model IDs of 3800-3802 for guild cloaks... doesn't seem to work here :(
                    //if ( item.SlotPosition == (int)eInventorySlot.Cloak && item.Emblem > 0 )
                    //	itemModel = 3799 + (int)player.Realm;

                    npcInventory.AddNPCEquipment(slot, item.Model, item.Color, item.Effect, item.Extension);
                }
            }

            npcInventory.CloseTemplate();
            var styles = PlayerCloner.GetStyles(target);
            var spells = PlayerCloner.GetSpells(target);

            int maxDistance = Spell.Radius == 0 ? 150 : Spell.Radius;
            Vector[] offsets = new Vector[numPets + 1];
            IllusionPet.eIllusionFlags mode = 0;

            switch ((eSpawnType)this.Spell.DamageType)
            {
                case eSpawnType.Random:
                    {
                        // We don't spawn them at actual random positions, because randomness is clumpy: https://conversableeconomist.blogspot.com/2015/03/randomness-is-lumpy-pareidolia.html
                        // Instead, we spawn them in a circle and slightly change the positions.
                        double increment = 2 * Math.PI / numPets;
                        double currentAngle = 0.0;
                        for (int i = 0; i < numPets; i++)
                        {
                            double angle = currentAngle + (Util.RandomDouble() * increment) * 1.5 - (0.5 * increment);
                            double distance = Util.RandomDouble();
                            distance = maxDistance * (1 - (distance * distance)); // Randomize distance closer to the edge...
                            offsets[i] = Vector.Create((int)Math.Round(distance * Math.Cos(angle)), (int)Math.Round(distance * Math.Sin(angle), 0));
                            currentAngle += increment;
                        }
                        offsets[numPets] = Vector.Zero;
                        mode |= IllusionPet.eIllusionFlags.RandomizePositions;
                    }
                    break;

                case eSpawnType.Circle:
                    {
                        double increment = 2 * Math.PI / numPets;
                        double currentAngle = 0.0;
                        for (int i = 0; i < numPets; i++)
                        {
                            double angle = currentAngle + increment;
                            offsets[i] = Vector.Create((int)Math.Round(maxDistance * Math.Cos(angle)), (int)Math.Round(maxDistance * Math.Sin(angle), 0));
                            currentAngle += increment;
                        }
                        offsets[numPets] = Vector.Zero;
                    }
                    break;
                
                case eSpawnType.Line:
                    {
                        int distance = Spell.Radius == 0 ? 30 : (int)Math.Round(Spell.Radius / (numPets + 1.00));
                        double angle = target.Orientation.InRadians;
                        var direction = Vector.Create((int)Math.Round(distance * Math.Cos(angle)), (int)Math.Round(distance * Math.Sin(angle), 0));
                        int i = 0;
                        int midPoint = numPets / 2;
                        while (i < midPoint)
                        {
                            offsets[i] = direction * (i - midPoint);
                            ++i;
                        }
                        while (i < numPets + 1)
                        {
                            offsets[i] = direction * ((i + 1) - midPoint);
                            ++i;
                        }
                        offsets[numPets] = Vector.Zero;
                    }
                    break;
            }

            bool hasLOS = LosCheckMgr.HasDataFor(target.CurrentRegion);
            bool enableTeleport = !target.CurrentRegion.IsDungeon && hasLOS && target == Caster;
            Util.Shuffle(offsets);
            Coordinate masterCoord = target.Coordinate + offsets[numPets];
            for (int i = 0; i < numPets; i++)
            {
                IllusionPet pet = CreatePet(target, mode, styles, spells, npcInventory);
                if (pet == null)
                {
                    continue;
                }

                var offset = offsets[i];
                Coordinate coord = target.Coordinate + offset;
                var walkOffset = coord - masterCoord;
                (pet.Brain as IllusionPetBrain).SetOffset(walkOffset.X, walkOffset.Y);
                if (enableTeleport) // Check LOS for initial placements & teleport, but then walk to the planned offset
                {
                    RaycastStats stats = new();
                    float dist = LosCheckMgr.GetCollisionDistance(target.CurrentRegion, target.Coordinate, coord, ref stats);
                    if (dist < float.MaxValue)
                    {
                        var normalDistance = offset.Length;
                        var ratio = (dist / normalDistance);
                        offset = Vector.Create((int)Math.Round(offset.X * ratio), (int)Math.Round(offset.Y * ratio), (int)Math.Round(offset.Z * ratio));
                    }
                    pet.Position = target.Position + offset;
                }
                else // Walk to destination
                {
                    pet.Position = target.Position;
                }

                offsets[i] = walkOffset;
                illusionPets.Add(pet);
            }

            if (enableTeleport)
            {
                target.MoveTo(target.Position + offsets[numPets]);
            }
            else
            {
                masterCoord = target.Coordinate;
            }

            for (int i = 0; i < numPets; ++i)
            {
                var living = illusionPets[i];
                living.AddToWorld();
                living.PathTo(masterCoord + offsets[i], target.MaxSpeed);
            }

            foreach (GamePlayer player in target.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                player.Out.SendSpellEffectAnimation(target, target, 7202, 0, false, 1);
                foreach (var living in illusionPets)
                {
                    player.Out.SendSpellEffectAnimation(living, living, 7202, 0, false, 1);
                }
            }
        }
        
        protected virtual IllusionPet CreatePet(GamePlayer target, IllusionPet.eIllusionFlags mode, List<Style> styles, IList spells, GameNpcInventoryTemplate npcInventory)
        {
            var pet = new IllusionPet(target, mode);
            pet.Owner = target;
            pet.Level = target.Level;
            pet.IsWorthReward = false;

            pet.Inventory = npcInventory;
            pet.Styles = styles;
            pet.Spells = spells;
            pet.LoadedFromScript = true;
            pet.AutoRespawn = false;
            pet.GuildName = target.GuildName;
            pet.Realm = target.Realm;
            pet.BaseEffectiveness = this.Spell.Damage / 100.0;
            pet.CloneMaxHealth = Math.Max(1, (int)Math.Round(target.GetModified(eProperty.MaxHealth) * this.Spell.AmnesiaChance / 100.0));
            pet.Health = pet.CloneMaxHealth;
            pet.WeaponDps = (int)target.WeaponDamage(target.AttackWeapon);
            pet.WeaponSpd = target.AttackWeapon?.SPD_ABS ?? 0;

            var model = GetModelFor(target);
            if (model != 0)
                pet.Model = model;
            
            pet.SwitchWeapon(target.ActiveWeaponSlot);
            return pet;
        }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            if (target is not GamePlayer)
            {
                return false;
            }

            if (FindStaticEffectOnTarget(target, typeof(NecromancerShadeEffect)) != null)
            {
                ErrorTranslationToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.TargetInvalid"), eChatType.CT_System);
                return false;
            }
            
            return base.ApplyEffectOnTarget(target, 1.0);
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string language = delveClient?.Account?.Language ?? Properties.SERV_LANGUAGE;
            eSpawnType spawnType = (eSpawnType)Spell.DamageType;
            string arrangement = LanguageMgr.GetIllusionArrangementOfType(language, spawnType);

            // Main descriptions
            string mainDesc1 = LanguageMgr.GetTranslation(language, "SpellDescription.IllusionSpell.MainDescription1", Spell.Value, arrangement);
            string mainDesc2 = LanguageMgr.GetTranslation(language, "SpellDescription.IllusionSpell.MainDescription2", Spell.Damage, Spell.AmnesiaChance, (Spell.Duration / 1000));
            string description = mainDesc1 + "\n" + mainDesc2;

            if (Spell.LifeDrainReturn > 0)
            {
                string subDesc1 = LanguageMgr.GetTranslation(language, "SpellDescription.IllusionSpell.SubDescription1", Spell.Value);
                description += "\n\n" + subDesc1;
            }

            string subDesc2Key = Spell.IsFocus
                ? "SpellDescription.IllusionSpell.SubDescription2a"
                : "SpellDescription.IllusionSpell.SubDescription2b";

            string subDesc2 = LanguageMgr.GetTranslation(language, subDesc2Key);
            description += "\n\n" + subDesc2;

            if (Spell.RecastDelay > 0)
            {
                int recastSeconds = Spell.RecastDelay / 1000;
                string subDesc3 = LanguageMgr.GetTranslation(language, "SpellDescription.Disarm.MainDescription2", recastSeconds);
                description += "\n\n" + subDesc3;
            }

            return description.TrimEnd();
        }
    }
}