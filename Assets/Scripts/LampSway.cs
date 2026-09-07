using UnityEngine;
using DG.Tweening;

public class LampSway : MonoBehaviour
{
    [Header("Углы Y")]
    [SerializeField] private float minAngle = -5f;
    [SerializeField] private float maxAngle = 5f;

    [Header("Скорость")]
    [SerializeField] private float minDuration = 4f;
    [SerializeField] private float maxDuration = 7f;

    [Header("Пауза")]
    [SerializeField] private float minPause = 0.1f;
    [SerializeField] private float maxPause = 1.5f;

    private float startX;
    private float startZ;

    private void Start()
    {
        // Запоминаем исходные значения
        startX = transform.localEulerAngles.x;
        startZ = transform.localEulerAngles.z;

        Sway();
    }

    private void Sway()
    {
        float targetY = Random.Range(minAngle, maxAngle);
        float duration = Random.Range(minDuration, maxDuration);

        transform.DOLocalRotate(
            new Vector3(startX, targetY, startZ),
            duration
        )
        .SetEase(Ease.InOutSine)
        .OnComplete(() =>
        {
            DOVirtual.DelayedCall(
                Random.Range(minPause, maxPause),
                Sway
            );
        });
    }

    private void OnDestroy()
    {
        transform.DOKill();
    }
}