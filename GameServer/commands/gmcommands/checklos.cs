using System.Collections.Generic;
using System.Diagnostics;
using DOL.GS.Geometry;
using DOL.Language;

namespace DOL.GS.Commands
{
    /// <summary>
    /// GM tool for the line-of-sight system.
    ///   /checklos              probe the selected target: client cache, server geometry, live LosCheckHandler answer
    ///   /checklos              (no target, ground target set) server geometry toward the ground target
    ///   /checklos stats        LosCheckHandler statistics (packets, cache hits, dedup, timeouts) and current settings
    ///   /checklos resetstats   reset those statistics
    /// Replaces the old async /checklos and OpenDAoC's /loscheck.
    /// </summary>
    [CmdAttribute(
        "&checklos",
        ePrivLevel.GM,
        "Commands.GM.Checklos.Description",
        "Commands.GM.Checklos.Usage")]
    public class CheckLosCommandHandler : AbstractCommandHandler, ICommandHandler
    {
        public void OnCommand(GameClient client, string[] args)
        {
            GamePlayer player = client?.Player;
            if (player == null)
                return;

            string lang = client!.Account.Language;

            if (args.Length > 1)
            {
                switch (args[1].ToLowerInvariant())
                {
                    case "stats":
                        ShowStatistics(client);
                        return;

                    case "resetstats":
                        LosCheckHandler.ResetStatistics();
                        DisplayMessage(client, LanguageMgr.GetTranslation(lang, "Commands.GM.Checklos.Stats.Reset"));
                        return;
                }
            }

            GameObject target = player.TargetObject;
            if (target != null)
            {
                new LosProbe(client, target).Run();
                return;
            }

            if (player.GroundTargetPosition != Position.Nowhere)
            {
                CheckGroundTarget(client);
                return;
            }

            DisplayMessage(client, LanguageMgr.GetTranslation(lang, "Commands.GM.Checklos.NeedTarget"));
        }

        private static void ShowStatistics(GameClient client)
        {
            GamePlayer player = client.Player;
            string lang = client.Account.Language;
            List<string> text = new List<string>();

            // Get translated statistics from the modified handler
            text.AddRange(LosCheckHandler.GetStatisticsReport(lang));
            text.Add("");
            text.Add(LanguageMgr.GetTranslation(lang, "Commands.GM.Checklos.Stats.Config", LosCheckHandler.Timeout, LosCheckHandler.CacheLifetime));
            text.Add(LanguageMgr.GetTranslation(lang, "Commands.GM.Checklos.Stats.ServerGeom", ServerProperties.Properties.LOS_MGR_ENABLE, ServerProperties.Properties.LOS_CHECK_USE_SERVER_GEOMETRY));
            text.Add(LanguageMgr.GetTranslation(lang, "Commands.GM.Checklos.Stats.Region", player.CurrentRegionID, (player.CurrentRegion != null && LosCheckMgr.HasDataFor(player.CurrentRegion))));
            text.Add("");
            text.Add(LanguageMgr.GetTranslation(lang, "Commands.GM.Checklos.Stats.Pending", player.LosCheckHandler?.ActiveCheckCount ?? 0));

            client.Out.SendCustomTextWindow(LanguageMgr.GetTranslation(lang, "Commands.GM.Checklos.WindowTitle.Stats"), text);
        }

        private static void CheckGroundTarget(GameClient client)
        {
            GamePlayer player = client.Player;
            string lang = client.Account.Language;
            Position ground = player.GroundTargetPosition;

            List<string> text = new List<string>
            {
                LanguageMgr.GetTranslation(lang, "Commands.GM.Checklos.Header.GroundTarget"),
                LanguageMgr.GetTranslation(lang, "Commands.GM.Checklos.Ground.Position", ground),
                LanguageMgr.GetTranslation(lang, "Commands.GM.Checklos.Ground.InView", player.GroundTargetInView),
                "",
                LanguageMgr.GetTranslation(lang, "Commands.GM.Checklos.Header.ServerGeom")
            };

            Region region = player.CurrentRegion;
            if (region != null && LosCheckMgr.HasDataFor(region))
            {
                RaycastStats stats = new RaycastStats();
                Stopwatch sw = Stopwatch.StartNew();
                float collision = LosCheckMgr.GetCollisionDistance(region, player.Coordinate, ground.Coordinate, ref stats);
                sw.Stop();
                DescribeGeometryResult(lang, collision, stats, sw, text);
            }
            else
            {
                text.Add(LanguageMgr.GetTranslation(lang, "Commands.GM.Checklos.Geom.NoData"));
            }

            text.Add("");
            text.Add(LanguageMgr.GetTranslation(lang, "Commands.GM.Checklos.Header.ClientCheck"));
            text.Add(LanguageMgr.GetTranslation(lang, "Commands.GM.Checklos.Client.Unsupported"));

            client.Out.SendCustomTextWindow(LanguageMgr.GetTranslation(lang, "Commands.GM.Checklos.WindowTitle.Main"), text);
        }

