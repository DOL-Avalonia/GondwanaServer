using DOL.Events;
using DOL.GS;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.Quests;
using DOL.GS.ServerProperties;
using System;
using System.Linq;

namespace DOL.GS.Scripts
{
    /// <summary>Fake NPC used to display custom models overhead for TextNPCs, Teleporters, and Quests.</summary>
    public class QuestIndicatorNPC : GameNPC
    {
        public GameNPC OwnerNPC { get; private set; }
        public eQuestIndicator StaticIndicator { get; set; } = eQuestIndicator.None;
        public Func<GamePlayer, bool> IsVisibleCondition { get; set; }

        public QuestIndicatorNPC(GameNPC owner, eQuestIndicator staticIndicator = eQuestIndicator.None)
        {
            OwnerNPC = owner;
            Name = "";
            Flags |= eFlags.PEACE | eFlags.CANTTARGET | eFlags.DONTSHOWNAME | eFlags.FLYING;
        }

        public override ushort GetModelForPlayer(GamePlayer player)
        {
            eQuestIndicator ind = eQuestIndicator.None;

            if (OwnerNPC != null)
            {
                if (!OwnerNPC.IsAlive || OwnerNPC.ObjectState != eObjectState.Active)
                    return 667;

                ind = OwnerNPC.GetQuestIndicator(player);
            }
            else
            {
                if (IsVisibleCondition != null && !IsVisibleCondition(player))
                    return 667;

                ind = StaticIndicator;
            }

            if (ind == eQuestIndicator.RedTarget)
            {
                string reqItem = Properties.FACEMOB_REQUIRED_ITEMTEMPLATE;
                if (!string.IsNullOrWhiteSpace(reqItem))
                {
                    int count = player.Inventory?.CountItemTemplate(reqItem, eInventorySlot.MinEquipable, eInventorySlot.LastBagHorse) ?? 0;
                    if (count <= 0)
                        return 667;
                }
            }

            return ind switch
            {
                eQuestIndicator.Tasks => 1761,
                eQuestIndicator.RedTarget => 1762,
                eQuestIndicator.BlueTarget => 1763,
                eQuestIndicator.GreenTarget => 1764,
                eQuestIndicator.Teleport => 1923,
                _ => 667
            };
        }
    }

    public static class QuestIndicatorManager
    {
        [ScriptLoadedEvent]
        public static void OnScriptLoaded(DOLEvent e, object sender, EventArgs args)
        {
            GameEventMgr.AddHandler(GameObjectEvent.AddToWorld, OnNpcAddToWorld);
            GameEventMgr.AddHandler(GameObjectEvent.RemoveFromWorld, OnNpcRemoveFromWorld);
            GameEventMgr.AddHandler(GameObjectEvent.MoveTo, OnNpcMove);
            GameEventMgr.AddHandler(GamePlayerEvent.LevelUp, OnPlayerStateChanged);
            GameEventMgr.AddHandler(GamePlayerEvent.GameEntered, OnPlayerStateChanged);
            GameEventMgr.AddHandler(RegionEvent.PlayerEnter, OnPlayerStateChanged);
        }

        private static void OnPlayerStateChanged(DOLEvent e, object sender, EventArgs args)
        {
            GamePlayer player = sender as GamePlayer;
            if (player == null && args is RegionPlayerEventArgs rpArgs)
                player = rpArgs.Player;

            if (player != null)
            {

                var existingTimer = player.TempProperties.getProperty<RegionTimer>("QuestIndicatorTimer");
                if (existingTimer != null)
                {
                    existingTimer.Stop();
                }

                var newTimer = new RegionTimer(player, new RegionTimerCallback(state =>
                {
                    if (player.ObjectState != GameObject.eObjectState.Active) return 0;

                    foreach (var npc in player.GetNPCsInRadius(WorldMgr.VISIBILITY_DISTANCE).Cast<GameNPC>())
                    {
                        if (npc is ITextNPC || npc is TeleportNPC || (npc.QuestIdListToGive != null && npc.QuestIdListToGive.Count > 0))
                        {
                            RefreshIndicator(npc, player);
                        }
                    }

                    foreach (var pq in player.QuestList.OfType<PlayerQuest>())
                        foreach (var goal in pq.Quest.Goals.Values)
                            goal.RefreshCustomIndicators(pq);

                    return 5000;
                }));

                player.TempProperties.setProperty("QuestIndicatorTimer", newTimer);
                newTimer.Start(1500);
            }
        }

