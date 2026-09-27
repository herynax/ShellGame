using System;
using System.Collections.Generic;
using FMODUnity;
using ShellGame.Core;
using ShellGame.Items;
using UnityEngine;

namespace ShellGame.Dialogue
{
    /// <summary>Событие, на которое враг может отреагировать.</summary>
    public enum EnemyReactionContext
    {
        [InspectorName("Начало уровня")] GameStart = 0,
        [InspectorName("Начало хода игрока")] PlayerTurnStarted = 1,
        [InspectorName("Начало хода врага")] EnemyTurnStarted = 2,
        [InspectorName("Игрок слишком долго думает")] PlayerThinking = 3,
        [InspectorName("Игрок открыл напёрсток")] PlayerRevealed = 4,
        [InspectorName("Враг открыл напёрсток")] EnemyRevealed = 5,
        [InspectorName("Кто-то использовал предмет")] ItemUsed = 6,
        [InspectorName("Кто-то промахнулся предметом")] ItemSelfHit = 7,
        [InspectorName("Уровень пройден (враг мёртв)")] LevelEndVictory = 8,
        [InspectorName("Уровень проигран (игрок мёртв)")] LevelEndDefeat = 9,
    }

    /// <summary>
    /// Дополнительное условие правила. Новое условие = одно значение здесь
    /// плюс один case в EnemyReactionMatcher.IsConditionMet.
    /// </summary>
    public enum EnemyReactionConditionType
    {
        [InspectorName("— (без условия)")] None = 0,
        [InspectorName("Игрок нашёл метку")] PlayerResultIsHit = 1,
        [InspectorName("Игрок промахнулся")] PlayerResultIsMiss = 2,
        [InspectorName("Враг нашёл метку")] EnemyResultIsHit = 3,
        [InspectorName("Враг промахнулся")] EnemyResultIsMiss = 4,
        [InspectorName("Промахов игрока подряд ≥")] ConsecutivePlayerMissesAtLeast = 5,
        [InspectorName("Попаданий игрока подряд ≥")] ConsecutivePlayerHitsAtLeast = 6,
        [InspectorName("У игрока мало здоровья (< доли)")] PlayerHealthBelow = 7,
        [InspectorName("У врага мало здоровья (< доли)")] EnemyHealthBelow = 8,
        [InspectorName("Использован этот предмет")] ItemIs = 9,
        [InspectorName("Предмет использовал")] ItemUserIs = 10,
        [InspectorName("Игрок выбрал быстрее (сек)")] DecisionFasterThan = 11,
        [InspectorName("Игрок выбирал дольше (сек)")] DecisionSlowerThan = 12,
    }

    public enum EnemyReactionUser
    {
        [InspectorName("Любой")] Any = 0,
        [InspectorName("Игрок")] Player = 1,
        [InspectorName("Враг")] Enemy = 2,
    }

    public static class EnemyReactionContexts
    {
        /// <summary>
        /// Реплики, которые показываются ВСЕГДА, на 100%: вступление на уровень
        /// и финал (победа/поражение). Для них не действуют ни общий шанс
        /// LineChance, ни антиспам (MinSecondsBetweenLines / MaxLinesPerTurn),
        /// ни CooldownSeconds правила, ни приоритет конкурентов — что бы ни
        /// происходило в игре, эти реплики враг произнесёт.
        /// </summary>
        public static bool IsCritical(EnemyReactionContext context)
        {
            return context == EnemyReactionContext.GameStart
                   || context == EnemyReactionContext.LevelEndVictory
                   || context == EnemyReactionContext.LevelEndDefeat;
        }
    }

    [Serializable]
    public sealed class EnemyReactionCondition
    {
        public EnemyReactionConditionType Type = EnemyReactionConditionType.None;

        [Tooltip("Предмет для ItemIs (перетащить ассет предмета). null = любой предмет.")]
        public ItemDefinition Item;

