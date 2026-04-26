#nullable enable

using AmteScripts.PvP;
using DOL.AI.Brain;
using DOL.GS;
using NUnit.Framework;
using NUnit.Framework.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;

namespace DOL.UnitTests.Gameserver
{
    [TestFixture]
    class UT_Predator : AbstractPredatorManager
    {
        private Guild fakeGuild1 = new Guild(new() { GuildName = "Les Tuches", ObjectId = Guid.NewGuid().ToString() });

        private Guild fakeGuild2 = new Guild(new() { GuildName = "Les ch'tis", ObjectId = Guid.NewGuid().ToString() });
        
        [Test]
        public void AssignPairsSingle()
        {
            var player = new PvPPlayerEntity(new FakePlayer("Jean"));
            var pairs = AssignPairs([player], null);
            
            Assert.AreEqual(1, pairs.Count);
            Assert.AreEqual(player, pairs.First().Predator);
            Assert.AreEqual(null, pairs.First().Prey);
        }

        [Test]
        public void AssignPairsTwo()
        {
            var player0 = new PvPPlayerEntity(new FakePlayer("Jean"));
            var player1 = new PvPPlayerEntity(new FakePlayer("François"));
            var pairs = AssignPairs([player0, player1], null);
            
            Assert.AreEqual(2, pairs.Count);
            Assert.AreEqual(pairs.Count, pairs.Select(p => p.Predator).Distinct().Count());
            Assert.AreEqual(pairs.Count, pairs.Select(p => p.Prey).Distinct().Count());
            Assert.That(pairs[0].Predator == player0);
            Assert.That(pairs[1].Predator == player1);
            Assert.That(pairs[0].Prey == player1);
            // Assert.That(pairs[1].Prey == null);
        }

        [Test]
        public void AssignPairsTwoWithNext()
        {
            var player0 = new PvPPlayerEntity(new FakePlayer("Jean"));
            var player1 = new PvPPlayerEntity(new FakePlayer("François"));
            var player2 = new PvPPlayerEntity(new FakePlayer("Marie-Françoise"));
            var pairs = AssignPairs([player0, player1], player2);

            Assert.AreEqual(2, pairs.Count);
            Assert.AreEqual(pairs.Count, pairs.Select(p => p.Predator).Distinct().Count());
            Assert.AreEqual(pairs.Count, pairs.Select(p => p.Prey).Distinct().Count());
            Assert.That(pairs[0].Predator == player0);
            Assert.That(pairs[1].Predator == player1);
            Assert.That(pairs[0].Prey == player1);
            Assert.That(pairs[1].Prey == player2);
        }

        [Test]
        public void AssignPairsLeftover()
        {
            var player0 = new PvPPlayerEntity(new FakePlayer("Jean"));
            var player1 = new PvPPlayerEntity(new FakePlayer("Valjean"));
            player0.AsPlayer!.SetGuild(fakeGuild1, false);
            player1.AsPlayer!.SetGuild(fakeGuild1, false);
            var pairs = AssignPairs([player0, player1], null);

            Assert.AreEqual(2, pairs.Count);
            Assert.AreEqual(null, pairs[0].Prey);
            Assert.AreEqual(null, pairs[1].Prey);
        }

        [Test]
        public void AssignPairsGuilds()
        {
            var player0 = new PvPPlayerEntity(new FakePlayer("Jean"));
            var player1 = new PvPPlayerEntity(new FakePlayer("François"));
            var player2 = new PvPPlayerEntity(new FakePlayer("Jean-François"));
            var player3 = new PvPPlayerEntity(new FakePlayer("Jean-Marie"));
            Assert.That(player0.AsPlayer != null);

            player0.AsPlayer!.SetGuild(fakeGuild1, false);
            player1.AsPlayer!.SetGuild(fakeGuild1, false);
            player2.AsPlayer!.SetGuild(fakeGuild2, false);
            player3.AsPlayer!.SetGuild(fakeGuild2, false);
            Assert.That(player0.AssociatedGuild == fakeGuild1);
            PvPPlayerEntity[] players = [player0, player1, player2, player3];
            var pairs = AssignPairs(players, null);

            Assert.AreEqual(4, pairs.Count);
            Assert.AreEqual(pairs.Count, pairs.Select(p => p.Predator).Distinct().Count());
            Assert.AreEqual(pairs.Count, pairs.Select(p => p.Prey).Distinct().Count());
            Assert.AreEqual(player0, pairs[0].Predator);
            Assert.AreNotEqual(null, pairs[0].Prey);
            Assert.AreNotEqual(pairs[0].Predator.AssociatedGuild, pairs[0].Prey!.AssociatedGuild);

            var jeanFrancois = pairs.Find(p => p.Predator == player2);
            Assert.AreNotEqual(null, jeanFrancois);
            Assert.AreNotEqual(null, jeanFrancois!.Prey);
            Assert.AreNotEqual(jeanFrancois.Prey!.AssociatedGuild, jeanFrancois.Predator.AssociatedGuild);

            // Testing that if we force `next` to be our previous last, then the guild order should be reversed
            var previousLast= pairs[3];
            var player4 = new PvPPlayerEntity(new FakePlayer("Jean-Marie-François"));
            player4.AsPlayer!.SetGuild(previousLast.Predator.AssociatedGuild!, false);
            pairs = AssignPairs(players, player4);
            Assert.AreEqual(pairs.Count, pairs.Select(p => p.Predator).Distinct().Count());
            Assert.AreEqual(pairs.Count, pairs.Select(p => p.Prey).Distinct().Count());
            Assert.AreEqual(player4, pairs[3].Prey);
            Assert.AreNotEqual(pairs[3].Predator.AssociatedGuild, pairs[3].Prey!.AssociatedGuild);
        }

