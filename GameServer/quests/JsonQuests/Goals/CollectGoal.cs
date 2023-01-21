﻿using DOL.Database;
using DOL.Events;
using DOL.GS.Behaviour;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DOL.GS.Quests
{
	public class CollectGoal : DataQuestJsonGoal
	{
		private readonly GameNPC m_target;
		public override GameNPC Target { get => m_target;}
		private readonly string m_text;
		private readonly ItemTemplate m_item;
		private readonly int m_itemCount = 1;

		public override eQuestGoalType Type => eQuestGoalType.Unknown;
		public override int ProgressTotal => m_itemCount;
		public override QuestZonePoint PointA => new(m_target);
		public override ItemTemplate QuestItem => m_item;
		public override bool hasInteraction { get; set; } = true;

		public CollectGoal(DataQuestJson quest, int goalId, dynamic db) : base(quest, goalId, (object)db)
		{
			m_target = WorldMgr.GetNPCsByNameFromRegion((string)db.TargetName ??  "", (ushort)db.TargetRegion, eRealm.None).FirstOrDefault();
			m_target ??= quest.Npc;
			m_text = db.Text;
			m_item = GameServer.Database.FindObjectByKey<ItemTemplate>((string)db.Item);
			m_itemCount = db.ItemCount;
			GameEventMgr.AddHandler(m_target, GameObjectEvent.ReceiveItem, _Notify);
		}

		public override Dictionary<string, object> GetDatabaseJsonObject()
		{
			var dict = base.GetDatabaseJsonObject();
			dict.Add("TargetName", m_target.Name);
			dict.Add("TargetRegion", m_target.CurrentRegionID);
			dict.Add("Text", m_text);
			dict.Add("Item", m_item.Id_nb);
			dict.Add("ItemCount", m_itemCount);
			return dict;
		}

		public override bool CanInteractWith(PlayerQuest questData, PlayerGoalState state, GameObject target)
			=> state?.IsActive == true && target.Name == m_target.Name && target.CurrentRegion == m_target.CurrentRegion;

		public override void NotifyActive(PlayerQuest quest, PlayerGoalState goal, DOLEvent e, object sender, EventArgs args)
		{
		}

		private void _Notify(DOLEvent e, object sender, EventArgs args)
		{
			if (e != GameObjectEvent.ReceiveItem || !(args is ReceiveItemEventArgs interact))
				return;
			if (!(interact.Source is GamePlayer player) || interact.Target != m_target)
				return;
			if (interact.Item.Id_nb != m_item.Id_nb)
				return;
			var (quest, goal) = DataQuestJsonMgr.FindQuestAndGoalFromPlayer(player, Quest.Id, GoalId);
			if (quest == null || goal is not {IsActive: true})
				return;

			var itemsCountToRemove = m_itemCount-goal.Progress;
			if(interact.Item.Count < itemsCountToRemove)
			{
				itemsCountToRemove = interact.Item.Count;
			}
			if (!player.Inventory.RemoveCountFromStack(interact.Item, itemsCountToRemove))
			{
				ChatUtil.SendImportant(player, "An error happened, retry in a few seconds");
				return;
			}

			goal.Progress += itemsCountToRemove - 1;
			if (!string.IsNullOrWhiteSpace(m_text))
				ChatUtil.SendPopup(player, BehaviourUtils.GetPersonalizedMessage(m_text, player));
			AdvanceGoal(quest, goal);
		}

		public override void Unload()
		{
			if (m_target != null)
				GameEventMgr.RemoveHandler(m_target, GameObjectEvent.ReceiveItem, _Notify);
			base.Unload();
		}
	}
}
