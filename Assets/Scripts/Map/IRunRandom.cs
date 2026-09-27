// File: Assets/Scripts/Map/IRunRandom.cs
using System.Collections.Generic;

namespace ShellGame.Map
{
    public interface IRunRandom
    {
        int Next(int maxExclusive);
        int Next(int minInclusive, int maxExclusive);
        float Value { get; } // [0,1)
        T Pick<T>(IReadOnlyList<T> items, IReadOnlyList<float> weights);
        void Shuffle<T>(IList<T> list);
    }
}