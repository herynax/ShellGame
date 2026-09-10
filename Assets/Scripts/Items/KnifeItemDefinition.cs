// START OF FILE KnifeItemDefinition.cs
using DG.Tweening;
using FMODUnity;
using ShellGame.Audio;
using ShellGame.Core;
using ShellGame.Health;
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

        [Header("Визуал (Анимация полета)")]
        public GameObject KnifeProjectilePrefab;
        public float KnifeFlightDuration = 0.35f;
        public bool SpinKnife = true;
        [Tooltip("Префаб частиц в точке попадания ножа")]
        public GameObject HitParticlesPrefab;

        [Header("Звук попадания")]
        public EventReference PlayerStabSound;

        private static TurnSide Opposite(TurnSide side) => side == TurnSide.Player ? TurnSide.Enemy : TurnSide.Player;

        public override bool CanUse(ItemEffectContext context)
        {
            if (context?.ActiveShells == null || context.ActiveShells.Count == 0) return false;
            
            if (context.UserSide == TurnSide.Player)
                return context.CanUsePlayerKnife?.Invoke() ?? false;

            return true;
        }

        public override bool Apply(ItemEffectContext context)
        {
            if (!CanUse(context)) return false;

            GameObject activeKnife = null;
            Tween bobTween = null;
            Tween shakeTween = null;

            if (KnifeProjectilePrefab != null)
            {
                Vector3 anchorPos = context.ItemWorldPosition + Vector3.up * 1f;
                Quaternion anchorRot = Quaternion.identity;

                if (ItemVisualAnchors.Instance != null)
                {
                    Transform anchor = context.UserSide == TurnSide.Player 
                        ? ItemVisualAnchors.Instance.PlayerKnifeHoverPoint 
                        : ItemVisualAnchors.Instance.EnemyKnifeHoverPoint;

                    if (anchor != null)
                    {
                        anchorPos = anchor.position;
                        anchorRot = anchor.rotation;
                    }
                }

                activeKnife = Instantiate(KnifeProjectilePrefab, context.ItemWorldPosition, Quaternion.identity);
                
                activeKnife.transform.DOMove(anchorPos, 0.4f).SetEase(Ease.OutBack);
                activeKnife.transform.DORotateQuaternion(anchorRot, 0.4f).SetEase(Ease.OutQuad);

                // Покачивание/тряска в ожидании удара — теперь для ОБЕИХ
                // сторон одинаково (раньше было только для игрока; враг с
                // ножом теперь тоже реально "ждёт" решения через
                // MakeDecisionAndAttack, так что визуально это уместно и
                // для него).
                bobTween = activeKnife.transform.DOMoveY(anchorPos.y + 0.05f, 1f)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetDelay(0.4f);

                shakeTween = activeKnife.transform.DOShakeRotation(1f, new Vector3(3f, 3f, 3f), 10, 90f)
                    .SetLoops(-1, LoopType.Restart)
                    .SetDelay(0.4f);
            }

            if (context.UserSide == TurnSide.Enemy)
            {
                if (context.EnemyAI == null || context.BeginKnifeAttack == null) return false;

                // Ход противника теперь считается разрешённым этим предметом —
                // GameManager не должен ещё раз запускать обычное решение
                // поверх этого же хода (см. ItemSpawner.TryUseEnemyItemsRoutine).
                context.EnemyTurnResolvedByItem = true;

                context.BeginKnifeAttack(RevealDuration, targetShell =>
                {
                    bobTween?.Kill();
                    shakeTween?.Kill();

                    TurnSide targetSide = targetShell.HasMarker ? Opposite(context.UserSide) : context.UserSide;
                    int damage = targetShell.HasMarker ? DamageToEnemy : DamageToPlayer;

                    ExecuteStrike(context, activeKnife, targetSide, damage);
                });

                // НАСТОЯЩИЙ выбор через ту же вероятностную Knowledge-модель,
                // что и обычный ход врага — раньше здесь была прямая проверка
                // shell.HasMarker, из-за которой нож всегда попадал в цель.
                // Shell.Select() сам подхватит вооружённый выше ShellKnifeGate
                // (гейт общий для обеих сторон) и пройдёт полный обычный цикл
                // выбора/реванила/смены хода — никакого ручного управления
                // раундом здесь больше не нужно.
                context.EnemyAI.MakeDecisionAndAttack(context.ActiveShells, chosen => chosen.Select());
                return true;
            }

            if (context.BeginKnifeAttack == null) return false;
            
            context.BeginKnifeAttack(RevealDuration, targetShell =>
            {
                bobTween?.Kill(); 
                shakeTween?.Kill();

                TurnSide targetSide = targetShell.HasMarker ? Opposite(context.UserSide) : context.UserSide;
                int damage = targetShell.HasMarker ? DamageToEnemy : DamageToPlayer;
                
                ExecuteStrike(context, activeKnife, targetSide, damage);
            });

            return true;
        }

        private void ExecuteStrike(ItemEffectContext context, GameObject knife, TurnSide targetSide, int damage)
        {
            if (knife == null)
            {
                context.Health.ApplyDamage(targetSide, damage);
                return;
            }

            Vector3 endPos = knife.transform.position + (targetSide == TurnSide.Player ? -Vector3.forward : Vector3.forward) * 2f;

            if (ItemVisualAnchors.Instance != null)
            {
                Transform hitAnchor = targetSide == TurnSide.Player 
                    ? ItemVisualAnchors.Instance.PlayerHitPoint 
                    : ItemVisualAnchors.Instance.EnemyHitPoint;

                if (hitAnchor != null)
                {
                    endPos = hitAnchor.position;
                }
            }

            knife.transform.DOKill(); 
            knife.transform.LookAt(endPos); 

            if (SpinKnife)
            {
                knife.transform.DORotate(new Vector3(360f * 3f, 0, 0), KnifeFlightDuration, RotateMode.LocalAxisAdd)
                    .SetEase(Ease.Linear);
            }

            knife.transform.DOMove(endPos, KnifeFlightDuration)
                .SetEase(Ease.InCubic) 
                .OnComplete(() =>
                {
                    if (!PlayerStabSound.IsNull)
                    {
                        RuntimeManager.PlayOneShot(PlayerStabSound, endPos);
                    }

                    if (HitParticlesPrefab != null)
                    {
                        Instantiate(HitParticlesPrefab, endPos, Quaternion.identity);
                    }
                    
                    Destroy(knife);
                    context.Health.ApplyDamage(targetSide, damage);
                });
        }

        public override void ShowPlayerUseFeedback(ItemUseMessageView messageView)
        {
            messageView?.ShowPersistent("Выберите наперсток, чтобы ударить ножом!");
        }

        public override float EvaluateEnemyDesire(ItemEffectContext context)
        {
            if (!CanUse(context) || context.EnemyAI == null) return 0f;
            float knowledgeFraction = context.EnemyAI.GetTrackedKnowledgeFraction();
            return knowledgeFraction > 0.99f ? 0.9f : 0f; 
        }

        public override string GetEnemyUseAnnouncement() => "Враг использовал предмет: Нож!";
    }
}
// END OF FILE