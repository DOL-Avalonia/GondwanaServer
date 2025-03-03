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
using System.Numerics;
using DOL.AI.Brain;
using DOL.Events;
using DOL.GS.Effects;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS.Spells
{
    [SpellHandler("IllusionBladeSummon")]
    public class IllusionBladeSummon : SummonSpellHandler
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        public override bool ApplyEffectOnTarget(GameLiving target, double effectiveness)
        {
            //Template of the Illusionblade NPC
            INpcTemplate template = NpcTemplateMgr.GetTemplate(Spell.LifeDrainReturn);

            if (template == null)
            {
                if (log.IsWarnEnabled)
                    log.WarnFormat("NPC template {0} not found! Spell: {1}", Spell.LifeDrainReturn, Spell.ToString());
                MessageToCaster(LanguageMgr.GetTranslation((Caster as GamePlayer)?.Client, "SpellHandler.Convoker.TemplateNotFound", (ushort)Spell.LifeDrainReturn), eChatType.CT_System);
                return false;
            }

            GameSpellEffect effect = CreateSpellEffect(target, effectiveness);
            IControlledBrain brain = GetPetBrain(Caster);
            m_pet = GetGamePet(template);
            m_pet.SetOwnBrain(brain as AI.ABrain);
            m_pet.Position = GetSummonPosition();
            // m_pet.CurrentSpeed = 0;
            m_pet.Realm = Caster.Realm;
            m_pet.Race = 0;
            m_pet.Level = 44; // lowered in patch 1109b, also calls AutoSetStats()
            m_pet.AddToWorld();
            //Check for buffs
            if (brain is ControlledNpcBrain)
                (brain as ControlledNpcBrain)!.CheckSpells(StandardMobBrain.eCheckSpellType.Defensive);

            AddHandlers();
            SetBrainToOwner(brain);

            effect.Start(m_pet);
            //Set pet infos & Brain
            return true;
        }

        protected override GamePet GetGamePet(INpcTemplate template) { return new IllusionBladePet(template); }
        protected override IControlledBrain GetPetBrain(GameLiving owner) { return new ProcPetBrain(owner); }
        protected override void SetBrainToOwner(IControlledBrain brain) { }
        protected override void AddHandlers() { GameEventMgr.AddHandler(m_pet, GameLivingEvent.AttackFinished, EventHandler); }

        protected void EventHandler(DOLEvent e, object sender, EventArgs arguments)
        {
            AttackFinishedEventArgs args = arguments as AttackFinishedEventArgs;
            if (args == null || args.AttackData == null)
                return;
        }
        public IllusionBladeSummon(GameLiving caster, Spell spell, SpellLine line)
            : base(caster, spell, line) { }
    }
}

namespace DOL.GS
{
    public class IllusionBladePet : GamePet
    {
        public override int MaxHealth
        {
            get { return Level * 10; }
        }
        public override void OnAttackedByEnemy(AttackData ad) { }
        public IllusionBladePet(INpcTemplate npcTemplate) : base(npcTemplate) { }
    }
}

