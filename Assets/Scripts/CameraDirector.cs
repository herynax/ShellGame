using UnityEngine;
using Unity.Cinemachine; // Если старая версия: using Cinemachine;

public class CameraDirector : MonoBehaviour
{
    public enum ViewState { Main, Left, Right, Top }
    private enum ScreenEdge { None, Left, Right, Top, Bottom }

    [Header("Настройки времени и зон")]
    public float timeToSwitch = 1.0f;
    public float edgeMargin = 50f;

    [Header("Камеры Cinemachine")]
    public CinemachineCamera mainCamera;
    public CinemachineCamera leftCamera;
    public CinemachineCamera rightCamera;
    public CinemachineCamera topCamera;

    [Header("Управление анимацией")]
    [Tooltip("Перетащи сюда свою обычную Main Camera, на которой висит CinemachineBrain")]
    public CinemachineBrain cinemachineBrain;

    [Header("Настройки Паузы")]
    public bool pauseOnLeft = false;
    public bool pauseOnRight = false;
    public bool pauseOnTop = true;

    // Внутренние переменные
    private ViewState currentState = ViewState.Main;
    private float hoverTimer = 0f;

    void Start()
    {
        // Если забыли прикрепить CinemachineBrain в инспекторе, скрипт найдет его сам
        if (cinemachineBrain == null)
        {
            cinemachineBrain = Camera.main.GetComponent<CinemachineBrain>();
        }

        SwitchToView(ViewState.Main);
        Time.timeScale = 1f;
    }

    void Update()
    {
        // ПРОВЕРКА НА АНИМАЦИЮ: 
        // Если Синемашина прямо сейчас меняет камеры, мы сбрасываем таймер 
        // и вообще выходим из Update (return). Ждем окончания анимации!
        if (cinemachineBrain != null && cinemachineBrain.IsBlending)
        {
            hoverTimer = 0f;
            return;
        }

        ScreenEdge edge = GetMouseEdge();

        // Проверяем, является ли край правильным для текущего состояния
        if (IsValidEdgeForTransition(edge))
        {
            hoverTimer += Time.unscaledDeltaTime;

            if (hoverTimer >= timeToSwitch)
            {
                ExecuteTransition(edge);
                hoverTimer = 0f;
            }
        }
        else
        {
            hoverTimer = 0f;
        }
    }

    private ScreenEdge GetMouseEdge()
    {
        Vector3 mousePos = Input.mousePosition;

        if (mousePos.x < 0 || mousePos.x > Screen.width || mousePos.y < 0 || mousePos.y > Screen.height)
            return ScreenEdge.None;

        if (mousePos.x <= edgeMargin) return ScreenEdge.Left;
        if (mousePos.x >= Screen.width - edgeMargin) return ScreenEdge.Right;
        if (mousePos.y >= Screen.height - edgeMargin) return ScreenEdge.Top;
        if (mousePos.y <= edgeMargin) return ScreenEdge.Bottom;

        return ScreenEdge.None;
    }

    private bool IsValidEdgeForTransition(ScreenEdge edge)
    {
        if (edge == ScreenEdge.None) return false;

        switch (currentState)
        {
            case ViewState.Main:
                return edge == ScreenEdge.Left || edge == ScreenEdge.Right || edge == ScreenEdge.Top;

            case ViewState.Left:
                return edge == ScreenEdge.Right;

            case ViewState.Right:
                return edge == ScreenEdge.Left;

            case ViewState.Top:
                return edge == ScreenEdge.Bottom;
        }
        return false;
    }

    private void ExecuteTransition(ScreenEdge edge)
    {
        if (currentState == ViewState.Main)
        {
            if (edge == ScreenEdge.Left) SwitchToView(ViewState.Left);
            else if (edge == ScreenEdge.Right) SwitchToView(ViewState.Right);
            else if (edge == ScreenEdge.Top) SwitchToView(ViewState.Top);
        }
        else
        {
            SwitchToView(ViewState.Main);
        }
    }

    private void SwitchToView(ViewState newState)
    {
        currentState = newState;

        mainCamera.Priority = 0;
        if (leftCamera) leftCamera.Priority = 0;
        if (rightCamera) rightCamera.Priority = 0;
        if (topCamera) topCamera.Priority = 0;

        switch (newState)
        {
            case ViewState.Main:
                mainCamera.Priority = 10;
                Time.timeScale = 1f;
                break;
            case ViewState.Left:
                if (leftCamera) leftCamera.Priority = 10;
                Time.timeScale = pauseOnLeft ? 0f : 1f;
                break;
            case ViewState.Right:
                if (rightCamera) rightCamera.Priority = 10;
                Time.timeScale = pauseOnRight ? 0f : 1f;
                break;
            case ViewState.Top:
                if (topCamera) topCamera.Priority = 10;
                Time.timeScale = pauseOnTop ? 0f : 1f;
                break;
        }
    }
}