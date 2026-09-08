// START OF FILE CrossItemDefinition.cs
using UnityEngine;
using ShellGame.Core;
using ShellGame.Health;
using FMODUnity;

namespace ShellGame.Items
{
    /// <summary>
    /// Предмет "Деревянный крест" (Святая Мантия).
    /// Отменяет следующий полученный урон. Не стакается.
    /// </summary>
    [CreateAssetMenu(fileName = "CrossItem", menuName = "ShellGame/Items/Cross Item")]
    public sealed class CrossItemDefinition : ItemDefinition
    {
        [Header("Настройки Креста")]
        [Tooltip("Префаб креста, который будет парить над персонажем")]
        public GameObject FloatingCrossPrefab;
        [Tooltip("Звук при разбивании креста (когда блокируется урон)")]
        public EventReference ShieldBreakSound;

        public override bool CanUse(ItemEffectContext context)
        {
            if (context?.Health == null) return false;
            
            // Крест можно использовать ТОЛЬКО если щита сейчас нет (они не стакаются)
            return !context.Health.HasShield(context.UserSide);
        }

        public override bool Apply(ItemEffectContext context)
        {
            if (!CanUse(context)) return false;

            // 1. Активируем логический щит
            context.Health.AddShield(context.UserSide);

            // 2. Создаем визуальный крест
            if (FloatingCrossPrefab != null)
            {
                Vector3 anchorPos = context.ItemWorldPosition;
                var provider = HealthSoundProvider.Instance;

                // Находим позицию, чтобы повесить крест над головой Игрока/Врага
                if (context.UserSide == TurnSide.Player)
                {
                    if (provider != null && provider.playerTransform != null)
                        anchorPos = provider.playerTransform.position + Vector3.up * 1.0f;
                    else if (Camera.main != null)
                        anchorPos = Camera.main.transform.position + Camera.main.transform.forward * 1.5f + Vector3.up * 0.4f;
                }
                else
                {
                    if (provider != null && provider.enemyTransform != null)
                        anchorPos = provider.enemyTransform.position + Vector3.up * 1.8f;
                    else
                        anchorPos = context.ItemWorldPosition + Vector3.up * 2f;
                }

                // Спавним, вешаем наш скрипт-контроллер и инициализируем
                GameObject crossObj = Instantiate(FloatingCrossPrefab, anchorPos, Quaternion.identity);
                var visual = crossObj.AddComponent<CrossVisual>();
                visual.Initialize(context.UserSide, ShieldBreakSound);
            }

            return true;
        }

        public override void ShowPlayerUseFeedback(ItemUseMessageView messageView)
        {
            messageView?.ShowMessage("Святая защита! Следующий урон будет отменен.");
        }

        public override float EvaluateEnemyDesire(ItemEffectContext context)
        {
            if (!CanUse(context) || context.Health == null) return 0f;
            
            // Защита всегда полезна. Но чем меньше ХП (выше доза), тем отчаяннее враг хочет её прожать.
            float doseFraction = context.Health.GetDoseFraction(context.UserSide);
            return Mathf.Clamp01(0.6f + doseFraction * 0.4f); 
        }

        public override string GetEnemyUseAnnouncement() => "Враг использовал предмет: Деревянный Крест (защита от урона)!";
    }
}
// END OF FILE