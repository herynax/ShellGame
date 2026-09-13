// START OF FILE KnifeItemDefinition.cs
using DG.Tweening;
using FMOD.Studio;
using FMODUnity;
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

        private static TurnSide Opposite(TurnSide side) => side == TurnSide.Player ? TurnSide.Enemy : TurnSide.Player;

        public override bool CanUse(ItemEffectContext context)
        {
            if (context?.ActiveShells == null || context.ActiveShells.Count == 0) return false;
            return context.UserSide == TurnSide.Player ? (context.CanUsePlayerKnife?.Invoke() ?? false) : true;
        }

        public override bool Apply(ItemEffectContext context)
        {
            if (!CanUse(context)) return false;

            GameObject activeKnife = null;
            Tween bobTween = null;
            Tween shakeTween = null;

            // 1. ПАРИМ НАД СТОЛОМ (Замах)
            if (KnifeProjectilePrefab != null)
            {
                Vector3 anchorPos = context.ItemWorldPosition + Vector3.up * 1f;
                Quaternion anchorRot = Quaternion.identity;

                if (ItemVisualAnchors.Instance != null)
                {
                    Transform anchor = context.UserSide == TurnSide.Player 
                        ? ItemVisualAnchors.Instance.PlayerKnifeHoverPoint 
                        : ItemVisualAnchors.Instance.EnemyKnifeHoverPoint;
                    if (anchor != null) { anchorPos = anchor.position; anchorRot = anchor.rotation; }
                }

                activeKnife = Instantiate(KnifeProjectilePrefab, context.ItemWorldPosition, Quaternion.identity);
                activeKnife.transform.DOMove(anchorPos, 0.4f).SetEase(Ease.OutBack);
                activeKnife.transform.DORotateQuaternion(anchorRot, 0.4f).SetEase(Ease.OutQuad);

                if (context.UserSide == TurnSide.Player)
                {
                    bobTween = activeKnife.transform.DOMoveY(anchorPos.y + 0.05f, 1f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetDelay(0.4f);
                    shakeTween = activeKnife.transform.DOShakeRotation(1f, new Vector3(3f, 3f, 3f), 10, 90f).SetLoops(-1, LoopType.Restart).SetDelay(0.4f);
                }
            }

            // 2. ЛОГИКА ВЫБОРА
            if (context.UserSide == TurnSide.Enemy)
            {
                if (context.EnemyAI == null || context.BeginKnifeAttack == null) return false;
                context.EnemyTurnResolvedByItem = true;

                context.BeginKnifeAttack(RevealDuration, targetShell =>
                {
                    bobTween?.Kill(); shakeTween?.Kill();
                    TurnSide targetSide = targetShell.HasMarker ? Opposite(context.UserSide) : context.UserSide;
                    ExecuteStrike(context, activeKnife, targetSide, targetShell.HasMarker ? DamageToEnemy : DamageToPlayer);
                });
                context.EnemyAI.MakeDecisionAndAttack(context.ActiveShells, chosen => chosen.Select());
                return true;
            }

            context.BeginKnifeAttack(RevealDuration, targetShell =>
            {
                bobTween?.Kill(); shakeTween?.Kill();
                TurnSide targetSide = targetShell.HasMarker ? Opposite(context.UserSide) : context.UserSide;
                ExecuteStrike(context, activeKnife, targetSide, targetShell.HasMarker ? DamageToEnemy : DamageToPlayer);
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

            // ЗВУК ПОЛЕТА: запускаем только при броске
            EventInstance flightInstance = !KnifeFlightSound.IsNull ? RuntimeManager.CreateInstance(KnifeFlightSound) : default;
            if (flightInstance.isValid())
            {
                RuntimeManager.AttachInstanceToGameObject(flightInstance, knife.transform);
                flightInstance.start();
            }

            Vector3 endPos = (ItemVisualAnchors.Instance != null) 
                ? (targetSide == TurnSide.Player ? ItemVisualAnchors.Instance.PlayerHitPoint.position : ItemVisualAnchors.Instance.EnemyHitPoint.position)
                : knife.transform.position + Vector3.forward * 2f;

            knife.transform.DOKill(); 
            knife.transform.LookAt(endPos); 

            if (SpinKnife)
                knife.transform.DORotate(new Vector3(360f * 3f, 0, 0), KnifeFlightDuration, RotateMode.LocalAxisAdd).SetEase(Ease.Linear);

            knife.transform.DOMove(endPos, KnifeFlightDuration)
                .SetEase(Ease.InCubic) 
                .OnComplete(() =>
                {
                    // Останавливаем звук полёта
                    if (flightInstance.isValid()) { flightInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT); flightInstance.release(); }

                    if (!ImpactSound.IsNull) RuntimeManager.PlayOneShot(ImpactSound, endPos);
                    if (HitParticlesPrefab != null) Instantiate(HitParticlesPrefab, endPos, Quaternion.identity);
                    
                    Destroy(knife);
                    context.Health.ApplyDamage(targetSide, damage);
                });
        }
    }
}
// END OF FILE