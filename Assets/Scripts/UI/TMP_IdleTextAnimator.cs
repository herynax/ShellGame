using UnityEngine;
using TMPro;

/// <summary>
/// Тревожная, хаотичная idle-анимация текста TextMeshPro.
/// Каждая буква двигается независимо от других (свой рандомный сид),
/// с редкими резкими рывками — не синхронная волна, а "нервный" эффект.
/// Повесить на GameObject с TextMeshProUGUI или TextMeshPro (3D).
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class TMP_IdleTextAnimator : MonoBehaviour
{
    [Header("Дрожание (основное движение)")]
    [Tooltip("Амплитуда дрожания по X/Y")]
    public float jitterAmount = 1.5f;

    [Tooltip("Скорость дрожания (частота noise)")]
    public float jitterSpeed = 6f;

    [Header("Покачивание/поворот")]
    public float wobbleAngle = 10f;
    public float wobbleSpeed = 4f;

    [Header("Резкие рывки (spikes)")]
    [Tooltip("Включить редкие резкие скачки отдельных букв")]
    public bool useSpikes = true;

    [Tooltip("Шанс рывка на букву в секунду (0.1 = редко, 1 = часто)")]
    [Range(0f, 2f)]
    public float spikeChancePerSecond = 0.35f;

    [Tooltip("Сила рывка")]
    public float spikeStrength = 6f;

    [Tooltip("Длительность рывка в секундах")]
    public float spikeDuration = 0.12f;

    [Header("Случайность на букву")]
    [Tooltip("Насколько сильно отличаются скорость/фаза у разных букв. " +
             "0 = все одинаковые (синхронно), 1 = максимальный разнобой")]
    [Range(0f, 1f)]
    public float perCharacterRandomness = 1f;

    private TMP_Text _text;
    private float[] _seeds;          // уникальный сид на каждую букву
    private float[] _speedMul;       // индивидуальный множитель скорости
    private float[] _nextSpikeCheck; // таймер следующей проверки на рывок
    private float[] _spikeTimeLeft;  // сколько ещё длится текущий рывок
    private Vector2[] _spikeDir;     // направление текущего рывка

    void Awake()
    {
        _text = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        SetupSeeds();
    }

    void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
    }

    void OnTextChanged(UnityEngine.Object obj)
    {
        if (obj == _text)
            SetupSeeds();
    }

    void SetupSeeds()
    {
        int count = _text.textInfo != null ? Mathf.Max(_text.textInfo.characterCount, 1) : 1;
        // Пересоздаём массивы только если размер изменился, чтобы не терять текущие рывки без нужды
        if (_seeds == null || _seeds.Length != count)
        {
            _seeds = new float[count];
            _speedMul = new float[count];
            _nextSpikeCheck = new float[count];
            _spikeTimeLeft = new float[count];
            _spikeDir = new Vector2[count];

            for (int i = 0; i < count; i++)
            {
                _seeds[i] = Random.Range(0f, 1000f);
                _speedMul[i] = Mathf.Lerp(1f, Random.Range(0.5f, 1.8f), perCharacterRandomness);
                _nextSpikeCheck[i] = Time.time + Random.Range(0f, 1f);
            }
        }
    }

    void LateUpdate()
    {
        if (_text.textInfo == null) return;
        if (_seeds == null || _seeds.Length != _text.textInfo.characterCount)
            SetupSeeds();

        AnimateText();
    }

    void AnimateText()
    {
        _text.ForceMeshUpdate();

        TMP_TextInfo textInfo = _text.textInfo;
        float time = Time.time;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible)
                continue;

            int materialIndex = charInfo.materialReferenceIndex;
            int vertexIndex = charInfo.vertexIndex;
            Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;

            Vector3 charMid = (vertices[vertexIndex + 0] + vertices[vertexIndex + 2]) / 2;

            float seed = _seeds[i];
            float speedMul = _speedMul[i];

            // --- Независимое дрожание через несвязанные каналы Perlin noise ---
            float nx = (Mathf.PerlinNoise(seed, time * jitterSpeed * speedMul) - 0.5f) * 2f;
            float ny = (Mathf.PerlinNoise(seed + 50f, time * jitterSpeed * speedMul + 33f) - 0.5f) * 2f;
            Vector3 jitterOffset = new Vector3(nx, ny, 0f) * jitterAmount;

            // --- Покачивание/поворот, тоже несинхронное ---
            float wobbleNoise = Mathf.PerlinNoise(seed + 90f, time * wobbleSpeed * speedMul);
            float wobble = (wobbleNoise - 0.5f) * 2f * wobbleAngle;

            // --- Резкие рывки ---
            Vector3 spikeOffset = Vector3.zero;
            if (useSpikes)
            {
                if (_spikeTimeLeft[i] > 0f)
                {
                    _spikeTimeLeft[i] -= Time.deltaTime;
                    float t = Mathf.Clamp01(_spikeTimeLeft[i] / spikeDuration);
                    // резкий вылет и затухание (easeOutExpo-ish)
                    float falloff = t * t;
                    spikeOffset = _spikeDir[i] * spikeStrength * falloff;
                }
                else if (time >= _nextSpikeCheck[i])
                {
                    _nextSpikeCheck[i] = time + Random.Range(0.3f, 1.2f);
                    if (Random.value < spikeChancePerSecond * 0.5f)
                    {
                        _spikeTimeLeft[i] = spikeDuration;
                        _spikeDir[i] = Random.insideUnitCircle.normalized;
                    }
                }
            }

            for (int v = 0; v < 4; v++)
            {
                Vector3 orig = vertices[vertexIndex + v];

                Vector3 dir = orig - charMid;
                dir = Quaternion.Euler(0, 0, wobble) * dir;
                Vector3 newPos = charMid + dir;

                newPos += jitterOffset;
                newPos += spikeOffset;

                vertices[vertexIndex + v] = newPos;
            }
        }

        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            var meshInfo = textInfo.meshInfo[i];
            meshInfo.mesh.vertices = meshInfo.vertices;
            _text.UpdateGeometry(meshInfo.mesh, i);
        }
    }
}