using DOL.GS;
using DOL.GS.Geometry;
using System.Collections.Generic;
using static AmteScripts.Managers.PvpManager;

namespace AmteScripts.PvP.Modes
{
    public sealed class TreasureHuntMode : PvPGameModeBase
    {
        public override eSessionTypes Type => eSessionTypes.TreasureHunt;
        protected override void OnOpen() { }
        protected override void OnClose() { }
        public override List<GameStaticItem> CreateOutpostItems(Position center, GamePlayer owner) => PvPAreaOutposts.CreateTreasureHuntBase(center, owner);
    }
}