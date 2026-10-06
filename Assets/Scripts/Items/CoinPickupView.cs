using DG.Tweening;
using FMODUnity;
using ShellGame.Audio;
using ShellGame.Core;
using UnityEngine;

namespace ShellGame.Items
{
    /// <summary>
    /// Физическая монета на столе.
    ///
    /// Состояния тела: покой (замороженная кинематика — лежит в стопке, держит
    /// форму и не «плывёт») и падение/разлёт (динамическое тело с гравитацией
    /// и CCD). Снятие с заморозки = Drop, возврат = Settle.
    ///
    /// CCD обязателен: монета — пластина толщиной 1.8 см, падает с 0.4 м
    /// (удар ~2.8 м/с, то есть 4.7 см за кадр) и на Discrete-детекции просто
    /// пролетает сквозь лежащие монеты.
    ///
    /// Legacy OnMouseEnter/OnMouseDown здесь намеренно НЕ используются: они требуют
    /// камеру с тегом MainCamera и в этом проекте срабатывают нестабильно
    /// (та же причина, по которой предметы переведены на внешний ввод — см.
    /// ItemPickupView). Весь ввод по монетам держит CoinPileController, который
    /// получает луч из CrosshairRay — то есть работает и по прицелу, и по курсору.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CoinPickupView : MonoBehaviour
    {
        [Header("Visual")]
        [Tooltip("Дочерний визуал. Двигаем и масштабируем только его — корень с коллайдером и Rigidbody не трогаем, иначе ховер уводит луч с монеты.")]
        [SerializeField] private Transform _visual;
        [SerializeField] private float _hoverHeight = 0.02f;
        [SerializeField] private float _hoverScale = 1.2f;
        [SerializeField] private float _hoverDuration = 0.15f;

        [Header("Scattering")]
        [Tooltip("Зазор между монетами в стопке, м. Шаг стопки считается как высота коллайдера плюс этот зазор.")]
        [SerializeField, Min(0f)] private float _stackGap = 0.0015f;
        [Tooltip("Скорость броска в сторону, м/с. 0.5 — еле заметно, 1.5 — уверенно, 2.5 — сильно.")]
        [SerializeField, Range(0.1f, 4f)] private float _scatterSpeed = 0.9f;
        [Tooltip("Насколько монета подпрыгивает вверх при разбросе, доля от горизонтальной скорости.")]
        [SerializeField, Range(0f, 1.5f)] private float _scatterUpSpeed = 0.35f;
        [Tooltip("Разброс вращения, чтобы монетка кувыркалась при падении.")]
        [SerializeField, Range(0f, 1f)] private float _scatterSpin = 0.06f;

        [Header("Impacts")]
        [Tooltip("Насколько сильно ударенная монета отталкивается в сторону, доля от скорости удара. Заменяет упругость и подброс: соседку видно как сдвинутую, а не прыгающую.")]
        [SerializeField, Range(0f, 2f)] private float _shoveFactor = 0.6f;
        [Tooltip("Скорость удара, ниже которого сдвиг не срабатывает, м/с. Отсекает лёгкие стуки, чтобы россыпь не дёргалась сама.")]
        [SerializeField, Range(0f, 2f)] private float _shoveMinImpact = 0.4f;

        [Header("Sounds")]
        [SerializeField] private EventReference _pickupSound;
        [SerializeField] private EventReference _stackSound;
        [SerializeField] private EventReference _scatterSound;

        private bool _isHovered;
        private bool _isFlying;
        private CoinPileController _currentPile;
        private Tween _hoverTween;
        private IAudioService _audio;
        private Vector3 _coinSize = Vector3.zero;

        public Rigidbody Body { get; private set; }

        public CoinPileController CurrentPile
        {
            get => _currentPile;
            internal set => _currentPile = value;
        }
        public bool IsInPile => _currentPile != null;
        public bool IsFlying => _isFlying;
        public bool IsHovered => _isHovered;

        /// <summary>
        /// Габариты монеты в мировых единицах, независимые от её наклона.
        ///
        /// Важно: берём локальную рамку коллайдера, а НЕ collider.bounds. bounds —
        /// это мировой AABB, и он раздувается, когда монету разбросили и она легла
        /// на ребро. Тогда высота «прыгала» с 1.8 см до 14.5 см, MaxStackStep
        /// выбирал худшую монету и весь ряд стопки разъезжался — монеты висели
        /// в воздухе друг над другом вместо того, чтобы лежать вплотную.
        /// </summary>
        public Vector3 CoinSize
        {
            get
            {
                var collider = GetComponent<Collider>();
                if (collider == null) return _coinSize;

                if (collider is BoxCollider box)
                {
                    // box.size локальный, lossyScale приводит его к миру.
                    // Поворот на lossyScale не влияет.
                    _coinSize = Vector3.Scale(box.size, transform.lossyScale);
                    return _coinSize;
                }

                // Для прочих типов берём размер, закешированный в Awake, когда монета
                // ещё лежала плашмя: иначе наклон снова утолщил бы AABB.
                if (_coinSize == Vector3.zero) _coinSize = collider.bounds.size;
                return _coinSize;
            }
        }

        /// <summary>Высота монеты — шаг, на который поднимается каждая следующая в стопке.</summary>
        public float CoinHeight => CoinSize.y;

        /// <summary>Ширина монеты по горизонтали — размер ячейки сетки стопок.</summary>
        public float CoinWidth => Mathf.Max(CoinSize.x, CoinSize.z);

        public float StackStep => CoinHeight + _stackGap;

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();

            // Материал монет намеренно остаётся дефолтным. Попытка вернуть
            // упругость через PhysicsMaterial дала прыгучесть: bounciness в
            // PhysX общий для пары монет и для пары монета-пол, поэтому отскок
            // при приземлении отлично от «сдвинуть соседку». Передачу импульса
            // решает OnCollisionEnter — сдвигом вбок, без вертикали.
            //
            // Кэш габаритов: дальше размер берётся из локальной рамки коллайдера,
            // но для нестандартных типов нужен эталон в неповёрнутом состоянии.
            _coinSize = GetComponent<Collider>().bounds.size;

            if (!ServiceLocator.TryGet<IAudioService>(out _audio))
            {
                _audio = new FMODAudioService();
                ServiceLocator.Register(_audio);
            }

            // Заложенная монета заморожена: разлёт по клику снимет её с
            // заморозки через Drop.
            Settle();
        }