        [Tooltip("Сторона для ItemUserIs: чей выбор предмета вызвал реакцию.")]
        public EnemyReactionUser User = EnemyReactionUser.Any;

        [Tooltip("Порог для счётчиков (Consecutive*).")]
        public int Amount = 1;

        [Tooltip("Секунды для DecisionFasterThan / DecisionSlowerThan.")]
        public float Seconds = 1f;

        [Range(0f, 1f), Tooltip("Остаток здоровья НИЖЕ этой доли от максимума (0.34 = меньше трети жизни).")]
        public float Fraction = 0.34f;
    }

    [Serializable]
    public sealed class EnemyReactionLine
    {
        [TextArea(2, 5)] public string Text;
        public Color TextColor = Color.white;

        [Tooltip("Голос реплики (FMOD). Пусто — реплика беззвучная.")]
        public EventReference VoiceEvent;

        [Min(0f), Tooltip("Минимальное время показа, даже если игрок сразу нажал скип.")]
        public float MinDisplayDuration = 0.6f;

        [Tooltip("true — ждёт клик/пробел (игрок может скинуть), false — закрывается сама.")]
        public bool WaitForClick = true;

        [Tooltip("Переопределение AutoSkipSeconds конфига для этой реплики. -1 (по умолчанию) — взять из конфига. 0 — не автоскипать, ждать клика. Больше 0 — уйти через столько секунд.")]
        public float AutoSkipSeconds = -1f;
    }

    /// <summary>
    /// Группа реплик. Если в группе одна реплика — это просто запись в пуле.
    /// Если несколько — они показываются по очереди (как вступительная речь
    /// или финальная фраза врага). Сама группа выбирается случайно из всех
    /// групп правила.
    /// </summary>
    [Serializable]
    public sealed class EnemyReactionGroup
    {
        [Tooltip("Только для читаемости в инспекторе.")]
        public string Name = "";

        [Tooltip("Реплики показываются по очереди. Пустая группа никогда не выбирается.")]
        public EnemyReactionLine[] Lines = Array.Empty<EnemyReactionLine>();
    }

    [Serializable]
    public sealed class EnemyReactionRule
    {
        [Tooltip("Событие, на которое сработает правило.")]
        public EnemyReactionContext Context;

        [Tooltip("Чем выше приоритет, тем важнее правило, если под контекст подошло несколько правил.")]
        public int Priority;

        [Tooltip("Игнорировать общий шанс LineChance и антиспам (MinSecondsBetweenLines / MaxLinesPerTurn). Для вступления, первого обмена и финала уровня.")]
        public bool AlwaysShow;

        [Min(0f), Tooltip("Сколько секунд это правило нельзя повторять после последнего показа.")]
        public float CooldownSeconds;

        [Tooltip("Не показывать раньше этого номера хода игрока. 0 — без ограничения.")]
        public int MinTurn;

        [Tooltip("Не показывать позже этого номера хода игрока. 0 — без ограничения.")]
        public int MaxTurn;

        [Tooltip("Все условия должны выполняться одновременно. Пустой список — правило подходит всегда.")]
        public List<EnemyReactionCondition> Conditions = new List<EnemyReactionCondition>();

        [Tooltip("Пулы реплик: одна группа выбирается случайно и показывается целиком.")]
        public List<EnemyReactionGroup> Groups = new List<EnemyReactionGroup>();
    }

    /// <summary>
    /// Все реплики врага и правила их показа в одном ассете: один враг —
    /// один конфиг. Кладётся в Resources/Configs/EnemyReactions, после чего
    /// EnemyReactionBootstrap сам поднимает директора на сценах из списка
    /// Scenes. Новый враг на новом уровне = новый ассет, кода не требуется.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyReactionConfig", menuName = "ShellGame/Dialogue/Enemy Reaction Config")]
    public sealed class EnemyReactionConfig : ScriptableObject
    {
        public const string ResourcesFolder = "Configs/EnemyReactions";

