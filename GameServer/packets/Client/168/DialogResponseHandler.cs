/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */

using AmteScripts.Managers;
using DOL.Events;
using DOL.GS.Finance;
using DOL.GS.Housing;
using DOL.GS.Keeps;
using DOL.Language;
using System;
using System.Linq;

namespace DOL.GS.PacketHandler.Client.v168
{
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.DialogResponse, "Response Packet from a Question Dialog", eClientStatus.PlayerInGame)]
    public class DialogResponseHandler : IPacketHandler
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()!.DeclaringType);

        public void HandlePacket(GameClient client, GSPacketIn packet)
        {
            ushort data1 = packet.ReadShort();
            ushort data2 = packet.ReadShort();
            ushort data3 = packet.ReadShort();
            var messageType = (byte)packet.ReadByte();
            var response = (byte)packet.ReadByte();

            new DialogBoxResponseAction(client.Player, data1, data2, data3, messageType, response).Start(1);
        }

        /// <summary>
        /// Handles dialog responses from players
        /// </summary>
        protected class DialogBoxResponseAction : RegionAction
        {
            /// <summary>
            /// The general data field
            /// </summary>
            protected readonly uint m_data1;

            /// <summary>
            /// The general data field
            /// </summary>
            protected readonly int m_data2;

            /// <summary>
            /// The general data field
            /// </summary>
            protected readonly int m_data3;

            /// <summary>
            /// The dialog type
            /// </summary>
            protected readonly int m_messageType;

            /// <summary>
            /// The players response
            /// </summary>
            protected readonly byte m_response;

            /// <summary>
            /// Constructs a new DialogBoxResponseAction
            /// </summary>
            /// <param name="actionSource">The responding player</param>
            /// <param name="data1">The general data field</param>
            /// <param name="data2">The general data field</param>
            /// <param name="data3">The general data field</param>
            /// <param name="messageType">The dialog type</param>
            /// <param name="response">The players response</param>
            public DialogBoxResponseAction(GamePlayer actionSource, uint data1, int data2, int data3, int messageType, byte response)
                : base(actionSource)
            {
                m_data1 = data1;
                m_data2 = data2;
                m_data3 = data3;
                m_messageType = messageType;
                m_response = response;
            }

            /// <summary>
            /// Called on every timer tick
            /// </summary>
            public override void OnTick()
            {
                var player = (GamePlayer)m_actionSource;

                if (player == null)
                    return;

                // log.DebugFormat("Dialog - response: {0}, messageType: {1}, data1: {2}, data2: {3}, data3: {4}", m_response, m_messageType, m_data1, m_data2, m_data3);
                switch ((eDialogCode)m_messageType)
                {
                    case eDialogCode.CustomDialog:
                        {
                            if (m_data2 == 0x01)
                            {
                                CustomDialogResponse callback;
                                lock (player)
                                {
                                    callback = player.CustomDialogCallback;
                                    player.CustomDialogCallback = null;
                                }

                                if (callback == null)
                                    return;

                                callback(player, m_response);
                            }
                            break;
                        }
                    case eDialogCode.GuildInvite:
                        {
                            var guildLeader = WorldMgr.GetObjectByIDFromRegion(player.CurrentRegionID, (ushort)m_data1) as GamePlayer;
                            if (m_response == 0x01) //accept
                            {
                                if (guildLeader == null)
                                {
                                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.SameRegionGuildLeaderToAccept"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                    return;
                                }
                                if (player.Guild != null)
                                {
                                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.StillInGuild"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                    return;
                                }
                                if (!PvpManager.CanGroup(guildLeader, player, false))
                                {
                                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.InviteExpired"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                    return;
                                }
                                if (guildLeader.Guild != null)
                                {
                                    guildLeader.Guild.AddPlayer(player);
                                    return;
                                }

                                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.GuildLeaderNotInGuild"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                return;
                            }

                            if (guildLeader != null)
                            {
                                guildLeader.Out.SendMessage(LanguageMgr.GetTranslation(guildLeader.Client.Account.Language, "DialogResponseHandler.GuildInviteDeclined", player.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                            return;
                        }
                    case eDialogCode.GuildLeave:
                        {
                            if (m_response == 0x01) //accepte
                            {
                                if (player.Guild == null)
                                {
                                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.NotInGuild"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                    return;
                                }

                                player.Guild.RemovePlayer(player.Name, player);
                            }
                            else
                            {
                                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.GuildDeclineQuit"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                return;
                            }
                            break;
                        }
                    case eDialogCode.QuestSubscribe:
                        {
                            var questNPC = (GameLiving)WorldMgr.GetObjectByIDFromRegion(player.CurrentRegionID, (ushort)m_data2);
                            if (questNPC == null)
                                return;

                            var args = new QuestEventArgs(questNPC, player, (ushort)m_data1);
                            if (m_response == 0x01) // accept
                            {
                                // TODO add quest to player
                                // Note: This is done withing quest code since we have to check requirements, etc for each quest individually
                                // i'm reusing the questsubscribe command for quest abort since its 99% the same, only different event dets fired
                                player.Notify(m_data3 == 0x01 ? GamePlayerEvent.AbortQuest : GamePlayerEvent.AcceptQuest, player, args);
                                return;
                            }
                            player.Notify(m_data3 == 0x01 ? GamePlayerEvent.ContinueQuest : GamePlayerEvent.DeclineQuest, player, args);
                            return;
                        }
                    case eDialogCode.GroupInvite:
                        {
                            if (m_response == 0x01)
                            {
                                GameClient cln = WorldMgr.GetClientFromID(m_data1);
                                if (cln == null)
                                    return;

                                GamePlayer groupLeader = cln.Player;
                                if (groupLeader == null)
                                    return;

                                if (player.Group != null)
                                {
                                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.StillInGroup"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                    return;
                                }
                                if (!GameServer.ServerRules.IsAllowedToGroup(groupLeader, player, false))
                                {
                                    return;
                                }
                                if (!PvpManager.CanGroup(groupLeader, player, false))
                                {
                                    return;
                                }
                                if (player.InCombatPvE)
                                {
                                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.CannotJoinGroupInCombat"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                    return;
                                }
                                if (groupLeader.Group != null)
                                {
                                    if (groupLeader.Group.Leader != groupLeader) return;
                                    if (groupLeader.Group.MemberCount >= ServerProperties.Properties.GROUP_MAX_MEMBER)
                                    {
                                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.GroupFull"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                        return;
                                    }
                                    groupLeader.Group.AddMember(player);
                                    GameEventMgr.Notify(GamePlayerEvent.AcceptGroup, player);
                                    return;
                                }

                                var group = new Group(groupLeader);
                                GroupMgr.AddGroup(group);

                                group.AddMember(groupLeader);
                                group.AddMember(player);

                                GameEventMgr.Notify(GamePlayerEvent.AcceptGroup, player);

                                return;
                            }
                            break;
                        }
                    case eDialogCode.KeepClaim:
                        {
                            if (m_response == 0x01)
                            {
                                if (player.Guild == null)
                                {
                                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.MustBeGuildMemberToUseCommands"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                    return;
                                }

                                AbstractGameKeep keep = GameServer.KeepManager.GetKeepCloseToSpot(player.Position, WorldMgr.VISIBILITY_DISTANCE);
                                if (keep == null)
                                {
                                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.NearKeepToClaim"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                    return;
                                }

                                if (keep.CheckForClaim(player))
                                {
                                    keep.Claim(player);
                                }
                                break;
                            }
                            break;
                        }
                    case eDialogCode.HousePayRent:
                        {
                            if (m_response == 0x00)
                            {
                                if (player.TempProperties.getProperty<long>(HousingConstants.MoneyForHouseRent, -1) != -1)
                                {
                                    player.TempProperties.removeProperty(HousingConstants.MoneyForHouseRent);
                                }

                                if (player.TempProperties.getProperty<long>(HousingConstants.BPsForHouseRent, -1) != -1)
                                {
                                    player.TempProperties.removeProperty(HousingConstants.BPsForHouseRent);
                                }

                                player.TempProperties.removeProperty(HousingConstants.HouseForHouseRent);

                                return;
                            }

                            var house = player.TempProperties.getProperty<House>(HousingConstants.HouseForHouseRent, null);
                            var moneyToAdd = player.TempProperties.getProperty<long>(HousingConstants.MoneyForHouseRent, -1);
                            var bpsToMoney = player.TempProperties.getProperty<long>(HousingConstants.BPsForHouseRent, -1);

                            if (moneyToAdd != -1)
                            {
                                // if we're giving money and already have some in the lockbox, make sure we don't
                                // take more than what would cover 4 weeks of rent.
                                if (moneyToAdd + house.KeptMoney > HouseMgr.GetRentByModel(house.Model) * ServerProperties.Properties.RENT_LOCKBOX_PAYMENTS)
                                    moneyToAdd = (HouseMgr.GetRentByModel(house.Model) * ServerProperties.Properties.RENT_LOCKBOX_PAYMENTS) - house.KeptMoney;

                                // take the money from the player
                                if (!player.RemoveMoney(Currency.Copper.Mint(moneyToAdd)))
                                    return;
                                InventoryLogging.LogInventoryAction(player, house.DatabaseItem.ObjectId, "(HOUSE;" + house.HouseNumber + ")", eInventoryActionType.Other, moneyToAdd);

                                // add the money to the lockbox
                                house.KeptMoney += moneyToAdd;

                                // save the house and the player
                                house.SaveIntoDatabase();
                                player.SaveIntoDatabase();

                                // notify the player of what we took and how long they are prepaid for
                                string depositMoneyMsg = LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.DepositInLockbox", Money.GetString(moneyToAdd, player.Client.Account.Language));
                                player.Out.SendMessage(depositMoneyMsg, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                string lockboxMoneyMsg = LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.LockboxHasMoneyWeeklyPayment", Money.GetString(house.KeptMoney, player.Client.Account.Language), Money.GetString(HouseMgr.GetRentByModel(house.Model)));
                                player.Out.SendMessage(lockboxMoneyMsg, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                string housePrepaidMsg = LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.HousePrepaidForPayments", (house.KeptMoney / HouseMgr.GetRentByModel(house.Model)));
                                player.Out.SendMessage(housePrepaidMsg, eChatType.CT_System, eChatLoc.CL_SystemWindow);

                                // clean up
                                player.TempProperties.removeProperty(HousingConstants.MoneyForHouseRent);
                            }
                            else
                            {
                                if (bpsToMoney + house.KeptMoney > HouseMgr.GetRentByModel(house.Model) * ServerProperties.Properties.RENT_LOCKBOX_PAYMENTS)
                                    bpsToMoney = (HouseMgr.GetRentByModel(house.Model) * ServerProperties.Properties.RENT_LOCKBOX_PAYMENTS) - house.KeptMoney;

                                if (!player.RemoveMoney(Currency.BountyPoints.Mint(Money.GetGold(bpsToMoney))))
                                    return;

                                // add the bps to the lockbox
                                house.KeptMoney += bpsToMoney;

                                // save the house and the player
                                house.SaveIntoDatabase();
                                player.SaveIntoDatabase();

                                // notify the player of what we took and how long they are prepaid for
                                string depositBPsMsg = LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.DepositInLockbox", Money.GetString(bpsToMoney, player.Client.Account.Language));
                                player.Out.SendMessage(depositBPsMsg, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                string lockboxBPsMsg = LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.LockboxHasMoneyWeeklyPayment", Money.GetString(house.KeptMoney, player.Client.Account.Language), Money.GetString(HouseMgr.GetRentByModel(house.Model)));
                                player.Out.SendMessage(lockboxBPsMsg, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                string housePrepaidBPsMsg = LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.HousePrepaidForPayments", (house.KeptMoney / HouseMgr.GetRentByModel(house.Model)));
                                player.Out.SendMessage(housePrepaidBPsMsg, eChatType.CT_System, eChatLoc.CL_SystemWindow);

                                // clean up
                                player.TempProperties.removeProperty(HousingConstants.BPsForHouseRent);
                            }

                            // clean up
                            player.TempProperties.removeProperty(HousingConstants.MoneyForHouseRent);
                            break;
                        }
                    case eDialogCode.OpenMarket:
                        {
                            Console.WriteLine("OpenMarket: " + m_response);
                            if (m_response == 0x01)
                            {
                                if (player.TemporaryConsignmentMerchant == null || player.TemporaryConsignmentMerchant.ObjectState == GameObject.eObjectState.Deleted)
                                {
                                    TemporaryConsignmentMerchant consignmentMerchant = new TemporaryConsignmentMerchant();
                                    consignmentMerchant.playerOwner = player;
                                    player.TemporaryConsignmentMerchant = consignmentMerchant;
                                    consignmentMerchant.AddToWorld();
                                }
                            }
                            break;
                        }
                    case eDialogCode.AskName:
                        {
                            GameClient cln = WorldMgr.GetClientFromID(m_data1);
                            if (m_response == 0x01 && cln != null && !cln.Player.SerializedAskNameList.Contains(player.Name))
                            {
                                cln.Player.SerializedAskNameList = cln.Player.SerializedAskNameList.Append(player.Name).ToArray();
                                // Save to database
                                cln.Player.SaveIntoDatabase();
                                // Update player in world SendLivingDataUpdate
                                cln.Player.Out.SendObjectRemove(player);
                                cln.Player.Out.SendPlayerCreate(player);
                                cln.Player.Out.SendLivingEquipmentUpdate(player);
                                cln.Player.Out.SendMessage(LanguageMgr.GetTranslation(cln, "Commands.Players.Askname.Added", player.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            }
                            break;
                        }
                    case eDialogCode.CloseMarket:
                        {
                            if (m_response == 0x01)
                            {
                                if (player.TemporaryConsignmentMerchant != null)
                                    player.TemporaryConsignmentMerchant.Delete();
                            }
                            break;
                        }
                    case eDialogCode.MasterLevelWindow:
                        {
                            player.Out.SendMasterLevelWindow(m_response);
                            break;
                        }
                }
            }
        }

    }
}