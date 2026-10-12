using System;
using System.Linq;
using AmteScripts.Managers;
using DOL.Events;
using DOL.GS.Finance;
using DOL.GS.Housing;
using DOL.GS.Keeps;
using DOL.Language;

namespace DOL.GS.PacketHandler.Client.v168
{
    [PacketHandlerAttribute(PacketHandlerType.TCP, eClientPackets.DialogResponse, "Response Packet from a Question Dialog", eClientStatus.PlayerInGame)]
    public class DialogResponseHandler : PacketHandler
    {
        protected override void HandlePacketInternal(GameClient client, GSPacketIn packet)
        {
            ushort data1 = packet.ReadShort();
            ushort data2 = packet.ReadShort();
            ushort data3 = packet.ReadShort();
            byte messageType = (byte)packet.ReadByte();
            byte response = (byte)packet.ReadByte();

            GamePlayer player = client.Player;
            if (player == null)
                return;

            switch ((eDialogCode)messageType)
            {
                case eDialogCode.CustomDialog:
                    {
                        if (data2 != 0x01)
                            return;

                        CustomDialogResponse callback;
                        lock (player)
                        {
                            callback = player.CustomDialogCallback;
                            player.CustomDialogCallback = null;
                        }

                        callback?.Invoke(player, response);
                        return;
                    }

                case eDialogCode.GuildInvite:
                    {
                        GamePlayer guildLeader = WorldMgr.GetObjectByIDFromRegion(player.CurrentRegionID, data1) as GamePlayer;

                        if (response == 0x01)
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

                        guildLeader?.Out.SendMessage(LanguageMgr.GetTranslation(guildLeader.Client.Account.Language, "DialogResponseHandler.GuildInviteDeclined", player.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }

                case eDialogCode.GuildLeave:
                    {
                        if (response != 0x01)
                        {
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.GuildDeclineQuit"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        if (player.Guild == null)
                        {
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.NotInGuild"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        player.Guild.RemovePlayer(player.Name, player);
                        return;
                    }

                case eDialogCode.QuestSubscribe:
                    {
                        if (WorldMgr.GetObjectByIDFromRegion(player.CurrentRegionID, data2) is not GameLiving questNPC)
                            return;

                        QuestEventArgs args = new QuestEventArgs(questNPC, player, data1);

                        if (response == 0x01) // Accept
                            // TODO add quest to player
                            // Note: This is done withing quest code since we have to check requirements, etc for each quest individually
                            // i'm reusing the questsubscribe command for quest abort since its 99% the same, only different event dets fired
                            player.Notify(data3 == 0x01 ? GamePlayerEvent.AbortQuest : GamePlayerEvent.AcceptQuest, player, args);
                        else
                            player.Notify(data3 == 0x01 ? GamePlayerEvent.ContinueQuest : GamePlayerEvent.DeclineQuest, player, args);

                        return;
                    }

                case eDialogCode.GroupInvite:
                    {
                        if (response != 0x01)
                            return;

                        GameClient leaderClient = WorldMgr.GetClientFromID(data1);
                        GamePlayer groupLeader = leaderClient?.Player;
                        if (groupLeader == null)
                            return;

                        if (player.Group != null)
                        {
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.StillInGroup"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        if (!GameServer.ServerRules.IsAllowedToGroup(groupLeader, player, false))
                            return;

                        if (!PvpManager.CanGroup(groupLeader, player, false))
                            return;

                        if (player.InCombatPvE)
                        {
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.CannotJoinGroupInCombat"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }

                        if (groupLeader.Group != null)
                        {
                            if (groupLeader.Group.Leader != groupLeader)
                                return;

                            if (groupLeader.Group.MemberCount >= ServerProperties.Properties.GROUP_MAX_MEMBER)
                            {
                                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.GroupFull"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                return;
                            }

                            groupLeader.Group.AddMember(player);
                            GameEventMgr.Notify(GamePlayerEvent.AcceptGroup, player);
                            return;
                        }

                        Group group = new Group(groupLeader);
                        GroupMgr.AddGroup(group);
                        group.AddMember(groupLeader);
                        group.AddMember(player);
                        GameEventMgr.Notify(GamePlayerEvent.AcceptGroup, player);
                        return;
                    }

                case eDialogCode.KeepClaim:
                    {
                        if (response != 0x01)
                            return;

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
                            keep.Claim(player);

                        return;
                    }

                case eDialogCode.HousePayRent:
                    {
                        if (response == 0x00)
                        {
                            if (player.TempProperties.getProperty<long>(HousingConstants.MoneyForHouseRent, -1) != -1)
                                player.TempProperties.removeProperty(HousingConstants.MoneyForHouseRent);
                            if (player.TempProperties.getProperty<long>(HousingConstants.BPsForHouseRent, -1) != -1)
                                player.TempProperties.removeProperty(HousingConstants.BPsForHouseRent);
                            player.TempProperties.removeProperty(HousingConstants.HouseForHouseRent);
                            return;
                        }

                        House house = player.TempProperties.getProperty<House>(HousingConstants.HouseForHouseRent, null);
                        if (house == null)
                            return;

                        long moneyToAdd = player.TempProperties.getProperty<long>(HousingConstants.MoneyForHouseRent, -1);
                        long bpsToMoney = player.TempProperties.getProperty<long>(HousingConstants.BPsForHouseRent, -1);
                        long rent = HouseMgr.GetRentByModel(house.Model);
                        long maxKept = rent * ServerProperties.Properties.RENT_LOCKBOX_PAYMENTS;

                        if (moneyToAdd != -1)
                        {
                            if (moneyToAdd + house.KeptMoney > maxKept)
                                moneyToAdd = maxKept - house.KeptMoney;

                            if (!player.RemoveMoney(Currency.Copper.Mint(moneyToAdd)))
                                return;

                            InventoryLogging.LogInventoryAction(player, house.DatabaseItem.ObjectId, "(HOUSE;" + house.HouseNumber + ")", eInventoryActionType.Other, moneyToAdd);
                            house.KeptMoney += moneyToAdd;
                            house.SaveIntoDatabase();
                            player.SaveIntoDatabase();

                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.DepositInLockbox", Money.GetString(moneyToAdd, player.Client.Account.Language)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.LockboxHasMoneyWeeklyPayment", Money.GetString(house.KeptMoney, player.Client.Account.Language), Money.GetString(rent)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.HousePrepaidForPayments", house.KeptMoney / rent), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }
                        else
                        {
                            if (bpsToMoney + house.KeptMoney > maxKept)
                                bpsToMoney = maxKept - house.KeptMoney;

                            if (!player.RemoveMoney(Currency.BountyPoints.Mint(Money.GetGold(bpsToMoney))))
                                return;

                            // add the bps to the lockbox
                            house.KeptMoney += bpsToMoney;

                            // save the house and the player
                            house.SaveIntoDatabase();
                            player.SaveIntoDatabase();

                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.DepositInLockbox", Money.GetString(bpsToMoney, player.Client.Account.Language)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.LockboxHasMoneyWeeklyPayment", Money.GetString(house.KeptMoney, player.Client.Account.Language), Money.GetString(rent)), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "DialogResponseHandler.HousePrepaidForPayments", house.KeptMoney / rent), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            player.TempProperties.removeProperty(HousingConstants.BPsForHouseRent);
                        }

                        player.TempProperties.removeProperty(HousingConstants.MoneyForHouseRent);
                        return;
                    }

                case eDialogCode.OpenMarket:
                    {
                        if (response != 0x01)
                            return;

                        if (player.TemporaryConsignmentMerchant == null || player.TemporaryConsignmentMerchant.ObjectState == GameObject.eObjectState.Deleted)
                        {
                            TemporaryConsignmentMerchant consignmentMerchant = new TemporaryConsignmentMerchant();
                            consignmentMerchant.playerOwner = player;
                            player.TemporaryConsignmentMerchant = consignmentMerchant;
                            consignmentMerchant.AddToWorld();
                        }

                        return;
                    }

                case eDialogCode.AskName:
                    {
                        GameClient cln = WorldMgr.GetClientFromID(data1);

                        if (response == 0x01 && cln?.Player != null && !cln.Player.SerializedAskNameList.Contains(player.Name))
                        {
                            cln.Player.SerializedAskNameList = cln.Player.SerializedAskNameList.Append(player.Name).ToArray();
                            cln.Player.SaveIntoDatabase();
                            cln.Player.Out.SendObjectRemove(player);
                            cln.Player.Out.SendPlayerCreate(player);
                            cln.Player.Out.SendLivingEquipmentUpdate(player);
                            cln.Player.Out.SendMessage(LanguageMgr.GetTranslation(cln, "Commands.Players.Askname.Added", player.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        }

                        return;
                    }

                case eDialogCode.CloseMarket:
                    {
                        if (response == 0x01)
                            player.TemporaryConsignmentMerchant?.Delete();

                        return;
                    }

                case eDialogCode.MasterLevelWindow:
                    {
                        player.Out.SendMasterLevelWindow(response);
                        return;
                    }
            }
        }
    }
}