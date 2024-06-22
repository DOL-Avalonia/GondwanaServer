using DOL.Database;
using DOL.Events;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DOL.GS.PacketHandler;
using DOL.GS.Scripts;
using DOL.GameEvents;
using System.Threading.Tasks;
using DOL.Language;
using DOL.GS.Behaviour;
using DOL.MobGroups;

namespace DOL.GS.Quests;

public static class DataQuestJsonMgr
{
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    public static Dictionary<ushort, DataQuestJson> Quests = new();

    [ScriptLoadedEvent]
    public static void OnScriptsCompiled(DOLEvent e, object sender, EventArgs args)
    {
        GameServer.Database.RegisterDataObject(typeof(DBDataQuestJson));
        log.Info("QuestLoader: initialized.");
    }

    [GameServerStartedEvent]
    public static void OnGameServerStarted(DOLEvent e, object sender, EventArgs args)
    {
        ReloadQuests();
        GameEventMgr.AddHandlerUnique(GameObjectEvent.Interact, OnInteract);
        GameEventMgr.AddHandlerUnique(GamePlayerEvent.AcceptQuest, OnAcceptQuest);
    }

    public static DataQuestJson GetQuest(ushort id)
    {
        return Quests.TryGetValue(id, out var quest) ? quest : null;
    }
    public static DataQuestJson GetQuest(string name)
    {
        return Quests.Values.FirstOrDefault(quest => quest.Name == name);
    }

    public static List<string> ReloadQuests()
    {
        var errors = new List<string>();
        var oldErrorQuests = new List<int>();

        var old = Quests;
        foreach (var quest in old.Values.Where(quest => quest != null))
        {
            try
            {
                quest.Unload();
            }
            catch (Exception ex)
            {
                errors.Add($"Error when unloading quest \"{quest.Name}\" (ID: {quest.Id}): {ex.Message}");
                log.Error($"QuestLoader: error when unloading quest {quest.Id}", ex);
                oldErrorQuests.Add(quest.Id);
            }
        }

        var quests = new Dictionary<ushort, DataQuestJson>();
        foreach (var db in GameServer.Database.SelectAllObjects<DBDataQuestJson>())
        {
            if (oldErrorQuests.Contains(db.Id))
            {
                quests.Add(db.Id, old[db.Id]);
                errors.Add($"Quest \"{db.Name}\" (ID: {db.Id}) skipped because it's not unloaded");
                continue;
            }
            try
            {
                var loaded = new DataQuestJson(db);
                quests.Add(loaded.Id, loaded);
            }
            catch (Exception ex)
            {
                errors.Add($"Error with quest \"{db.Name}\" (ID: {db.Id}): {ex.Message}");
                log.Error($"QuestLoader: error when loading quest {db.Id}", ex);
            }
        }

        // just exchange the reference
        Quests = quests;

        log.Info($"QuestLoader: {old.Count} quests unloaded, {Quests.Count} quests loaded");
        errors.Add($"QuestLoader: {old.Count} quests unloaded, {Quests.Count} quests loaded");
        return errors;
    }

