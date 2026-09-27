// START OF FILE KnifeItemDefinition.cs
using FMODUnity;
using ShellGame.Core;
using ShellGame.Shells;
using UnityEngine;

namespace ShellGame.Items
{
    [CreateAssetMenu(fileName = "KnifeItem", menuName = "ShellGame/Items/Knife Item")]
    public sealed class KnifeItemDefinition : ItemDefinition
    {
        [Header("Настройки ножа")]
        public float RevealDuration = 0.8f;
        public int DamageToEnemy = 1;
        public int DamageToPlayer = 1;

        [Header("Визуал")]
        public GameObject KnifeProjectilePrefab;
        public float KnifeFlightDuration = 0.35f;
        public bool SpinKnife = true;
        public GameObject HitParticlesPrefab;

        [Header("Звуки")]
        [Tooltip("Звук полёта (зацикленный)")]
        public EventReference KnifeFlightSound;
        [Tooltip("Звук при попадании")]
        public EventReference ImpactSound;

        [Header("Желание для ИИ врага")]
        [Tooltip("Базовое желание взять нож (0..1) — насколько охотно враг берётся за него, когда вообще не понимает, где метки.")]
        [Range(0f, 1f)] public float EnemyDesireBase = 0.3f;
        [Tooltip("Насколько уверенность врага в отслеживании меток повышает желание. Итог: EnemyDesireBase + уверенность * вес.")]
        [Range(0f, 1f)] public float EnemyDesireConfidenceWeight = 0.7f;

        private static TurnSide Opposite(TurnSide side) => side == TurnSide.Player ? TurnSide.Enemy : TurnSide.Player;

        public override bool CanUse(ItemEffectContext context)
        {
            if (context?.ActiveShells == null || context.ActiveShells.Count == 0) return false;
            return context.UserSide == TurnSide.Player ? (context.CanUsePlayerKnife?.Invoke() ?? false) : true;
        }

        public override bool Apply(ItemEffectContext context)
        {
            if (!CanUse(context)) return false;

            KnifeVisual activeKnife = null;

            // 1. ПАРИМ НАД СТОЛОМ (Замах): тряска как у молотка, звук броска НЕ играет
            if (KnifeProjectilePrefab != null)
            {
                Vector3 anchorPos = context.ItemWorldPosition + Vector3.up * 1f;

                if (ItemVisualAnchors.Instance != null)
                {
                    Transform anchor = context.UserSide == TurnSide.Player
                        ? ItemVisualAnchors.Instance.PlayerKnifeHoverPoint
                        : ItemVisualAnchors.Instance.EnemyKnifeHoverPoint;
                    if (anchor != null) anchorPos = anchor.position;
                }

                GameObject knifeObj = Instantiate(KnifeProjectilePrefab, context.ItemWorldPosition, Quaternion.identity);
                activeKnife = knifeObj.AddComponent<KnifeVisual>();
                activeKnife.SetAnchor(anchorPos);
            }

            // 2. ЛОГИКА ВЫБОРА
            if (context.UserSide == TurnSide.Enemy)
            {
                if (context.EnemyAI == null || context.BeginKnifeAttack == null) return false;
                context.EnemyTurnResolvedByItem = true;

                context.BeginKnifeAttack(RevealDuration, targetShell =>
                {
                    TurnSide targetSide = targetShell.HasMarker ? Opposite(context.UserSide) : context.UserSide;
                    ExecuteStrike(context, activeKnife, targetSide, targetShell.HasMarker ? DamageToEnemy : DamageToPlayer);
                });
                context.EnemyAI.MakeDecisionAndAttack(context.ActiveShells, chosen => chosen.Select(), afterItemUse: true);
                return true;
            }

            context.BeginKnifeAttack(RevealDuration, targetShell =>
            {
                TurnSide targetSide = targetShell.HasMarker ? Opposite(context.UserSide) : context.UserSide;
                ExecuteStrike(context, activeKnife, targetSide, targetShell.HasMarker ? DamageToEnemy : DamageToPlayer);
            });

            return true;
        }

        private void ExecuteStrike(ItemEffectContext context, KnifeVisual knife, TurnSide targetSide, int damage)
        {
            // Промах: удар пришёлся по самому себе (по пустому напёртку).
            if (targetSide == context.UserSide)
                ItemUseEvents.RaiseItemSelfHit(context.UserSide);

            if (knife == null)
            {
                context.Health.ApplyDamage(targetSide, damage);
                return;
            }

            Vector3 endPos = (ItemVisualAnchors.Instance != null)
                ? (targetSide == TurnSide.Player ? ItemVisualAnchors.Instance.PlayerHitPoint.position : ItemVisualAnchors.Instance.EnemyHitPoint.position)
                : knife.transform.position + Vector3.forward * 2f;

            // Звук полёта (привязан к ножу) и трейл включаются в момент полёта
            // в цель (ThrowAttackAt), после попадания звук останавливается.
            knife.ThrowAttackAt(KnifeFlightSound, endPos, KnifeFlightDuration, SpinKnife, () =>
            {
                if (!ImpactSound.IsNull) RuntimeManager.PlayOneShot(ImpactSound, endPos);
                if (HitParticlesPrefab != null) Instantiate(HitParticlesPrefab, endPos, Quaternion.identity);

                context.Health.ApplyDamage(targetSide, damage);
            });
        }

        /// <summary>
        /// Нож — атака: враг бьёт по напёртку, в котором по его мнению метка.
        /// Чем увереннее он отслеживает метки, тем охотнее берёт нож в руки —
        /// бросать наугад враг не станет, риск остаться без дозы слишком велик.
        /// </summary>
        public override float EvaluateEnemyDesire(ItemEffectContext context)
        {
            if (!CanUse(context)) return 0f;

            float confidence = context.EnemyAI != null ? context.EnemyAI.GetTrackedKnowledgeFraction() : 0.5f;
            return Mathf.Clamp01(EnemyDesireBase + confidence * EnemyDesireConfidenceWeight);
        }

        public override string GetEnemyUseAnnouncement() => "Враг метнул нож в напёрток!";
    }
}
// END OF FILE