        [Test]
        public void AssignPairsOrder()
        {
            var player0 = new PvPPlayerEntity(new FakePlayer("Jean"));
            var player1 = new PvPPlayerEntity(new FakePlayer("Antoinette"));
            var player2 = new PvPPlayerEntity(new FakePlayer("François"));
            var player3 = new PvPPlayerEntity(new FakePlayer("Marie-Antoinette"));
            var player4 = new PvPPlayerEntity(new FakePlayer("Jean-François"));
            var player5 = new PvPPlayerEntity(new FakePlayer("Laure-Antoinette"));
            var player6 = new PvPPlayerEntity(new FakePlayer("Jean-Marie"));
            PvPPlayerEntity[] players = [player0, player1, player2, player3, player4, player5, player6];

            player0.AsPlayer!.SetGuild(fakeGuild1, false);
            player2.AsPlayer!.SetGuild(fakeGuild1, false);
            player4.AsPlayer!.SetGuild(fakeGuild2, false);
            player6.AsPlayer!.SetGuild(fakeGuild2, false);
            var pairs = AssignPairs(players, null);
            Assert.AreEqual(7, pairs.Count);
            Assert.AreEqual(pairs.Count, pairs.Select(p => p.Predator).Distinct().Count());
            Assert.AreEqual(pairs.Count, pairs.Select(p => p.Prey).Distinct().Count());

            // Testing that each solo player comes before the next solo player
            for (int i = 0; i < players.Length; ++i)
            {
                if (players[i].AssociatedGuild != null)
                    continue;

                var nextSoloPlayer = players.Skip(i + 1).FirstOrDefault(e => e.AssociatedGuild == null);
                var nextSoloPredator = pairs.SkipWhile(e => e.Predator != players[i]).Skip(1).FirstOrDefault(p => p.Predator.AssociatedGuild == null);
                Assert.AreEqual(nextSoloPlayer, nextSoloPredator?.Predator);
            }

            // First guild to appear should be guild 1
            Assert.AreEqual(fakeGuild1, pairs.Select(p => p.Predator.AssociatedGuild).FirstOrDefault(g => g != null));

            // First solo player should be antoinette
            Assert.AreEqual(player1, pairs.FirstOrDefault(p => p.Predator.AssociatedGuild == null)?.Predator);
        }

        [Test]
        public void RecombobulateSingle()
        {
            var player = new PvPPlayerEntity(new FakePlayer("Jean"));
            var allPlayers = new List<PvPEntity>([player]);
            var predators = new List<PredatorPair>(allPlayers.Select(p => new PredatorPair(p)));
            var recombobulated = Recombobulate(predators, allPlayers);
            
            Assert.AreEqual(0, recombobulated.Count);
            Assert.AreEqual(1, predators.Count);
            Assert.AreEqual(player, predators.First().Predator);
            Assert.AreEqual(null, predators.First().Prey);
        }

        [Test]
        public void RecombobulateTwo()
        {
            var player0 = new PvPPlayerEntity(new FakePlayer("Jean"));
            var player1 = new PvPPlayerEntity(new FakePlayer("François"));
            var allPlayers = new List<PvPEntity>([player0, player1]);
            var predators = new List<PredatorPair>(allPlayers.Select(p => new PredatorPair(p)));
            var recombobulated = Recombobulate(predators, allPlayers);
            
            Assert.AreEqual(2, recombobulated.Count);
            Assert.AreEqual(recombobulated.Count, recombobulated.Select(p => p.Predator).Distinct().Count());
            Assert.AreEqual(recombobulated.Count, recombobulated.Select(p => p.Prey).Distinct().Count());
            Assert.AreEqual(player0, recombobulated[0].Predator);
            Assert.AreEqual(player1, recombobulated[1].Predator);
            Assert.AreEqual(player1, recombobulated[0].Prey);
            Assert.AreEqual(player0, recombobulated[1].Prey);
            Assert.AreEqual(recombobulated, predators);
        }

