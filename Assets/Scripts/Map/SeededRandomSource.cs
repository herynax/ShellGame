using System;
using System.Collections.Generic;

namespace ShellGame.Map
{
    public sealed class SeededRandomSource : IRunRandom
    {
        private readonly Random _random;

        public SeededRandomSource(int seed) => _random = new Random(seed);

        public int Next(int maxExclusive) => _random.Next(maxExclusive);
        public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
        public float Value => (float)_random.NextDouble();

        public T Pick<T>(IReadOnlyList<T> items, IReadOnlyList<float> weights)
        {
            float total = 0f;
            for (int i = 0; i < weights.Count; i++) total += weights[i];

            float roll = Value * total;
            float cumulative = 0f;

            for (int i = 0; i < items.Count; i++)
            {
                cumulative += weights[i];
                if (roll <= cumulative)
                    return items[i];
            }

            return items[^1];
        }

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}