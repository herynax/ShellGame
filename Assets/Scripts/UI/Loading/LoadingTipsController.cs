using TMPro;
using UnityEngine;
using DG.Tweening;

namespace ShellGame.UI
{
    public class LoadingTipsController : MonoBehaviour
    {
        [Header("Ссылки")]
        [SerializeField] private CanvasGroup tipCanvasGroup;
        [SerializeField] private TMP_Text tipText;
        [SerializeField] private LoadingTipsConfig tipsConfig;

        [Header("Тайминги")]
        [SerializeField] private float delayBeforeTipShow = 0.1f;
        [SerializeField] private float tipFadeInDuration = 0.3f;
        [SerializeField] private float tipFadeOutDuration = 0.25f;

        private Tween activeTween;
        private int lastTipIndex = -1;

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
            SceneLoader.ScreenFullyBlack += HandleScreenFullyBlack;
            SceneLoader.ScreenRevealing += HandleScreenRevealing;
        }

        private void OnDisable()
        {
            SceneLoader.ScreenFullyBlack -= HandleScreenFullyBlack;
            SceneLoader.ScreenRevealing -= HandleScreenRevealing;
            activeTween?.Kill();
        }

        private void HandleScreenFullyBlack()
        {
            if (tipCanvasGroup == null || tipText == null) return;

            tipText.text = PickRandomTip();
            if (string.IsNullOrEmpty(tipText.text)) return;

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
    }
}