        /// <summary>
        /// Сдвинуть ударенную монету в сторону от ударившей.
        ///
        /// Упругость в PhysX не годится для этой задачи: она симметрична, поэтому
        /// и подбрасывает соседку вверх при ударе сбоку, а не двигает её прочь.
        /// При равных массах и нулевой упругости импульс делится пополам, и
        /// соседка отползает на пару сантиметров — то есть монетка удара почти
        /// не передаёт. Поэтому удар обрабатывается явно: скорость удара
        /// добавляется в горизонтальном направлении от ударившего.
        ///
        /// Направление берётся от координат, а не от нормали столкновения:
        /// так не нужно гадать о знаке. Проверка нормали отсекает удары сверху и
        /// снизу — упавшая сверху монетка сдвинула бы соседку по стопке вбок,
        /// хотя должна просто прилечь.
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (Body == null || Body.isKinematic) return;

            Vector3 normal = collision.GetContact(0).normal;
            float sideways = new Vector2(normal.x, normal.z).magnitude;
            if (sideways < 0.5f) return;

            float impact = collision.relativeVelocity.magnitude;
            if (impact < _shoveMinImpact) return;

            Vector3 away = transform.position - collision.collider.transform.position;
            away.y = 0f;
            if (away.sqrMagnitude < 1e-6f) return;