        [Test]
        public void RecombobulateThree()
        {
            var player0 = new PvPPlayerEntity(new FakePlayer("Jean"));
            var player1 = new PvPPlayerEntity(new FakePlayer("François"));
            var player2 = new PvPPlayerEntity(new FakePlayer("Marie-Françoise"));
            var predatorPlayers = new List<PvPEntity>([player0, player1, player2]);
            var predators = new List<PredatorPair>(predatorPlayers.Select(p => new PredatorPair(p)));
            var recombobulated = Recombobulate(predators, predatorPlayers);
            
            Assert.AreEqual(3, recombobulated.Count);
            Assert.AreEqual(recombobulated.Count, recombobulated.Select(p => p.Predator).Distinct().Count());
            Assert.AreEqual(recombobulated.Count, recombobulated.Select(p => p.Prey).Distinct().Count());
            Assert.AreEqual(player0, recombobulated[0].Predator);
            Assert.AreEqual(player1, recombobulated[0].Prey);
            Assert.AreEqual(player1, recombobulated[1].Predator);
            Assert.AreEqual(player2, recombobulated[1].Prey);
            Assert.AreEqual(player2, recombobulated[2].Predator);
            Assert.AreEqual(player0, recombobulated[2].Prey);
        }

        [Test]
        public void RecombobulateTwoWithExtraPrey()
        {
            var player0 = new PvPPlayerEntity(new FakePlayer("Jean"));
            var player1 = new PvPPlayerEntity(new FakePlayer("François"));
            var player2 = new PvPPlayerEntity(new FakePlayer("Marie-Françoise"));
            var predatorPlayers = new List<PvPEntity>([player0, player1]);
            var predators = new List<PredatorPair>(predatorPlayers.Select(p => new PredatorPair(p)));
            var recombobulated = Recombobulate(predators, [player2, player0, player1]);

            Assert.AreEqual(2, recombobulated.Count);
            Assert.AreEqual(recombobulated.Count, recombobulated.Select(p => p.Predator).Distinct().Count());
            Assert.AreEqual(recombobulated.Count, recombobulated.Select(p => p.Prey).Distinct().Count());
            Assert.AreEqual(player0, recombobulated[0].Predator);
            Assert.AreEqual(player1, recombobulated[1].Predator);
            //Assert.AreEqual(player2, recombobulated[0].Prey);
            //Assert.AreEqual(player0, recombobulated[1].Prey);
        }

        [Test]
        public void RecombobulateLeftover()
        {
            var player0 = new PvPPlayerEntity(new FakePlayer("Jean"));
            var player1 = new PvPPlayerEntity(new FakePlayer("Valjean"));
            player0.AsPlayer!.SetGuild(fakeGuild1, false);
            player1.AsPlayer!.SetGuild(fakeGuild1, false);
            var predatorPlayers = new List<PvPEntity>([player0, player1]);
            var predators = new List<PredatorPair>(predatorPlayers.Select(p => new PredatorPair(p)));
            var recombobulated = Recombobulate(predators, predatorPlayers);
            
            Assert.AreEqual(0, recombobulated.Count);
            Assert.AreEqual(null, predators[0].Prey);
            Assert.AreEqual(null, predators[1].Prey);
        }

