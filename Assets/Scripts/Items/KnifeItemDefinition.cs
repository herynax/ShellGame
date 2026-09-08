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
    /// <summary>
    /// Предмет "Нож".
    /// Игрок выбирает наперсток. Наперсток приподнимается.
    /// На пике подъема нож выстреливает в того, кто должен получить урон.
    /// </summary>
    [CreateAssetMenu(fileName = "KnifeItem", menuName = "ShellGame/Items/Knife Item")]
    public sealed class KnifeItemDefinition : ItemDefinition
    {
        [Header("Настройки ножа")]
        public float RevealDuration = 0.8f;
        public int DamageToEnemy = 1;
        public int DamageToPlayer = 1;

        [Header("Визуал (Анимация полета)")]
        [Tooltip("Префаб ножа (3D модель). Должен быть направлен острием по оси Z.")]
        public GameObject KnifeProjectilePrefab;
        public float KnifeFlightDuration = 0.35f;
        public bool SpinKnife = true;

        [Header("Звук попадания")]
        [Tooltip("Звук при попадании ножа в ИГРОКА (стаб)")]
        public EventReference PlayerStabSound;

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

            // 1. Создаем визуал ножа, который "парит" в ожидании броска
            GameObject activeKnife = null;
            Tween bobTween = null;

            if (KnifeProjectilePrefab != null)
            {
                // Появляется на месте кликнутого предмета
                activeKnife = Instantiate(KnifeProjectilePrefab, context.ItemWorldPosition, Quaternion.identity);
                
                // Подлетает выше, чем обычный ховер
                Vector3 floatPos = context.ItemWorldPosition + Vector3.up * 0.45f;
                
                activeKnife.transform.DOMove(floatPos, 0.3f).SetEase(Ease.OutBack);

                if (context.UserSide == TurnSide.Player)
                {
                    // Игрок активировал: нож смотрит лезвием вниз 
                    // (предполагаем, что лезвие смотрит по Z. Чтобы смотрело вниз, крутим по X на 90 градусов)
                    activeKnife.transform.DORotate(new Vector3(90f, 0f, 0f), 0.3f).SetEase(Ease.OutQuad);
                    
                    // Запускаем легкое покачивание (дыхание/ожидание)
                    bobTween = activeKnife.transform.DOMoveY(floatPos.y + 0.1f, 1f)
                        .SetEase(Ease.InOutSine)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetDelay(0.3f);
                }
                else
                {
                    // Враг активировал: нож просто смотрит в сторону игрока (в камеру)
                    if (Camera.main != null)
                    {
                        Vector3 dirToCam = Camera.main.transform.position - floatPos;
                        activeKnife.transform.rotation = Quaternion.LookRotation(dirToCam);
                    }
                }
            }

            // Логика противника (бьет автоматически после небольшой паузы)
            if (context.UserSide == TurnSide.Enemy)
            {
                if (context.EnemyAI == null) return false;
                
                Shell targetShell = null;

                foreach (var shell in context.ActiveShells)
                {
                    if (shell.HasMarker)
                    {
                        targetShell = shell;
                        break;
                    }
                }
                
                if (targetShell == null)
                    targetShell = context.ActiveShells[Random.Range(0, context.ActiveShells.Count)];

                targetShell.RevealMarker(RevealDuration);
                context.ConsumedExtraDelay = context.ResolveShellRevealDuration?.Invoke(RevealDuration) ?? RevealDuration;

                float delay = context.ConsumedExtraDelay * 0.4f; 
                DOVirtual.DelayedCall(delay, () =>
                {
                    TurnSide targetSide = targetShell.HasMarker ? TurnSide.Player : TurnSide.Enemy;
                    int damage = targetShell.HasMarker ? DamageToEnemy : DamageToPlayer;
                    ExecuteStrike(context, activeKnife, targetSide, damage);
                });
                
                return true;
            }

            // Логика игрока (ждет, пока игрок выберет наперсток)
            if (context.BeginKnifeAttack == null) return false;
            
            context.BeginKnifeAttack(RevealDuration, targetShell =>
            {
                bobTween?.Kill(); // Останавливаем парение

                TurnSide targetSide = targetShell.HasMarker ? TurnSide.Enemy : TurnSide.Player;
                int damage = targetShell.HasMarker ? DamageToEnemy : DamageToPlayer;
                
                // Передаем наш уже парящий нож в функцию удара
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

            Vector3 endPos;
            var provider = HealthSoundProvider.Instance;

            if (targetSide == TurnSide.Player)
            {
                if (provider != null && provider.playerTransform != null)
                    endPos = provider.playerTransform.position;
                else if (Camera.main != null)
                    endPos = Camera.main.transform.position + Camera.main.transform.forward * 0.6f - Camera.main.transform.up * 0.2f;
                else
                    endPos = knife.transform.position - Vector3.forward * 2f;
            }
            else
            {
                if (provider != null && provider.enemyTransform != null)
                    endPos = provider.enemyTransform.position + Vector3.up * 1.0f;
                else
                    endPos = knife.transform.position + Vector3.forward * 2f;
            }

            // Убиваем все анимации покачивания перед броском
            knife.transform.DOKill(); 
            knife.transform.LookAt(endPos); // Нацеливаем

            if (SpinKnife)
            {
                // Быстрое сальто по оси Х во время полета
                knife.transform.DORotate(new Vector3(360f * 3f, 0, 0), KnifeFlightDuration, RotateMode.LocalAxisAdd)
                    .SetEase(Ease.Linear);
            }

            // Сам полет до точки
            knife.transform.DOMove(endPos, KnifeFlightDuration)
                .SetEase(Ease.InCubic) 
                .OnComplete(() =>
                {
                    // Проигрываем звук попадания в игрока (если есть)
                    if (targetSide == TurnSide.Player && !PlayerStabSound.IsNull)
                    {
                        RuntimeManager.PlayOneShot(PlayerStabSound, endPos);
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