    public static void OnInteract(DOLEvent _, object sender, EventArgs args)
    {
        if (args is not InteractEventArgs arguments || arguments.Source == null)
            return;

        var player = arguments.Source;
        var possibleQuests = sender is GameNPC npc ? npc.QuestIdListToGive : Array.Empty<ushort>();
        if (possibleQuests.Count == 0)
            return;
            if (player.QuestList.Any(q => possibleQuests.Contains(q.QuestId) || q.CanInteractWith(sender)))
                return; // Quest in progress
        foreach (var questId in possibleQuests)
        {
            var quest = GetQuest(questId);
            if (sender is ITextNPC textNPC && textNPC.CheckQuestAvailable(player, quest.Name))
                return;
            if (IsDoingTimerQuest(player) && quest.Goals.Any(g => g.Value is TimerGoal))
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DataQuestJson.JsonQuest.Timer"),
                    eChatType.CT_System, eChatLoc.CL_SystemWindow);
                return;
            }
            else if (quest != null && quest.CheckQuestQualification(player))
            {
                player.Out.SendQuestOfferWindow(quest.Npc, player, PlayerQuest.CreateQuestPreview(quest, player));
                return;
            }
        }
    }
    public static bool IsDoingTimerQuest(GamePlayer player)
    {
        //return if player is doing a quest that has a goal with a timer
        return player.QuestList.Any(q => q is PlayerQuest playerQuest && playerQuest.Quest.Goals.Any(g => g.Value is TimerGoal));
    }

    public static void OnAcceptQuest(DOLEvent _, object sender, EventArgs args)
    {
        if (args is not QuestEventArgs arguments || arguments.Source == null)
            return;
        var player = arguments.Player;
        var quest = Quests.Values.FirstOrDefault(q => q.Id == arguments.QuestID);
        if (quest == null || arguments.Source != quest.Npc || !quest.CheckQuestQualification(player))
            return;
        var npc = quest.Npc;
        //if player has no inventory space and  first quest goal has startitem
        if (player.Inventory.FindFirstEmptySlot(eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack) == eInventorySlot.Invalid && quest.Goals.Count > 0 && quest.Goals[1].StartItemTemplate != null)
        {
            player.Out.SendMessage(string.Format("You don't have enough inventory space to accept this quest. Please make room for {0} and try again.", quest.Goals[1].StartItemTemplate.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            return;
        }
        ChatUtil.SendScreenCenter(player, $"Quest \"{quest.Name}\" accepted!");
        player.Out.SendSoundEffect(7, 0, 0, 0, 0, 0);
        var dbQuest = new DBQuest
        {
            Character_ID = player.InternalID,
            Name = typeof(PlayerQuest).FullName,
            Step = 1,
            CustomPropertiesString = JsonConvert.SerializeObject(new PlayerQuest.JsonState { QuestId = quest.Id, Goals = null }),
        };
        var dq = new PlayerQuest(player, dbQuest);
        if (player.AddQuest(dq))
        {
            if (!string.IsNullOrWhiteSpace(dq.Quest.AcceptText))
            {
                var formatMsg = dq.Quest.AcceptText.Replace(@"\n", "\n");

                var finalMsg = Util.SplitCSV(BehaviourUtils.GetPersonalizedMessage(formatMsg, player), true);

                player.Out.SendCustomTextWindow(npc.Name + " dit", finalMsg);
            }
            dq.SaveIntoDatabase();
            if (quest.RelatedMobGroups != null)
            {
                foreach (MobGroup group in quest.RelatedMobGroups)
                {
                    dq.UpdateGroupMob(group);
                }
            }
            quest.SendNPCsQuestEffects(player);
            player.Out.SendQuestListUpdate();
        }

        var questEvent = GameEventManager.Instance.Events.FirstOrDefault(e =>
        e.QuestStartingId?.Equals(dq.Quest.Id + "-start") == true &&
       !e.StartedTime.HasValue &&
        e.Status == EventStatus.NotOver &&
        e.StartConditionType == StartingConditionType.Quest);
        if (questEvent != null)
        {
            System.Threading.Tasks.Task.Run(() => GameEventManager.Instance.StartEvent(questEvent, null, player));
        }
        if (dq.Quest.StartEvent)
        {
            questEvent = GameEventManager.Instance.Events.FirstOrDefault(e =>
            e.ID?.Equals(dq.Quest.StartEventId) == true &&
            !e.StartedTime.HasValue &&
            e.Status == EventStatus.NotOver &&
            e.StartConditionType == StartingConditionType.Quest);
            if (questEvent != null)
            {
                System.Threading.Tasks.Task.Run(() => GameEventManager.Instance.StartEvent(questEvent, null, player));
            }
        }
        else if (dq.Quest.ResetEvent)
        {
            questEvent = GameEventManager.Instance.Events.FirstOrDefault(e =>
            e.ID?.Equals(dq.Quest.StartEventId) == true &&
            e.StartedTime.HasValue &&
            e.StartConditionType == StartingConditionType.Quest);
            if (questEvent != null)
            {
                System.Threading.Tasks.Task.Run(() => GameEventManager.Instance.ResetEvent(questEvent));
            }
        }
    }

    public static (PlayerQuest quest, PlayerGoalState goal) FindQuestAndGoalFromPlayer(GamePlayer player, ushort questId, int goalId)
    {
        var quest = player.QuestList.FirstOrDefault(q => q.QuestId == questId);
        var goal = quest?.GoalStates.Find(gs => gs.GoalId == goalId);
        return (quest, goal);
    }
}