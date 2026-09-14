using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.Language;
using System;
using System.Collections;

namespace DOL.GS
{
    /// <summary>
    /// GameMovingObject is a base class for boats and siege weapons.
    /// </summary>
    public class GameSiegeCatapult : GameSiegeWeapon
    {
        public GameSiegeCatapult()
            : base()
        {
            MeleeDamageType = eDamageType.Crush;
            Name = "field catapult";
            AmmoType = 0x13;
            this.Effect = 0x89C;
            this.Model = 0xA26;
            ActionDelay = new int[]
            {
                0,//none
				5000,//aiming
				10000,//arming
				0,//loading
				2500//fireing
			};//en ms
            /*SpellLine siegeWeaponSpellLine = SkillBase.GetSpellLine(GlobalSpellsLines.SiegeWeapon_Spells);
			IList spells = SkillBase.GetSpellList(siegeWeaponSpellLine.KeyName);
			if (spells != null)
			{
				foreach (Spell spell in spells)
				{
					if (spell.ID == 2430) //TODO good id for catapult
					{
						if(spell.Level <= Level)
						{
							m_spellHandler = ScriptMgr.CreateSpellHandler(this, spell, siegeWeaponSpellLine);
						}
						break;
					}
				}
			}*/
        }

        protected IList SelectTargets()
        {
            ArrayList list = new ArrayList(20);

            foreach (GamePlayer player in WorldMgr.GetPlayersCloseToSpot(GroundTargetPosition, (ushort)150))
            {
                if (GameServer.ServerRules.IsAllowedToAttack(Owner, player, true))
                {
                    list.Add(player);
                }
            }
            foreach (GameNPC npc in WorldMgr.GetNPCsCloseToSpot(GroundTargetPosition, (ushort)150))
            {
                if (GameServer.ServerRules.IsAllowedToAttack(Owner, npc, true))
                {
                    list.Add(npc);
                }
            }

            if (!list.Contains(this.TargetObject))
            {
                list.Add(this.TargetObject);
            }
            return list;
        }

        public override void DoDamage()
        {
            //			InventoryItem ammo = this.Ammo[AmmoSlot] as InventoryItem;
            //todo remove ammo + spell in db and uncomment
            //m_spellHandler.StartSpell(player);
            base.DoDamage();//anim mut be called after damage
            if (GroundTargetPosition == Position.Nowhere) return;
            IList targets = SelectTargets();

            foreach (GameLiving living in targets)
            {
                int damageAmount = 50 + Util.Random(200);
                living.TakeDamage(Owner, eDamageType.Crush, damageAmount, 0);

                if (Owner is GamePlayer p)
                {
                    Owner.SendMessage(LanguageMgr.GetTranslation(p.Client.Account.Language, "GameSiegeWeapon.Catapult.YouHit", this.Name, living.Name, damageAmount), eChatType.CT_YouHit, eChatLoc.CL_SystemWindow);
                }

                foreach (GamePlayer player in living.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    player.Out.SendCombatAnimation(this, living, 0x0000, 0x0000, 0x00, 0x00, 0x14, living.HealthPercent);
            }
            return;
        }
        public override bool ReceiveItem(GameLiving source, DOL.Database.InventoryItem item)
        {
            //todo check if bullet
            return base.ReceiveItem(source, item);
        }
    }
}