        [Test]
        public void RecombobulateGuilds()
        {
            var player0 = new PvPPlayerEntity(new FakePlayer("Jean"));
            var player1 = new PvPPlayerEntity(new FakePlayer("François"));
            var player2 = new PvPPlayerEntity(new FakePlayer("Jean-François"));
            var player3 = new PvPPlayerEntity(new FakePlayer("Jean-Marie"));
            Assert.That(player0.AsPlayer != null);

            player0.AsPlayer!.SetGuild(fakeGuild1, false);
            player1.AsPlayer!.SetGuild(fakeGuild1, false);
            player2.AsPlayer!.SetGuild(fakeGuild2, false);
            player3.AsPlayer!.SetGuild(fakeGuild2, false);
            Assert.That(player0.AssociatedGuild == fakeGuild1);
            var predatorPlayers = new List<PvPEntity>([player0, player1, player2, player3]);
            var predators = new List<PredatorPair>(predatorPlayers.Select(p => new PredatorPair(p)));
            var recombobulated = Recombobulate(predators, predatorPlayers);
             
            Assert.AreEqual(predators.Count, recombobulated.Count);
            Assert.AreEqual(predators.Count, predators.Select(p => p.Predator).Distinct().Count());
            Assert.AreEqual(predators.Count, predators.Select(p => p.Prey).Distinct().Count());
            Assert.AreEqual(player0, predators[0].Predator);
            Assert.AreNotEqual(null, predators[0].Prey);
            Assert.AreNotEqual(predators[0].Predator.AssociatedGuild, predators[0].Prey!.AssociatedGuild);

            var jeanFrancois = predators.Find(p => p.Predator == player2);
            Assert.AreNotEqual(null, jeanFrancois);
            Assert.AreNotEqual(null, jeanFrancois!.Prey);
            Assert.AreNotEqual(jeanFrancois.Prey!.AssociatedGuild, jeanFrancois.Predator.AssociatedGuild);
            
            var player4 = new PvPPlayerEntity(new FakePlayer("Jean-Marie-François"));
            player4.AsPlayer!.SetGuild(fakeGuild1, false);
            predators = new List<PredatorPair>(predatorPlayers.Select(p => new PredatorPair(p)));
            recombobulated = Recombobulate(predators, predatorPlayers.Prepend(player4));
            Assert.AreEqual(predators.Count, recombobulated.Count);
            Assert.AreEqual(recombobulated.Count, recombobulated.Select(p => p.Predator).Distinct().Count());
            Assert.AreEqual(recombobulated.Count, recombobulated.Select(p => p.Prey).Distinct().Count());
            Assert.AreNotEqual(player4, recombobulated[0].Prey);
            Assert.AreEqual(player4, recombobulated.FirstOrDefault(p => p.Predator == player2)?.Prey);
            Assert.AreNotEqual(recombobulated[0].Predator.AssociatedGuild, recombobulated[0].Prey!.AssociatedGuild);
        }

        [Test]
        public void RecombobulateOrder()
        {
            var player0 = new PvPPlayerEntity(new FakePlayer("Jean"));
            var player1 = new PvPPlayerEntity(new FakePlayer("Antoinette"));
            var player2 = new PvPPlayerEntity(new FakePlayer("François"));
            var player3 = new PvPPlayerEntity(new FakePlayer("Marie-Antoinette"));
            var player4 = new PvPPlayerEntity(new FakePlayer("Jean-François"));
            var player5 = new PvPPlayerEntity(new FakePlayer("Laure-Antoinette"));
            var player6 = new PvPPlayerEntity(new FakePlayer("Jean-Marie"));
            // Jean - Francois
            // Jean-Francois - Jean-Marie
            // Jean => Jean-Francois => Antoinette => Francois => Marie-Antoinette => Jean-Marie => Laure-Antoinette => Jean

            player0.AsPlayer!.SetGuild(fakeGuild1, false);
            player2.AsPlayer!.SetGuild(fakeGuild1, false);
            player4.AsPlayer!.SetGuild(fakeGuild2, false);
            player6.AsPlayer!.SetGuild(fakeGuild2, false);
            PvPPlayerEntity[] players = [player0, player1, player2, player3, player4, player5, player6];
            var predators = new List<PredatorPair>(players.Select(p => new PredatorPair(p)));
            var recombobulated = Recombobulate(predators, players);
            Assert.AreEqual(predators.Count, recombobulated.Count);
            Assert.AreEqual(predators.Count, predators.Select(p => p.Predator).Distinct().Count());
            Assert.AreEqual(predators.Count, predators.Select(p => p.Prey).Distinct().Count());

            // Testing that each solo player comes before the next solo player
            for (int i = 0; i < players.Length; ++i)
            {
                if (players[i].AssociatedGuild != null)
                    continue;

                var nextSoloPlayer = players.Skip(i + 1).FirstOrDefault(e => e.AssociatedGuild == null);
                var nextSoloPredator = predators.SkipWhile(e => e.Predator != players[i]).Skip(1).FirstOrDefault(p => p.Predator.AssociatedGuild == null);
                Assert.AreEqual(nextSoloPlayer, nextSoloPredator?.Predator);
            }

            // First guild to appear should be guild 1
            Assert.AreEqual(fakeGuild1, predators.Select(p => p.Predator.AssociatedGuild).FirstOrDefault(g => g != null));

            // First solo player should be antoinette
            Assert.AreEqual(player1, predators.FirstOrDefault(p => p.Predator.AssociatedGuild == null)?.Predator);
        }
    }
}
