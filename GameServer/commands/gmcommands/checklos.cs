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

            if (args.Length > 1)
            {
                switch (args[1].ToLowerInvariant())
                {
                    case "stats":
                        ShowStatistics(client);
                        return;

                    case "resetstats":
                        LosCheckHandler.ResetStatistics();
                        DisplayMessage(client, "LoS statistics have been reset.");
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

            DisplayMessage(client, LanguageMgr.GetTranslation(client!.Account.Language, "Commands.GM.Checklos.NeedTarget"));
        }

        private static void ShowStatistics(GameClient client)
        {
            GamePlayer player = client.Player;
            List<string> text = new List<string>();

            text.AddRange(LosCheckHandler.GetStatisticsReport().Split('\n'));
            text.Add("");
            text.Add($"Timeout: {LosCheckHandler.Timeout} ms | Cache lifetime: {LosCheckHandler.CacheLifetime} ms");
            text.Add($"Server geometry loaded (LOS_MGR_ENABLE): {ServerProperties.Properties.LOS_MGR_ENABLE}"
                     + $" | used to answer checks (LOS_CHECK_USE_SERVER_GEOMETRY): {ServerProperties.Properties.LOS_CHECK_USE_SERVER_GEOMETRY}");
            text.Add($"Region {player.CurrentRegionID} has .obj geometry: {(player.CurrentRegion != null && LosCheckMgr.HasDataFor(player.CurrentRegion))}");
            text.Add($"Pending / cached checks on your client: {player.LosCheckHandler?.ActiveCheckCount ?? 0}");

            client.Out.SendCustomTextWindow("Check LOS - statistics", text);
        }

        private static void CheckGroundTarget(GameClient client)
        {
            GamePlayer player = client.Player;
            Position ground = player.GroundTargetPosition;

            List<string> text = new List<string>
            {
                $"Ground target: {ground}",
                $"Ground target in view (client cache): {player.GroundTargetInView}",
            };

            Region region = player.CurrentRegion;
            if (region != null && LosCheckMgr.HasDataFor(region))
            {
                RaycastStats stats = new RaycastStats();
                Stopwatch sw = Stopwatch.StartNew();
                float collision = LosCheckMgr.GetCollisionDistance(region, player.Coordinate, ground.Coordinate, ref stats);
                sw.Stop();
                text.Add(DescribeGeometryResult(collision, stats, sw));
            }
            else
            {
                text.Add("Server geometry: no .obj data loaded for this region.");
            }

            text.Add("Client check: the client can only test line of sight between two objects, not toward a ground position.");
            client.Out.SendCustomTextWindow("Check LOS", text);
        }

        private static string DescribeGeometryResult(float collision, RaycastStats stats, Stopwatch sw)
        {
            string verdict = float.IsPositiveInfinity(collision)
                ? "clear line of sight (note: distances < 16 or > visibility range are not tested by the geometry)"
                : $"BLOCKED at {collision:0} units";

            return $"Server geometry: {verdict} - {stats.nbNodeTests} node tests, {stats.nbFaceTests} face tests, {sw.Elapsed.TotalMilliseconds:0.00} ms";
        }

        /// <summary>
        /// One probe per command. It collects the synchronous information, then waits for the LosCheckHandler answer.
        /// Cached / geometry-resolved answers arrive synchronously (before SendLosCheckRequest returns), real client
        /// answers arrive later on the game loop; both paths end in Show().
        /// </summary>
        private sealed class LosProbe : ILosCheckListener
        {
            private readonly GameClient _client;
            private readonly GameObject _target;
            private readonly List<string> _text = new List<string>();
            private readonly Stopwatch _stopwatch = new Stopwatch();
            private int _checksBefore;

            public LosProbe(GameClient client, GameObject target)
            {
                _client = client;
                _target = target;
            }

            public void Run()
            {
                GamePlayer player = _client.Player;

                _text.Add($"Target: {_target.Name} (OID {_target.ObjectID}, distance {(int)_target.Coordinate.DistanceTo(player.Position)})");
                _text.Add($"Target in view (client cache): {player.TargetInView}");

                Region region = player.CurrentRegion;
                if (region != null && LosCheckMgr.HasDataFor(region))
                {
                    RaycastStats stats = new RaycastStats();
                    Stopwatch sw = Stopwatch.StartNew();
                    float collision = LosCheckMgr.GetCollisionDistance(player, _target, ref stats);
                    sw.Stop();
                    _text.Add(DescribeGeometryResult(collision, stats, sw));
                }
                else
                {
                    _text.Add("Server geometry: no .obj data loaded for this region.");
                }

                _checksBefore = player.LosCheckHandler?.ActiveCheckCount ?? 0;

                _stopwatch.Start();
                if (!player.Out.SendLosCheckRequest(player, _target, this))
                {
                    _stopwatch.Stop();
                    _text.Add("LosCheckHandler: the check could not be started (target in another region, invalid object id or client not playing).");
                    Show();
                }
            }

            public void HandleLosCheckResponse(GamePlayer losChecker, LosCheckResponse response, ushort sourceId, ushort targetId)
            {
                _stopwatch.Stop();
                long ms = _stopwatch.ElapsedMilliseconds;
                string origin = ms < 1
                    ? "instant: answered from the cache, by deduplication or by the server geometry"
                    : "real client packet round-trip";

                _text.Add($"LosCheckHandler answer: {response} after {ms} ms ({origin}) [source {sourceId} -> target {targetId}]");
                _text.Add($"Pending / cached checks on your client: {_checksBefore} before, {(_client.Player?.LosCheckHandler?.ActiveCheckCount ?? 0)} now");
                Show();
            }

            private void Show()
            {
                if (_client.Player == null || _client.ClientState != GameClient.eClientState.Playing)
                    return;

                _client.Out.SendCustomTextWindow("Check LOS", _text);
            }
        }
    }
}