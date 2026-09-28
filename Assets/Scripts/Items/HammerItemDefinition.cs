// START OF FILE HammerItemDefinition.cs
using FMODUnity;
using ShellGame.Core;
using ShellGame.Health;
using ShellGame.Shells;
using UnityEngine;

namespace ShellGame.Items
{
    [CreateAssetMenu(fileName = "HammerItem", menuName = "ShellGame/Items/Hammer Item")]
    public sealed class HammerItemDefinition : ItemDefinition
    {
        public float RevealDuration = 0.8f;
        public int DamageToPlayer = 1;

        [Tooltip("Урон самому себе, когда враг промахнулся молотком по напёртку с меткой.")]
        public int DamageToEnemy = 1;

        [Header("Желание для ИИ врага")]
        [Tooltip("Базовое желание взять молоток (0..1) — насколько охотно враг берётся за него, когда вообще не понимает, где метки.")]
        [Range(0f, 1f)] public float EnemyDesireBase = 0.35f;
        [Tooltip("Насколько уверенность врага в отслеживании меток повышает желание. Итог: EnemyDesireBase + уверенность * вес. Бьть-то надо по ПУСТОМУ напёртку, поэтому без понимания, где метки, враж за молоток не хватается.")]
        [Range(0f, 1f)] public float EnemyDesireConfidenceWeight = 0.65f;
        [Tooltip("Сколько напёртков на столе считается «тесно» — при меньшем количестве желание падает пропорционально (молотом нечего разбивать, если на столе всего два напёртка).")]
        [Min(1)] public int EnemyCrowdedShellCount = 4;
        [Tooltip("Сколько секунд после удара молотком ждать, прежде чем враг сделает обычный ход. Страховка: обычный выбор врага не должен начаться поверх удара. Реальная длительность раскрытия напёртка подставляется автоматически, если она больше.")]
        [Min(0f)] public float EnemyStrikeSettleSeconds = 1.2f;

        [Header("Визуал")]
        [Tooltip("Префаб молотка, который будет летать и бить. Автоматически получит HammerVisual.")]
        public GameObject HammerPrefab;
        [Tooltip("Префаб щепок/пыли при разрушении наперстка")]
        public GameObject SmashParticlesPrefab;
        [Tooltip("Префаб частиц в точке попадания молотка по игроку")]
        public GameObject HitParticlesPrefab;
        
        [Header("Звуки")]
        public EventReference SmashSound;
        public EventReference HitFaceSound;
        public EventReference HammerFlightSound; 

        public override bool CanUse(ItemEffectContext context)
        {
            if (context?.ActiveShells == null || context.ActiveShells.Count == 0) return false;
            if (context.Health == null || context.BeginHammerAttack == null) return false;

            // Игрок бьёт молотком по напёртку сам, у него дополнительно
            // проверяется, что сейчас его ход и никто не целится.
            if (context.UserSide == TurnSide.Player)
                return context.CanUsePlayerHammer?.Invoke() ?? false;

            // Врагу нужен ИИ, чтобы тот сам выбрал пустой напёрток.
            return context.EnemyAI != null;
        }

