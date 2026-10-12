using static AmteScripts.Managers.PvpManager;

namespace AmteScripts.PvP.Modes
{
    public sealed class DeathmatchMode : PvPGameModeBase
    {
        public override eSessionTypes Type => eSessionTypes.Deathmatch;
        public override int KillBasePoints => 5;
        public override bool KillHasRr5Bonus => true;
        protected override void OnOpen() { }
        protected override void OnClose() { }
    }
}