using System.Collections;
using ShellGame.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

public class MainMenu : MonoBehaviour
{
    public void ExitGame()
    {
        StartCoroutine(ExitGameWithFade());
    }

    private IEnumerator ExitGameWithFade()
    {
        // Получаем CanvasGroup из SceneLoader для фейд-эффекта
        if (SceneLoader.Instance != null && SceneLoader.Instance.fadeCanvasGroup != null)
        {
            CanvasGroup fadeCanvas = SceneLoader.Instance.fadeCanvasGroup;
            float fadeDuration = SceneLoader.Instance.fadeDuration;

            Debug.Log("Экран темнеет перед выходом...");

            // Фейдим музыку в 0 параллельно с фейдом канваса в чёрное —
            // обе анимации идут одну и ту же fadeDuration, поэтому
            // заканчиваются синхронно.
            if (MusicManager.Instance != null)
                MusicManager.Instance.FadeOutMusic(fadeDuration);

            // Фейдим в чёрный экран
            yield return fadeCanvas.DOFade(1f, fadeDuration)
                .SetUpdate(true)
                .WaitForCompletion();

            Debug.Log("Экран полностью затемнел. Выходим из игры...");
        }

        // Закрываем приложение
        Application.Quit();

        // На случай, если Application.Quit() не сработает в редакторе
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}