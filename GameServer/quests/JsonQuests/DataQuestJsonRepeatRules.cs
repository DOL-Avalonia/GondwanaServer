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

            Task.Run(async () =>
            {
                string resolvedName = await quest.GetNameForPlayer(player);
                string questName = resolvedName ?? quest.Name;

                if (repeat == eQuestRepeatInterval.Daily)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DataQuestJson.JsonQuest.Repeat.DailyCompleted", questName), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
                else if (repeat == eQuestRepeatInterval.Weekly)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DataQuestJson.JsonQuest.Repeat.WeeklyCompleted", questName), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                }
            });
        }
    }
}
