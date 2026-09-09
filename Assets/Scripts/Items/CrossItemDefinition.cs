// START OF FILE CrossItemDefinition.cs
using UnityEngine;
using ShellGame.Core;
using ShellGame.Health;
using FMODUnity;

namespace ShellGame.Items
{
    [CreateAssetMenu(fileName = "CrossItem", menuName = "ShellGame/Items/Cross Item")]
    public sealed class CrossItemDefinition : ItemDefinition
    {
        [Header("Настройки Креста")]
        public GameObject FloatingCrossPrefab;
        [Tooltip("Префаб частиц при разрушении креста")]
        public GameObject ShieldBreakParticlesPrefab;
        public EventReference ShieldBreakSound;

        public override bool CanUse(ItemEffectContext context)
        {
            if (context?.Health == null) return false;
            return !context.Health.HasShield(context.UserSide);
        }

        public override bool Apply(ItemEffectContext context)
        {
            if (!CanUse(context)) return false;

            context.Health.AddShield(context.UserSide);

            if (FloatingCrossPrefab != null)
            {
                // По умолчанию спавним над столом
                Vector3 anchorPos = context.ItemWorldPosition + Vector3.up * 1f;

                // ЧИТАЕМ ИЗ ПОИНТОВ
                if (ItemVisualAnchors.Instance != null)
                {
                    Transform anchor = context.UserSide == TurnSide.Player 
                        ? ItemVisualAnchors.Instance.PlayerCrossHoverPoint 
                        : ItemVisualAnchors.Instance.EnemyCrossHoverPoint;

                    if (anchor != null)
                    {
                        anchorPos = anchor.position;
                    }
                }

                GameObject crossObj = Instantiate(FloatingCrossPrefab, anchorPos, Quaternion.identity);
                var visual = crossObj.AddComponent<CrossVisual>();
                visual.Initialize(context.UserSide, ShieldBreakSound, ShieldBreakParticlesPrefab);
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
            float doseFraction = context.Health.GetDoseFraction(context.UserSide);
            return Mathf.Clamp01(0.6f + doseFraction * 0.4f); 
        }

        public override string GetEnemyUseAnnouncement() => "Враг использовал предмет: Деревянный Крест (защита от урона)!";
    }
}
// END OF FILE