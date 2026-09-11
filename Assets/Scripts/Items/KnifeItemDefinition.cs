// START OF FILE KnifeItemDefinition.cs
using DG.Tweening;
using FMOD.Studio;
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

        [Header("Звук")]
        [Tooltip("3D-звук полёта ножа. Стартует, когда нож заспавнен, и крепится к его трансформу (позиция трекается автоматически).")]
        public EventReference KnifeFlightSound;
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
            EventInstance flightSound = default;

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

                // Звук полёта — 3D-инстанс, привязанный к трансформу ножа.
                // AttachInstanceToGameObject сам обновляет 3D-attributes
                // каждый кадр, пока инстанс жив, так что отдельно дёргать
                // set3DAttributes на каждом Move/Rotate не нужно.
                if (!KnifeFlightSound.IsNull)
                {
                    flightSound = RuntimeManager.CreateInstance(KnifeFlightSound);
                    RuntimeManager.AttachInstanceToGameObject(flightSound, activeKnife.transform);
                    flightSound.start();
                }

                activeKnife.transform.DOMove(anchorPos, 0.4f).SetEase(Ease.OutBack);
                activeKnife.transform.DORotateQuaternion(anchorRot, 0.4f).SetEase(Ease.OutQuad);

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
                if (context.EnemyAI == null || context.BeginKnifeAttack == null)
                {
                    StopFlightSound(flightSound);
                    return false;
                }

                context.EnemyTurnResolvedByItem = true;

                context.BeginKnifeAttack(RevealDuration, targetShell =>
                {
                    bobTween?.Kill();
                    shakeTween?.Kill();

                    TurnSide targetSide = targetShell.HasMarker ? Opposite(context.UserSide) : context.UserSide;
                    int damage = targetShell.HasMarker ? DamageToEnemy : DamageToPlayer;

                    ExecuteStrike(context, activeKnife, flightSound, targetSide, damage);
                });

                context.EnemyAI.MakeDecisionAndAttack(context.ActiveShells, chosen => chosen.Select());
                return true;
            }

            if (context.BeginKnifeAttack == null)
            {
                StopFlightSound(flightSound);
                return false;
            }
            
            context.BeginKnifeAttack(RevealDuration, targetShell =>
            {
                bobTween?.Kill(); 
                shakeTween?.Kill();

                TurnSide targetSide = targetShell.HasMarker ? Opposite(context.UserSide) : context.UserSide;
                int damage = targetShell.HasMarker ? DamageToEnemy : DamageToPlayer;
                
                ExecuteStrike(context, activeKnife, flightSound, targetSide, damage);
            });

            return true;
        }

        private void ExecuteStrike(ItemEffectContext context, GameObject knife, EventInstance flightSound, TurnSide targetSide, int damage)
        {
            if (knife == null)
            {
                StopFlightSound(flightSound);
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
                    // Втыкание: звук стеба + фейдаут звука полёта (через
                    // release-огибающую самого события — ALLOWFADEOUT её
                    // уважает, отдельный DOTween-твин громкости не нужен).
                    if (!PlayerStabSound.IsNull)
                    {
                        RuntimeManager.PlayOneShot(PlayerStabSound, endPos);
                    }

                    StopFlightSound(flightSound);

                    if (HitParticlesPrefab != null)
                    {
                        Instantiate(HitParticlesPrefab, endPos, Quaternion.identity);
                    }
                    
                    Destroy(knife);
                    context.Health.ApplyDamage(targetSide, damage);
                });
        }

        private static void StopFlightSound(EventInstance instance)
        {
            if (!instance.isValid()) return;

            instance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            instance.release();
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