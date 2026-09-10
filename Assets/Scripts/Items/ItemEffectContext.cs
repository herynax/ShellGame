using System;
using System.Collections.Generic;
using ShellGame.AI;
using ShellGame.Core;
using ShellGame.Health;
using ShellGame.Shells;
using UnityEngine; // Добавлено для Vector3

namespace ShellGame.Items
{
    public sealed class ItemEffectContext
    {
        public TurnSide UserSide;
        public HealthController Health;
        public IReadOnlyList<Shell> ActiveShells;
        public EnemyAIController EnemyAI;

        // === Позиция предмета в мире (для спавна эффектов/снарядов) ===
        public Vector3 ItemWorldPosition;

        public Action<TurnSide, float> SlowGamePaceUntilNextChoice;
        public Func<bool> CanSlowGamePace;
        public Action<float> ReduceEnemyTrackingLossNextShuffle;
        public Func<bool> CanReduceEnemyTrackingLossNextShuffle;
        
        public Func<bool> CanUsePlayerMonocle;
        public Action<float, Action<Shell>> BeginShellPeek;

        public Func<bool> CanUsePlayerKnife;
        public Action<float, Action<Shell>> BeginKnifeAttack;

        public Func<bool> CanUseEnemySlowItem;
        public Action StartEnemySlowItemCooldown;
        public Func<float, float> ResolveShellRevealDuration;
        public float ConsumedExtraDelay;
        public Action SkipCurrentTurn;
        public Action RequestExtraTurn;
        public Func<bool> CanRequestExtraTurn;

        public Func<bool> CanUsePlayerHammer;
        public Action<float, Action<Shell>> BeginHammerAttack;
        public Action ReduceMaxShells;
        public Action<Shell> RemoveShellFromPlay;

        public bool EnemyTurnResolvedByItem;
    }
}