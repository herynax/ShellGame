using DG.Tweening;
using ShellGame.Items;
using Unity.Cinemachine;
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
    private Transform coinTarget;
    private Quaternion rotationBeforeCoin;
    private CinemachineCamera coinCamera;
    private float fovBeforeCoin;
    private Tween fovTween;

    private void OnEnable()
    {
        CoinFlipVisual.TossStarted += OnCoinTossStarted;
        CoinFlipVisual.TossLanded += OnCoinTossLanded;
    }

    private void OnDisable()
    {
        CoinFlipVisual.TossStarted -= OnCoinTossStarted;
        CoinFlipVisual.TossLanded -= OnCoinTossLanded;
        RestorePlayerControl();
    }
    
    void Start()
    {
        if (cameraTransform == null) cameraTransform = transform;
        startRotation = cameraTransform.localRotation;
    }
    void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (coinTarget != null)
            return;

        HandleLook();
    }

    private void LateUpdate()
    {
        if (coinTarget == null || cameraTransform == null)
            return;

        Vector3 direction = coinTarget.position - cameraTransform.position;
        if (direction.sqrMagnitude > 0.0001f)
            cameraTransform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    private void OnCoinTossStarted(Transform target, float zoomFov)
    {
        if (target == null || cameraTransform == null)
            return;

        RestorePlayerControl();
        coinTarget = target;
        rotationBeforeCoin = cameraTransform.localRotation;

        var brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
        coinCamera = brain != null ? brain.ActiveVirtualCamera as CinemachineCamera : null;
        if (coinCamera == null || zoomFov <= 0f)
            return;

        fovBeforeCoin = coinCamera.Lens.FieldOfView;
        fovTween = DOTween.To(
            () => coinCamera != null ? coinCamera.Lens.FieldOfView : fovBeforeCoin,
            SetCoinCameraFov,
            Mathf.Max(1f, fovBeforeCoin - zoomFov),
            0.35f).SetEase(Ease.OutQuad);
    }

    private void OnCoinTossLanded(Transform target)
    {
        if (target == coinTarget)
            RestorePlayerControl();
    }

    private void SetCoinCameraFov(float value)
    {
        if (coinCamera == null) return;
        var lens = coinCamera.Lens;
        lens.FieldOfView = value;
        coinCamera.Lens = lens;
    }

    private void RestorePlayerControl()
    {
        bool wasFollowingCoin = coinTarget != null;
        coinTarget = null;

        if (wasFollowingCoin && cameraTransform != null)
            cameraTransform.localRotation = rotationBeforeCoin;

        fovTween?.Kill();
        fovTween = null;

        if (coinCamera != null && fovBeforeCoin > 0f)
        {
            var cameraToRestore = coinCamera;
            fovTween = DOTween.To(
                () => cameraToRestore != null ? cameraToRestore.Lens.FieldOfView : fovBeforeCoin,
                value =>
                {
                    if (cameraToRestore == null) return;
                    var lens = cameraToRestore.Lens;
                    lens.FieldOfView = value;
                    cameraToRestore.Lens = lens;
                },
                fovBeforeCoin,
                0.3f).SetEase(Ease.OutQuad);
        }

        coinCamera = null;
        fovBeforeCoin = 0f;
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