        [Header("Где работает")]
        [Tooltip("Имена сцен, на которых активен этот враг. Пусто — на любой сцене, где есть DialogueView.")]
        public List<string> Scenes = new List<string>();

        [Tooltip("Имя врага — только для отладки в логе.")]
        public string EnemyId = "Enemy";

        [Header("Частота реплик")]
        [Range(0f, 1f), Tooltip("Главный регулятор: шанс, что на событие вообще будет реакция (для правил с AlwaysShow игнорируется).")]
        public float LineChance = 0.55f;

        [Min(0f), Tooltip("Минимум секунд между двумя репликами — антиспам.")]
        public float MinSecondsBetweenLines = 5f;

        [Min(1), Tooltip("Максимум реплик за один ход игрока.")]
        public int MaxLinesPerTurn = 1;

        [Header("Тайминги")]
        [Min(0f), Tooltip("Через сколько секунд молчания игрока на его ходу враг начинает его подгонять (контекст PlayerThinking). 0 — отключить.")]
        public float PlayerThinkingWarningSeconds = 4f;

        [Min(0f), Tooltip("Пауза перед вступительными репликами на старте уровня.")]
        public float FirstLineDelaySeconds = 0.6f;

        [Header("Показ реплик")]
        [Min(0f), Tooltip("Сколько секунд реплика висит максимум, прежде чем уйти сама, если игрок не кликнул. Игрок может закрыть её раньше. 0 — не автоскипать, реплики ждут клика.")]
        public float AutoSkipSeconds = 5f;

        [Header("Правила")]
        public List<EnemyReactionRule> Rules = new List<EnemyReactionRule>();

        public bool AppliesToScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return false;

            if (Scenes == null || Scenes.Count == 0)
                return true;

            foreach (var scene in Scenes)
            {
                if (string.Equals(scene, sceneName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Время автоскипа для конкретной реплики: её собственное
        /// переопределение, если оно задано, иначе общее AutoSkipSeconds
        /// конфига.
        /// </summary>
        public float ResolveAutoSkipSeconds(EnemyReactionLine line)
        {
            if (line != null && line.AutoSkipSeconds >= 0f)
                return line.AutoSkipSeconds;

            return Mathf.Max(0f, AutoSkipSeconds);
        }

        /// <summary>Антиспам и общий шанс. lastShownAt = -inf, если реплик ещё не было.</summary>
        public bool IsFrequencyGateOpen(float now, float lastShownAt, int linesShownThisTurn)
        {
            if (MaxLinesPerTurn > 0 && linesShownThisTurn >= MaxLinesPerTurn)
                return false;

            if (MinSecondsBetweenLines > 0f && now - lastShownAt < MinSecondsBetweenLines)
                return false;

            return UnityEngine.Random.value < Mathf.Clamp01(LineChance);
        }
    }

    /// <summary>Снимок игрового состояния, по которому выбирается реплика.</summary>
    public struct EnemyReactionQuery
    {
        public EnemyReactionContext Context;

        /// <summary>Чьё действие вызвало контекст (кто открыл напёрток, чей ход начался).</summary>
        public TurnSide Actor;

        /// <summary>true, если HasMarker осмысленно (контексты открытия напёртка).</summary>
        public bool HasResult;
        public bool HasMarker;

        /// <summary>Предмет, использованный в этом ходу (или только что использованный).</summary>
        public ItemDefinition Item;
        public TurnSide ItemUser;

        public int TurnNumber;
        public float DecisionSeconds;
        public int ConsecutivePlayerMisses;
        public int ConsecutivePlayerHits;

        /// <summary>Остаток здоровья игрока, 0..1 (1 = полное).</summary>
        public float PlayerHealthFraction;

        /// <summary>Остаток здоровья врага, 0..1.</summary>
        public float EnemyHealthFraction;

        public TurnSide DeadSide;
    }
}
