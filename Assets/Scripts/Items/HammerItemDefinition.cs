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

        public override bool CanUse(ItemEffectContext context)
        {
            // Молоток может юзать ТОЛЬКО ИГРОК 
            if (context?.ActiveShells == null || context.UserSide != TurnSide.Player) return false;
            return context.CanUsePlayerHammer?.Invoke() ?? false;
        }

        public override bool Apply(ItemEffectContext context)
        {
            if (!CanUse(context) || context.BeginHammerAttack == null) return false;

            HammerVisual activeHammer = null;

            if (HammerPrefab != null)
            {
                GameObject obj = Instantiate(HammerPrefab, context.ItemWorldPosition, Quaternion.identity);
                activeHammer = obj.AddComponent<HammerVisual>();
            }

            context.BeginHammerAttack(RevealDuration, targetShell =>
            {
                if (targetShell.HasMarker)
                {
                    // Ошибка: там метка! Поднимаем наперсток, чтобы игрок увидел метку.
                    targetShell.RevealMarker(RevealDuration);

                    // Берем позицию из Анкоров (или просто перед камерой, если не настроено)
                    Vector3 facePos = ItemVisualAnchors.Instance != null && ItemVisualAnchors.Instance.PlayerHitPoint != null 
                        ? ItemVisualAnchors.Instance.PlayerHitPoint.position 
                        : (Camera.main != null ? Camera.main.transform.position : targetShell.transform.position - Vector3.forward * 2f);
                    
                    if (activeHammer != null)
                    {
                        activeHammer.FlyToFace(facePos, () =>
                        {
                            if (!HitFaceSound.IsNull) RuntimeManager.PlayOneShot(HitFaceSound, facePos);
                            if (HitParticlesPrefab != null)
                                Instantiate(HitParticlesPrefab, facePos, Quaternion.identity);
                            context.Health.ApplyDamage(TurnSide.Player, DamageToPlayer);
                        });
                    }
                    else
                    {
                        context.Health.ApplyDamage(TurnSide.Player, DamageToPlayer);
                    }
                }
                else
                {
                    // Успех: наперсток пустой! Разбиваем его прямо на столе (без поднятия).
                    if (activeHammer != null)
                    {
                        activeHammer.Strike(targetShell.transform.position, () => 
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

            return true;
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

        public override float EvaluateEnemyDesire(ItemEffectContext context) => 0f;
    }
}
// END OF FILE