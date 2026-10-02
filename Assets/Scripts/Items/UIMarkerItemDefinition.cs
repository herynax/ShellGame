using UnityEngine;

namespace ShellGame.Items
{
    /// <summary>
    /// Конкретный ItemDefinition для UI-маркеров (продажа, выход и т.д.).
    /// Не является реальным предметом и никогда не может быть использован.
    /// </summary>
    [CreateAssetMenu(fileName = "UIMarkerItem", menuName = "ShellGame/Items/UI Marker Item")]
    public class UIMarkerItemDefinition : ItemDefinition
    {
        public override bool Apply(ItemEffectContext context) => false;

        public override bool CanUse(ItemEffectContext context) => false;

        public override float EvaluateEnemyDesire(ItemEffectContext context) => 0f;
    }
}