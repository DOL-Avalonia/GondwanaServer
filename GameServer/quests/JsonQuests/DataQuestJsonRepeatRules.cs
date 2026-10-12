using System;
using System.Threading.Tasks;
using DOL.Database;
using DOL.GS.PacketHandler;
using DOL.Language;

namespace DOL.GS.Quests
{
    public static class DataQuestJsonRepeatRules
    {
        public static bool CanPlayerAccept(DataQuestJson quest, GamePlayer player)
        {
            var repeat = (eQuestRepeatInterval)quest.RepeatInterval;
            if (repeat == eQuestRepeatInterval.None)
                return true;

            var cd = GameServer.Database.SelectObject<DBDataQuestJsonCooldown>(
                DB.Column("PlayerID").IsEqualTo(player.InternalID)
                  .And(DB.Column("QuestID").IsEqualTo(quest.Id)));

            if (cd == null)
                return true;

            var nowUtc = PeriodicQuestTime.UtcNow;
            if (nowUtc < cd.NextAvailableUtc)
                return false;

            // cooldown expired -> allow again
            GameServer.Database.DeleteObject(cd);
            return true;
        }

        public static void OnQuestJsonCompleted(DataQuestJson quest, GamePlayer player)
        {
            var repeat = (eQuestRepeatInterval)quest.RepeatInterval;
            if (repeat == eQuestRepeatInterval.None)
                return;

            var nowUtc = PeriodicQuestTime.UtcNow;
            var nextUtc = PeriodicQuestTime.GetNextResetUtc(quest.RepeatInterval, nowUtc);

            var cd = GameServer.Database.SelectObject<DBDataQuestJsonCooldown>(
                DB.Column("PlayerID").IsEqualTo(player.InternalID)
                  .And(DB.Column("QuestID").IsEqualTo(quest.Id)));

            if (cd == null)
            {
                cd = new DBDataQuestJsonCooldown
                {
                    PlayerID = player.InternalID,
                    QuestID = quest.Id
                };
                GameServer.Database.AddObject(cd);
            }

            cd.CompletedUtc = nowUtc;
            cd.NextAvailableUtc = nextUtc;
            GameServer.Database.SaveObject(cd);

            GameLoopAsync.Then(quest.GetNameForPlayer(player), (player, repeat, fallback: quest.Name), static (resolved, s) =>
            {
                if (s.player.ObjectState != GameObject.eObjectState.Active) return;
                string questName = resolved ?? s.fallback;
                string key = s.repeat == eQuestRepeatInterval.Daily ? "DataQuestJson.JsonQuest.Repeat.DailyCompleted"
                           : s.repeat == eQuestRepeatInterval.Weekly ? "DataQuestJson.JsonQuest.Repeat.WeeklyCompleted" : null;
                if (key != null) s.player.Out.SendMessage(LanguageMgr.GetTranslation(s.player.Client.Account.Language, key, questName), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            });
        }
    }
}
