using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;
using ShellGame.Core;
using ShellGame.Gameplay;
using ShellGame.Health; // Добавлено для доступа к HealthController

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    public static event Action<float> ScreenGoingBlack;
    public static event Action ScreenFullyBlack;
    public static event Action<float> ScreenRevealing;

    [Header("Настройки фейда")]
    public CanvasGroup fadeCanvasGroup;
    public float fadeDuration = 0.5f;

    [Header("Опции")]
    public float delayBeforeFadeOut = 0.3f;
    public bool blockInputDuringLoad = true;

    [Header("Смерть (Переход)")]
    public bool loadNextSceneByName = true; 
    public string nextSceneOnEnemyDeath;
    public string firstSceneOnPlayerDeath = "Tutorial";
    public string roomLightTag = "RoomLight";
    public float roomDarkenDuration = 1.5f;
    public float canvasFadeInTriggerIntensity = 2f;

    private Canvas fadeCanvas;
    private bool isLoading = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (fadeCanvasGroup == null) fadeCanvasGroup = GetComponentInChildren<CanvasGroup>();
        
        fadeCanvas = GetComponentInChildren<Canvas>();
        if (fadeCanvas != null) fadeCanvas.sortingOrder = 9999;
    }

    private void Start()
    {
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }
    }

    private void OnEnable() => GameEvents.SideDied += HandleSideDied;
    private void OnDisable() => GameEvents.SideDied -= HandleSideDied;

    private void HandleSideDied(TurnSide side)
    {
        if (isLoading) return;
        
        // Запускаем единый сценарий смерти для любой из сторон
        StartCoroutine(UnifiedDeathRoutine(side));
    }

    /// <summary>
    /// Единая логика смерти:
    /// 1. Плавно тушит свет (если есть) и делает Fade In черного экрана
    /// 2. Ждет полного окончания визуального затемнения
    /// 3. Ждет окончания звука смерти в FMOD (через HealthController)
    /// 4. Ждет 0.15 секунд
    /// 5. Загружает нужную сцену (рестарт для игрока, Next Scene для врага)
    /// 6. Делает Fade Out (показывает новую сцену)
    /// </summary>
    private IEnumerator UnifiedDeathRoutine(TurnSide deadSide)
    {
        isLoading = true;

        if (fadeCanvasGroup == null)
        {
            Debug.LogError("fadeCanvasGroup не назначена в SceneLoader!");
            isLoading = false;
            yield break;
        }

        if (blockInputDuringLoad) fadeCanvasGroup.blocksRaycasts = true;

        // --- ШАГ 1: ВИЗУАЛЬНОЕ ЗАТЕМНЕНИЕ (СВЕТ + КАНВАС) ---
        Tween canvasFadeTween = null;
        Light roomLight = FindRoomLight();

        Debug.Log($"[SceneLoader] Начинаем затемнение экрана (Смерть: {deadSide})...");

        FMODUnity.RuntimeManager.StudioSystem.setParameterByName("DoseCounter", 0f);
        ScreenGoingBlack?.Invoke(fadeDuration);
        canvasFadeTween = fadeCanvasGroup.DOFade(1f, fadeDuration).SetUpdate(true);

        if (roomLight != null)
        {
            Tween lightTween = roomLight.DOIntensity(0f, roomDarkenDuration).SetUpdate(true);
            yield return lightTween.WaitForCompletion();
        }

        if (canvasFadeTween != null && canvasFadeTween.IsActive())
        {
            yield return canvasFadeTween.WaitForCompletion();
        }

        ScreenFullyBlack?.Invoke();

        // --- ШАГ 3: КОРОТКАЯ ПАУЗА 0.15 СЕКУНД ---
        Debug.Log("[SceneLoader] Звук завершён. Ждём 0.25 сек...");
        yield return new WaitForSecondsRealtime(0.25f);
        

        // --- ШАГ 4: ЗАГРУЗКА НУЖНОЙ СЦЕНЫ ---
        AsyncOperation asyncLoad = null;

        if (deadSide == TurnSide.Player)
        {
            // Смерть игрока -> возврат на стартовую сцену
            string targetScene = string.IsNullOrEmpty(firstSceneOnPlayerDeath)
                ? "Tutorial"
                : firstSceneOnPlayerDeath;

            Debug.Log($"[SceneLoader] Возвращаемся на первую сцену: {targetScene}");
            asyncLoad = SceneManager.LoadSceneAsync(targetScene);
        }
        else
        {
            // Смерть врага -> Загрузка следующей сцены
            EnsureSessionProgression().AdvanceToNextLevel();

            if (loadNextSceneByName)
            {
                Debug.Log($"[SceneLoader] Загружаем следующую сцену по имени: {nextSceneOnEnemyDeath}");
                asyncLoad = SceneManager.LoadSceneAsync(nextSceneOnEnemyDeath);
            }
            else
            {
                int nextBuildIndex = SceneManager.GetActiveScene().buildIndex + 1;
                if (nextBuildIndex >= SceneManager.sceneCountInBuildSettings) nextBuildIndex = 0;
                
                Debug.Log($"[SceneLoader] Загружаем следующую сцену по индексу: {nextBuildIndex}");
                asyncLoad = SceneManager.LoadSceneAsync(nextBuildIndex);
            }
        }

        // Ждем пока сцена полностью загрузится
        while (asyncLoad != null && !asyncLoad.isDone)
        {
            yield return null;
        }

        // --- ШАГ 5: ФЕЙД АУТ (ПРОЯВЛЕНИЕ ЭКРАНА) ---
        Debug.Log($"[SceneLoader] Сцена загружена! Ждём перед Fade Out...");
        yield return new WaitForSecondsRealtime(delayBeforeFadeOut);

        Debug.Log($"[SceneLoader] Начинаем Fade Out...");
        ScreenRevealing?.Invoke(fadeDuration);
        
        yield return fadeCanvasGroup.DOFade(0f, fadeDuration)
            .SetUpdate(true)
            .WaitForCompletion();

        Debug.Log($"[SceneLoader] Переход завершен!");
        fadeCanvasGroup.blocksRaycasts = false;
        isLoading = false;
    }

    // ==========================================
    // СТАРЫЕ МЕТОДЫ (Сохранены для внешних вызовов)
    // ==========================================
    
    public void LoadScene(string sceneName)
    {
        if (isLoading) return;
        StartCoroutine(LoadSceneRoutine(sceneName: sceneName));
    }

    public void LoadScene(int sceneIndex)
    {
        if (isLoading) return;
        StartCoroutine(LoadSceneRoutine(sceneIndex: sceneIndex));
    }

    private IEnumerator LoadSceneRoutine(string sceneName = "", int sceneIndex = -1)
    {
        isLoading = true;
        if (blockInputDuringLoad && fadeCanvasGroup != null) fadeCanvasGroup.blocksRaycasts = true;

        ScreenGoingBlack?.Invoke(fadeDuration);
        if (fadeCanvasGroup != null) yield return fadeCanvasGroup.DOFade(1f, fadeDuration).SetUpdate(true).WaitForCompletion();

        AsyncOperation asyncLoad = (!string.IsNullOrEmpty(sceneName)) ? SceneManager.LoadSceneAsync(sceneName) : SceneManager.LoadSceneAsync(sceneIndex);
        while (!asyncLoad.isDone) yield return null;

        ScreenFullyBlack?.Invoke();
        yield return new WaitForSecondsRealtime(delayBeforeFadeOut);

        ScreenRevealing?.Invoke(fadeDuration);
        if (fadeCanvasGroup != null)
        {
            yield return fadeCanvasGroup.DOFade(0f, fadeDuration).SetUpdate(true).WaitForCompletion();
            fadeCanvasGroup.blocksRaycasts = false;
        }
        isLoading = false;
    }

    private Light FindRoomLight()
    {
        if (string.IsNullOrEmpty(roomLightTag)) return null;
        GameObject tagged = GameObject.FindGameObjectWithTag(roomLightTag);
        return tagged != null ? tagged.GetComponent<Light>() : null;
    }

    private GameSessionProgression EnsureSessionProgression()
    {
        var progression = FindObjectOfType<GameSessionProgression>();
        if (progression != null)
            return progression;

        var progressionObject = new GameObject("GameSessionProgression");
        progression = progressionObject.AddComponent<GameSessionProgression>();
        return progression;
    }

    public void SetFadeAlpha(float alpha) { if (fadeCanvasGroup != null) fadeCanvasGroup.alpha = Mathf.Clamp01(alpha); }
    public void FadeInInstant() { if (fadeCanvasGroup != null) { fadeCanvasGroup.alpha = 1f; fadeCanvasGroup.blocksRaycasts = true; } }
    public void FadeOutInstant() { if (fadeCanvasGroup != null) { fadeCanvasGroup.alpha = 0f; fadeCanvasGroup.blocksRaycasts = false; } }
}