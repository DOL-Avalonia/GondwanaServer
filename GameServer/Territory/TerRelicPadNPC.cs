using DOL.Events;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.Language;
using DOL.Territories;
using System.Linq;

namespace AmteScripts.Managers
{
    public class TerRelicPadNPC : GameNPC
    {
        public TerritoryRelicStatic CurrentRelic { get; set; }
        private GameStaticItem _visualPad;
        private TerRelicPadArea _area;

        public override bool AddToWorld()
        {
            bool success = base.AddToWorld();
            if (success)
            {
                _visualPad = new GameStaticItem
                {
                    Model = 2655,
                    Name = "Territory Relic Pad",
                    Position = this.Position
                };
                _visualPad.AddToWorld();

                _area = new TerRelicPadArea(this, 150);
                this.CurrentRegion.AddArea(_area);
            }
            return success;
        }

        public override bool RemoveFromWorld()
        {
            if (_visualPad != null) _visualPad.RemoveFromWorld();
            if (_area != null && CurrentRegion != null) CurrentRegion.RemoveArea(_area);
            return base.RemoveFromWorld();
        }

        public void HandlePlayerEnter(GamePlayer player)
        {
            if (CurrentRelic != null) return;

            TerritoryRelicInventoryItem item = null;
            for (eInventorySlot slot = eInventorySlot.FirstBackpack; slot <= eInventorySlot.LastBackpack; slot++)
            {
                var invItem = player.Inventory.GetItem(slot);
                if (invItem is TerritoryRelicInventoryItem trItem)
                {
                    item = trItem;
                    break;
                }
            }

            if (item == null) return;

            // Check if this pad's territory belongs to the player's guild
            var myTerritory = TerritoryManager.GetCurrentTerritory(this);
            if (myTerritory == null || !myTerritory.IsOwnedBy(player))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TerritoryRelics.Pad.WrongGuild"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }

            // Secure the Relic
            item.IsRemovalExpected = true;
            if (player.Inventory.RemoveItem(item))
            {
                CurrentRelic = item.RelicReference;
                CurrentRelic.AttachToTerritory(myTerritory, this);
                player.CapturedRelics++;
                TaskManager.UpdateTaskProgress(player, "CapturedRelics", 1);
                player.SaveIntoDatabase();
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TerritoryRelics.Pad.Secured", CurrentRelic.Name), eChatType.CT_Skill, eChatLoc.CL_SystemWindow);
            }
        }
    }

    public class TerRelicPadArea : Area.Circle
    {
        private TerRelicPadNPC _padNpc;

        public TerRelicPadArea(TerRelicPadNPC pad, int radius)
            : base("TerRelicPadArea", pad.Position.X, pad.Position.Y, pad.Position.Z, radius)
        {
            _padNpc = pad;
        }

        public override void OnPlayerEnter(GamePlayer player)
        {
            base.OnPlayerEnter(player);
            _padNpc.HandlePlayerEnter(player);
        }
    }
}