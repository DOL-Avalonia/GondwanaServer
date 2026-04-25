using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DOL.AI.Brain;
using DOL.Database;
using DOL.GS.Finance;
using DOL.Language;
using DOL.GS.PacketHandler;
using GameServerScripts.Amtescripts.Managers;
using DOL.GS.ServerProperties;


namespace DOL.GS.Scripts
{
    public interface IGuardNPC
    {

    }

    public class GuardNPC : AmteMob, IGuardNPC
    {
        public GuardNPC()
        {
            SetOwnBrain(new GuardNPCBrain());
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GuardNPC.Interact.Text1"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GuardNPC.Interact.Text2"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GuardNPC.Interact.Text3"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
            return true;
        }

        public override bool WhisperReceive(GameLiving source, string text)
        {
            if (source is not GamePlayer player || !base.WhisperReceive(source, text))  //|| BlacklistMgr.IsBlacklisted(player))
                return false;

            switch (text)
            {
                case "Signaler":
                case "Report":
                    int reported = DeathCheck.Instance.ReportPlayer(player);
                    //if (BlacklistMgr.ReportPlayer(player)) Old Way not used anymore
                    if (reported > 0)
                    {
                        string words = reported == 1 ? LanguageMgr.GetTranslation(player.Client.Account.Language, "GuardNPC.Report.Oneplayer") : LanguageMgr.GetTranslation(player.Client.Account.Language, "GuardNPC.Report.Moreplayers", reported);
                        player.Out.SendMessage(words, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    }
                    break;

                case "Voir":
                case "Look":
                    IList<DOLCharacters> outlaws = GameServer.Database.SelectObjects<DOLCharacters>(DB.Column("LastPlayed").IsGreatherThan("DATE_SUB(NOW(), INTERVAL 1 MONTH)").And(DB.Column("IsWanted").IsNotEqualTo(0)));

                    if (outlaws == null || outlaws.Count == 0)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GuardNPC.Response.Nobodywanted"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                        break;
                    }

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine(LanguageMgr.GetTranslation(player.Client.Account.Language, "GuardNPC.Response.Blacklist"));

                    if (Properties.SHOW_NEW_PLAYER_STATS)
                    {
                        int baseGold = Properties.REWARD_OUTLAW_HEAD_GOLD;
                        int maxMultiplier = Properties.REWARD_OUTLAW_HEAD_MAX_STATS_MULTIPLIER;

                        foreach (var outlaw in outlaws)
                        {
                            TaskXPlayer tData = GameServer.Database.SelectObject<TaskXPlayer>(DB.Column("PlayerId").IsEqualTo(outlaw.ObjectId));
                            int assassinKills = tData != null ? tData.AssassinationKillsStats : 0;

                            int cappedKills = Math.Min(assassinKills, maxMultiplier);
                            int statsMultiplier = Math.Max(1, cappedKills / 3);
                            int repMultiplier = outlaw.Reputation < 0 ? Math.Abs(outlaw.Reputation) : 1;

                            int goldReward = baseGold * repMultiplier * statsMultiplier;
                            string rewardText = LanguageMgr.GetTranslation(player.Client.Account.Language, "GuardNPC.Response.GoldReward", goldReward);

                            if (assassinKills > 0)
                            {
                                int bpReward = cappedKills * 2;
                                rewardText += LanguageMgr.GetTranslation(player.Client.Account.Language, "GuardNPC.Response.BPReward", bpReward);
                            }

                            sb.AppendLine(LanguageMgr.GetTranslation(player.Client.Account.Language, "GuardNPC.Response.WantedReward", outlaw.Name, rewardText));
                        }
                    }
                    else
                    {
                        foreach (var s in outlaws)
                        {
                            sb.AppendLine(s.Name);
                        }
                    }

                    player.Out.SendMessage(sb.ToString(), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    break;
            }
            return true;
        }

        public override bool ReceiveItem(GameLiving source, InventoryItem item)
        {
            if (!(source is GamePlayer srcPlayer) || item == null || !item.Id_nb.StartsWith(srcPlayer.HeadTemplate.Id_nb))
                return false;

            if (!item.CanDropAsLoot)
            {
                srcPlayer.Out.SendMessage(LanguageMgr.GetTranslation(srcPlayer.Client.Account.Language, "GuardNPC.Response.Dontknow"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return false;
            }

            if (new DateTime(2000, 1, 1).Add(new TimeSpan(0, 0, item.MaxCondition)) < DateTime.Now.Subtract(new TimeSpan(1, 0, 0, 0)))
            {
                srcPlayer.Out.SendMessage(LanguageMgr.GetTranslation(srcPlayer.Client.Account.Language, "GuardNPC.Response.Rottenhead"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return false;
            }

            if (!srcPlayer.Inventory.RemoveCountFromStack(item, 1))
                return false;

            List<string> messages = item.Template.MessageArticle.Split(';').ToList();

            if (Properties.SHOW_NEW_PLAYER_STATS)
            {
                int baseGold = Properties.REWARD_OUTLAW_HEAD_GOLD;
                int maxMultiplier = Properties.REWARD_OUTLAW_HEAD_MAX_STATS_MULTIPLIER;
                int finalGoldReward = baseGold;
                int finalBpReward = 0;

                if (messages.Count >= 2)
                {
                    string victimId = messages[0];
                    int victimRep = int.Parse(messages[1]);

                    TaskXPlayer tData = GameServer.Database.SelectObject<TaskXPlayer>(DB.Column("PlayerId").IsEqualTo(victimId));
                    int assassinKills = tData != null ? tData.AssassinationKillsStats : 0;
                    int cappedKills = Math.Min(assassinKills, maxMultiplier);
                    int statsMultiplier = Math.Max(1, cappedKills / 3);
                    int repMultiplier = victimRep < 0 ? Math.Abs(victimRep) : 1;

                    // Base rewards calculations
                    int calculatedGold = baseGold * repMultiplier * statsMultiplier;

                    // Calculate item-based gold bonus for the turning-in player
                    int coinBonusPercent = srcPlayer.GetModified(eProperty.MythicalCoin);
                    finalGoldReward = calculatedGold + ((calculatedGold * coinBonusPercent) / 100);

                    if (assassinKills > 0)
                    {
                        int calculatedBp = cappedKills * 2;
                        int bpBonusPercent = srcPlayer.GetModified(eProperty.BountyPoints);
                        finalBpReward = calculatedBp + ((calculatedBp * bpBonusPercent) / 100);
                    }
                }

                var prime = Money.GetMoney(0, 0, finalGoldReward, 0, 0);
                srcPlayer.AddMoney(Currency.Copper.Mint(prime));

                if (finalBpReward == 0)
                {
                    string rewardMsg = LanguageMgr.GetTranslation(srcPlayer.Client.Account.Language, "GuardNPC.Response.Headreward1", finalGoldReward);
                    srcPlayer.Out.SendMessage(rewardMsg, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                }
                else if (finalBpReward > 0)
                {
                    string rewardMsg = LanguageMgr.GetTranslation(srcPlayer.Client.Account.Language, "GuardNPC.Response.Headreward2", finalGoldReward);
                    srcPlayer.GainBountyPoints(finalBpReward);
                    rewardMsg += " " + LanguageMgr.GetTranslation(srcPlayer.Client.Account.Language, "GuardNPC.Response.HeadrewardBounty", finalBpReward);
                    srcPlayer.Out.SendMessage(rewardMsg, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                }
            }
            else
            {
                int reward = Properties.REWARD_OUTLAW_HEAD_GOLD;

                if (messages.Count >= 2)
                {
                    reward *= (int)(-int.Parse(messages[1]) / 0.5);
                }

                var prime = Money.GetMoney(0, 0, reward, 0, 0);
                srcPlayer.AddMoney(Currency.Copper.Mint(prime));
                srcPlayer.Out.SendMessage(LanguageMgr.GetTranslation(srcPlayer.Client.Account.Language, "GuardNPC.Response.Headreward1", reward), eChatType.CT_System, eChatLoc.CL_PopupWindow);
            }

            return true;
        }

        public override void WalkToSpawn(short speed)
        {
            base.WalkToSpawn(MaxSpeed);
        }
    }

    public class GuardTextNPC : TextNPC, IGuardNPC
    {
        public GuardTextNPC()
        {
            SetOwnBrain(new GuardNPCBrain());
        }
    }
}