        public override bool Apply(ItemEffectContext context)
        {
            if (!CanUse(context)) return false;

            HammerVisual activeHammer = null;

            if (HammerPrefab != null)
            {
                GameObject obj = Instantiate(HammerPrefab, context.ItemWorldPosition, Quaternion.identity);
                activeHammer = obj.AddComponent<HammerVisual>();
            }

            TurnSide userSide = context.UserSide;
            int selfDamage = userSide == TurnSide.Player ? DamageToPlayer : DamageToEnemy;

            context.BeginHammerAttack(RevealDuration, targetShell =>
            {
                if (targetShell.HasMarker)
                {
                    // Ошибка: там метка! Поднимаем наперсток, чтобы игрок увидел метку.
                    targetShell.RevealMarker(RevealDuration);
                    ItemUseEvents.RaiseItemSelfHit(userSide);

                    Vector3 facePos = ResolveHitPoint(userSide, targetShell);

                    if (activeHammer != null)
                    {
                        activeHammer.FlyToFace(HammerFlightSound, facePos, () =>
                        {
                            if (!HitFaceSound.IsNull) RuntimeManager.PlayOneShot(HitFaceSound, facePos);
                            if (HitParticlesPrefab != null)
                                Instantiate(HitParticlesPrefab, facePos, Quaternion.identity);
                            context.Health.ApplyDamage(userSide, selfDamage);
                        });
                    }
                    else
                    {
                        context.Health.ApplyDamage(userSide, selfDamage);
                    }
                }
                else
                {
                    // Успех: наперсток пустой! Разбиваем его прямо на столе (без поднятия).
                    if (activeHammer != null)
                    {
                        activeHammer.Strike(HammerFlightSound, targetShell.transform.position, () =>
                        {
                            SmashShell(context, targetShell);
                        });
                    }
                    else
                    {
                        SmashShell(context, targetShell);
                    }
                }
            });

            if (userSide == TurnSide.Enemy)
            {
                // Враг выбирает напёрток сам — причём пустой (см.
                // EnemyAIController.MakeDecisionAndPickEmpty). Молоток не
                // завершает ход врага: после удара он всё равно выбирает
                // напёрток для обычной атаки, поэтому этот выбор откладываем
                // через ConsumedExtraDelay, чтобы он не начался поверх удара.
                context.ConsumedExtraDelay += ResolveEnemySettleSeconds(context);
                context.EnemyAI.MakeDecisionAndPickEmpty(context.ActiveShells, chosen => chosen.Select(TurnSide.Enemy));
            }

            return true;
        }

        private float ResolveEnemySettleSeconds(ItemEffectContext context)
        {
            float settle = Mathf.Max(0f, EnemyStrikeSettleSeconds);

            // Раскрытие напёртка при промахе идёт параллельно удару и может
            // быть длиннее (уровень задаёт свою длительность) — берём максимум.
            if (context.ResolveShellRevealDuration != null)
                settle = Mathf.Max(settle, context.ResolveShellRevealDuration(RevealDuration));

            return settle;
        }

        private static Vector3 ResolveHitPoint(TurnSide side, Shell targetShell)
        {
            if (ItemVisualAnchors.Instance != null)
            {
                Transform anchor = side == TurnSide.Player
                    ? ItemVisualAnchors.Instance.PlayerHitPoint
                    : ItemVisualAnchors.Instance.EnemyHitPoint;

                if (anchor != null)
                    return anchor.position;
            }

            return Camera.main != null
                ? Camera.main.transform.position
                : targetShell.transform.position - Vector3.forward * 2f;
        }

        private void SmashShell(ItemEffectContext context, Shell targetShell)
        {
            if (!SmashSound.IsNull) RuntimeManager.PlayOneShot(SmashSound, targetShell.transform.position);
            
            if (SmashParticlesPrefab != null)
                Instantiate(SmashParticlesPrefab, targetShell.transform.position, Quaternion.identity);

            // Убираем наперсток с поля
            context.RemoveShellFromPlay?.Invoke(targetShell);
            
            // Снижаем макс количество наперстков до конца уровня
            context.ReduceMaxShells?.Invoke();
        }

        public override void ShowPlayerUseFeedback(ItemUseMessageView messageView)
        {
            messageView?.ShowPersistent("Выберите ПУСТОЙ наперсток, чтобы разбить его молотком!");
        }

        /// <summary>
        /// Молоток выигрывается тем, что на столе много напёртков: каждый
        /// разбитый уменьшает их число до конца уровня. Но бить надо по
        /// ПУСТОМУ, поэтому враг берётся за молоток, только когда примерно
        /// понимает, где метки, — и тем охотнее, чем больше напёртков на столе.
        /// </summary>
        public override float EvaluateEnemyDesire(ItemEffectContext context)
        {
            if (!CanUse(context)) return 0f;

            float confidence = context.EnemyAI != null ? context.EnemyAI.GetTrackedKnowledgeFraction() : 0.5f;
            float confidencePart = Mathf.Clamp01(EnemyDesireBase + confidence * EnemyDesireConfidenceWeight);

            float crowd = Mathf.Clamp01(context.ActiveShells.Count / Mathf.Max(1f, EnemyCrowdedShellCount));
            return confidencePart * crowd;
        }

        public override string GetEnemyUseAnnouncement() => "Враг взял молоток — ищет пустой напёрток!";
    }
}
// END OF FILE