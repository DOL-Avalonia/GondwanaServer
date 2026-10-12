using AmteScripts.Managers;
using DOL.Database;
using DOL.Events;
using DOL.GameEvents;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.Language;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace Amte
{
    public class LordRvR : GameNPC
    {
        public static int CLAIM_TIME_SECONDS = 30;
        public static int CLAIM_TIME_BETWEEN_SECONDS = 120;
        public string originalGuildName;
        public DateTime lastClaim = new(1);

        private ECSGameTimer _claimTimer;
        private bool _scoring;
        private eRealm _holdingRealm;
        private long _holdingSince;
        private readonly Dictionary<eRealm, TimeSpan> _scores = new() { { eRealm.Albion, TimeSpan.Zero }, { eRealm.Midgard, TimeSpan.Zero }, { eRealm.Hibernia, TimeSpan.Zero } };

        public double timeBeforeClaim => CLAIM_TIME_BETWEEN_SECONDS - (DateTime.Now - lastClaim).TotalSeconds;

        private static eRealm RealmFromName(string n) => n switch { "Albion" => eRealm.Albion, "Midgard" => eRealm.Midgard, "Hibernia" => eRealm.Hibernia, _ => eRealm.None };

        /// Holding time accrues lazily: on guild change, on StopRvR and when someone asks for the scores.
        private void Accumulate()
        {
            long now = GameLoop.GameLoopTime;
            if (_scoring && _holdingRealm != eRealm.None) _scores[_holdingRealm] += TimeSpan.FromMilliseconds(now - _holdingSince);
            _holdingSince = now;
        }

        public override string GuildName
        {
            get => base.GuildName;
            set { Accumulate(); base.GuildName = value; _holdingRealm = RealmFromName(value); }
        }

        public override bool Interact(GamePlayer player)
        {
            if (!base.Interact(player))
                return false;

            if (RvrManager.Instance.IsOpen)
            {
                if (timeBeforeClaim > 0)
                {
                    player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.RvR.Open.UnClaimble", Math.Round(timeBeforeClaim, 1)), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                    return true;
                }
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.RvR.Open.Claimable"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
            }
            else
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.RvR.Closed"), eChatType.CT_System, eChatLoc.CL_PopupWindow);

            return true;
        }

        public override void LoadFromDatabase(DataObject obj)
        {
            base.LoadFromDatabase(obj);
            originalGuildName = GuildName;
        }

        public override bool WhisperReceive(GameLiving source, string text)
        {
            var player = source as GamePlayer;
            if (!base.WhisperReceive(source, text) || player == null)
                return false;
            if (text != "prendre le contrôle" && text != "seize control")
                return true;
            if (player.InCombat)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.RvR.In.Combat"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }
            if (timeBeforeClaim > 0)
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.RvR.Open.UnClaimble", Math.Round(timeBeforeClaim, 1)), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }
            if (_claimTimer is { IsAlive: true })
            {
                player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.RvR.Occupied"), eChatType.CT_System, eChatLoc.CL_PopupWindow);
                return true;
            }

            var startTime = DateTime.Now;
            _claimTimer = new ECSGameTimer(
                this,
                timer =>
                {
                    if (player.InCombat)
                    {
                        _claimTimer = null;
                        player.Out.SendCloseTimerWindow();
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.Interrupt"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        return 0;
                    }
                    if (player.GetDistanceTo(this) > WorldMgr.GIVE_ITEM_DISTANCE)
                    {
                        _claimTimer = null;
                        player.Out.SendCloseTimerWindow();
                        player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.RvR.Too.Far"), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
                        return 0;
                    }
                    var passedTime = DateTime.Now - startTime;
                    if (passedTime.TotalSeconds < CLAIM_TIME_SECONDS)
                        return 500;

                    player.Out.SendCloseTimerWindow();
                    TakeControl(player);
                    _claimTimer = null;
                    return 0;
                },
                500
            );

            player.Out.SendTimerWindow(LanguageMgr.GetTranslation(player.Client.Account.Language, "GameObjects.GamePlayer.RvR.Capture.Timer"), CLAIM_TIME_SECONDS);

            foreach (GamePlayer pl in GetPlayersInRadius(ushort.MaxValue - 1))
            {
                    pl.Out.SendMessage(LanguageMgr.GetTranslation(pl.Client.Account.Language, "GameObjects.GamePlayer.RvR.Capture.Start", player.GuildName), eChatType.CT_Important, eChatLoc.CL_SystemWindow);
            }

            return true;
        }

        public override void RestoreOriginalGuildName()
        {
            GuildName = originalGuildName;
        }

        public virtual void TakeControl(GamePlayer player)
        {
            lastClaim = DateTime.Now;
            GuildName = player.GuildName;
            var territory = RvrManager.Instance.GetRvRTerritory(this.CurrentRegionID);
            var fortName = string.IsNullOrEmpty(this.originalGuildName) ? (territory?.Name ?? string.Empty) : this.originalGuildName;

            foreach (GameClient client in WorldMgr.GetAllPlayingClients())
            {
                client.Out.SendMessage(LanguageMgr.GetTranslation(client.Account.Language, "GameObjects.GamePlayer.RvR.Control", player.GuildName, fortName), eChatType.CT_Help, eChatLoc.CL_SystemWindow);
            }
            player.GainGuildMeritPoints(800);
            TaskManager.UpdateTaskProgress(player, "TakeKeeps", 1);
            player.CapturedKeeps++;
            player.Out.SendSoundEffect(9202, player.Position, 0);
            NewsMgr.CreateNews("GameObjects.GamePlayer.RvR.Control", 0, eNewsType.RvRGlobal, false, true, player.GuildName, fortName);

            RvrManager.Instance.OnControlChange(this.InternalID, player.Guild);
        }

        public virtual void StartRvR()
        {
            _holdingRealm = RealmFromName(GuildName);
            _holdingSince = GameLoop.GameLoopTime;
            _scoring = true;
            lastClaim = new DateTime(1);
        }

        public virtual void StopRvR()
        {
            _scoring = false;
            _scores[eRealm.Albion] = TimeSpan.Zero; _scores[eRealm.Midgard] = TimeSpan.Zero; _scores[eRealm.Hibernia] = TimeSpan.Zero;
        }

        public virtual string GetScores(string language)
        {
            Accumulate();
            string str = LanguageMgr.GetTranslation(language, "RvRManager.HoldingTime") + "\n";
            foreach (KeyValuePair<eRealm, TimeSpan> kv in _scores)
                str += GlobalConstants.RealmToName(kv.Key) + ": " + Math.Round(kv.Value.TotalSeconds, 1) + " " + LanguageMgr.GetTranslation(language, "RvRManager.Seconds") + "\n";
            return str;
        }
    }
}