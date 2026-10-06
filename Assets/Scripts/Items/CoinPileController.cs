using System.Collections.Generic;
using DG.Tweening;
using ShellGame.Audio;
using ShellGame.Core;
using ShellGame.Meta;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShellGame.Items
{
    /// <summary>
    /// Контроллер кучи монет на столе. Владеет зоной, из которой монеты падают,
    /// раскладкой в стопки и разбросом.
    ///
    /// Монеты появляются сверху зоны и падают по-настоящему: DropCoins создаёт их
    /// над полом, найденным лучом вниз, включает физику, а UpdateFlyingCoins
    /// укладывает монету, когда та успокоилась. Раньше каждая точка выдачи сразу
    /// вызывала заморозку, из-за чего монеты не падали никогда.
    ///
    /// Ввод сюда не опрашивается: курсор в бою залочен, поэтому клики приходят из
    /// RoundInputSystem, который берёт луч из CrosshairRay — тот же, что и для
    /// напертков. Здесь живут только PickUnderRay, SetHoveredCoin и HandleClick.
    /// </summary>
    public sealed class CoinPileController : MonoBehaviour
    {
        public static CoinPileController Instance { get; private set; }

        [Header("References")]
        [SerializeField] private CoinPickupView _coinPrefab;
        [SerializeField] private BoxCollider _coinZone;

        public BoxCollider CoinZone
        {
            get => _coinZone;
            set => _coinZone = value;
        }

        [Header("Input")]
        [Tooltip("Запасной захват как доля от ширины монеты. Точный луч по габаритам — основной; этот радиус нужен на скользящих углах. Держим его не меньше половины ширины монеты, иначе запасной захват ловит строго меньше, чем уже поймал точный луч.")]
        [SerializeField, Range(0.5f, 2f)] private float _softPickScale = 0.75f;
        [Tooltip("Второй клик быстрее этого времени считается двойным, с.")]
        [SerializeField, Range(0.1f, 0.6f)] private float _doubleClickThreshold = 0.3f;
        [Tooltip("Насколько близко должен быть второй клик к первому, в пикселях экрана. При залоченном курсоре проверка не нужна — прицел и так на месте.")]
        [SerializeField, Range(5f, 200f)] private float _doubleClickRadius = 60f;

        [Header("Dropping")]
        [Tooltip("Насколько выше найденного пола появляется монета, м.")]
        [SerializeField, Range(0.1f, 2f)] private float _dropHeight = 0.4f;
        [Tooltip("С какой высоты пускаем луч вниз в поисках пола, м.")]
        [SerializeField, Min(0.5f)] private float _floorProbeHeight = 2f;
        [Tooltip("Минимальное время падения, за которое монета не успевает заснуть. Спасает от мгновенной укладки только что созданной монеты.")]
        [SerializeField, Range(0.02f, 0.5f)] private float _minFallTime = 0.1f;
        [Tooltip("Сколько секунд монете максимум на падение, потом мы её укладываем принудительно.")]
        [SerializeField, Range(0.5f, 6f)] private float _maxFlightTime = 2.5f;
        [Tooltip("Порог покоя: монета считается упавшей, когда скорость ниже этих значений.")]
        [SerializeField, Min(0.0001f)] private float _restSpeedThreshold = 0.02f;
        [SerializeField, Min(0.0001f)] private float _restSpinThreshold = 0.02f;

        [Header("Stacking")]
        [Tooltip("Монет в одной стопке. Больше — раскладываем в следующую ячейку сетки.")]
        [SerializeField, Min(1)] private int _maxCoinsPerStack = 10;
        [Tooltip("Зазор между ячейками сетки, кратный ширине монеты. 1 — впритык, больше — разреженнее.")]
        [SerializeField, Range(1f, 3f)] private float _cellSpacing = 1.15f;
        [Tooltip("Отступ от края зоны, м.")]
        [SerializeField, Min(0f)] private float _edgeMargin = 0.02f;
        [Tooltip("Сила разлёта стопки по клику. 1 — как одиночная монетка (её собственная сила задана на префабе), больше — разлетается сильнее. Каждая монета стопки получает свой случайный импульс с этим множителем.")]
        [SerializeField, Range(0.1f, 4f)] private float _stackScatterForce = 1.5f;
        [SerializeField] private float _stackFormationDuration = 0.3f;
        [SerializeField] private float _stackReflowDuration = 0.15f;

        private readonly List<CoinPickupView> _allCoins = new List<CoinPickupView>();
        private readonly List<CoinStack> _stacks = new List<CoinStack>();
        private readonly List<FlyingCoin> _flyingCoins = new List<FlyingCoin>();

        private IAudioService _audio;
        private CoinPickupView _hoveredCoin;
        private float _lastClickTime = -10f;
        private Vector2 _lastClickScreenPosition;

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

            if (_coinZone == null)
                _coinZone = GetComponent<BoxCollider>();

            if (!ServiceLocator.TryGet<IAudioService>(out _audio))
            {
                _audio = new FMODAudioService();
                ServiceLocator.Register(_audio);
            }
        }

        private void Update()
        {
            UpdateFlyingCoins();
        }

        // ---------------------------------------------------------------- ввод

        /// <summary>
        /// Монета под лучом. Сначала точный луч — монета широкая и тонкая, но
        /// берётся точно. Мягкий сферокаст нужен только на скользящих углах.
        /// </summary>
public CoinPickupView PickUnderRay(Ray ray)
        {
            if (_allCoins.Count == 0) return null;
            if (!RayEntersZone(ray)) return null;

            var hits = Physics.RaycastAll(ray, 100f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            var coin = NearestCoin(hits);
            if (coin != null) return coin;

            float radius = SoftPickRadius();
            if (radius <= 0.001f) return null;

            var softHits = Physics.SphereCastAll(ray, radius, 100f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            return NearestCoin(softHits);
        }

        /// <summary>
        /// Радиус «запасного» захвата. Раньше он брался из _hoverRayRadius (0.04 м),
        /// но монета шире — её половина около 0.072 м. То есть сфера ловила
        /// строго меньше, чем уже пойманный точным лучом, и на скользящих углах
        /// монету не находило вовсе. Теперь радиус выводится из ширины монеты,
        /// чтобы запасной захват реально перекрывал саму монету.
        /// </summary>
        private float SoftPickRadius()
        {
            float byCoin = CoinWidth * _softPickScale;
            return Mathf.Clamp(byCoin, 0.01f, 0.2f);
        }

        /// <summary>
        /// Дешёвый отсев: луч, который вообще не смотрит на зону, не может попасть
        /// в монету. Спасает от двух физических запросов каждый кадр, когда прицел
        /// смотрит в другую сторону стола.
        /// </summary>
        private bool RayEntersZone(Ray ray)
        {
            if (_coinZone == null) return false;

            // Раньше высота запаса выводилась из _maxCoinsPerStack, и стопка из
            // 10 монет ровно упиралась в границу отсева — верхняя монета переставала
            // браться под прицел. Теперь берём настоящий AABB живых монет.
            Bounds bounds = new Bounds(_coinZone.bounds.center, Vector3.zero);
            bool any = false;

            foreach (var coin in _allCoins)
            {
                if (coin == null) continue;

                var collider = coin.GetComponent<Collider>();
                if (collider == null) continue;

                if (!any)
                {
                    bounds = collider.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            // Монет нет вовсе — отсекаем по самой зоне, иначе PickUnderRay
            // вернул бы монету, которой уже не существует.
            if (!any) return false;

            bounds.Expand(SoftPickRadius() + _edgeMargin);
            return bounds.IntersectRay(ray);
        }

        private static CoinPickupView NearestCoin(RaycastHit[] hits)
        {
            if (hits.Length == 0) return null;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            // Ищем именно монету, а не первый попавшийся коллайдер: под монетами
            // лежит поверхность падения, и она перехватывала бы луч.
            foreach (var hit in hits)
            {
                var coin = hit.collider.GetComponentInParent<CoinPickupView>();
                if (coin != null) return coin;
            }

            return null;
        }

        /// <summary>Ховер на монету. Двигает только визуал, физику не трогает.</summary>
        public void SetHoveredCoin(CoinPickupView coin)
        {
            // Именно Unity-овский "==", а не ReferenceEquals: он ловит и
            // уничтоженные монеты, а на destroy-обёртке null-коalescing бросил бы
            // MissingReferenceException.
            if (_hoveredCoin == coin) return;

            if (_hoveredCoin != null) _hoveredCoin.SetHovered(false);

            _hoveredCoin = coin;

            if (_hoveredCoin != null) _hoveredCoin.SetHovered(true);
        }

        /// <summary>
        /// Клик по монетам. Одиночный клик разбрасывает монету под прицелом,
        /// двойной собирает всё в стопки.
        ///
        /// Раньше двойной засчитывался только при клике по пустому месту
        /// (_hoveredCoin == null). Над кучей монет курсор всегда на монете, поэтому
        /// двойной клик по куче не срабатывал никогда — только разбрасывал её дважды.
        /// Теперь жест различается по времени, а не по тому, что под курсором.
        /// </summary>
        public void HandleClick()
        {
            if (IsDoubleClick())
            {
                _lastClickTime = -10f;
                StackAll();
                return;
            }

            _lastClickTime = Time.unscaledTime;
            var mouse = Mouse.current;
            if (mouse != null) _lastClickScreenPosition = mouse.position.ReadValue();

            var coin = _hoveredCoin;
            if (coin == null) return;

            ScatterStack(coin);
        }

        /// <summary>
        /// Разлететь стопкой: клик по монетке сбивает всю её стопку, а не только
        /// монетку под прицелом. Если монетка уже вне стопки (лежит разбросанная
        /// после прошлого разлёта) — разлетается сама.
        ///
        /// Сила разлёта — _stackScatterForce, настраивается отдельно от одиночной
        /// монетки, у которой своя сила на префабе.
        /// </summary>
        public void ScatterStack(CoinPickupView clicked)
        {
            if (clicked == null) return;

            PruneDestroyedCoins();

            var stack = FindStackOf(clicked);
            if (stack == null)
            {
                clicked.RequestScatter(1f);
                TrackFlight(clicked);
                return;
            }

            // Снимаем стопку со счёта ДО разлёта: RequestScatter дёргает
            // DetachFromPile → RemoveCoin, а тот пересобирает стопку, из которой
            // монету уже вынули.
            _stacks.Remove(stack);

            var coins = new List<CoinPickupView>(stack.Coins);
            stack.Coins.Clear();

            foreach (var coin in coins)
            {
                if (coin == null) continue;

                // Ссылку сбрасываем заранее, иначе DetachFromPile попытается
                // пересобрать уже снятую стопку.
                coin.CurrentPile = null;
                coin.RequestScatter(_stackScatterForce);
                TrackFlight(coin);
            }
        }

        private CoinStack FindStackOf(CoinPickupView coin)
        {
            foreach (var stack in _stacks)
            {
                if (stack.Coins.Contains(coin)) return stack;
            }

            return null;
        }

        private bool IsDoubleClick()
        {
            if (Time.unscaledTime - _lastClickTime > _doubleClickThreshold) return false;

            // Прицел на месте, промахнуться второй раз некуда.
            if (CrosshairRay.IsCursorCaptured()) return true;

            var mouse = Mouse.current;
            if (mouse == null) return true;

            return (mouse.position.ReadValue() - _lastClickScreenPosition).sqrMagnitude
                   < _doubleClickRadius * _doubleClickRadius;
        }

        // ------------------------------------------------------------ падение

        /// <summary>
        /// Разлетевшаяся или только что выданная монета укладывается, когда
        /// успокоилась или истекло время падения. Без этого куча росла бы с каждой
        /// разлетевшейся монетой, а монетка могла бы улететь за пределы зоны.
        /// </summary>
        private void UpdateFlyingCoins()
        {
            for (int i = _flyingCoins.Count - 1; i >= 0; i--)
            {
                var flying = _flyingCoins[i];
                var coin = flying.Coin;

                if (coin == null)
                {
                    _flyingCoins.RemoveAt(i);
                    continue;
                }

                // Уже уложена — двойным кликом или по своему усмотрению.
                if (!coin.IsFlying)
                {
                    _flyingCoins.RemoveAt(i);
                    continue;
                }

                flying.Elapsed += Time.deltaTime;
                flying.TimeLeft -= Time.deltaTime;

                // Укладываем, когда монета реально успокоилась, но не раньше минимального
                // времени падения: только что выданная ещё не набрала скорость и
                // заснуть успевает.
                //
                // Раньше проверка стояла наоборот — как только _minFallTime
                // истекал, условие вырождалось и монета укладывалась на лету. А
                // Rest() обнуляет скорость, то есть импульс разлёта погибал
                // через 0.1 с: монетка сдвигалась на пару сантиметров и
                // замирала, вместо того чтобы разлететься.
                if (flying.TimeLeft > 0f)
                {
                    if (flying.Elapsed < _minFallTime) continue;
                    if (!IsResting(coin.Body)) continue;
                }

                // Порядок обязателен: сначала заморозка, потом clamp. У кинематического тела
                // смещение позиции держится, у динамического solver отменит
                // правку на ближайшем шаге и монета снова вылезет за край зоны.
                coin.Settle();
                ClampToZone(coin.transform);

                // Замораживаем ТОЛЬКО монеты, входящие в стопку: уложенные должны
                // стоять неподвижно. Всё, что осталось лежать россыпью, снова
                // становится физическим телом — иначе падающая или кликнутая
                // монета задевала бы его и тут же проходила насквозь, не сбив
                // ни одной соседки.
                if (!IsInAnyStack(coin)) coin.Rest();

                _flyingCoins.RemoveAt(i);
            }
        }

        private bool IsResting(Rigidbody body)
        {
            if (body == null) return true;
            if (body.IsSleeping()) return true;

            float speedSq = _restSpeedThreshold * _restSpeedThreshold;
            float spinSq = _restSpinThreshold * _restSpinThreshold;

            return body.linearVelocity.sqrMagnitude < speedSq && body.angularVelocity.sqrMagnitude < spinSq;
        }

        private void TrackFlight(CoinPickupView coin)
        {
            if (coin == null) return;

            for (int i = 0; i < _flyingCoins.Count; i++)
            {
                if (_flyingCoins[i].Coin == coin) return;
            }

            _flyingCoins.Add(new FlyingCoin { Coin = coin, TimeLeft = _maxFlightTime });
        }

        // ------------------------------------------------------- точки появления

        /// <summary>
        /// Выдать N монет: создать их над полом зоны и отпустить в свободное падение.
        /// Так выглядит старт рана (SyncToCount) и продажа предмета в магазине.
        /// </summary>
        public int DropCoins(int count)
        {
            if (count <= 0) return 0;

            if (_coinPrefab == null || _coinZone == null)
            {
                Debug.LogWarning("[CoinPileController] Не заданы префаб монеты или зона — монеты не созданы.");
                return 0;
            }

            int created = 0;
            for (int i = 0; i < count; i++)
            {
                if (!TryGetSpawnPoint(out var position)) continue;

                var coin = Instantiate(_coinPrefab, position, FlatRandomRotation(), transform);
                coin.name = $"Coin_{_allCoins.Count}";
                RegisterCoin(coin);

                coin.Drop();
                TrackFlight(coin);
                created++;
            }

            return created;
        }

        /// <summary>
        /// Разброс только вокруг вертикали: монетка падает плашмя и ложится в кучу
        /// предсказуемо. Полностью случайный наклон ставил бы её на ребро.
        /// </summary>
        private static Quaternion FlatRandomRotation()
        {
            return Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        /// <summary>
        /// Точка появления над полом зоны. Пол ищем лучом вниз, а не берём верх
        /// bounds: зона в бою — сплошной куб, а в магазине — триггер над отдельной
        /// площадкой CoinLanding. Луч находит верх реальной поверхности в обоих
        /// случаях, и не важно, что поверх перекрыто мешем.
        /// </summary>
        private bool TryGetSpawnPoint(out Vector3 position)
        {
            position = default;
            if (_coinZone == null) return false;

            Bounds bounds = _coinZone.bounds;
            float inset = CoinWidth * 0.5f + _edgeMargin;

            float x = Random.Range(bounds.min.x + inset, bounds.max.x - inset);
            float z = Random.Range(bounds.min.z + inset, bounds.max.z - inset);

            float floorY = TryGetFloorHeight(new Vector2(x, z), out float probed) ? probed : bounds.max.y;

            position = new Vector3(x, floorY + _dropHeight, z);
            return true;
        }

        /// <summary>
        /// Верхняя точка твёрдой поверхности под (x, z). Коллайдеры монет
        /// игнорируем, иначе луч упёрся бы в уже упавшую монету и следующая
        /// полетела бы с неё, а не с пола.
        /// </summary>
        private bool TryGetFloorHeight(Vector2 xz, out float floorY)
        {
            floorY = default;
            if (_coinZone == null) return false;

            Bounds bounds = _coinZone.bounds;
            var origin = new Vector3(xz.x, bounds.max.y + _floorProbeHeight, xz.y);
            float distance = _floorProbeHeight + bounds.size.y + _dropHeight;

            var hits = Physics.RaycastAll(origin, Vector3.down, distance,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);

            float best = float.NegativeInfinity;
            foreach (var hit in hits)
            {
                if (hit.collider.GetComponentInParent<CoinPickupView>() != null) continue;
                if (hit.point.y > best) best = hit.point.y;
            }

            if (best == float.NegativeInfinity) return false;

            floorY = best;
            return true;
        }

        private float CoinWidth => _coinPrefab != null ? _coinPrefab.CoinWidth : 0f;

        // ------------------------------------------------------------- раскладка

        /// <summary>Собрать все монеты в стопки сеткой по зоне (двойной клик).</summary>
        public void StackAll()
        {
            // Сначала гасим все незавершённые твины, иначе убитый OnComplete
            // оставит часть монет в кинематике навсегда.
            foreach (var coin in _allCoins)
            {
                if (coin != null) coin.transform.DOKill();
            }

            RebuildLayout(_stackFormationDuration);
        }

        /// <summary>Разбросить всё (вызывается из кода, отдельной кнопки нет).</summary>
        public void ScatterAll()
        {
            PruneDestroyedCoins();

            foreach (var coin in _allCoins)
            {
                if (coin == null) continue;
                coin.RequestScatter();
                TrackFlight(coin);
            }

            _stacks.Clear();
        }

        /// <summary>
        /// Разложить все монеты по сетке стопок. Позиция стопки задаётся один раз
        /// (BasePosition) и дальше не пересчитывается: раньше центр брался как
        /// среднее по позициям монет, включая ещё летящие, из-за чего башня
        /// разъезжалась и «плыла» вверх.
        /// </summary>
        private void RebuildLayout(float duration)
        {
            if (_coinZone == null) return;

            SetHoveredCoin(null);
            _flyingCoins.Clear();
            PruneDestroyedCoins();

            var coins = new List<CoinPickupView>(_allCoins);
            _stacks.Clear();
            if (coins.Count == 0) return;

            float step = MaxStackStep(coins);
            float cell = MaxCoinWidth(coins) * _cellSpacing;

            int perStack = Mathf.Max(1, _maxCoinsPerStack);
            int stacksNeeded = Mathf.CeilToInt(coins.Count / (float)perStack);

            CalculateGrid(stacksNeeded, cell, out int cols, out int rows);

            Bounds bounds = _coinZone.bounds;
            float startX = bounds.center.x - (cols - 1) * cell * 0.5f;
            float startZ = bounds.center.z - (rows - 1) * cell * 0.5f;

            int index = 0;
            for (int s = 0; s < stacksNeeded; s++)
            {
                float x = startX + (s % cols) * cell;
                float z = startZ + (s / cols) * cell;

                float floorY = TryGetFloorHeight(new Vector2(x, z), out float probed) ? probed : bounds.max.y;

                var stack = new CoinStack
                {
                    BasePosition = new Vector3(x, floorY + step * 0.5f, z),
                    // Один поворот на стопку: внутри стопки монеты ложатся
                    // знак в знак, а сами стопки отличаются друг от друга.
                    BaseRotation = FlatRandomRotation(),
                    Step = step
                };
                _stacks.Add(stack);

                int take = Mathf.Min(perStack, coins.Count - index);
                for (int i = 0; i < take; i++, index++)
                {
                    stack.Coins.Add(coins[index]);
                    coins[index].CurrentPile = this;
                    coins[index].MoveTo(stack.SlotPosition(i), stack.BaseRotation, duration, Ease.OutQuad);
                }
            }
        }

        /// <summary>
        /// Сколько стопок влезает в зону по ширине и глубине. Если нужное число
        /// слотов не помещается, ячейка сжимается — иначе часть монет высыпалась бы
        /// за край зоны.
        /// </summary>
        private void CalculateGrid(int stacksNeeded, float cell, out int cols, out int rows)
        {
            Bounds bounds = _coinZone.bounds;
            float usableX = Mathf.Max(cell, bounds.size.x - _edgeMargin * 2f);
            float usableZ = Mathf.Max(cell, bounds.size.z - _edgeMargin * 2f);

            cols = Mathf.Max(1, Mathf.FloorToInt(usableX / cell));
            rows = Mathf.Max(1, Mathf.FloorToInt(usableZ / cell));

            if ((long)cols * rows < stacksNeeded)
            {
                float shrink = Mathf.Sqrt(stacksNeeded / (float)(cols * rows));
                cols = Mathf.Max(1, Mathf.FloorToInt(cols / shrink));
            }

            // Столбцов не должно быть больше, чем стопок, иначе сетка центрируется
            // по пустым ячейкам и куча уезжает к краю зоны.
            cols = Mathf.Clamp(cols, 1, stacksNeeded);
            rows = Mathf.Max(1, Mathf.CeilToInt(stacksNeeded / (float)cols));
        }

        private static float MaxStackStep(List<CoinPickupView> coins)
        {
            float step = 0.005f;
            foreach (var coin in coins)
            {
                if (coin == null) continue;
                step = Mathf.Max(step, coin.StackStep);
            }
            return step;
        }

        private static float MaxCoinWidth(List<CoinPickupView> coins)
        {
            float width = 0.01f;
            foreach (var coin in coins)
            {
                if (coin == null) continue;
                width = Mathf.Max(width, coin.CoinWidth);
            }
            return width;
        }

        /// <summary>
        /// Монета вышла из стопки (её разбросили), но осталась в общем списке кучи.
        /// </summary>
        public void DetachFromStack(CoinPickupView coin)
        {
            RemoveCoin(coin);
        }

        /// <summary>
        /// Убрать монету из стопки, сдвинув остальные вниз. Монета остаётся на столе,
        /// поэтому из _allCoins она НЕ вынимается.
        /// </summary>
        public void RemoveCoin(CoinPickupView coin)
        {
            if (coin == null) return;

            foreach (var stack in _stacks)
            {
                if (!stack.Coins.Remove(coin)) continue;

                coin.CurrentPile = null;
                ReflowStack(stack, _stackReflowDuration);

                if (stack.Count == 0)
                    _stacks.Remove(stack);

                break;
            }
        }

        private void ReflowStack(CoinStack stack, float duration)
        {
            for (int i = 0; i < stack.Count; i++)
            {
                var coin = stack.Coins[i];
                if (coin == null) continue;
                coin.MoveTo(stack.SlotPosition(i), stack.BaseRotation, duration, Ease.OutQuad);
            }
        }

        /// <summary>
        /// Регистрация новой монеты
        /// </summary>
        public void RegisterCoin(CoinPickupView coin)
        {
            if (coin == null) return;
            if (!_allCoins.Contains(coin))
                _allCoins.Add(coin);
        }

        /// <summary>
        /// Снимает уничтоженные монеты из учёта (монеты могли быть удалены сценой).
        /// </summary>
        private void PruneDestroyedCoins()
        {
            for (int i = _allCoins.Count - 1; i >= 0; i--)
            {
                if (_allCoins[i] == null)
                    _allCoins.RemoveAt(i);
            }

            for (int i = _flyingCoins.Count - 1; i >= 0; i--)
            {
                if (_flyingCoins[i].Coin == null)
                    _flyingCoins.RemoveAt(i);
            }

            for (int i = _stacks.Count - 1; i >= 0; i--)
            {
                _stacks[i].Coins.RemoveAll(c => c == null);
                if (_stacks[i].Count == 0)
                    _stacks.RemoveAt(i);
            }
        }

        /// <summary>
        /// Уничтожить N монет (после оплаты покупки в магазине).
        /// </summary>
        public int RemoveCoins(int count)
        {
            if (count <= 0) return 0;
            PruneDestroyedCoins();

            int removed = 0;
            for (int i = _allCoins.Count - 1; i >= 0 && removed < count; i--)
            {
                var coin = _allCoins[i];
                if (coin == null) continue;

                RemoveCoin(coin);
                _allCoins.RemoveAt(i);
                Destroy(coin.gameObject);
                removed++;
            }

            return removed;
        }

        /// <summary>
        /// Приводит количество физических монет к targetCount.
        /// Это единственная точка, где визуал догоняет счётчик Coins в PlayerInventorySO.
        /// </summary>
        public void SyncToCount(int targetCount)
        {
            PruneDestroyedCoins();
            if (targetCount < 0) targetCount = 0;

            if (_allCoins.Count > targetCount)
                RemoveCoins(_allCoins.Count - targetCount);
            else if (_allCoins.Count < targetCount)
                DropCoins(targetCount - _allCoins.Count);
        }

        /// <summary>
        /// Получить общее количество монет
        /// </summary>
        public int TotalCoins
        {
            get
            {
                PruneDestroyedCoins();
                return _allCoins.Count;
            }
        }

        /// <summary>
        /// Получить данные для чекпоинта
        /// </summary>
        public List<CoinPileCheckpointData> GetCheckpointData()
        {
            var result = new List<CoinPileCheckpointData>();

            foreach (var stack in _stacks)
            {
                if (stack.Count == 0) continue;

                result.Add(new CoinPileCheckpointData
                {
                    Position = ToZoneLocal(stack.BasePosition),
                    CoinCount = stack.Count,
                    IsStacked = true
                });
            }

            // Разлетевшиеся монеты сохраняем одной группой — точная форма кучи
            // после перезагрузки не важна, важно их количество.
            var scattered = new List<CoinPickupView>();
            foreach (var coin in _allCoins)
            {
                if (coin != null && !IsInAnyStack(coin))
                    scattered.Add(coin);
            }

            if (scattered.Count > 0)
            {
                Vector3 center = Vector3.zero;
                foreach (var coin in scattered)
                    center += coin.transform.position;
                center /= scattered.Count;

                result.Add(new CoinPileCheckpointData
                {
                    Position = ToZoneLocal(center),
                    CoinCount = scattered.Count,
                    IsStacked = false
                });
            }

            return result;
        }

        private bool IsInAnyStack(CoinPickupView coin)
        {
            foreach (var stack in _stacks)
            {
                if (stack.Coins.Contains(coin)) return true;
            }
            return false;
        }

        private Vector3 ToZoneLocal(Vector3 worldPosition)
        {
            return _coinZone != null
                ? _coinZone.transform.InverseTransformPoint(worldPosition)
                : worldPosition;
        }

        /// <summary>
        /// Восстановить из чекпоинта
        /// </summary>
        public void RestoreFromCheckpoint(List<CoinPileCheckpointData> data)
        {
            if (data == null || _coinPrefab == null || _coinZone == null) return;

            ClearAll();

            foreach (var pileData in data)
            {
                Vector3 worldPos = _coinZone.transform.TransformPoint(pileData.Position);
                float floorY = TryGetFloorHeight(
                    new Vector2(worldPos.x, worldPos.z), out float probed) ? probed : worldPos.y;

                // Монеты одной группы появляются с разбросом, иначе все сразу
                // оказались бы в одной точке и вытолкнули друг друга из физики.
                float scatter = CoinWidth * 0.6f;

                for (int i = 0; i < pileData.CoinCount; i++)
                {
                    var position = new Vector3(
                        worldPos.x + Random.Range(-scatter, scatter),
                        floorY + _dropHeight,
                        worldPos.z + Random.Range(-scatter, scatter));

                    var coin = Instantiate(_coinPrefab, position, FlatRandomRotation(), transform);
                    coin.name = $"Coin_{_allCoins.Count}";

                    RegisterCoin(coin);
                    coin.Drop();
                    TrackFlight(coin);
                }
            }
        }

        private void ClampToZone(Transform coinTransform)
        {
            if (_coinZone == null) return;

            Bounds bounds = _coinZone.bounds;
            float inset = Mathf.Min(_edgeMargin,
                Mathf.Min(bounds.size.x, bounds.size.z) * 0.25f);

            // Только X/Z: по Y монету держит физика, укладка не должна её поднимать.
            Vector3 position = coinTransform.position;
            position.x = Mathf.Clamp(position.x, bounds.min.x + inset, bounds.max.x - inset);
            position.z = Mathf.Clamp(position.z, bounds.min.z + inset, bounds.max.z - inset);
            coinTransform.position = position;
        }

        public void ClearAll()
        {
            SetHoveredCoin(null);
            _flyingCoins.Clear();

            foreach (var coin in _allCoins)
            {
                if (coin != null)
                    Destroy(coin.gameObject);
            }
            _allCoins.Clear();
            _stacks.Clear();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Стопка в сетке. BasePosition задаётся один раз при раскладке и не
        /// пересчитывается: позиция монеты — это BasePosition плюс её номер,
        /// поэтому башня не разъезжается, пока монеты укладываются.
        /// </summary>
        private sealed class CoinStack
        {
            public Vector3 BasePosition;
            public Quaternion BaseRotation = Quaternion.identity;
            public float Step = 0.005f;
            public readonly List<CoinPickupView> Coins = new List<CoinPickupView>();

            public int Count => Coins.Count;

            public Vector3 SlotPosition(int index) => BasePosition + Vector3.up * (Step * index);
        }

        /// <summary>Монета в полёте: ждём, пока упадёт, и кладём её на место.</summary>
        private struct FlyingCoin
        {
            public CoinPickupView Coin;
            public float TimeLeft;
            public float Elapsed;
        }
    }
}