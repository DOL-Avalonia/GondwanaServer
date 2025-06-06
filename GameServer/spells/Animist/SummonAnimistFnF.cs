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
using DOL.Events;
using DOL.GS.PacketHandler;
using DOL.AI.Brain;
using DOL.GS.Effects;
using DOL.GS.ServerProperties;
using DOL.Language;

namespace DOL.GS.Spells
{
    [SpellHandler("SummonAnimistFnF")]
    public class SummonAnimistFnF : SummonAnimistPet
    {
        public SummonAnimistFnF(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }

        public override bool CheckBeginCast(GameLiving selectedTarget, bool quiet)
        {
            int nCount = 0;

            Region rgn = WorldMgr.GetRegion(Caster.CurrentRegion.ID);

            if (rgn == null || rgn.GetZone(Caster.GroundTargetPosition.Coordinate) == null)
            {
                if (Caster is GamePlayer)
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "SummonAnimistFnF.CheckBeginCast.NoGroundTarget"), eChatType.CT_SpellResisted);
                return false;
            }

            foreach (GameNPC npc in Caster.CurrentRegion.GetNPCsInRadius(Caster.GroundTargetPosition.Coordinate, (ushort)Properties.TURRET_AREA_CAP_RADIUS, false, true))
                if (npc.Brain is TurretFNFBrain)
                    nCount++;

            if (nCount >= Properties.TURRET_AREA_CAP_COUNT)
            {
                if (Caster is GamePlayer)
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "SummonAnimistFnF.CheckBeginCast.TurretAreaCap"), eChatType.CT_SpellResisted);
                return false;
            }

            if (Caster.PetCount >= Properties.TURRET_PLAYER_CAP_COUNT)
            {
                if (Caster is GamePlayer)
                    MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)!.Client, "SummonAnimistFnF.CheckBeginCast.TurretPlayerCap"), eChatType.CT_SpellResisted);
                return false;
            }

            return base.CheckBeginCast(selectedTarget, quiet);
        }

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            if (!base.ApplyEffectOnTarget(target, effectiveness))
                return false;

            if (Spell.SubSpellID > 0 && SkillBase.GetSpellByID(Spell.SubSpellID) != null)
            {
                m_pet.Spells.Add(SkillBase.GetSpellByID(Spell.SubSpellID));
            }

            (m_pet.Brain as TurretBrain)!.IsMainPet = false;

            (m_pet.Brain as IOldAggressiveBrain)!.AddToAggroList(target, 1);
            (m_pet.Brain as TurretBrain)!.Think();
            //[Ganrod] Nidel: Set only one spell.
            (m_pet as TurretPet)!.TurretSpell = m_pet.Spells[0] as Spell;
            Caster.PetCount++;
            return true;
        }

        protected override void SetBrainToOwner(IControlledBrain brain)
        {
        }

        protected override void OnNpcReleaseCommand(DOLEvent e, object sender, EventArgs arguments)
        {
            m_pet = sender as GamePet;
            if (m_pet == null)
                return;

            if ((m_pet.Brain as TurretFNFBrain) == null)
                return;

            if (Caster.ControlledBrain == null)
            {
                ((GamePlayer)Caster).Out.SendPetWindow(null, ePetWindowAction.Close, 0, 0);
            }

            GameEventMgr.RemoveHandler(m_pet, GameLivingEvent.PetReleased, OnNpcReleaseCommand);

            GameSpellEffect effect = FindEffectOnTarget(m_pet, this);
            if (effect != null)
                effect.Cancel(false);
        }

        public override int OnEffectExpires(GameSpellEffect effect, bool noMessages)
        {
            Caster.PetCount--;

            return base.OnEffectExpires(effect, noMessages);
        }

        protected override IControlledBrain GetPetBrain(GameLiving owner)
        {
            return new TurretFNFBrain(owner);
        }

        public override bool CastSubSpells(GameLiving target)
        {
            return false;
        }

        public override string GetDelveDescription(GameClient delveClient)
        {
            string mainDesc = LanguageMgr.GetTranslation(delveClient, "SpellDescription.SummonAnimistFnF.MainDescription");
            return mainDesc;
        }
    }
}
