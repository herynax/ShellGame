using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace ShellGame.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class ProgressBarController : MonoBehaviour
    {
        [Header("Ссылки")]
        [Tooltip("Статичная подложка прогресс-бара (Sliced Image, фон).")]
        [SerializeField] private Image backgroundImage;

        [Tooltip("Контейнер с компонентом RectMask2D — его ширину мы анимируем от 0 до fullWidth.")]
        [SerializeField] private RectTransform fillMaskRect;

        [Tooltip("Sliced Image с фиксированной шириной (fullWidth), лежит внутри fillMaskRect и обрезается маской.")]
        [SerializeField] private RectTransform fillImageRect;

        [SerializeField] private CanvasGroup barCanvasGroup;

        [Header("Ширина")]
        [Tooltip("Если 0 — ширина берётся автоматически из backgroundImage в Awake.")]
        [SerializeField] private float fullWidthOverride = 0f;

        [Header("Сглаживание")]
        [SerializeField] private float smoothDuration = 0.25f;

        private float fullWidth;
        private Tween fillTween;
        private float targetProgress;

        private void Awake()
        {
            fullWidth = fullWidthOverride > 0f
                ? fullWidthOverride
                : backgroundImage.rectTransform.rect.width;

            // FillImage всегда имеет полную ширину бара — двигать/сжимать его нельзя,
            // иначе 9-slice поедет. Обрезка идёт исключительно через RectMask2D родителя.
            if (fillImageRect != null)
            {
                Vector2 size = fillImageRect.sizeDelta;
                size.x = fullWidth;
                fillImageRect.sizeDelta = size;
            }

            SetMaskWidth(0f);
            targetProgress = 0f;

            if (barCanvasGroup != null)
            {
                barCanvasGroup.alpha = 0f;
                barCanvasGroup.blocksRaycasts = false;
                barCanvasGroup.interactable = false;
            }
        }

        private void OnEnable()
        {
            SceneLoader.LoadingScreenShown += HandleLoadingScreenShown;
            SceneLoader.LoadProgressChanged += HandleProgressChanged;
            SceneLoader.ScreenRevealing += HandleScreenRevealing;
        }

        private void OnDisable()
        {
            SceneLoader.LoadingScreenShown -= HandleLoadingScreenShown;
            SceneLoader.LoadProgressChanged -= HandleProgressChanged;
            SceneLoader.ScreenRevealing -= HandleScreenRevealing;
            fillTween?.Kill();
        }

        private void HandleLoadingScreenShown()
        {
            fillTween?.Kill();
            SetMaskWidth(0f);
            targetProgress = 0f;

            if (barCanvasGroup != null)
                barCanvasGroup.DOFade(1f, 0.2f).SetUpdate(true);
        }

        private void HandleProgressChanged(float normalizedProgress)
        {
            targetProgress = Mathf.Clamp01(normalizedProgress);

            if (fillMaskRect == null) return;

            fillTween?.Kill();
            fillTween = DOTween.To(
                    () => fillMaskRect.sizeDelta.x,
                    SetMaskWidth,
                    fullWidth * targetProgress,
                    smoothDuration)
                .SetUpdate(true)
                .SetEase(Ease.OutQuad);
        }

        private void HandleScreenRevealing(float fadeDuration)
        {
            if (barCanvasGroup == null) return;

            fillTween?.Kill();
            SetMaskWidth(fullWidth);

            barCanvasGroup.DOFade(0f, fadeDuration * 0.5f).SetUpdate(true);
        }

        private void SetMaskWidth(float width)
        {
            if (fillMaskRect == null) return;

            Vector2 size = fillMaskRect.sizeDelta;
            size.x = width;
            fillMaskRect.sizeDelta = size;
        }
    }
}