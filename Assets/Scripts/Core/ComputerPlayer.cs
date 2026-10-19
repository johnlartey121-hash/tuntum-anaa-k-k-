using System;

namespace Tuntum.Core
{
    /// <summary>
    /// The computer counts cards like a sharp player: it remembers every colour already drawn
    /// and usually calls the colour with more cards left. It slips up now and then so it can be beaten.
    /// </summary>
    public sealed class ComputerPlayer
    {
        readonly Random rng;
        public double MistakeRate { get; }

        public ComputerPlayer(Random rng, double mistakeRate = 0.25)
        {
            this.rng = rng;
            MistakeRate = mistakeRate;
        }

        public CardColor Choose(Match match)
        {
            // only uses what every player has seen: the colours drawn so far
            int redsLeft = 26 - match.RedsDrawn, blacksLeft = 26 - match.BlacksDrawn;
            CardColor best = redsLeft == blacksLeft
                ? (rng.NextDouble() < 0.5 ? CardColor.Red : CardColor.Black)
                : (redsLeft > blacksLeft ? CardColor.Red : CardColor.Black);
            if (rng.NextDouble() < MistakeRate) best = best == CardColor.Red ? CardColor.Black : CardColor.Red;
            return best;
        }
    }
}
