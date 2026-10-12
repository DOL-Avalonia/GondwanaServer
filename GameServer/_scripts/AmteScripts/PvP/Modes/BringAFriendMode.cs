using AmteScripts.Areas;
using DOL.Events;
using DOL.GS;
using DOL.GS.Geometry;
using DOL.GS.PacketHandler;
using DOL.GS.Scripts;
using DOL.Language;
using System;
using System.Collections.Generic;
using static AmteScripts.Managers.PvpManager;

namespace AmteScripts.PvP.Modes
{
    public sealed class BringAFriendMode : PvPGameModeBase
    {
        public override eSessionTypes Type => eSessionTypes.BringAFriend;
        public override bool OutpostIsSafeArea => true;
        private readonly DOLEventHandler _onDying, _onBringAFriend;
        public BringAFriendMode() { _onDying = OnDying; _onBringAFriend = OnBringAFriend; }

        protected override void OnOpen()
        {
            GameEventMgr.AddHandler(GameLivingEvent.Dying, _onDying);
            GameEventMgr.AddHandler(GameLivingEvent.BringAFriend, _onBringAFriend);
        }

        protected override void OnClose()
        {
            GameEventMgr.RemoveHandler(GameLivingEvent.Dying, _onDying);
            GameEventMgr.RemoveHandler(GameLivingEvent.BringAFriend, _onBringAFriend);
            foreach (GameNPC npc in Ctx.FindNpcs(static n => n is FollowingFriendMob)) ((FollowingFriendMob)npc).ResetFollow();
        }

        public override List<GameStaticItem> CreateOutpostItems(Position center, GamePlayer owner) => PvPAreaOutposts.CreateGuildOutpostTemplate01(center, owner);

        private void OnBringAFriend(DOLEvent e, object sender, EventArgs args)
        {
            if (sender is not GamePlayer player || args is not BringAFriendArgs { Entered: true, FinalStage: true, Friend: FollowingFriendMob mob }) return;
            ModuleService.Run(static s => s.mode.AddFriendToSafeArea(s.player, s.mob), (mode: this, player, mob));
        }

        private void OnDying(DOLEvent e, object sender, EventArgs args)
        {
            if (sender is not FollowingFriendMob mob) return;
            GamePlayer killer = (args as DyingEventArgs)?.Killer as GamePlayer;
            ModuleService.Run(static s => s.mode.HandleFriendDied(s.mob, s.killer), (mode: this, mob, killer));
        }

        private void AddFriendToSafeArea(GamePlayer owner, FollowingFriendMob mob)
        {
            if (!IsActive)
                return;

            float speed = mob.MaxSpeed <= 0 ? 1 : mob.MaxSpeed;
            float x = 200f / speed, y = mob.AggroMultiplier;
            int basePoints = (int)Math.Round(y <= 1.0f ? 10.0 * x : 10.0 * x * y), bonus = 0;
            if (!string.IsNullOrEmpty(mob.GuildName) && PvpService.Instance.FindSafeAreaForTarget(owner) is PvpTempArea area)
            {
                int newCount = area.GetFamilyCount(mob.GuildName) + 1;
                area.SetFamilyCount(mob.GuildName, newCount);
                int total = Ctx.FindNpcs(n => n.CurrentZone == mob.CurrentZone && n is FollowingFriendMob f && f.GuildName == mob.GuildName).Count;
                if (newCount >= total) bonus = total < 2 ? 0 : total == 2 ? 2 : total == 3 ? 6 : total == 4 ? 10 : total <= 6 ? 15 : 20;
            }
            Ctx.Scoreboard.Award(owner, s => { s.Friends_BroughtFriendsPoints += basePoints; s.Friends_BroughtFamilyBonus += bonus; });
            owner.Out.SendMessage(LanguageMgr.GetTranslation(owner.Client.Account.Language, "PvPManager.BroughtToSafety", mob.Name, basePoints), eChatType.CT_SpellExpires, eChatLoc.CL_SystemWindow);
        }

        private void HandleFriendDied(FollowingFriendMob mob, GamePlayer killer)
        {
            if (!IsActive) return;
            GamePlayer followed = mob.PlayerFollow;
            bool friendly = followed == null || killer == followed || (followed.Guild != null && killer != null && followed.Guild == killer.Guild);
            if (friendly || killer == null)
                return;

            if (Ctx.IsSessionZone(followed!.CurrentZone))
                Ctx.Scoreboard.Award(killer, static s => { s.Friends_FriendKilledCount++; s.Friends_FriendKilledPoints += 2; });

            if (Ctx.IsSessionZone(killer.CurrentZone))
                Ctx.Scoreboard.Award(killer, static s => { s.Friends_KillEnemyFriendCount++; s.Friends_KillEnemyFriendPoints += 2; });
        }
    }
}