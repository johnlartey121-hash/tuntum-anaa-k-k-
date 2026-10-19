using System;
using System.Collections.Generic;
using System.Linq;

namespace Tuntum.Core
{
    public sealed class PlayerState
    {
        public string Name;
        public int Score;
        public int Guesses;
        public int BestStreak;
        public PlayerState(string name) { Name = name; }
        public int Accuracy => Guesses == 0 ? 0 : (int)Math.Round(Score * 100.0 / Guesses);
    }

    /// <summary>What one guess did. The same record drives local games and the online server's replies.</summary>
    public readonly struct GuessResult
    {
        public readonly int Index;          // position of the card in the deck (0..51)
        public readonly int Player;         // who guessed
        public readonly CardColor Called;
        public readonly Card Card;
        public readonly bool Correct;
        public readonly int Streak;         // the guesser's streak after this card (0 when wrong)
        public readonly int StreakBefore;
        public readonly int Milestone;      // 0 = none, 1 = 3 in a row, 2 = 5, 3 = 7, 4 = 10, 5 = every 5 after
        public readonly int NextTurn;
        public readonly bool GameOver;

        public GuessResult(int index, int player, CardColor called, Card card, bool correct, int streak, int streakBefore, int milestone, int nextTurn, bool over)
        {
            Index = index; Player = player; Called = called; Card = card; Correct = correct;
            Streak = streak; StreakBefore = streakBefore; Milestone = milestone; NextTurn = nextTurn; GameOver = over;
        }

        public bool TurnPassed => !Correct && !GameOver;
    }

    /// <summary>
    /// Rules of Tuntum anaa Kɔkɔɔ: call the colour of the top card. Right = +1 and go again.
    /// Wrong = your turn ends. When all 52 cards are gone, the most points wins.
    /// A match either owns its deck (local play, server) or is fed results by the server (online client).
    /// </summary>
    public sealed class Match
    {
        public const int DeckSize = 52;

        public readonly PlayerState[] Players;
        public int Turn { get; private set; }
        public int Index { get; private set; }
        public int Streak { get; private set; }
        public int Turns { get; private set; } = 1;
        public readonly List<CardColor> Drawn = new List<CardColor>();
        public Card? LastMiss { get; private set; }

        readonly List<Card> deck; // null on an online client

        public Match(string a, string b, List<Card> deck)
        {
            if (deck != null && deck.Count != DeckSize) throw new ArgumentException("A deck has 52 cards.", nameof(deck));
            Players = new[] { new PlayerState(a), new PlayerState(b) };
            this.deck = deck;
        }

        public static Match Local(string a, string b, Random rng) => new Match(a, b, Card.Shuffled(rng));
        public static Match Remote(string a, string b) => new Match(a, b, null);

        public bool OwnsDeck => deck != null;
        public int CardsLeft => DeckSize - Index;
        public bool IsOver => Index >= DeckSize;
        public int RedsDrawn => Drawn.Count(c => c == CardColor.Red);
        public int BlacksDrawn => Drawn.Count - RedsDrawn;

        /// <summary>Winner index, or -1 for a draw. Only meaningful when the match is over.</summary>
        public int Winner => Players[0].Score == Players[1].Score ? -1 : Players[0].Score > Players[1].Score ? 0 : 1;

        public static int MilestoneFor(int streak)
        {
            switch (streak)
            {
                case 3: return 1;
                case 5: return 2;
                case 7: return 3;
                case 10: return 4;
                default: return streak > 10 && streak % 5 == 0 ? 5 : 0;
            }
        }

        /// <summary>Resolve a guess using this match's own deck.</summary>
        public GuessResult Guess(CardColor called)
        {
            if (deck == null) throw new InvalidOperationException("This match has no deck; apply results from the server instead.");
            if (IsOver) throw new InvalidOperationException("The deck is finished.");
            var card = deck[Index];
            return Apply(Index, called, card);
        }

        /// <summary>Apply a result decided elsewhere (the online server). The card decides right or wrong.</summary>
        public GuessResult Apply(int index, CardColor called, Card card)
        {
            if (index != Index) throw new InvalidOperationException($"Expected card {Index}, got {index}.");
            if (IsOver) throw new InvalidOperationException("The deck is finished.");
            var p = Players[Turn];
            int player = Turn, before = Streak;
            bool ok = card.Color == called;
            p.Guesses++;
            Drawn.Add(card.Color);
            Index++;
            int milestone = 0;
            if (ok)
            {
                p.Score++;
                Streak++;
                p.BestStreak = Math.Max(p.BestStreak, Streak);
                milestone = MilestoneFor(Streak);
            }
            else
            {
                Streak = 0;
                LastMiss = card;
                if (!IsOver) { Turn = 1 - Turn; Turns++; }
            }
            return new GuessResult(index, player, called, card, ok, Streak, before, milestone, Turn, IsOver);
        }

        /// <summary>Peek is only for the computer player's "card counting" and tests; never shown to players.</summary>
        public IReadOnlyList<Card> Remaining => deck == null ? Array.Empty<Card>() : (IReadOnlyList<Card>)deck.GetRange(Index, CardsLeft);
    }
}
