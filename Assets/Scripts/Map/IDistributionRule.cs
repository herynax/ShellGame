using System.Collections.Generic;

namespace ShellGame.Map
{
    public interface IDistributionRule
    {
        string Description { get; }
        void Reset();
        bool IsViolated(MapNode node, IReadOnlyList<MapNode> predecessors);
    }

    public sealed class NoConsecutiveTypeRule : IDistributionRule
    {
        private readonly MapNodeType _type;
        public string Description { get; }

        public NoConsecutiveTypeRule(MapNodeType type)
        {
            _type = type;
            Description = $"Два {type} подряд по одному пути";
        }

        public void Reset() { }

        public bool IsViolated(MapNode node, IReadOnlyList<MapNode> predecessors)
        {
            foreach (var p in predecessors)
                if (p.Type == _type && node.Type == _type)
                    return true;
            return false;
        }
    }

    public sealed class MaxEnemyStreakRule : IDistributionRule
    {
        private readonly int _maxStreak;
        private readonly Dictionary<int, int> _streakCache = new();
        public string Description => $"Более {_maxStreak} боёв подряд";

        public MaxEnemyStreakRule(int maxStreak) => _maxStreak = maxStreak;

        public void Reset() => _streakCache.Clear();

        public bool IsViolated(MapNode node, IReadOnlyList<MapNode> predecessors)
        {
            if (node.Type != MapNodeType.Enemy)
            {
                _streakCache[node.Id] = 0;
                return false;
            }

            int longestIncoming = 0;
            foreach (var p in predecessors)
                if (p.Type == MapNodeType.Enemy)
                    longestIncoming = System.Math.Max(longestIncoming, _streakCache.TryGetValue(p.Id, out var s) ? s : 1);

            int streak = longestIncoming + 1;
            _streakCache[node.Id] = streak;
            return streak > _maxStreak;
        }
    }
}