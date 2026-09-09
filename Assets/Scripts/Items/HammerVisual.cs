// START OF FILE HammerVisual.cs
using DG.Tweening;
using ShellGame.Core;
using ShellGame.Shells;
using UnityEngine;

namespace ShellGame.Items
{
    public class HammerVisual : MonoBehaviour
    {
        private Shell _hoveredShell;
        private bool _isStriking;
        
        [Header("Настройки тревоги")]
        public float ShakeIntensity = 15f;
        public float ShakeSpeed = 30f;

        private void OnEnable()
        {
            GameEvents.ShellHoverEnter += OnHoverEnter;
            GameEvents.ShellHoverExit += OnHoverExit;
        }

        private void OnDisable()
        {
            GameEvents.ShellHoverEnter -= OnHoverEnter;
            GameEvents.ShellHoverExit -= OnHoverExit;
        }

        private void OnHoverEnter(Shell shell) => _hoveredShell = shell;
        private void OnHoverExit(Shell shell)
        {
            if (_hoveredShell == shell) _hoveredShell = null;
        }

        private void Update()
        {
            if (_isStriking) return;

            // Определяем цель: либо наведенный наперсток, либо центр стола
            Vector3 targetPos = _hoveredShell != null 
                ? _hoveredShell.transform.position + Vector3.up * 0.7f 
                : transform.position; // Если нет ховера, висит где был

            // Плавное следование за курсором
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 8f);

            // Тревожная тряска (Jitter) в режиме ожидания (замах)
            float t = Time.time * ShakeSpeed;
            float noiseX = (Mathf.PerlinNoise(t, 0f) - 0.5f) * ShakeIntensity;
            float noiseY = (Mathf.PerlinNoise(0f, t) - 0.5f) * ShakeIntensity;
            float noiseZ = (Mathf.PerlinNoise(t, t) - 0.5f) * ShakeIntensity;

            // Базовый наклон (как будто держит в руке готовясь ударить) + тряска
            transform.rotation = Quaternion.Euler(30f + noiseX, noiseY, noiseZ);
        }

        public void Strike(Vector3 targetPos, System.Action onImpact)
        {
            _isStriking = true;
            
            // Замахиваемся чуть выше перед ударом
            Sequence seq = DOTween.Sequence();
            seq.Append(transform.DOMoveY(transform.position.y + 0.3f, 0.15f).SetEase(Ease.OutQuad));
            seq.Join(transform.DORotate(new Vector3(-45f, 0, 0), 0.15f)); // Откидываем назад
            
            // Резкий удар вниз
            seq.Append(transform.DOMove(targetPos, 0.1f).SetEase(Ease.InExpo));
            seq.Join(transform.DORotate(new Vector3(90f, 0, 0), 0.1f).SetEase(Ease.InExpo)); // Бьет "головкой" вниз
            
            seq.OnComplete(() =>
            {
                onImpact?.Invoke();
                Destroy(gameObject); // Исчезает после удара
            });
        }

        public void FlyToFace(Vector3 facePos, System.Action onImpact)
        {
            _isStriking = true;

            // Летит в лицо, кувыркаясь и трясясь
            transform.DORotate(new Vector3(360f * 3f, 180f, 0f), 0.4f, RotateMode.FastBeyond360).SetEase(Ease.InBack);
            transform.DOMove(facePos, 0.4f).SetEase(Ease.InBack).OnComplete(() =>
            {
                onImpact?.Invoke();
                Destroy(gameObject);
            });
        }
    }
}
// END OF FILE