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
using DOL.GS.ServerProperties;
using DOL.Language;
using System;
using System.Collections;

namespace DOL.GS.Keeps
{
    /// <summary>
    /// Represents a keep hastener
    /// </summary>
    public class FrontierHastener : GameKeepGuard
    {
        public override eFlags Flags
        {
            get { return eFlags.PEACE; }
        }

        protected override void SetModel()
        {
            if (!ServerProperties.Properties.AUTOMODEL_GUARDS_LOADED_FROM_DB && !LoadedFromScript)
            {
                return;
            }
            switch (Realm)
            {
                case eRealm.None:
                case eRealm.Albion:
                    {
                        Model = (ushort)eLivingModel.AlbionHastener;
                        Size = 45;
                        break;
                    }
                case eRealm.Midgard:
                    {
                        Model = (ushort)eLivingModel.MidgardHastener;
                        Size = 50;
                        Flags ^= eFlags.GHOST;
                        break;
                    }
                case eRealm.Hibernia:
                    {
                        Model = (ushort)eLivingModel.HiberniaHastener;
                        Size = 45;
                        break;
                    }
            }
            return;
        }

        #region Examine/Interact Message

        /// <summary>
        /// Adds messages to ArrayList which are sent when object is targeted
        /// </summary>
        /// <param name="player">GamePlayer that is examining this object</param>
        /// <returns>list with string messages</returns>
        public override IList GetExamineMessages(GamePlayer player)
        {
            IList list = new ArrayList();
            list.Add("You examine " + GetName(0, false) + ".  " + GetPronoun(0, true) + " is " + GetAggroLevelString(player, false) + " and is a hastener.");
            return list;
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            TurnTo(player, 5000);
            GameNPCHelper.CastSpellOnOwnerAndPets(this, player, SkillBase.GetSpellByID(GameHastener.SPEEDOFTHEREALMID), SkillBase.GetSpellLine(GlobalSpellsLines.Realm_Spells), false);
            return true;
        }
        #endregion Examine/Interact Message

        protected override void SetRespawnTime()
        {
            RespawnInterval = 5000;
        }

        protected override void SetName()
        {
            Name = LanguageMgr.GetTranslation(Properties.SERV_LANGUAGE, "SetGuardName.Hastener");
            return;
        }
    }
}