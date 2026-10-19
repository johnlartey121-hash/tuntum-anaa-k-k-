using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Tuntum.Core;

namespace Tuntum.Tests
{
    public class MatchTests
    {
        static List<Card> Deck(params string[] firstCodes)
        {
            // a full deck whose first cards are the given ones, the rest in standard order
            var first = firstCodes.Select(c => { Assert.IsTrue(Card.TryParse(c, out var k), c); return k; }).ToList();
            var rest = Card.FullDeck().Where(c => !first.Contains(c));
            return first.Concat(rest).ToList();
        }

        [Test]
        public void FullDeckHas52UniqueCards_HalfRed()
        {
            var d = Card.FullDeck();
            Assert.AreEqual(52, d.Distinct().Count());
            Assert.AreEqual(26, d.Count(c => c.IsRed));
        }

        [Test]
        public void ShuffleKeepsEveryCard()
        {
            var d = Card.Shuffled(new Random(7));
            CollectionAssert.AreEquivalent(Card.FullDeck(), d);
        }

        [Test]
        public void CardCodesRoundTrip()
        {
            foreach (var c in Card.FullDeck())
            {
                Assert.IsTrue(Card.TryParse(c.Code, out var back));
                Assert.AreEqual(c, back);
            }
            Assert.IsFalse(Card.TryParse("1X", out _));
            Assert.IsFalse(Card.TryParse("", out _));
        }

        [Test]
        public void RightGuessScoresAndKeepsTurn()
        {
            var m = new Match("Ama", "Kofi", Deck("QH", "2S"));
            var r = m.Guess(CardColor.Red);
            Assert.IsTrue(r.Correct);
            Assert.AreEqual(1, m.Players[0].Score);
            Assert.AreEqual(0, m.Turn);
            Assert.AreEqual(1, m.Streak);
            Assert.IsFalse(r.TurnPassed);
        }

        [Test]
        public void WrongGuessPassesTurnAndKeepsPoints()
        {
            var m = new Match("Ama", "Kofi", Deck("QH", "2S"));
            m.Guess(CardColor.Red);
            var r = m.Guess(CardColor.Red); // 2 of spades is black
            Assert.IsFalse(r.Correct);
            Assert.IsTrue(r.TurnPassed);
            Assert.AreEqual(1, m.Turn);
            Assert.AreEqual(1, m.Players[0].Score);
            Assert.AreEqual(0, m.Streak);
            Assert.AreEqual(1, r.StreakBefore);
            Assert.AreEqual(new Card(Suit.Spades, 1), m.LastMiss);
        }

        [Test]
        public void StreakMilestonesAt3_5_7_10_15()
        {
            Assert.AreEqual(0, Match.MilestoneFor(2));
            Assert.AreEqual(1, Match.MilestoneFor(3));
            Assert.AreEqual(2, Match.MilestoneFor(5));
            Assert.AreEqual(3, Match.MilestoneFor(7));
            Assert.AreEqual(4, Match.MilestoneFor(10));
            Assert.AreEqual(0, Match.MilestoneFor(12));
            Assert.AreEqual(5, Match.MilestoneFor(15));
        }

        [Test]
        public void ThirdRightGuessReportsTheCheerMilestone()
        {
            var m = new Match("Ama", "Kofi", Deck("QH", "AD", "5H"));
            m.Guess(CardColor.Red); m.Guess(CardColor.Red);
            var r = m.Guess(CardColor.Red);
            Assert.AreEqual(3, r.Streak);
            Assert.AreEqual(1, r.Milestone);
            Assert.AreEqual(3, m.Players[0].BestStreak);
        }

        [Test]
        public void GameEndsAfter52CardsAndScoresAddUp()
        {
            var rng = new Random(42);
            var m = Match.Local("A", "B", rng);
            var cpu = new ComputerPlayer(rng);
            GuessResult last = default;
            while (!m.IsOver) last = m.Guess(cpu.Choose(m));
            Assert.IsTrue(last.GameOver);
            Assert.AreEqual(52, m.Players.Sum(p => p.Guesses));
            Assert.Throws<InvalidOperationException>(() => m.Guess(CardColor.Red));
            Assert.AreEqual(52, m.Drawn.Count);
            Assert.LessOrEqual(m.Players.Sum(p => p.Score), 52);
        }

        [Test]
        public void WrongGuessOnLastCardDoesNotPassTurn()
        {
            var deck = Card.FullDeck();
            var m = new Match("A", "B", deck);
            for (int i = 0; i < 51; i++) m.Guess(deck[i].Color);
            var r = m.Guess(deck[51].IsRed ? CardColor.Black : CardColor.Red);
            Assert.IsTrue(r.GameOver);
            Assert.IsFalse(r.TurnPassed);
        }

        [Test]
        public void RemoteMatchAppliesServerResultsInOrder()
        {
            var m = Match.Remote("A", "B");
            Assert.Throws<InvalidOperationException>(() => m.Guess(CardColor.Red));
            m.Apply(0, CardColor.Black, new Card(Suit.Clubs, 4));
            Assert.Throws<InvalidOperationException>(() => m.Apply(0, CardColor.Black, new Card(Suit.Clubs, 5)));
            Assert.AreEqual(1, m.Players[0].Score);
        }

        [Test]
        public void ComputerCallsTheColourWithMoreCardsLeft()
        {
            var m = new Match("A", "B", Deck("AH", "2H", "3H", "4S"));
            m.Guess(CardColor.Red); m.Guess(CardColor.Red); m.Guess(CardColor.Red);
            var cpu = new ComputerPlayer(new Random(1), 0);
            Assert.AreEqual(CardColor.Black, cpu.Choose(m)); // 26 black vs 23 red left
        }

        [Test]
        public void DrawWhenScoresAreEqual()
        {
            var m = new Match("A", "B", Card.FullDeck());
            Assert.AreEqual(-1, m.Winner);
        }
    }
}
