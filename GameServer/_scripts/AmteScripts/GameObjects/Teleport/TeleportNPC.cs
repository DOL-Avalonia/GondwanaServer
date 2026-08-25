using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DOL.AI.Brain;
using DOL.Database;
using DOL.GameEvents;
using DOL.GS.PacketHandler;
using System.Reflection;
using log4net;
using DOL.Language;
using DOL.Territories;
using DOL.GS.Geometry;
using System.Linq;
using DOL.GS.Quests;
using static System.Net.Mime.MediaTypeNames;
using DOLDatabase.Tables;
using Grpc.Core;

namespace DOL.GS.Scripts
{
    public class TeleportNPC : GameNPC
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod()!.DeclaringType);

        #region Variables
        public Dictionary<string, JumpPos> JumpPositions;
        private int m_Range;
        private byte m_MinLevel;
        private string m_Text = String.Empty;
        private string m_Text_Refuse = String.Empty;
        protected DBTeleportNPC db;
        protected bool m_busy;

        public bool HasHourConditions { get; private set; }
        public bool IsTerritoryLinked { get; set; }
        public ushort RequiredModel { get; set; }

        public int Range { get => m_Range; set => m_Range = value; }
        public byte MinLevel { get => m_MinLevel; set => m_MinLevel = value; }
        public string Text { get => m_Text; set => m_Text = value; }
        public string Text_Refuse { get => m_Text_Refuse; set => m_Text_Refuse = value; }

        public bool? IsOutlawFriendly { get; set; }

        public bool ShowTPIndicator { get; set; }
        public string WhisperPassword { get; set; } = String.Empty;

        private static HashSet<GamePlayer> AuthorizedPlayers = new();
        public bool ShowBoundary { get; set; }
        public int BoundaryModel { get; set; } = 2069;
        private readonly List<GameStaticItem> _boundaryObjects = new();

        public bool UseAreaPulse { get; set; }
        public int AreaPulseSeconds { get; set; }
        public ushort AreaPulseClientEffect { get; set; }
        public ushort AreaPulseCastEffect { get; set; }
        public ushort AreaPulsePlayerEffect { get; set; }
        private long _nextPulseMs;
        public int AreaPulseCastTicks { get; set; } = 20;
        private const int AREA_PULSE_BLAST_MS = 500;
        private bool _areaPulseInProgress;

        public bool IsAreaPulseActive =>
            UseAreaPulse &&
            AreaPulseSeconds > 0 &&
            Range > 0 &&
            JumpPositions != null &&
            JumpPositions.Keys.Any(k => k.StartsWith("Area", StringComparison.OrdinalIgnoreCase));
        #endregion

        #region Interaction
        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player)) return false;

            if (!WillTalkTo(player))
                return false;

            if (!string.IsNullOrEmpty(WhisperPassword))
            {
                lock (AuthorizedPlayers)
                {
                    if (!AuthorizedPlayers.Contains(player))
                    {
                        return true;
                    }
                }
            }

            SendList(player);
            return true;
        }

        public override eQuestIndicator GetQuestIndicator(GamePlayer player)
        {
            if (ShowTPIndicator)
            {
                return ShouldShowInvisibleModel(player) ? eQuestIndicator.Teleport : eQuestIndicator.None;
            }
            return base.GetQuestIndicator(player);
        }

        private void SendList(GamePlayer player)
        {
            if (!string.IsNullOrEmpty(m_Text))
            {
                var list = GetList(player);
                if (!string.IsNullOrEmpty(list))
                {
                    var text = string.Format(m_Text, player.Name, player.LastName, player.GuildName, player.Salutation, player.RaceName, GetList(player));
                    player.Out.SendMessage(text, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                }
            }
            else
            {
                player.Out.SendMessage(GetList(player), eChatType.CT_System, eChatLoc.CL_PopupWindow);
            }
        }

        /// <summary>
        /// Checks whether the NPC will talk to a player if true, or respond with some variance of "I hate you!" to the player if false
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public bool WillTalkTo(GamePlayer player, bool silent = false)
        {
            if (player.TempProperties.getProperty<bool>("ArenaParticipant", false) || player.TempProperties.getProperty<bool>("ArenaQueued", false))
            {
                if (!silent)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleportNPC.NoEnterInArena"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                }
                return false;
            }

            if (IsTerritoryLinked == true)
            {
                var territory = CurrentTerritory ?? TerritoryManager.GetCurrentTerritory(this);
                bool accessGranted = false;

                if (territory != null && !territory.IsNeutral())
                {
                    if (player.Guild != null && player.Guild == territory.OwnerGuild)
                    {
                        accessGranted = true;
                    }
                }

                if (!accessGranted)
                {
                    if (!silent)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleportNPC.NotInOwnedTerritory"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    }
                    return false;
                }
            }

            if (RequiredModel != 0 && player.Model != RequiredModel)
            {
                if (!silent)
                {
                    player.Out.SendMessage("...", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                }
                return false;
            }

            if (this.IsOutlawFriendly.HasValue)
            {
                if (this.IsOutlawFriendly.Value)
                {
                    if (player.Reputation >= 0 && player.Client.Account.PrivLevel == 1)
                    {
                        if (!silent)
                        {
                            player.SendTranslatedMessage("TeleportNPC.YouAreNotOutlaw", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                        }
                        return false;
                    }
                }
                else
                {
                    if (player.Reputation < 0 && player.Client.Account.PrivLevel == 1)
                    {
                        if (!silent)
                        {
                            player.SendTranslatedMessage("TeleportNPC.YouAreOutlaw", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                        }
                        return false;
                    }
                }
            }

            if (player.Level < m_MinLevel)
            {
                var text = string.IsNullOrEmpty(m_Text_Refuse) ? LanguageMgr.GetTranslation(player, "TeleportNPC.RequiredLevel") : m_Text_Refuse;
                player.SendMessage(text, eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return false;
            }

            return true;
        }

        public override bool WhisperReceive(GameLiving source, string str)
        {
            if (!base.WhisperReceive(source, str) || source is not GamePlayer player) return false;

            if (!WillTalkTo(player))
            {
                return false;
            }

            bool saidPassword = false;
            if (!string.IsNullOrEmpty(WhisperPassword))
            {
                if (!string.Equals(str, WhisperPassword))
                {
                    lock (AuthorizedPlayers)
                    {
                        if (!AuthorizedPlayers.Contains(player))
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    saidPassword = true;
                    lock (AuthorizedPlayers)
                    {
                        AuthorizedPlayers.Add(player);
                        // Clean up eventually?
                    }
                }
            }

            if (JumpPositions.TryGetValue(str, out var jumpPos))
            {
                var conditionsNotMet = CheckConditionsNotMet(player, jumpPos);

                if (conditionsNotMet.Count == 0)
                {
                    if (m_busy)
                    {
                        player.SendTranslatedMessage("TeleportNPC.Busy", eChatType.CT_System, eChatLoc.CL_PopupWindow);
                        return true;
                    }

                    RegionTimer TimerTL = new RegionTimer(this, Teleportation);
                    TimerTL.Properties.setProperty("TP", jumpPos);
                    TimerTL.Properties.setProperty("player", player);
                    TimerTL.Start(3000);
                    foreach (GamePlayer players in player.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        players.Out.SendSpellCastAnimation(this, 1, 120);
                        players.Out.SendEmoteAnimation(player, eEmote.Bind);
                    }
                    m_busy = true;
                    return true;
                }
                else
                {
                    SendConditionsNotMetMessage(player, conditionsNotMet);
                }
            }
            else
            {
                if (saidPassword)
                {
                    SendList(player);
                }
                else
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleportNPC.UnknownDestination"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                }
            }
            return true;
        }

        public override bool ReceiveItem(GameLiving source, InventoryItem item)
        {
            if (!(source is GamePlayer player) || String.IsNullOrEmpty(item?.Id_nb))
                return false;

            if (IsTerritoryLinked)
            {
                var territory = CurrentTerritory ?? TerritoryManager.GetCurrentTerritory(this);
                bool accessGranted = false;

                if (territory != null && !territory.IsNeutral())
                {
                    if (player.Guild != null && player.Guild == territory.OwnerGuild)
                    {
                        accessGranted = true;
                    }
                }

                if (!accessGranted)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleportNPC.NotInOwnedTerritory"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return false;
                }
            }

            foreach (JumpPos pos in JumpPositions.Values)
            {
                if (pos.Conditions.Item.Equals(item.Id_nb, StringComparison.CurrentCultureIgnoreCase))
                {
                    RegionTimer TimerTL = new RegionTimer(this, Teleportation);
                    TimerTL.Properties.setProperty("TP", pos);
                    TimerTL.Properties.setProperty("player", player);
                    TimerTL.Start(3000);
                    foreach (GamePlayer players in player.GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        players.Out.SendSpellCastAnimation(this, 1, 20);
                        players.Out.SendEmoteAnimation(player, eEmote.Bind);
                    }
                    m_busy = true;
                    return false;
                }
            }
            return false;
        }

        private List<string> CheckConditionsNotMet(GamePlayer player, JumpPos jumpPos)
        {
            var conditionsNotMet = new List<string>();
            var eventName = GetEventName(jumpPos.Conditions.ActiveEventId);

            if (jumpPos.Conditions.BlockRelic && player.HasTerritoryRelic())
            {
                conditionsNotMet.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleportNPC.RelicBlocked"));
            }
            if (!string.IsNullOrEmpty(jumpPos.Conditions.ActiveEventId))
            {
                var e = GameEventManager.Instance.GetEventByID(jumpPos.Conditions.ActiveEventId);
                var now = DateTimeOffset.UtcNow;
                if (e.StartedTime == null || e.StartedTime > now || (e.EndTime != null && e.EndTime < now))
                {
                    conditionsNotMet.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleportNPC.EventNotOccurred", eventName));
                }
            }
            if (player.Level < jumpPos.Conditions.LevelMin)
            {
                conditionsNotMet.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleportNPC.TooLittleExperience"));
            }
            if (player.Level > jumpPos.Conditions.LevelMax)
            {
                conditionsNotMet.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleportNPC.TooMuchExperience"));
            }
            if (!string.IsNullOrEmpty(jumpPos.Conditions.Item) && !jumpPos.Conditions.PlayerHasItem(player))
            {
                conditionsNotMet.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleportNPC.ItemRequired"));
            }
            if (!jumpPos.Conditions.IsActiveAtTick(WorldMgr.GetCurrentGameTime(player)))
            {
                conditionsNotMet.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleportNPC.WrongTime"));
            }
            if (jumpPos.Conditions.RequiredCompletedQuestID > 0)
            {
                var questName = GetQuestName(jumpPos.Conditions.RequiredCompletedQuestID);
                if (player.HasFinishedQuest(DataQuestJsonMgr.GetQuest((ushort)jumpPos.Conditions.RequiredCompletedQuestID)) == 0)
                {
                    conditionsNotMet.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleportNPC.QuestNotCompleted", questName));
                }
            }
            if (jumpPos.Conditions.RequiredQuestStepID > 0)
            {
                var questName = GetQuestName(jumpPos.Conditions.RequiredCompletedQuestID);
                if (!IsPlayerOnQuestStep(player, jumpPos.Conditions.RequiredCompletedQuestID, jumpPos.Conditions.RequiredQuestStepID))
                {
                    conditionsNotMet.Add(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleportNPC.QuestStepNotCompleted", questName, jumpPos.Conditions.RequiredQuestStepID));
                }
            }
            if (conditionsNotMet.Count > 1)
            {
                return new List<string> { LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleportNPC.ConditionsNotMet") };
            }

            return conditionsNotMet;
        }

        private void SendConditionsNotMetMessage(GamePlayer player, List<string> conditionsNotMet)
        {
            foreach (var message in conditionsNotMet)
            {
                player.Out.SendMessage(message, eChatType.CT_System, eChatLoc.CL_PopupWindow);
            }
        }

        private bool IsPlayerOnQuestStep(GamePlayer player, int questID, int stepID)
        {
            var quest = player.IsDoingQuest(DataQuestJsonMgr.GetQuest((ushort)questID));
            if (quest != null)
            {
                return quest.GoalStates.Any(g => g.GoalId == stepID && g.IsActive);
            }
            return false;
        }

        private string GetEventName(string eventId)
        {
            var eventDb = GameServer.Database.SelectObject<EventDB>(DB.Column("Event_ID").IsEqualTo(eventId));
            return eventDb?.EventName ?? eventId;
        }

        private string GetQuestName(int questId)
        {
            var questDb = GameServer.Database.SelectObject<DBDataQuestJson>(DB.Column("Id").IsEqualTo(questId));
            return questDb?.Name ?? $"Quest {questId}";
        }

        protected virtual int Teleportation(RegionTimer timer)
        {
            JumpPos pos = timer.Properties.getProperty<JumpPos>("TP", null);
            GamePlayer player = timer.Properties.getProperty<GamePlayer>("player", null);
            if (pos == null || player == null) return 0;
            if (player.InCombat)
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleportNPC.NoTPCombat"), eChatType.CT_Important,
                                       eChatLoc.CL_SystemWindow);
            else
                pos.Jump(this, player);
            m_busy = false;
            return 0;
        }
        #endregion

        #region Area pulse logic
        public void HandleAreaPulseThink()
        {
            if (!IsAreaPulseActive)
                return;

            if (HasHourConditions)
            {
                var areaJump = JumpPositions.Values.FirstOrDefault(j => j.Name.StartsWith("Area", StringComparison.OrdinalIgnoreCase));

                if (areaJump != null && !areaJump.Conditions.IsActiveAtTick(WorldMgr.GetCurrentGameTime()))
                {
                    return;
                }
            }

            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (_nextPulseMs <= 0)
            {
                _nextPulseMs = nowMs + (long)AreaPulseSeconds * 1000L;
                return;
            }

            if (nowMs < _nextPulseMs)
                return;

            _nextPulseMs = nowMs + (long)AreaPulseSeconds * 1000L;

            StartAreaPulseSequence();
        }

        private void StartAreaPulseSequence()
        {
            if (_areaPulseInProgress)
                return;

            if (ObjectState != eObjectState.Active || CurrentRegion == null)
                return;

            if (!JumpPositions.Keys.Any(k => k.StartsWith("Area", StringComparison.OrdinalIgnoreCase)))
                return;

            _areaPulseInProgress = true;

            if (AreaPulseCastTicks <= 0)
            {
                RegionTimer blastTimer = new RegionTimer(this, AreaPulseBlastCallback);
                blastTimer.Start(100);
                return;
            }

            if (AreaPulseCastEffect > 0)
            {
                ushort castTicks = (ushort)AreaPulseCastTicks;
                foreach (GamePlayer p in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    p.Out.SendSpellCastAnimation(this, AreaPulseCastEffect, castTicks);
                }
            }
            else if (AreaPulseClientEffect > 0)
            {
                ushort castTicks = (ushort)AreaPulseCastTicks;
                foreach (GamePlayer p in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                {
                    p.Out.SendSpellCastAnimation(this, AreaPulseClientEffect, castTicks);
                }
            }

            int castMs = AreaPulseCastTicks * 100;

            if (AreaPulseClientEffect <= 0 && AreaPulsePlayerEffect <= 0 && AreaPulseCastEffect <= 0)
            {
                RegionTimer t = new RegionTimer(this, AreaPulseTeleportCallback);
                t.Start(castMs);
                return;
            }

            RegionTimer nextPhaseTimer = new RegionTimer(this, AreaPulseBlastCallback);
            nextPhaseTimer.Start(castMs);
        }

        private int AreaPulseBlastCallback(RegionTimer timer)
        {
            try
            {
                if (ObjectState != eObjectState.Active || CurrentRegion == null)
                {
                    _areaPulseInProgress = false;
                    return 0;
                }

                if (AreaPulseClientEffect > 0)
                {
                    foreach (GamePlayer p in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                    {
                        p.Out.SendSpellEffectAnimation(this, this, AreaPulseClientEffect, 0, false, 1);
                    }
                }

                if (AreaPulsePlayerEffect > 0)
                {
                    var validJumps = JumpPositions.Values.Where(j => j.Name.StartsWith("Area", StringComparison.OrdinalIgnoreCase)).ToList();

                    if (validJumps.Count > 0)
                    {
                        foreach (GamePlayer p in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                        {
                            if (p.GetDistanceTo(this) <= m_Range)
                            {
                                if (!WillTalkTo(p, silent: true))
                                    continue;

                                if (IsTerritoryLinked)
                                {
                                    if (CurrentTerritory == null || CurrentTerritory.IsNeutral()
                                        || p.Guild == null || p.Guild != CurrentTerritory.OwnerGuild)
                                    {
                                        continue;
                                    }
                                }
                                bool canJumpAny = false;
                                foreach (var jump in validJumps)
                                {
                                    if (jump.CanJump(p))
                                    {
                                        canJumpAny = true;
                                        break;
                                    }
                                }

                                if (canJumpAny)
                                {
                                    p.Out.SendSpellEffectAnimation(this, p, AreaPulsePlayerEffect, 0, false, 1);
                                }
                            }
                        }
                    }
                }

                RegionTimer tpTimer = new RegionTimer(this, AreaPulseTeleportCallback);
                tpTimer.Start(AREA_PULSE_BLAST_MS);
            }
            catch
            {
                _areaPulseInProgress = false;
            }

            return 0;
        }

        private int AreaPulseTeleportCallback(RegionTimer timer)
        {
            try
            {
                if (ObjectState != eObjectState.Active || CurrentRegion == null)
                {
                    _areaPulseInProgress = false;
                    return 0;
                }

                if (m_Range <= 0)
                {
                    _areaPulseInProgress = false;
                    return 0;
                }

                var validJumps = JumpPositions.Values.Where(j => j.Name.StartsWith("Area", StringComparison.OrdinalIgnoreCase)).ToList();

                if (validJumps.Count == 0)
                {
                    _areaPulseInProgress = false;
                    return 0;
                }

                validJumps.Sort((a, b) => {
                    bool aHasSlot = a.Conditions.RequiredSlot > 0;
                    bool bHasSlot = b.Conditions.RequiredSlot > 0;

                    if (aHasSlot && !bHasSlot) return -1;
                    if (!aHasSlot && bHasSlot) return 1;

                    if (aHasSlot && bHasSlot)
                    {
                        return a.Conditions.RequiredSlot.CompareTo(b.Conditions.RequiredSlot);
                    }

                    return 0;
                });

                foreach (GamePlayer player in GetPlayersInRadius((ushort)m_Range))
                {
                    if (player == null) continue;
                    if (!WillTalkTo(player, silent: true)) continue;
                    if (player.InCombat) continue;

                    if (IsTerritoryLinked)
                    {
                        if (CurrentTerritory == null || CurrentTerritory.IsNeutral()
                            || player.Guild == null || player.Guild != CurrentTerritory.OwnerGuild)
                        {
                            continue;
                        }
                    }

                    foreach (var pos in validJumps)
                    {
                        if (pos.CanJump(player))
                        {
                            pos.Jump(this, player);
                            break;
                        }
                    }
                }
            }
            finally
            {
                _areaPulseInProgress = false;
            }

            return 0;
        }
        #endregion

        #region JumpArea
        public void JumpArea()
        {
            if (m_Range <= 0 || JumpPositions.Count < 1)
                return;

            var validJumps = JumpPositions.Values.Where(j => j.Name.StartsWith("Area", StringComparison.OrdinalIgnoreCase)).ToList();

            validJumps.Sort((a, b) => {
                bool aHasSlot = a.Conditions.RequiredSlot > 0;
                bool bHasSlot = b.Conditions.RequiredSlot > 0;

                if (aHasSlot && !bHasSlot) return -1;
                if (!aHasSlot && bHasSlot) return 1;
                if (aHasSlot && bHasSlot) return a.Conditions.RequiredSlot.CompareTo(b.Conditions.RequiredSlot);
                return 0;
            });

            foreach (GamePlayer player in GetPlayersInRadius((ushort)m_Range))
            {
                if (player.Level >= m_MinLevel)
                {
                    if (player.InCombat)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleportNPC.NoTPCombat"),
                                               eChatType.CT_Important,
                                               eChatLoc.CL_SystemWindow);
                    }
                    else
                    {
                        if (IsTerritoryLinked)
                        {
                            if (CurrentTerritory == null || CurrentTerritory.IsNeutral()
                                || player.Guild == null || player.Guild != CurrentTerritory.OwnerGuild)
                            {
                                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleportNPC.NotInOwnedTerritory"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                                continue;
                            }
                        }

                        foreach (var pos in validJumps)
                        {
                            if (pos.CanJump(player))
                            {
                                pos.Jump(this, player);
                                break;
                            }
                        }
                    }
                }
                else
                    player.Out.SendMessage(string.Format(m_Text_Refuse, player.Name, player.LastName, player.GuildName, player.Salutation, player.RaceName), eChatType.CT_System, eChatLoc.CL_SystemWindow);
            }
        }
        #endregion

        #region Boundary ring spawn/cleanup
        private void ClearBoundary()
        {
            foreach (var o in _boundaryObjects)
            {
                try { o.RemoveFromWorld(); } catch { }
            }
            _boundaryObjects.Clear();
        }

        private void SpawnBoundary()
        {
            ClearBoundary();

            if (!ShowBoundary || m_Range <= 100)
                return;

            if (ObjectState != eObjectState.Active || CurrentRegion == null)
                return;

            int model = BoundaryModel > 0 ? BoundaryModel : 2069;
            double k = 28.0 / (2.0 * Math.PI * 1000.0);
            int count = (int)Math.Round((2.0 * Math.PI * m_Range) * k);
            count = Math.Max(6, Math.Min(64, count));

            int ringRadius = Math.Max(64, m_Range - 50);
            var center = this.Position;

            for (int i = 0; i < count; i++)
            {
                double t = (2.0 * Math.PI * i) / count;

                Angle outward = Angle.Radians(t);
                Vector offset = Vector.Create(outward, ringRadius);
                Angle towardCenter = Angle.Radians(t + Math.PI);

                var mini = new GameStaticItem
                {
                    Model = (ushort)model,
                    Name = "Boundary",
                    Position = Position.Create(
                        regionID: CurrentRegionID,
                        x: center.X + offset.X,
                        y: center.Y + offset.Y,
                        z: center.Z,
                        heading: towardCenter.InHeading
                    )
                };

                mini.AddToWorld();
                _boundaryObjects.Add(mini);
            }
        }
        #endregion

        #region Database
        public override void LoadFromDatabase(DataObject mobobject)
        {
            base.LoadFromDatabase(mobobject);

            db = GameServer.Database.SelectObject<DBTeleportNPC>(t => t.MobID == InternalID);
            if (db == null)
                return;
            m_Range = db.Range;
            m_MinLevel = db.Level;
            m_Text = db.Text;
            m_Text_Refuse = db.Text_Refuse;
            IsTerritoryLinked = db.IsTerritoryLinked;
            ShowTPIndicator = db.ShowTPIndicator;
            WhisperPassword = db.WhisperPassword;
            ShowBoundary = db.ShowBoundary;
            BoundaryModel = db.BoundaryModel;
            UseAreaPulse = db.UseAreaPulse;
            AreaPulseSeconds = db.AreaPulseSeconds;
            AreaPulseClientEffect = (ushort)Math.Max(0, db.AreaPulseClientEffect);
            AreaPulseCastEffect = (ushort)Math.Max(0, db.AreaPulseCastEffect);
            AreaPulsePlayerEffect = (ushort)Math.Max(0, db.AreaPulsePlayerEffect);
            AreaPulseCastTicks = Math.Max(0, db.AreaPulseCastTicks);

            //Set this value only when OR Exclusive
            if (db.IsOutlawFriendly ^ db.IsRegularFriendly)
            {
                if (db.IsRegularFriendly)
                {
                    IsOutlawFriendly = false;
                }

                if (db.IsOutlawFriendly)
                {
                    IsOutlawFriendly = true;
                }
            }
            else if (db.IsRegularFriendly && db.IsOutlawFriendly)
            {
                log.Error("Cannot load IsOutlawFriendly Status because both values are set. Update database(TeleportNPC) for id: " + this.InternalID + " npc: " + this.Name);
            }

            LoadJumpPos();
        }

        public override void SaveIntoDatabase()
        {
            base.SaveIntoDatabase();

            bool add = (db == null);
            if (add)
                db = new DBTeleportNPC();

            db!.JumpPosition = GetJumpPosString();
            db.Level = m_MinLevel;
            db.MobID = InternalID;
            db.Range = m_Range;
            db.Text = m_Text;
            db.Text_Refuse = m_Text_Refuse;
            db.IsTerritoryLinked = IsTerritoryLinked;
            db.WhisperPassword = WhisperPassword;
            db.ShowTPIndicator = ShowTPIndicator;
            db.ShowBoundary = ShowBoundary;
            db.BoundaryModel = BoundaryModel;
            db.UseAreaPulse = UseAreaPulse;
            db.AreaPulseSeconds = AreaPulseSeconds;
            db.AreaPulseClientEffect = AreaPulseClientEffect;
            db.AreaPulseCastEffect = AreaPulseCastEffect;
            db.AreaPulsePlayerEffect = AreaPulsePlayerEffect;

            db.AreaPulseCastTicks = Math.Max(0, AreaPulseCastTicks);
            db.AreaPulseCastTicks = Math.Max(0, db.AreaPulseCastTicks);

            if (IsOutlawFriendly.HasValue)
            {
                if (IsOutlawFriendly.Value)
                {
                    db.IsOutlawFriendly = true;
                }
                else
                {
                    db.IsRegularFriendly = true;
                }
            }

            if (add)
                GameServer.Database.AddObject(db);
            else
                GameServer.Database.SaveObject(db);
        }

        public override void DeleteFromDatabase()
        {
            base.DeleteFromDatabase();
            if (db != null)
                GameServer.Database.DeleteObject(db);
        }

        private void LoadJumpPos()
        {
            string[] objs = db.JumpPosition.Split('|');
            JumpPositions = new Dictionary<string, JumpPos>(objs.Length);

            foreach (string S_pos in objs)
            {
                if (string.IsNullOrEmpty(S_pos))
                    continue;
                try
                {
                    JumpPos pos = new JumpPos(S_pos);
                    if (!string.IsNullOrEmpty(pos.Name))
                        JumpPositions.Add(pos.Name, pos);
                    if (pos.Conditions.RequiredQuestStepID > 0 && pos.Conditions.RequiredCompletedQuestID == 0)
                    {
                        log.Warn($"TeleportNPC {Name} ({InternalID}) condition \"{pos.Name}\" has RequiredQuestStepID != 0 but RequiredCompletedQuestID == 0, can't know which quest we are talking about");
                    }
                }
                catch { }
            }
        }

        private string GetJumpPosString()
        {
            if (JumpPositions == null || JumpPositions.Count == 0)
                return "";

            return string.Join('|', JumpPositions.Values);
        }
        #endregion

        #region JumpPos Gestion
        public string GetList(GamePlayer player)
        {
            StringBuilder sb = new StringBuilder();
            foreach (JumpPos pos in JumpPositions.Values)
            {
                if (pos.IsInList(player))
                {
                    sb.Append('\n');
                    sb.Append('[');
                    sb.Append(pos.Name);
                    sb.Append("]");
                }
            }
            return sb.ToString();
        }

        public void AddJumpPos(string name, int x, int y, int z, ushort heading, ushort regionID)
        {
            if (JumpPositions.ContainsKey(name))
                JumpPositions[name] = new JumpPos(name, x, y, z, heading, regionID);
            else
                JumpPositions.Add(name, new JumpPos(name, x, y, z, heading, regionID));
        }

        public bool RemoveJumpPos(string name)
        {
            if (JumpPositions.ContainsKey(name))
            {
                JumpPositions.Remove(name);
                return true;
            }
            return false;
        }

        public ArrayList GetJumpList()
        {
            ArrayList jumps = new ArrayList(JumpPositions.Values);
            return jumps;
        }
        #endregion

        public override bool AddToWorld()
        {
            if (!base.AddToWorld()) return false;

            if (JumpPositions == null)
                JumpPositions = new Dictionary<string, JumpPos>();

            else if (JumpPositions.Values.Any(j => j.Conditions.HourMin >= 0 || j.Conditions.HourMax <= 24))
                HasHourConditions = true;

            if (Brain is not TeleportNPCBrain)
                SetOwnBrain(new TeleportNPCBrain());

            foreach (var jump in JumpPositions.Values.Where(j => !string.IsNullOrEmpty(j.Conditions.ActiveEventId)))
            {
                var e = GameEventManager.Instance.GetEventByID(jump.Conditions.ActiveEventId);

                if (e == null)
                {
                    log.Warn($"TeleportNPC {Name} ({InternalID}) has jump {jump.Name} referencing event {jump.Conditions.ActiveEventId} which was not found");
                    continue;
                }

                lock (e.RelatedNPCs)
                {
                    e.RelatedNPCs.Add(this);
                }
            }

            SpawnBoundary();

            if (IsAreaPulseActive)
            {
                long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _nextPulseMs = nowMs + (long)AreaPulseSeconds * 1000L;
            }

            return true;
        }

        public override bool MoveTo(Position position)
        {
            if (!base.MoveTo(position)) return false;

            SpawnBoundary();
            return true;
        }

        public override bool RemoveFromWorld()
        {
            ClearBoundary();
            return base.RemoveFromWorld();
        }

        public bool ShouldShowInvisibleModel(GamePlayer player)
        {
            if (IsTerritoryLinked)
            {
                var territory = CurrentTerritory ?? TerritoryManager.GetCurrentTerritory(this);
                if (territory == null || territory.IsNeutral() || player.Guild == null || player.Guild != territory.OwnerGuild)
                    return false;
            }

            return JumpPositions.Values.Any(c => c.CanJump(player));
        }

        public class JumpPos
        {
            public Position Position = Position.Nowhere;
            public string Name;
            public TeleportCondition Conditions;

            public JumpPos(string SaveStr)
            {
                try
                {
                    string[] args = SaveStr.Split(';');
                    Name = args[0];
                    Position = Position.Create(
                        regionID: ushort.Parse(args[5]),
                        x: int.Parse(args[1]),
                        y: int.Parse(args[2]),
                        z: int.Parse(args[3]),
                        heading: ushort.Parse(args[4])
                    );
                    Conditions = new TeleportCondition(args.Length > 6 ? args[6] : "");
                }
                catch
                {
                    log.Error("TELEPORTNPC: Erreur lors du parsing de \"" + SaveStr + "\".");
                }
            }

            public JumpPos(string name, int x, int y, int z, ushort heading, ushort regionID)
            {
                Name = name;
                Position = Position.Create(regionID, x, y, z, heading);
                Conditions = new TeleportCondition("");
            }

            public override string ToString()
            {
                return Name + ";" + Position.X + ";" + Position.Y + ";" + Position.Z + ";" +
                    Position.Orientation.InHeading + ";" + Position.RegionID + ";" + Conditions.GetStringDB();
            }

            public bool IsInList(GamePlayer player)
            {
                return Conditions.Visible && CanJump(player);
            }

            public bool CanJump(GamePlayer player)
            {
                if (Conditions.BlockRelic && player.HasTerritoryRelic())
                    return false;
                if (!Conditions.IsActiveAtTick(WorldMgr.GetCurrentGameTime(player)))
                    return false;
                if (player.Level < Conditions.LevelMin || player.Level > Conditions.LevelMax)
                    return false;
                if (!string.IsNullOrEmpty(Conditions.Item) && !Conditions.PlayerHasItem(player))
                    return false;
                if (!string.IsNullOrEmpty(Conditions.ActiveEventId))
                {
                    var e = GameEventManager.Instance.GetEventByID(Conditions.ActiveEventId);
                    var now = DateTimeOffset.UtcNow;
                    if (e == null || e.StartedTime == null || e.StartedTime > now || (e.EndTime != null && e.EndTime < now))
                    {
                        return false;
                    }
                }
                if (Conditions.RequiredCompletedQuestID > 0)
                {
                    if (Conditions.RequiredQuestStepID > 0 && !IsPlayerOnQuestStep(player, Conditions.RequiredCompletedQuestID, Conditions.RequiredQuestStepID))
                        return false;
                    if (player.HasFinishedQuest(DataQuestJsonMgr.GetQuest((ushort)Conditions.RequiredCompletedQuestID)) == 0)
                        return false;
                }
                return true;
            }

            private bool IsPlayerOnQuestStep(GamePlayer player, int questID, int stepID)
            {
                var quest = player.IsDoingQuest(DataQuestJsonMgr.GetQuest((ushort)questID));
                if (quest != null)
                {
                    return quest.GoalStates.Any(g => g.GoalId == stepID && g.IsActive);
                }
                return false;
            }

            private void HandleInstancedJump(TeleportNPC source, GamePlayer player)
            {
                string ownerId = "";
                List<GamePlayer> playersToMove = new List<GamePlayer>();

                switch (Conditions.InstanceRule)
                {
                    case eInstanceRule.Solo:
                        ownerId = "Solo:" + player.InternalID;
                        playersToMove.Add(player);
                        break;
                    case eInstanceRule.Group:
                        if (player.Group == null)
                        {
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleportNPC.TpInstanceMustGroup"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        if (player.Group.Leader != player)
                        {
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleportNPC.TpInstanceGroupLeader"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        ownerId = "Group:" + player.Group.Leader.InternalID;
                        playersToMove.AddRange(player.Group.GetPlayersInTheGroup());
                        break;
                    case eInstanceRule.Guild:
                        if (player.Guild == null || player.Guild.IsSystemGuild)
                        {
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleportNPC.TpInstanceMustGuild"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        ownerId = "Guild:" + player.Guild.GuildID;
                        playersToMove.AddRange(player.Guild.GetListOfOnlineMembers().Where(m => m.CurrentRegionID == player.CurrentRegionID));
                        break;
                    case eInstanceRule.Battlegroup:
                        if (player.BattleGroup == null)
                        {
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleportNPC.TpInstanceMustBG"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        if (player.BattleGroup.Leader != player)
                        {
                            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleportNPC.TpInstanceBGLeader"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                            return;
                        }
                        ownerId = "BG:" + player.BattleGroup.Leader.InternalID;
                        playersToMove.AddRange(player.BattleGroup.Members.Values.OfType<GamePlayer>());
                        break;
                }

                // Filter out offline players or players who are too far away
                var finalPlayers = new List<GamePlayer>();
                foreach (var p in playersToMove)
                {
                    if (p.IsWithinRadius(source, 2000) && CanJump(p))
                        finalPlayers.Add(p);
                }

                if (finalPlayers.Count == 0) return;

                // Grab Median level for scaling
                int medianLevel = player.Level;
                var levels = finalPlayers.Select(p => (int)p.Level).OrderBy(l => l).ToList();
                if (levels.Count > 0)
                    medianLevel = levels[levels.Count / 2];

                ushort baseRegionID = Position.RegionID;
                ushort skinID = baseRegionID;

                // Custom instance skin if configured in the Teleporter Conditions
                if (Conditions.InstanceSkin > 0)
                {
                    skinID = Conditions.InstanceSkin;
                }

                CustomEventInstance instance = WorldMgr.Regions.Values.OfType<CustomEventInstance>()
                    .FirstOrDefault(i => i.InstanceOwnerID == ownerId && i.RegionData.Id == baseRegionID);

                if (instance == null)
                {
                    ushort requestedID = WorldMgr.DEFAULT_VALUE_FOR_INSTANCE_ID_SEARCH_START;
                    while (requestedID < ushort.MaxValue && WorldMgr.Regions.ContainsKey(requestedID))
                        requestedID++;

                    if (requestedID == ushort.MaxValue)
                    {
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "TeleportNPC.NoInstanceNoRegion"), eChatType.CT_System, eChatLoc.CL_SystemWindow);
                        return;
                    }

                    instance = WorldMgr.CreateInstance(requestedID, skinID, typeof(CustomEventInstance)) as CustomEventInstance;
                    if (instance != null)
                    {
                        instance.InstanceOwnerID = ownerId;
                        instance.InstanceType = Conditions.InstanceRule;
                        if (Conditions.InstanceRule == eInstanceRule.Solo) instance.Player = player;
                        if (Conditions.InstanceRule == eInstanceRule.Group) { instance.Group = player.Group; instance.Player = player.Group.Leader; }
                        if (Conditions.InstanceRule == eInstanceRule.Guild) { instance.Guild = player.Guild; instance.Player = player; }
                        if (Conditions.InstanceRule == eInstanceRule.Battlegroup) { instance.BattleGroup = player.BattleGroup; instance.Player = player.BattleGroup.Leader; }

                        long m = 0, me = 0, i = 0, b = 0;
                        instance.LoadFromDatabase(WorldMgr.RegionData[baseRegionID].Mobs, ref m, ref me, ref i, ref b);
                    }
                }

                if (instance != null)
                {
                    if (Conditions.ScaleMobs || Conditions.SmartScale)
                    {
                        int targetLvl = medianLevel;

                        if (Conditions.SmartScale && Conditions.InstanceRule == eInstanceRule.Solo)
                        {
                            targetLvl = GetSmartScaleLevel(player);
                        }
                        else
                        {
                            targetLvl = medianLevel + Conditions.ScaleOffset;
                        }

                        instance.ScaleMobs(targetLvl, Conditions.BossScaling);
                    }

                    if (Conditions.ClonePlayerClasses)
                    {
                        instance.ApplyPlayerClassesToMobs(finalPlayers, Conditions.CloneClassesColor);
                    }

                    foreach (var p in finalPlayers)
                    {
                        p.MoveTo(Position.Create(instance.ID, Position.X, Position.Y, Position.Z, Position.Orientation.InHeading));
                        if (Conditions.Bind) p.Bind(true);
                    }
                }
            }

            private int GetSmartScaleLevel(GamePlayer player)
            {
                int pLvl = player.Level;
                eClassType cType = player.CharacterClass.ClassType;

                if (cType == eClassType.ListCaster)
                {
                    return GetBlueConLevel(pLvl);
                }
                else if (cType == eClassType.Hybrid)
                {
                    if (pLvl >= 42) return Math.Max(1, pLvl - 4);
                    if (pLvl >= 35) return Math.Max(1, pLvl - 3);
                    if (pLvl >= 22) return Math.Max(1, pLvl - 2);
                    if (pLvl >= 12) return Math.Max(1, pLvl - 1);
                    return pLvl;
                }
                else if (cType == eClassType.PureTank)
                {
                    if (pLvl > 22) return GetOrangeConLevel(pLvl);
                    if (pLvl >= 12) return pLvl + 1;
                    return pLvl;
                }
                return pLvl;
            }

            /// <summary>
            /// Gets the exact level for a Blue Con mob relative to the player's level bracket.
            /// </summary>
            private int GetBlueConLevel(int pLvl)
            {
                int offset = 1;
                if (pLvl >= 40) offset = 5;
                else if (pLvl >= 30) offset = 4;
                else if (pLvl >= 20) offset = 3;
                else if (pLvl >= 10) offset = 2;
                else offset = 1;

                return Math.Max(1, pLvl - offset);
            }

            /// <summary>
            /// Gets the exact level for an Orange Con mob relative to the player's level bracket.
            /// </summary>
            private int GetOrangeConLevel(int pLvl)
            {
                int offset = 2;
                if (pLvl >= 40) offset = 3;
                else if (pLvl >= 30) offset = 2;
                else if (pLvl >= 20) offset = 2;
                else offset = 1;

                return Math.Min(255, pLvl + offset);
            }

            public void Jump(GameLiving source, GamePlayer player)
            {
                if (player.Level < Conditions.LevelMin || player.Level > Conditions.LevelMax)
                    return;
                if (!string.IsNullOrEmpty(Conditions.Item))
                {
                    if (Conditions.RequiredSlot > 0)
                    {
                        var item = player.Inventory.GetItem((eInventorySlot)Conditions.RequiredSlot);

                        if (Conditions.ConditionAmount > 0 && item != null)
                        {
                            if (item.Condition > 0)
                                item.Condition -= Conditions.ConditionAmount;

                            if (item.Condition <= 0)
                            {
                                player.Inventory.RemoveItem(item);
                                string msg = LanguageMgr.GetTranslation(player.Client, "TeleportNPC.ItemConsumed", item.Name);
                                player.Out.SendMessage(msg, eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                            }
                            else
                            {
                                player.Out.SendInventoryItemsUpdate(new InventoryItem[] { item });
                                if (item.ConditionPercent <= 70)
                                {
                                    string msg = LanguageMgr.GetTranslation(player.Client, "TeleportNPC.ItemDegrading", item.Name, item.ConditionPercent);
                                    player.Out.SendMessage(msg, eChatType.CT_System, eChatLoc.CL_SystemWindow);
                                }
                            }
                        }
                    }
                    else
                    {
                        if (!player.Inventory.RemoveTemplate(Conditions.Item, 1, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack))
                            return;
                        InventoryLogging.LogInventoryAction(player, source, eInventoryActionType.Other, Conditions.ItemTemplate, 1);
                    }
                }

                if (Conditions.InstanceRule != eInstanceRule.None)
                {
                    HandleInstancedJump((TeleportNPC)source, player);
                }
                else
                {
                    player.MoveTo(Position);
                    if (Conditions.Bind) player.Bind(true);
                }
            }
        }

        public class TeleportCondition
        {
            private string _item;

            public bool Bind;
            public bool Visible = true;
            public bool BlockRelic;
            public eInstanceRule InstanceRule = eInstanceRule.None;
            public bool ScaleMobs = false;
            public bool SmartScale = false;
            public int ScaleOffset = 0;
            public string BossScaling = string.Empty;
            public ushort InstanceSkin = 0;
            public bool ClonePlayerClasses = false;
            public ushort CloneClassesColor = 0;

            public string Item
            {
                get { return _item; }
                set
                {
                    _item = value;
                    ItemTemplate = GameServer.Database.FindObjectByKey<ItemTemplate>(_item);
                }
            }
            public int LevelMin;
            public int LevelMax = 50;
            public int HourMin;
            public int HourMax = 24;
            public string ActiveEventId = String.Empty;
            public ItemTemplate ItemTemplate { get; private set; }
            public int RequiredCompletedQuestID { get; set; }
            public int RequiredQuestStepID { get; set; }
            public int RequiredSlot { get; set; }
            public int ConditionAmount { get; set; }

            public bool PlayerHasItem(GamePlayer player)
            {
                if (string.IsNullOrEmpty(Item)) return true;

                if (RequiredSlot > 0)
                {
                    var item = player.Inventory.GetItem((eInventorySlot)RequiredSlot);
                    return item != null && item.Id_nb.Equals(Item, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    return player.Inventory.GetFirstItemByID(Item, eInventorySlot.FirstBackpack, eInventorySlot.LastBackpack) != null;
                }
            }

            public bool IsActiveAtTick(uint tick)
            {
                if (HourMin <= 0 && HourMax >= 24)
                {
                    return true;
                }

                uint minTick = ((uint)HourMin) * 60 * 60 * 1000;
                uint maxTick = ((uint)HourMax) * 60 * 60 * 1000;

                //Heure
                if (maxTick > minTick && (tick < minTick || tick >= maxTick))
                    return false;
                if (maxTick < minTick && (tick < minTick && tick >= maxTick))
                    return false;
                if (maxTick == minTick && tick != minTick)
                    return false;
                return true;
            }

            public TeleportCondition(string db)
            {
                if (db.Length >= 1)
                {
                    try
                    {
                        string[] args = db.Split('/');
                        foreach (string s in args)
                        {
                            string[] arg = s.Split('=');
                            switch (arg[0])
                            {
                                case "BlockRelic": BlockRelic = bool.Parse(arg[1]); break;
                                case "Bind": Bind = bool.Parse(arg[1]); break;
                                case "Visible": Visible = bool.Parse(arg[1]); break;
                                case "Item": Item = arg[1]; break;
                                case "LevelMin": LevelMin = int.Parse(arg[1]); break;
                                case "LevelMax": LevelMax = int.Parse(arg[1]); break;
                                case "HourMin": HourMin = int.Parse(arg[1]); break;
                                case "HourMax": HourMax = int.Parse(arg[1]); break;
                                case "RequiredCompletedQuestID": RequiredCompletedQuestID = int.Parse(arg[1]); break;
                                case "RequiredQuestStepID": RequiredQuestStepID = int.Parse(arg[1]); break;
                                case "EventID": ActiveEventId = arg[1]; break;
                                case "Slot": RequiredSlot = int.Parse(arg[1]); break;
                                case "Condition": ConditionAmount = int.Parse(arg[1]); break;

                                // Instance variables
                                case "InstanceRule":
                                    if (Enum.TryParse(arg[1], true, out eInstanceRule rule)) InstanceRule = rule;
                                    break;
                                case "ScaleMobs":
                                    if (arg[1].Equals("Smart", StringComparison.OrdinalIgnoreCase))
                                    {
                                        ScaleMobs = true; SmartScale = true; ScaleOffset = 0;
                                    }
                                    else if (bool.TryParse(arg[1], out bool bScale))
                                    {
                                        ScaleMobs = bScale; SmartScale = false; ScaleOffset = 0;
                                    }
                                    else if (int.TryParse(arg[1], out int iScale))
                                    {
                                        ScaleMobs = true; SmartScale = false; ScaleOffset = iScale;
                                    }
                                    break;
                                case "BossScaling": BossScaling = arg[1]; break;
                                case "InstanceSkin": ushort.TryParse(arg[1], out InstanceSkin); break;
                                case "ClonePlayerClasses":
                                    string[] cpcArgs = arg[1].Split(',');
                                    if (cpcArgs.Length > 0) bool.TryParse(cpcArgs[0], out ClonePlayerClasses);
                                    if (cpcArgs.Length > 1) ushort.TryParse(cpcArgs[1], out CloneClassesColor);
                                    break;
                            }
                        }
                    }
                    catch
                    {
                        log.Error("TELEPORTNPC: Erreur lors du parse de \"" + db + "\".");
                    }
                }
            }

            public string GetStringDB()
            {
                StringBuilder sb = new StringBuilder();
                if (BlockRelic)
                {
                    if (sb.Length > 0) sb.Append("/");
                    sb.Append("BlockRelic=");
                    sb.Append(BlockRelic);
                }
                if (!Visible)
                {
                    if (sb.Length > 0) sb.Append("/");
                    sb.Append("Visible=");
                    sb.Append(Visible);
                }
                if (Bind)
                {
                    if (sb.Length > 0) sb.Append("/");
                    sb.Append("Bind=");
                    sb.Append(Bind);
                }
                if (!string.IsNullOrEmpty(Item))
                {
                    if (sb.Length > 0) sb.Append("/");
                    sb.Append("Item=");
                    sb.Append(Item);
                }
                if (RequiredSlot > 0)
                {
                    if (sb.Length > 0) sb.Append("/");
                    sb.Append("Slot=");
                    sb.Append(RequiredSlot);
                }
                if (ConditionAmount > 0)
                {
                    if (sb.Length > 0) sb.Append("/");
                    sb.Append("Condition=");
                    sb.Append(ConditionAmount);
                }
                if (LevelMin > 1)
                {
                    if (sb.Length > 0) sb.Append("/");
                    sb.Append("LevelMin=");
                    sb.Append(LevelMin);
                }
                if (LevelMax < 50)
                {
                    if (sb.Length > 0) sb.Append("/");
                    sb.Append("LevelMax=");
                    sb.Append(LevelMax);
                }
                if (HourMin >= 0)
                {
                    if (sb.Length > 0) sb.Append("/");
                    sb.Append("HourMin=");
                    sb.Append(HourMin);
                }
                if (HourMax >= 0)
                {
                    if (sb.Length > 0) sb.Append("/");
                    sb.Append("HourMax=");
                    sb.Append(HourMax);
                }
                if (RequiredCompletedQuestID != 0)
                {
                    if (sb.Length > 0) sb.Append("/");
                    sb.Append("RequiredCompletedQuestID=");
                    sb.Append(RequiredCompletedQuestID);
                }
                if (RequiredQuestStepID != 0)
                {
                    if (sb.Length > 0) sb.Append("/");
                    sb.Append("RequiredQuestStepID=");
                    sb.Append(RequiredQuestStepID);
                }
                if (!string.IsNullOrEmpty(ActiveEventId))
                {
                    if (sb.Length > 0) sb.Append("/");
                    sb.Append("EventID=");
                    sb.Append(ActiveEventId);
                }

                if (InstanceRule != eInstanceRule.None) { if (sb.Length > 0) sb.Append("/"); sb.Append("InstanceRule=").Append(InstanceRule); }
                if (ScaleMobs || SmartScale)
                {
                    if (sb.Length > 0) sb.Append("/");
                    sb.Append("ScaleMobs=");
                    if (SmartScale) sb.Append("Smart");
                    else sb.Append(ScaleOffset != 0 ? ScaleOffset.ToString() : "True");
                }
                if (!string.IsNullOrEmpty(BossScaling)) { if (sb.Length > 0) sb.Append("/"); sb.Append("BossScaling=").Append(BossScaling); }
                if (InstanceSkin > 0) { if (sb.Length > 0) sb.Append("/"); sb.Append("InstanceSkin=").Append(InstanceSkin); }
                if (ClonePlayerClasses) { if (sb.Length > 0) sb.Append("/"); sb.Append("ClonePlayerClasses=").Append(ClonePlayerClasses).Append(",").Append(CloneClassesColor); }
                return sb.ToString();
            }

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("Bind le joueur après l'avoir TP : ");
                sb.Append(Bind ? "oui" : "non");
                sb.Append("\nVisible dans la liste: ");
                sb.Append(Visible ? "oui" : "non");

                if (InstanceRule != eInstanceRule.None) sb.Append($"\nInstance: {InstanceRule}");
                if (SmartScale) sb.Append($"\nMob Scaling Enabled: Smart (Class-Based)");
                else if (ScaleMobs) sb.Append($"\nMob Scaling Enabled: oui (Offset: {ScaleOffset})");
                if (!string.IsNullOrEmpty(BossScaling)) sb.Append($"\nBoss Modifiers: {BossScaling}");
                if (InstanceSkin > 0) sb.Append($"\nInstance Skin: {InstanceSkin}");
                if (ClonePlayerClasses) sb.Append($"\nClone Player Classes: {ClonePlayerClasses} (Color: {CloneClassesColor})");

                if (BlockRelic)
                {
                    if (sb.Length > 0) sb.Append("\n");
                    sb.Append("Bloque le TP avec une relique : oui");
                }
                if (!string.IsNullOrEmpty(Item))
                {
                    if (sb.Length > 0) sb.Append("\n");
                    sb.Append("Item: ");
                    sb.Append(Item);
                    if (RequiredSlot > 0)
                    {
                        sb.Append(" (in slot " + ((eInventorySlot)RequiredSlot).ToString() + ")");
                        if (ConditionAmount > 0)
                            sb.Append(" (Consumes " + ConditionAmount + " condition)");
                    }
                }
                if (LevelMin > 1 || LevelMax < 50)
                {
                    if (sb.Length > 0) sb.Append("\n");
                    sb.Append("Niveaux entre ");
                    sb.Append(LevelMin);
                    sb.Append(" et ");
                    sb.Append(LevelMax);
                }
                if (HourMin >= 0 || HourMax >= 0)
                {
                    if (sb.Length > 0) sb.Append("\n");
                    sb.Append("Heures entre ");
                    sb.Append(HourMin);
                    sb.Append(" et ");
                    sb.Append(HourMax);
                }
                if (RequiredCompletedQuestID != 0)
                {
                    if (sb.Length > 0) sb.Append("\n");
                    sb.Append("Required completed quest ID: ");
                    sb.Append(RequiredCompletedQuestID);
                }
                if (RequiredQuestStepID != 0)
                {
                    if (sb.Length > 0) sb.Append("\n");
                    sb.Append("Required quest step ID: ");
                    sb.Append(RequiredQuestStepID);
                }
                if (!String.IsNullOrEmpty(ActiveEventId))
                {
                    if (sb.Length > 0) sb.Append("\n");
                    sb.Append("Required active event ID: ");
                    sb.Append(ActiveEventId);
                }
                return sb.ToString();
            }
        }
    }
}