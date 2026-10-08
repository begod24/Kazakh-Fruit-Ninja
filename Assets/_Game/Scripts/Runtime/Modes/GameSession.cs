using System;

namespace KazakhNinja
{
    public enum GameOverReason
    {
        None,
        OutOfLives,
        Bomb,
        TimeUp,
    }

    /// <summary>Score, lives and clock of one round. Plain C# so the rules can be unit tested.</summary>
    public sealed class GameSession
    {
        readonly GameRules rules;

        public GameSession(GameRules rules)
        {
            this.rules = rules;
            Lives = rules.lives;
            TimeLeft = rules.duration;
        }

        public int Score { get; private set; }
        public int Lives { get; private set; }
        public float TimeLeft { get; private set; }
        public float Elapsed { get; private set; }
        public int SlicedCount { get; private set; }
        public int BestCombo { get; private set; }
        /// <summary>Points multiplier; 2 while the Той bonus is active.</summary>
        public int Multiplier { get; set; } = 1;
        public GameOverReason EndReason { get; private set; }
        public bool IsOver => EndReason != GameOverReason.None;
        public bool HasLives => rules.lives > 0;
        public bool HasTimer => rules.duration > 0f;
        public int MaxLives => rules.lives;
        public BombEffect BombEffect => rules.bomb;
        public float Duration => rules.duration;

        public event Action<GameOverReason> Ended;

        /// <summary>Adds <paramref name="basePoints"/> times the multiplier; returns the points actually gained.</summary>
        public int AddPoints(int basePoints)
        {
            if (IsOver || basePoints <= 0) return 0;
            int gained = basePoints * Multiplier;
            Score += gained;
            return gained;
        }

        public void RegisterSlice()
        {
            if (!IsOver) SlicedCount++;
        }

        public void RegisterCombo(int count)
        {
            if (!IsOver && count > BestCombo) BestCombo = count;
        }

        /// <summary>A regular food fell uncut.</summary>
        public void Miss()
        {
            if (IsOver || !HasLives) return;
            LoseLife(GameOverReason.OutOfLives);
        }

        /// <summary>The blade hit a bomb; returns the points lost.</summary>
        public int Bomb()
        {
            if (IsOver) return 0;
            switch (rules.bomb)
            {
                case BombEffect.CostsLife when HasLives:
                    LoseLife(GameOverReason.Bomb);
                    return 0;
                case BombEffect.CostsPoints:
                    int lost = Math.Min(Score, rules.bombPenalty);
                    Score -= lost;
                    return lost;
                default:
                    End(GameOverReason.Bomb);
                    return 0;
            }
        }

        /// <param name="reason">Reported if this was the last life.</param>
        void LoseLife(GameOverReason reason)
        {
            Lives--;
            if (Lives <= 0) End(reason);
        }

        /// <param name="deltaTime">Game time: slow-motion also slows the round clock.</param>
        public void Tick(float deltaTime)
        {
            if (IsOver) return;
            Elapsed += deltaTime;
            if (!HasTimer) return;
            TimeLeft = Math.Max(0f, TimeLeft - deltaTime);
            if (TimeLeft <= 0f) End(GameOverReason.TimeUp);
        }

        void End(GameOverReason reason)
        {
            EndReason = reason;
            Ended?.Invoke(reason);
        }
    }
}
