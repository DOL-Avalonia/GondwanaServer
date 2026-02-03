using DOL.AI.Brain;
using DOL.Database;
using DOL.Events;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.GS.ServerProperties;
using DOL.Language;
using DOL.Territories;
using System.Linq;
using System.Threading.Tasks;

namespace DOL.GS.Scripts
{
    /// <summary>
    /// Beware, Changing this class Name or Namespace breaks TerritoryManager
    /// </summary>
    public class TerritoryBoss : AmteMob, IGuardNPC
    {
        private string originalGuildName;

        public TerritoryBoss()
        {
            var brain = new TerritoryBrain();
            brain.AggroLink = 3;
            brain.AggroRange = 500;
            SetOwnBrain(brain);
        }

        /// <inheritdoc />
        public override int BountyPointsValue
        {
            get => 10;
        }
        
        public override bool AddToWorld()
        {
            bool added = base.AddToWorld();

            if (!added)
            {
                return false;
            }

            var territory = TerritoryManager.Instance.Territories.FirstOrDefault(t => t.BossId.Equals(this.InternalID));

            if (territory != null && territory.OwnerGuild != null)
            {
                this.GuildName = territory.OwnerGuild.Name;
            }

            return true;
        }

        public override bool Interact(GamePlayer player)
        {
            if (player.Client.Account.PrivLevel == 1 && !IsWithinRadius(player, InteractDistance))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObject.Interact.TooFarAway", GetName(0, true)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                Notify(GameObjectEvent.InteractFailed, this, new InteractEventArgs(player));
                return false;
            }
            Notify(GameObjectEvent.Interact, this, new InteractEventArgs(player));
            player.Notify(GameObjectEvent.InteractWith, player, new InteractWithEventArgs(this));

            if (string.IsNullOrWhiteSpace(GuildName) || player.Guild == null)
                return false;
            if (player.Client.Account.PrivLevel == 1 && player.GuildName != GuildName)
                return false;
            if (!player.GuildRank.Claim)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameUtils.Guild.Territory.Boss.TalkRefusal", player.Name), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }

            return true;
        }

        public override void Die(GameObject killer)
        {
            bool shouldCapture = false;
            GamePlayer killingPlayer = killer as GamePlayer;

            if (killingPlayer != null && killingPlayer.Guild != null)
            {
                bool isSystemGuild = killingPlayer.Guild.IsSystemGuild;

                if (!isSystemGuild && !string.IsNullOrEmpty(Properties.SERVER_GUILDS))
                {
                    if (Properties.SERVER_GUILDS.Split('|').Contains(killingPlayer.Guild.GuildID))
                        isSystemGuild = true;
                }

                if (!isSystemGuild)
                {
                    var territory = TerritoryManager.GetTerritoryFromMobId(this.InternalID);
                    if (territory != null && GvGManager.IsCaptureAllowed(territory, killingPlayer))
                    {
                        shouldCapture = true;
                    }
                }
                else
                {
                    killingPlayer.Out.SendMessage(LanguageMgr.GetTranslation(killingPlayer.Client.Account.Language, "GameUtils.Guild.Territory.Boss.CaptureDisabled"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                }
            }

            if (shouldCapture)
            {
                this.GuildName = killingPlayer!.GuildName;
                TerritoryManager.Instance.ChangeGuildOwner(this, killingPlayer.Guild);

                base.Die(killer);
            }
            else
            {
                this.GuildName = originalGuildName;

                var eventToStop = this.Event;
                this.Event = null;

                try
                {
                    base.Die(killer);
                }
                finally
                {
                    if (eventToStop != null)
                    {
                        Task.Run(() => eventToStop.Stop(DOL.GameEvents.EndingConditionType.Kill, silent: true));
                    }
                }
            }
        }

        public override void LoadFromDatabase(DataObject obj)
        {
            base.LoadFromDatabase(obj);
            TerritoryBrain brain = this.Brain as TerritoryBrain;
            Mob mob = obj as Mob;
            if (mob != null)
            {
                this.originalGuildName = mob.Guild;
            }

            if (brain != null && mob != null)
            {
                if (mob.AggroRange > 0)
                    brain.AggroRange = mob.AggroRange;
            }
        }

        public override void RestoreOriginalGuildName()
        {
            this.GuildName = originalGuildName;
        }
    }
}