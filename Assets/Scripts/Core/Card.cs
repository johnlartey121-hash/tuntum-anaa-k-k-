using System;
using System.Collections.Generic;

namespace Tuntum.Core
{
    public enum Suit { Spades, Clubs, Hearts, Diamonds }

    /// <summary>The two calls a player can make. Tuntum = black, Kɔkɔɔ = red.</summary>
    public enum CardColor { Black, Red }

    [Serializable]
    public struct Card : IEquatable<Card>
    {
        public static readonly string[] Ranks = { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };

        public Suit Suit;
        /// <summary>0 = Ace … 12 = King.</summary>
        public int Rank;

        public Card(Suit suit, int rank)
        {
            if (rank < 0 || rank > 12) throw new ArgumentOutOfRangeException(nameof(rank));
            Suit = suit;
            Rank = rank;
        }

        public CardColor Color => Suit == Suit.Hearts || Suit == Suit.Diamonds ? CardColor.Red : CardColor.Black;
        public bool IsRed => Color == CardColor.Red;
        public bool IsCourt => Rank >= 10;
        public string RankText => Ranks[Rank];

        public string SuitSymbol => Suit switch
        {
            Suit.Spades => "♠",
            Suit.Clubs => "♣",
            Suit.Hearts => "♥",
            _ => "♦",
        };

        /// <summary>Short code used on the wire, e.g. "QH", "10S".</summary>
        public string Code => RankText + "SCHD"[(int)Suit];

        public static bool TryParse(string code, out Card card)
        {
            card = default;
            if (string.IsNullOrEmpty(code) || code.Length < 2 || code.Length > 3) return false;
            int s = "SCHD".IndexOf(code[code.Length - 1]);
            int r = Array.IndexOf(Ranks, code.Substring(0, code.Length - 1));
            if (s < 0 || r < 0) return false;
            card = new Card((Suit)s, r);
            return true;
        }

        public bool Equals(Card other) => Suit == other.Suit && Rank == other.Rank;
        public override bool Equals(object obj) => obj is Card c && Equals(c);
        public override int GetHashCode() => (int)Suit * 13 + Rank;
        public override string ToString() => RankText + SuitSymbol;

        public static List<Card> FullDeck()
        {
            var d = new List<Card>(52);
            for (int s = 0; s < 4; s++)
                for (int r = 0; r < 13; r++)
                    d.Add(new Card((Suit)s, r));
            return d;
        }

        public static List<Card> Shuffled(Random rng)
        {
            var d = FullDeck();
            for (int i = d.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (d[i], d[j]) = (d[j], d[i]);
            }
            return d;
        }
    }
}
