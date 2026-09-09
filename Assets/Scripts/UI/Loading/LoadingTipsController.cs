// START OF FILE LoadingTipsController.cs
using TMPro;
using UnityEngine;
using UnityEngine.UI; // Обязательно для работы с Image
using DG.Tweening;

namespace ShellGame.UI
{
    public class LoadingTipsController : MonoBehaviour
    {
        [Header("Ссылки")]
        [SerializeField] private CanvasGroup tipCanvasGroup;
        [SerializeField] private TMP_Text tipText;
        [SerializeField] private Image tipImage; // Ссылка на картинку UI
        [SerializeField] private LoadingTipsConfig tipsConfig;

        [Header("Тайминги")]
        [SerializeField] private float delayBeforeTipShow = 0.1f;
        [SerializeField] private float tipFadeInDuration = 0.3f;
        [SerializeField] private float tipFadeOutDuration = 0.25f;

        private Tween activeTween;
        private int lastTipIndex = -1;
        private int lastImageIndex = -1; // Чтобы картинки не повторялись подряд

        private void Awake()
        {
            if (tipCanvasGroup != null)
            {
                tipCanvasGroup.alpha = 0f;
                tipCanvasGroup.blocksRaycasts = false;
                tipCanvasGroup.interactable = false;
            }
        }

        private void OnEnable()
        {
            SceneLoader.LoadingScreenShown += HandleLoadingScreenShown;
            SceneLoader.ScreenRevealing += HandleScreenRevealing;
        }

        private void OnDisable()
        {
            SceneLoader.LoadingScreenShown -= HandleLoadingScreenShown;
            SceneLoader.ScreenRevealing -= HandleScreenRevealing;
            activeTween?.Kill();
        }

        private void HandleLoadingScreenShown()
        {
            if (tipCanvasGroup == null) return;

            // Настраиваем случайный текст
            if (tipText != null)
            {
                tipText.text = PickRandomTip();
            }

            // Настраиваем случайную картинку
            if (tipImage != null)
            {
                Sprite randomSprite = PickRandomImage();
                if (randomSprite != null)
                {
                    tipImage.sprite = randomSprite;
                    tipImage.gameObject.SetActive(true);
                }
                else
                {
                    tipImage.gameObject.SetActive(false); // Прячем Image, если картинок в конфиге нет
                }
            }

            activeTween?.Kill();
            tipCanvasGroup.alpha = 0f;

            activeTween = DOVirtual.DelayedCall(delayBeforeTipShow, () =>
            {
                activeTween = tipCanvasGroup.DOFade(1f, tipFadeInDuration).SetUpdate(true);
            }).SetUpdate(true);
        }

        private void HandleScreenRevealing(float fadeDuration)
        {
            if (tipCanvasGroup == null) return;

            activeTween?.Kill();
            activeTween = tipCanvasGroup
                .DOFade(0f, tipFadeOutDuration)
                .SetUpdate(true);
        }

        private string PickRandomTip()
        {
            if (tipsConfig == null || tipsConfig.tips == null || tipsConfig.tips.Length == 0)
                return string.Empty;

            if (tipsConfig.tips.Length == 1)
                return tipsConfig.tips[0];

            int index;
            do
            {
                index = Random.Range(0, tipsConfig.tips.Length);
            } while (index == lastTipIndex);

            lastTipIndex = index;
            return tipsConfig.tips[index];
        }

        private Sprite PickRandomImage()
        {
            if (tipsConfig == null || tipsConfig.images == null || tipsConfig.images.Length == 0)
                return null;

            if (tipsConfig.images.Length == 1)
                return tipsConfig.images[0];

            int index;
            int safetyGuard = 10;
            do
            {
                index = Random.Range(0, tipsConfig.images.Length);
                safetyGuard--;
            } while (index == lastImageIndex && safetyGuard > 0);

            lastImageIndex = index;
            return tipsConfig.images[index];
        }
    }
}
// END OF FILE