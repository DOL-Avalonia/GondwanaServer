using AmteScripts.Managers;
using DOL.GS;
using DOL.GS.PacketHandler;
using DOL.Language;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AmteScripts.PvP.Modes
{
    public sealed class BossHuntMode : PvPGameModeBase
    {
        public override PvpManager.eSessionTypes Type => PvpManager.eSessionTypes.BossHunt;
        public override int KillBasePoints => 10;
        public override bool KillHasRr5Bonus => true;

        private const int DAMAGE_THRESHOLD = 100;
        private readonly Dictionary<GameNPC, byte> _bossPins = new();
        private readonly Dictionary<(GamePlayer, GameNPC), long> _damage = new();

        protected override void OnOpen()
        {
            foreach (GameNPC boss in Ctx.FindNpcs(static n => n.IsBoss))
            {
                if (Ctx.MapPins.TryAllocate(out byte pin))
                    _bossPins[boss] = pin;
            }
        }

        protected override void OnClose()
        {
            foreach (byte pin in _bossPins.Values) Ctx.MapPins.Release(pin);
            _bossPins.Clear();
            _damage.Clear();
        }

        public override void OnPlayerLeft(GamePlayer p)
        {
            foreach (var key in _damage.Keys.Where(k => k.Item1 == p).ToList())
                _damage.Remove(key);
        }

        public override int Tick(long now)
        {
            IReadOnlyList<GamePlayer> players = Ctx.Participants.Players;

            foreach (var kvp in _bossPins.ToList())
            {
                GameNPC boss = kvp.Key;
                byte pin = kvp.Value;

                if (!boss.IsAlive || boss.ObjectState != GameObject.eObjectState.Active)
                {
                    Ctx.MapPins.Release(pin);
                    _bossPins.Remove(boss);
                    continue;
                }

                bool show = boss.InCombat || AnyParticipantNear(boss, players);
                Ctx.MapPins.Set(pin, boss.Position, show);
            }

            return 2000;
        }

        private static bool AnyParticipantNear(GameNPC boss, IReadOnlyList<GamePlayer> players)
        {
            for (int i = 0; i < players.Count; i++)
                if (players[i].IsWithinRadius(boss, WorldMgr.VISIBILITY_DISTANCE))
                    return true;
            return false;
        }

        public void OnBossHit(GamePlayer player, GameNPC boss, int damage)
        {
            if (!IsActive || player == null || boss == null) return;
            var key = (player, boss);

            _damage.TryGetValue(key, out long acc);
            acc += damage;

            if (acc < DAMAGE_THRESHOLD)
            {
                _damage[key] = acc;
                return;
            }

            long pointsEarned = acc / DAMAGE_THRESHOLD;
            _damage[key] = acc % DAMAGE_THRESHOLD;

            bool isSolo = PvpService.IsSolo(player);
            double pointValue = pointsEarned;

            if (boss.Level >= 55) pointValue *= 1.6;
            if (boss.BossDifficulty > 0)
            {
                double diff = boss.BossDifficulty;
                pointValue *= 1.0 + diff / 100.0 + Math.Pow(diff / 55.0, 2.0);
            }
            if (isSolo) pointValue *= 1.3;

            int finalPoints = (int)Math.Max(1, Math.Round(pointValue));

            Ctx.Scoreboard.Award(player, s => {
                s.Boss_BossHitsCount += (int)pointsEarned;
                s.Boss_BossHitsPoints += finalPoints;
            }, quiet: isSolo);

            player.Out.SendMessage(LanguageMgr.GetTranslation(player.Client.Account.Language, "PvPManager.BossHunt.ScoredPoints", finalPoints, boss.Name), eChatType.CT_System, eChatLoc.CL_SystemWindow);
        }
    }
}