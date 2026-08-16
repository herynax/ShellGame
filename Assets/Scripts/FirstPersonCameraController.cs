using UnityEngine;
using UnityEngine.InputSystem;

public class CinemachineStationaryLook : MonoBehaviour
{
    [Header("Что вращаем?")]
    public Transform cameraTransform;

    [Header("Чувствительность")]
    public float mouseSensitivity = 0.15f;
    
    [Header("Ограничения Вверх/Вниз (Pitch)")]
    public float minPitch = -60f; 
    public float maxPitch = 60f;  

    [Header("Ограничения Влево/Вправо (Yaw)")]
    public float minYaw = -90f;   
    public float maxYaw = 90f;    

    [Header("Плавность")]
    public float lookSmoothTime = 0.05f;
    public bool invertY = false;

    private float targetYaw;
    private float targetPitch;
    private float currentYaw;
    private float currentPitch;
    private float yawVelocity;   
    private float pitchVelocity; 
    private Quaternion startRotation;

    void Start()
    {
        if (cameraTransform == null) cameraTransform = transform;
        startRotation = cameraTransform.localRotation;
    }

    void Update()
    {
        // Курсор лочим централизованно (чтобы скрипты не дрались)
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        HandleLook();
    }

    private void HandleLook()
    {
        if (Mouse.current == null || cameraTransform == null) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        float yawInput = mouseDelta.x * mouseSensitivity;
        float pitchInput = mouseDelta.y * mouseSensitivity * (invertY ? 1f : -1f);

        targetYaw = Mathf.Clamp(targetYaw + yawInput, minYaw, maxYaw);
        targetPitch = Mathf.Clamp(targetPitch + pitchInput, minPitch, maxPitch); 

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

        cameraTransform.localRotation = startRotation * Quaternion.Euler(currentPitch, currentYaw, 0f);
    }
}