            Body.AddForce(away.normalized * (impact * _shoveFactor), ForceMode.VelocityChange);
        }

        /// <summary>
        /// Заморозить монету: кинематика, гравитация выключена. Это и есть
        /// состояние покоя в куче — стопка стоит неподвижно, не «плывёт» и
        /// жрёт ноль CPU.
        ///
        /// Кинематика — это сознательный выбор: у динамического тела куча
        /// расползается от микродвижений, а для «разлететься по клику» хватает
        /// одного снятия с заморозки. Вызывается и на время анимации переноса,
        /// чтобы твин и физика не дрались за одно тело.
        /// </summary>
        public void Settle()
        {
            _isFlying = false;
            transform.DOKill();

            if (_visual != null)
            {
                _visual.DOKill();
                _visual.localPosition = Vector3.zero;
                _visual.localScale = Vector3.one;
            }

            if (Body == null) return;

            // Порядок обязателен: скорости обнуляются ДО перевода в кинематику.
            // У кинематического тела их установка запрещена, и Unity сыплет
            // "Setting linear velocity of a kinematic body is not supported"
            // на каждой монете — два раза за её жизнь.
            if (!Body.isKinematic)
            {
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
            }

            Body.isKinematic = true;
            Body.useGravity = false;
            // Continuous на кинематике Unity не поддерживает — только Speculative.
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        /// <summary>
        /// Вернуть монету в физику без заморозки: динамическое тело с гравитацией
        /// и CCD. Это состояние монеты, лежащей россыпью — она спит, пока её не
        /// заденет падающая или кликнутая монета, и тогда едет дальше.
        ///
        /// Отличие от Drop — нет метки «в полёте»: CoinPileController перестаёт
        /// её отслеживать, но тело продолжает участвовать в столкновениях.
        /// </summary>
        public void Rest()
        {
            if (Body == null) return;

            // Settle сначала обнуляет скорости (пока тело ещё динамическое),
            // потом включает кинематику — и только так переход в кинематику
            // не сыпет ошибками Unity.
            Settle();

            Body.isKinematic = false;
            Body.useGravity = true;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
        }

        /// <summary>
        /// Отпустить монету: снимает с заморозки, включает физику, и она падает
        /// по-настоящему. Используется и для выдачи из зоны, и для разброса —
        /// разница только в приложенном импульсе.
        /// </summary>
        public void Drop()
        {
            if (Body == null) return;

            transform.DOKill();

            _isFlying = true;
            Rest();
        }

        /// <summary>
        /// Разбросить монету: снимается с заморозки, становится динамической и
        /// улетает в случайную сторону — дальше падает по-настоящему, пока
        /// CoinPileController снова не уложит её в покой.
        ///
        /// forceScale — множитель силы разлёта, чтобы разбрасывание стопки
        /// можно было настроить отдельно от одиночной монетки.
        /// </summary>
        public void RequestScatter(float forceScale = 1f)
        {
            if (Body == null) return;

            // Монета остаётся в общем списке кучи, но выходит из стопки.
            DetachFromPile();

            Drop();

            Vector2 dir = Random.insideUnitCircle.normalized;
            if (dir.sqrMagnitude < 0.001f) dir = Vector2.right;

            float scale = Mathf.Max(0.01f, forceScale);
            float speed = _scatterSpeed * scale * Random.Range(0.75f, 1.25f);
            Vector3 impulse = new Vector3(dir.x, 0f, dir.y) + Vector3.up * (_scatterUpSpeed * scale);

            // VelocityChange — скорость в м/с независимо от массы.
            Body.AddForce(impulse * speed, ForceMode.VelocityChange);
            Body.AddTorque(Random.insideUnitSphere * (_scatterSpin * scale), ForceMode.VelocityChange);

            if (!_scatterSound.IsNull)
                _audio?.PlayOneShot(_scatterSound, transform.position);
        }

        /// <summary>
        /// Выйти из стопки, оставаясь в общем списке кучи. Разделяет выход из
        /// стопки и сброс ссылки на кучу, потому что разброс это делает, а падение
        /// при выдаче — нет.
        /// </summary>
        public void DetachFromPile()
        {
            if (_currentPile == null) return;

            _currentPile.DetachFromStack(this);
            _currentPile = null;
        }

        /// <summary>Ховер двигает только визуал — коллайдер и физика не шевелятся.</summary>
        public void SetHovered(bool hovered)
        {
            if (_isHovered == hovered) return;
            _isHovered = hovered;

            if (_visual == null) return;

            _hoverTween?.Kill();
            _visual.DOKill();

            _hoverTween = _visual
                .DOScale(hovered ? _hoverScale : 1f, _hoverDuration)
                .SetEase(Ease.OutQuad);
            _visual.DOLocalMoveY(hovered ? _hoverHeight : 0f, _hoverDuration)
                .SetEase(Ease.OutQuad);

            if (!_pickupSound.IsNull)
                _audio?.PlayOneShot(_pickupSound, transform.position);
        }

        /// <summary>
        /// Анимированный перенос в заданную точку с заданным поворотом (сборка
        /// в стопку). Поворот обязателен: после разброса у монеты остаётся
        /// произвольный тангаж, и без явной установки знаки на монетах в стопке
        /// не совпадут. Тело на всё время переноса и после него остаётся
        /// замороженным — уложенная стопка должна стоять неподвижно.
        /// </summary>
        public void MoveTo(Vector3 targetPosition, Quaternion targetRotation, float duration, Ease ease)
        {
            Settle();

            transform.DOKill();
            transform.DOMove(targetPosition, duration).SetEase(ease);
            transform.DORotateQuaternion(targetRotation, duration).SetEase(ease).OnComplete(() =>
            {
                if (!_stackSound.IsNull)
                    _audio?.PlayOneShot(_stackSound, transform.position);
            });
        }

        private void OnDestroy()
        {
            _hoverTween?.Kill();
            transform.DOKill();
            if (_visual != null) _visual.DOKill();
        }
    }
}