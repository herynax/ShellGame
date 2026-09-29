using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace SpankyBoy.JuiceUI.Free
{
    [RequireComponent(typeof(RectTransform))]
    public class PanelAnimator_Free : MonoBehaviour
    {
        public enum AnimationType
        {
            Fade,
            Slide,
            Scale,
            Pop
        }

        public enum Direction
        {
            Left,
            Right,
            Top,
            Bottom
        }

        [Header("Animation Settings")]
        public AnimationType inAnimation = AnimationType.Fade;
        public AnimationType outAnimation = AnimationType.Fade;
        public Direction inDirection = Direction.Bottom;
        public Direction outDirection = Direction.Top;

        [Header("Timing")]
        public float inDuration = 0.5f;
        public float outDuration = 0.4f;
        public float inDelay = 0f;
        public float outDelay = 0f;
        public Ease inEase = Ease.OutQuad;
        public Ease outEase = Ease.InQuad;

        [Header("Behavior")]
        public bool playInOnEnable = true;
        public bool disableOnOutComplete = true;
        public bool resetStateAfterOut = true;

        [Header("Audio")]
        public AudioClip inSound;
        public AudioClip outSound;
        public AudioSource customAudioSource;

        [Range(0f, 1f)]
        public float volume = 1.0f;

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;
        private Vector2 originalPosition;
        private Vector3 originalScale;
        private AudioSource audioSource;

        private Sequence currentSequence;

        // Версия анимации.
        // Каждый новый Show/Hide инвалидирует предыдущий callback.
        private int animationVersion;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();

            audioSource = customAudioSource
                ? customAudioSource
                : GetComponent<AudioSource>();

            originalPosition = rectTransform.anchoredPosition;
            originalScale = transform.localScale;

            canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void OnEnable()
        {
            if (playInOnEnable)
            {
                AnimateIn();
            }
        }

        // =========================================================
        // PUBLIC API
        // =========================================================

        public void Show()
        {
            AnimateIn();
        }

        public void Hide()
        {
            AnimateOut();
        }

        // =========================================================
        // SHOW
        // =========================================================

        public void AnimateIn()
        {
            // Новый Show всегда имеет приоритет.
            CancelCurrentAnimation();

            int version = ++animationVersion;

            PlaySound(inSound);

            PrepareInAnimation();
            ExecuteInAnimation(version);
        }

        private void PrepareInAnimation()
        {
            switch (inAnimation)
            {
                case AnimationType.Fade:
                    canvasGroup.alpha = 0f;
                    break;

                case AnimationType.Slide:
                    rectTransform.anchoredPosition =
                        GetOffscreenPosition(inDirection);

                    canvasGroup.alpha = 1f;
                    break;

                case AnimationType.Scale:
                case AnimationType.Pop:
                    transform.localScale = Vector3.zero;
                    canvasGroup.alpha = 1f;
                    break;
            }
        }

        private void ExecuteInAnimation(int version)
        {
            currentSequence = DOTween.Sequence();

            switch (inAnimation)
            {
                case AnimationType.Fade:
                    currentSequence.Append(
                        canvasGroup
                            .DOFade(1f, inDuration)
                            .SetEase(inEase)
                    );
                    break;

                case AnimationType.Slide:
                    currentSequence.Append(
                        rectTransform
                            .DOAnchorPos(originalPosition, inDuration)
                            .SetEase(inEase)
                    );
                    break;

                case AnimationType.Scale:
                    currentSequence.Append(
                        transform
                            .DOScale(originalScale, inDuration)
                            .SetEase(inEase)
                    );
                    break;

                case AnimationType.Pop:
                    currentSequence.Append(
                        transform
                            .DOScale(originalScale, inDuration)
                            .SetEase(Ease.OutBack)
                    );
                    break;
            }

            currentSequence
                .SetDelay(inDelay)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    // Этот callback относится к старой анимации?
                    // Тогда он больше ничего не имеет права делать.
                    if (version != animationVersion)
                        return;

currentSequence = null;

            // Гарантируем конечное состояние.
            ApplyVisibleState();
                });
        }

        // =========================================================
        // HIDE
        // =========================================================

        public void AnimateOut()
        {
            // Новый Hide всегда отменяет предыдущую анимацию.
            CancelCurrentAnimation();

            int version = ++animationVersion;

            PlaySound(outSound);

            ExecuteOutAnimation(version);
        }

        private void ExecuteOutAnimation(int version)
        {
            currentSequence = DOTween.Sequence();

            switch (outAnimation)
            {
                case AnimationType.Fade:
                    currentSequence.Append(
                        canvasGroup
                            .DOFade(0f, outDuration)
                            .SetEase(outEase)
                    );
                    break;

                case AnimationType.Slide:
                    currentSequence.Append(
                        rectTransform
                            .DOAnchorPos(
                                GetOffscreenPosition(outDirection),
                                outDuration
                            )
                            .SetEase(outEase)
                    );
                    break;

                case AnimationType.Scale:
                    currentSequence.Append(
                        transform
                            .DOScale(Vector3.zero, outDuration)
                            .SetEase(outEase)
                    );
                    break;

                case AnimationType.Pop:
                    currentSequence.Append(
                        transform
                            .DOScale(Vector3.zero, outDuration)
                            .SetEase(Ease.InBack)
                    );
                    break;
            }

            currentSequence
                .SetDelay(outDelay)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    // Старый Hide больше не имеет права
                    // менять состояние объекта.
                    if (version != animationVersion)
                        return;

currentSequence = null;

            if (resetStateAfterOut)
                    {
                        ApplyResetState();
                    }

                    if (disableOnOutComplete)
                    {
                        gameObject.SetActive(false);
                    }
                });
        }

        // =========================================================
        // INTERRUPTION
        // =========================================================

        private void CancelCurrentAnimation()
        {
            // Инвалидируем все старые callbacks.
            animationVersion++;

            if (currentSequence != null)
            {
                currentSequence.Kill(false);
                currentSequence = null;
            }

            // На случай, если tween был создан напрямую
            // на компоненте, а не только внутри Sequence.
            rectTransform.DOKill(false);
            transform.DOKill(false);
            canvasGroup.DOKill(false);
        }

        // =========================================================
        // STATES
        // =========================================================

        private void ApplyVisibleState()
        {
            canvasGroup.alpha = 1f;
            rectTransform.anchoredPosition = originalPosition;
            transform.localScale = originalScale;

            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        private void ApplyResetState()
        {
            rectTransform.anchoredPosition = originalPosition;
            transform.localScale = originalScale;
            canvasGroup.alpha = 1f;

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        // =========================================================
        // POSITION
        // =========================================================

        private Vector2 GetOffscreenPosition(Direction direction)
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();

            if (parentCanvas == null)
                return originalPosition;

            RectTransform canvasRect =
                parentCanvas.GetComponent<RectTransform>();

            float canvasWidth = canvasRect.rect.width;
            float canvasHeight = canvasRect.rect.height;

            float panelWidth = rectTransform.rect.width;
            float panelHeight = rectTransform.rect.height;

            Vector2 offscreenPos = originalPosition;

            switch (direction)
            {
                case Direction.Left:
                    offscreenPos = new Vector2(
                        -canvasWidth / 2f - panelWidth / 2f,
                        originalPosition.y
                    );
                    break;

                case Direction.Right:
                    offscreenPos = new Vector2(
                        canvasWidth / 2f + panelWidth / 2f,
                        originalPosition.y
                    );
                    break;

                case Direction.Top:
                    offscreenPos = new Vector2(
                        originalPosition.x,
                        canvasHeight / 2f + panelHeight / 2f
                    );
                    break;

                case Direction.Bottom:
                    offscreenPos = new Vector2(
                        originalPosition.x,
                        -canvasHeight / 2f - panelHeight / 2f
                    );
                    break;
            }

            return offscreenPos;
        }

        // =========================================================
        // AUDIO
        // =========================================================

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
        }
    }
}
