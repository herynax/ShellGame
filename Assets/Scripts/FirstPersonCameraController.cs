using UnityEngine;
using UnityEngine.InputSystem; // Новая система ввода

/// <summary>
/// Контроллер для стационарной Cinemachine камеры.
/// Вращает саму виртуальную камеру, а Cinemachine уже передает это на Main Camera.
/// </summary>
public class CinemachineStationaryLook : MonoBehaviour
{
    [Header("Что вращаем?")]
    [Tooltip("Перетащите сюда вашу CinemachineCamera (виртуальную камеру) из иерархии")]
    public Transform cameraTransform;

    [Header("Чувствительность")]
    public float mouseSensitivity = 0.15f;
    
    [Header("Ограничения Вверх/Вниз (Pitch)")]
    public float minPitch = -60f; // Насколько высоко можно задрать голову
    public float maxPitch = 60f;  // Насколько низко можно опустить голову

    [Header("Ограничения Влево/Вправо (Yaw)")]
    public float minYaw = -90f;   // Насколько сильно можно повернуться влево
    public float maxYaw = 90f;    // Насколько сильно можно повернуться вправо

    [Header("Плавность (сглаживание)")]
    [Tooltip("Чем меньше значение, тем быстрее камера 'догоняет' курсор. 0 = мгновенно")]
    public float lookSmoothTime = 0.05f;

    [Header("Инверсия мыши")]
    public bool invertY = false;

    // Внутренние переменные
    private float targetYaw;
    private float targetPitch;
    private float currentYaw;
    private float currentPitch;

    private float yawVelocity;   
    private float pitchVelocity; 

    // Запоминаем стартовый поворот виртуальной камеры
    private Quaternion startRotation;

    void Start()
    {
        // Прячем и блокируем курсор
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Если забыли назначить камеру в инспекторе, скрипт попытается вращать сам себя
        if (cameraTransform == null) cameraTransform = transform;

        // Запоминаем изначальный поворот виртуальной камеры
        startRotation = cameraTransform.localRotation;
    }

    void Update()
    {
        HandleLook();
    }

    private void HandleLook()
    {
        if (Mouse.current == null || cameraTransform == null) return;

        // Получаем движение мыши
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        float yawInput = mouseDelta.x * mouseSensitivity;
        float pitchInput = mouseDelta.y * mouseSensitivity * (invertY ? 1f : -1f);

        // Прибавляем ввод к целевым углам
        targetYaw += yawInput;
        targetPitch += pitchInput;

        // ОГРАНИЧИВАЕМ УГЛЫ
        targetYaw = Mathf.Clamp(targetYaw, minYaw, maxYaw);     // Влево / Вправо
        targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch); // Вверх / Вниз

        // Сглаживаем
        if (lookSmoothTime > 0f)
        {
            currentYaw = Mathf.SmoothDamp(currentYaw, targetYaw, ref yawVelocity, lookSmoothTime);
            currentPitch = Mathf.SmoothDamp(currentPitch, targetPitch, ref pitchVelocity, lookSmoothTime);
        }
        else
        {
            currentYaw = targetYaw;
            currentPitch = targetPitch;
        }

        // Вращаем виртуальную камеру относительно её стартовой позиции
        cameraTransform.localRotation = startRotation * Quaternion.Euler(currentPitch, currentYaw, 0f);
    }

    void OnDisable()
    {
        // Возвращаем курсор при выключении
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}