        private static void OnNpcAddToWorld(DOLEvent e, object sender, EventArgs args)
        {
            if (sender is GameNPC npc && !(npc is QuestIndicatorNPC))
            {
                bool needsIndicator = false;

                if (npc is ITextNPC || npc is TeleportNPC)
                    needsIndicator = true;
                else if (npc.QuestIdListToGive != null && npc.QuestIdListToGive.Count > 0)
                    needsIndicator = true;

                if (needsIndicator) GetOrSpawnIndicator(npc);
            }
        }

        private static void OnNpcRemoveFromWorld(DOLEvent e, object sender, EventArgs args)
        {
            if (sender is GameNPC npc)
            {
                var fakeMob = npc.TempProperties.getProperty<QuestIndicatorNPC>("QuestIndicatorMob", null);
                if (fakeMob != null)
                {
                    fakeMob.RemoveFromWorld();
                    npc.TempProperties.removeProperty("QuestIndicatorMob");
                }
            }
        }

        private static void OnNpcMove(DOLEvent e, object sender, EventArgs args)
        {
            if (sender is GameNPC npc)
            {
                var fakeMob = npc.TempProperties.getProperty<QuestIndicatorNPC>("QuestIndicatorMob", null);
                if (fakeMob != null)
                    fakeMob.MoveTo(npc.Position + Vector.Create(z: 1));
            }
        }

        public static QuestIndicatorNPC GetOrSpawnIndicator(GameNPC npc)
        {
            if (npc == null || npc.ObjectState != GameObject.eObjectState.Active) return null;

            var fakeMob = npc.TempProperties.getProperty<QuestIndicatorNPC>("QuestIndicatorMob", null);
            if (fakeMob == null)
            {
                fakeMob = new QuestIndicatorNPC(npc);
                fakeMob.CurrentRegionID = npc.CurrentRegionID;
                fakeMob.Position = npc.Position + Vector.Create(z: 1);
                fakeMob.Heading = npc.Heading;
                fakeMob.AddToWorld();
                npc.TempProperties.setProperty("QuestIndicatorMob", fakeMob);
            }
            return fakeMob;
        }

        public static void RefreshIndicator(GameNPC npc, GamePlayer player)
        {
            if (npc == null || player == null || npc is QuestIndicatorNPC) return;

            eQuestIndicator ind = npc.GetQuestIndicator(player);

            string propKey = "QuestIndState_" + npc.InternalID;
            eQuestIndicator lastInd = player.TempProperties.getProperty<eQuestIndicator>(propKey, (eQuestIndicator)255);

            if (lastInd == ind)
                return;

            player.TempProperties.setProperty(propKey, ind);

            if ((byte)ind >= 0x20)
                player.Out.SendNPCsQuestEffect(npc, eQuestIndicator.None);
            else
                player.Out.SendNPCsQuestEffect(npc, ind);

            if ((byte)ind >= 0x20 || (npc is TeleportNPC tp && tp.ShowTPIndicator) || (npc is ITextNPC tnpc && (byte)(tnpc.GetTextNPCPolicy()?.Condition.CanGiveQuest ?? 0) >= 0x20))
            {
                var fakeMob = GetOrSpawnIndicator(npc);
                if (fakeMob != null)
                {
                    player.Out.SendObjectRemove(fakeMob);
                    player.Out.SendNPCCreate(fakeMob);
                }
            }
            else
            {
                var fakeMob = npc.TempProperties.getProperty<QuestIndicatorNPC>("QuestIndicatorMob", null);
                if (fakeMob != null)
                {
                    player.Out.SendObjectRemove(fakeMob);
                    player.Out.SendNPCCreate(fakeMob);
                }
            }
        }
    }
}