        private static void DescribeGeometryResult(string lang, float collision, RaycastStats stats, Stopwatch sw, List<string> text)
        {
            string verdict = float.IsPositiveInfinity(collision)
                ? LanguageMgr.GetTranslation(lang, "Commands.GM.Checklos.Geom.Clear")
                : LanguageMgr.GetTranslation(lang, "Commands.GM.Checklos.Geom.Blocked", collision);

            text.Add(LanguageMgr.GetTranslation(lang, "Commands.GM.Checklos.Geom.Verdict", verdict));
            text.Add(LanguageMgr.GetTranslation(lang, "Commands.GM.Checklos.Geom.Stats", stats.nbNodeTests, stats.nbFaceTests, sw.Elapsed.TotalMilliseconds));
        }

        private sealed class LosProbe : ILosCheckListener
        {
            private readonly GameClient _client;
            private readonly GameObject _target;
            private readonly string _lang;
            private readonly List<string> _text = new List<string>();
            private readonly Stopwatch _stopwatch = new Stopwatch();
            private int _checksBefore;

            public LosProbe(GameClient client, GameObject target)
            {
                _client = client;
                _target = target;
                _lang = client.Account.Language;
            }

            public void Run()
            {
                GamePlayer player = _client.Player;

                _text.Add(LanguageMgr.GetTranslation(_lang, "Commands.GM.Checklos.Header.TargetInfo"));
                _text.Add(LanguageMgr.GetTranslation(_lang, "Commands.GM.Checklos.Probe.TargetStr", _target.Name, _target.ObjectID, (int)_target.Coordinate.DistanceTo(player.Position)));
                _text.Add(LanguageMgr.GetTranslation(_lang, "Commands.GM.Checklos.Probe.InView", player.TargetInView));
                _text.Add("");
                _text.Add(LanguageMgr.GetTranslation(_lang, "Commands.GM.Checklos.Header.ServerGeom"));

                Region region = player.CurrentRegion;
                if (region != null && LosCheckMgr.HasDataFor(region))
                {
                    RaycastStats stats = new RaycastStats();
                    Stopwatch sw = Stopwatch.StartNew();
                    float collision = LosCheckMgr.GetCollisionDistance(player, _target, ref stats);
                    sw.Stop();
                    DescribeGeometryResult(_lang, collision, stats, sw, _text);
                }
                else
                {
                    _text.Add(LanguageMgr.GetTranslation(_lang, "Commands.GM.Checklos.Geom.NoData"));
                }

                _text.Add("");
                _checksBefore = player.LosCheckHandler?.ActiveCheckCount ?? 0;

                _stopwatch.Start();
                if (!player.Out.SendLosCheckRequest(player, _target, this))
                {
                    _stopwatch.Stop();
                    _text.Add(LanguageMgr.GetTranslation(_lang, "Commands.GM.Checklos.Header.LosHandler"));
                    _text.Add(LanguageMgr.GetTranslation(_lang, "Commands.GM.Checklos.Probe.Failed"));
                    Show();
                }
            }

            public void HandleLosCheckResponse(GamePlayer losChecker, LosCheckResponse response, ushort sourceId, ushort targetId)
            {
                _stopwatch.Stop();
                long ms = _stopwatch.ElapsedMilliseconds;

                string origin = ms < 1
                    ? LanguageMgr.GetTranslation(_lang, "Commands.GM.Checklos.Probe.OriginInst")
                    : LanguageMgr.GetTranslation(_lang, "Commands.GM.Checklos.Probe.OriginNet");

                _text.Add(LanguageMgr.GetTranslation(_lang, "Commands.GM.Checklos.Header.LosHandler"));
                _text.Add(LanguageMgr.GetTranslation(_lang, "Commands.GM.Checklos.Probe.Answer", response.ToString()));
                _text.Add(LanguageMgr.GetTranslation(_lang, "Commands.GM.Checklos.Probe.Time", ms, origin));
                _text.Add(LanguageMgr.GetTranslation(_lang, "Commands.GM.Checklos.Probe.Route", sourceId, targetId));
                _text.Add("");
                _text.Add(LanguageMgr.GetTranslation(_lang, "Commands.GM.Checklos.Header.Cache"));
                _text.Add(LanguageMgr.GetTranslation(_lang, "Commands.GM.Checklos.Probe.CacheStats", _checksBefore, (_client.Player?.LosCheckHandler?.ActiveCheckCount ?? 0)));

                Show();
            }

            private void Show()
            {
                if (_client.Player == null || _client.ClientState != GameClient.eClientState.Playing)
                    return;

                _client.Out.SendCustomTextWindow(LanguageMgr.GetTranslation(_lang, "Commands.GM.Checklos.WindowTitle.Main"), _text);
            }
        }
    }
}