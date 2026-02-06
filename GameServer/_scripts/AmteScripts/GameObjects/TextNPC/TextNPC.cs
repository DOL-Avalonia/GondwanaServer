/**
 * Created by Virant "Dre" Jérémy for Amtenael
 */

using System.Linq;
using System.Numerics;
using DOL.AI.Brain;
using DOL.Database;
using DOL.Events;
using DOL.GS.PacketHandler;
using DOL.GS.Quests;
using DOL.Language;
using DOL.Territories;
using System.Collections;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace DOL.GS.Scripts
{
    public class TextNPC : AmteMob, ITextNPC
    {
        private RegionTimer _hourCheckTimer;
        public TextNPCPolicy TextNPCData { get; set; }

        public TextNPCPolicy GetTextNPCPolicy(GameLiving target = null)
        {
            return TextNPCData;
        }

        public TextNPCPolicy GetOrCreateTextNPCPolicy(GameLiving target = null)
        {
            return GetTextNPCPolicy(target);
        }

        public TextNPC()
        {
            TextNPCData = new TextNPCPolicy(this);
            SetOwnBrain(new TextNPCBrain());
        }

        #region TextNPCPolicy
        public void SayRandomPhrase()
        {
            TextNPCData.SayRandomPhrase();
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            return GetTextNPCPolicy(player).Interact(player);
        }

        public override bool WhisperReceive(GameLiving source, string str)
        {
            if (!base.WhisperReceive(source, str))
                return false;

            return GetTextNPCPolicy(source as GamePlayer).WhisperReceive(source, str);
        }

        public override bool ReceiveItem(GameLiving source, InventoryItem item)
        {
            return GetTextNPCPolicy(source as GamePlayer).ReceiveItem(source, item);
        }

        public override void LoadFromDatabase(DataObject obj)
        {
            base.LoadFromDatabase(obj);
            DBTextNPC textDB;
            try
            {
                textDB = GameServer.Database.SelectObject<DBTextNPC>(t => t.MobID == obj.ObjectId);
            }
            catch
            {
                DBTextNPC.Init();
                textDB = GameServer.Database.SelectObject<DBTextNPC>(t => t.MobID == obj.ObjectId);
            }

            if (textDB != null)
                TextNPCData.LoadFromDatabase(textDB);
        }

        public override void SaveIntoDatabase()
        {
            base.SaveIntoDatabase();
            TextNPCData.SaveIntoDatabase();
        }

        public override void DeleteFromDatabase()
        {
            base.DeleteFromDatabase();
            TextNPCData.DeleteFromDatabase();
        }

        public override bool AddToWorld()
        {
            bool success = base.AddToWorld();
            if (success)
            {
                if (TextNPCData != null && TextNPCData.Condition != null &&
                   (TextNPCData.Condition.Heure_min > 0 || TextNPCData.Condition.Heure_max < 24))
                {
                    _hourCheckTimer = new RegionTimer(this, CheckHourConditions);
                    _hourCheckTimer.Start(15000);
                }
            }
            return success;
        }

        public override bool RemoveFromWorld()
        {
            if (_hourCheckTimer != null)
            {
                _hourCheckTimer.Stop();
                _hourCheckTimer = null;
            }
            return base.RemoveFromWorld();
        }

        /// <summary>
        /// Callback for the timer. Refreshes the Quest Indicator for nearby players
        /// if the open/close state has changed based on game time.
        /// </summary>
        private int CheckHourConditions(RegionTimer timer)
        {
            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
            {
                player.Out.SendNPCsQuestEffect(this, this.GetQuestIndicator(player));
            }
            return 15000;
        }

        public override eQuestIndicator GetQuestIndicator(GamePlayer player)
        {
            var policy = GetTextNPCPolicy(player) ?? GetTextNPCPolicy();
            if (policy == null)
                return base.GetQuestIndicator(player);
            
            if (!policy.CanInteractWith(player) || !policy.WillTalkTo(player, true))
            {
                return eQuestIndicator.None;
            }
            
            var result = base.GetQuestIndicator(player);
            if (result != eQuestIndicator.None)
                return result;

            return policy.Condition.CanGiveQuest;
        }

        #endregion
    }

    /// <summary>
    /// Provided only for compatibility
    /// </summary>
    public class EchangeurNPC : TextNPC { }
}