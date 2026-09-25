using System.Collections.Generic;
using ShellGame.Core;
using UnityEngine;

namespace ShellGame.Dialogue
{
    /// <summary>
    /// Чистая логика выбора реплики: проверка правил и условий + случайный
    /// выбор группы из пула. Отделена от EnemyReactionDirector, чтобы правила
    /// можно было проверять/менять без MonoBehaviour-обвязки.
    /// </summary>
    public static class EnemyReactionMatcher
    {
        /// <summary>lastShownAt = -inf, если правило ещё ни разу не показывалось.</summary>
        public static bool IsMatch(EnemyReactionRule rule, EnemyReactionQuery query, float now, float lastShownAt)
        {
            if (rule == null || rule.Context != query.Context)
                return false;

            // Вступление и финал уровня показываются всегда, на 100%:
            // никакие ограничения по ходу и откаты по Cooldown их не блокируют.
            bool critical = EnemyReactionContexts.IsCritical(rule.Context);

            if (!critical)
            {
                if (rule.MinTurn > 0 && query.TurnNumber < rule.MinTurn)
                    return false;

                if (rule.MaxTurn > 0 && query.TurnNumber > rule.MaxTurn)
                    return false;

                if (!rule.AlwaysShow
                    && rule.CooldownSeconds > 0f
                    && lastShownAt > float.NegativeInfinity
                    && now - lastShownAt < rule.CooldownSeconds)
                    return false;
            }

            if (!HasPlayableGroup(rule))
                return false;

            if (rule.Conditions == null)
                return true;

            for (int i = 0; i < rule.Conditions.Count; i++)
            {
                var condition = rule.Conditions[i];
                if (condition == null || condition.Type == EnemyReactionConditionType.None)
                    continue;

                if (!IsConditionMet(condition, query))
                    return false;
            }

            return true;
        }

        public static bool IsConditionMet(EnemyReactionCondition condition, EnemyReactionQuery query)
        {
            switch (condition.Type)
            {
                case EnemyReactionConditionType.PlayerResultIsHit:
                    return query.HasResult && query.Actor == TurnSide.Player && query.HasMarker;

                case EnemyReactionConditionType.PlayerResultIsMiss:
                    return query.HasResult && query.Actor == TurnSide.Player && !query.HasMarker;

                case EnemyReactionConditionType.EnemyResultIsHit:
                    return query.HasResult && query.Actor == TurnSide.Enemy && query.HasMarker;

                case EnemyReactionConditionType.EnemyResultIsMiss:
                    return query.HasResult && query.Actor == TurnSide.Enemy && !query.HasMarker;

                case EnemyReactionConditionType.ConsecutivePlayerMissesAtLeast:
                    return query.ConsecutivePlayerMisses >= Mathf.Max(1, condition.Amount);

                case EnemyReactionConditionType.ConsecutivePlayerHitsAtLeast:
                    return query.ConsecutivePlayerHits >= Mathf.Max(1, condition.Amount);

                case EnemyReactionConditionType.PlayerHealthBelow:
                    return query.PlayerHealthFraction < condition.Fraction;

                case EnemyReactionConditionType.EnemyHealthBelow:
                    return query.EnemyHealthFraction < condition.Fraction;

                case EnemyReactionConditionType.ItemIs:
                    return condition.Item == null ? query.Item != null : query.Item == condition.Item;

                case EnemyReactionConditionType.ItemUserIs:
                    if (condition.User == EnemyReactionUser.Any)
                        return true;
                    return condition.User == EnemyReactionUser.Player
                        ? query.ItemUser == TurnSide.Player
                        : query.ItemUser == TurnSide.Enemy;

                case EnemyReactionConditionType.DecisionFasterThan:
                    return query.DecisionSeconds >= 0f && query.DecisionSeconds < condition.Seconds;

                case EnemyReactionConditionType.DecisionSlowerThan:
                    return query.DecisionSeconds >= condition.Seconds;

                default:
                    return true;
            }
        }

        public static bool HasPlayableGroup(EnemyReactionRule rule)
        {
            if (rule == null || rule.Groups == null)
                return false;

            foreach (var group in rule.Groups)
            {
                if (IsPlayable(group))
                    return true;
            }

            return false;
        }

        public static bool IsPlayable(EnemyReactionGroup group)
        {
            if (group == null || group.Lines == null)
                return false;

            foreach (var line in group.Lines)
            {
                if (line != null && !string.IsNullOrEmpty(line.Text))
                    return true;
            }

            return false;
        }

        /// <summary>Случайная непустая группа из пула правила.</summary>
        public static EnemyReactionGroup PickGroup(IReadOnlyList<EnemyReactionGroup> groups)
        {
            if (groups == null || groups.Count == 0)
                return null;

            var playable = new List<EnemyReactionGroup>(groups.Count);
            foreach (var group in groups)
            {
                if (IsPlayable(group))
                    playable.Add(group);
            }

            if (playable.Count == 0)
                return null;

            return playable[Random.Range(0, playable.Count)];
        }
    }
}
