﻿using DOL.Database;
using DOL.Events;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.Styles;
using DOL.Language;
using DOLDatabase.Tables;
using System;
using System.Numerics;
using System.Text.RegularExpressions;

namespace DOL.GS.Spells
{
    [SpellHandler("ShadowStrike")]
    public class ShadowStrikeSpellHandler : SpellHandler
    {

        public override void FinishSpellCast(GameLiving target)
        {
            base.FinishSpellCast(target);

            GameEventMgr.RemoveHandler(Caster, GameLivingEvent.Moving, new DOLEventHandler(EventManager));
            GameEventMgr.RemoveHandler(Caster, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(EventManager));

            // Hide the player
            ((GamePlayer)Caster).Stealth(!Caster.IsStealthed);

            // teleport the player
            double angleFactor = (2 * Math.PI) / 4096.0;
            double headingInRadians = angleFactor * target.Heading;
            int offset = 10;

            int x = (int)(target.Position.X - (offset * Math.Sin(headingInRadians)));
            int y = (int)(target.Position.Y + (offset * Math.Cos(headingInRadians)));
            int z = target.Position.Z;
            ushort heading = m_caster.Heading;
            ushort regionID = target.CurrentRegionID;

            Position newPos = Position.Create(regionID, x, y, z, heading);

            m_caster.MoveTo(newPos);

            // use style

            Style style = new Style(GameServer.Database.SelectObjects<DBStyle>(DB.Column("StyleID").IsEqualTo(968))[0]);
            StyleProcessor.TryToUseStyle(Caster, target, style);
        }

        public override bool CastSpell(GameLiving target)
        {
            if (!base.CastSpell(target))
                return false;

            GameEventMgr.RemoveHandler(Caster, GameLivingEvent.Moving, new DOLEventHandler(EventManager));
            GameEventMgr.RemoveHandler(Caster, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(EventManager));
            GameEventMgr.AddHandler(Caster, GameLivingEvent.Moving, new DOLEventHandler(EventManager));
            GameEventMgr.AddHandler(Caster, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(EventManager));
            return true;
        }

        private void EventManager(DOLEvent e, object sender, EventArgs args)
        {
            GameEventMgr.RemoveHandler(Caster, GameLivingEvent.Moving, new DOLEventHandler(EventManager));
            GameEventMgr.RemoveHandler(Caster, GameLivingEvent.AttackedByEnemy, new DOLEventHandler(EventManager));

            if (e == GameLivingEvent.Moving)
                MessageToCaster(LanguageMgr.GetTranslation(((GamePlayer)Caster).Client.Account.Language, "ShadowStrikeAbility.Moving"), eChatType.CT_Important);
            if (e == GameLivingEvent.AttackedByEnemy)
                MessageToCaster(LanguageMgr.GetTranslation(((GamePlayer)Caster).Client.Account.Language, "ShadowStrikeAbility.Attacked"), eChatType.CT_Important);
            InterruptCasting();
        }

        public ShadowStrikeSpellHandler(GameLiving caster, Spell spell, SpellLine line) : base(caster, spell, line) { }
    }
}