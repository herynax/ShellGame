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
    /// Контроллер кучи монет на столе. Управляет стопками, рассеиванием и сборкой.
    /// Одиночный клик по монете = разброс этой монеты, двойной клик = сложить всё
    /// в стопки по близости.
    ///
    /// Ввод пол polled здесь, а не через legacy OnMouse* на каждой монете: тот путь
    /// требует камеру с тегом MainCamera и в этом проекте не срабатывает надёжно
    /// (см. комментарий в ItemPickupView). Опрос живёт здесь, потому что контроллер
    /// persistent (DontDestroyOnLoad) и существует в бою, в магазине и на карте.
    /// </summary>
    public sealed class CoinPileController : MonoBehaviour
    {
        public static CoinPileController Instance { get; private set; }

        [Header("References")]
        [SerializeField] private CoinPickupView _coinPrefab;
        [SerializeField] private BoxCollider _coinZone; // Zone where coins are constrained

        public BoxCollider CoinZone
        {
            get => _coinZone;
            set => _coinZone = value;
        }

        [Header("Input")]
        [Tooltip("Радиус луча ховера, м. Чуть шире монеты, чтобы наведение не дрожало.")]
        [SerializeField, Range(0.01f, 0.2f)] private float _hoverRayRadius = 0.04f;
        [Tooltip("Второй клик быстрее этого времени считается двойным, с.")]
        [SerializeField, Range(0.1f, 0.6f)] private float _doubleClickThreshold = 0.3f;
        [Tooltip("Насколько близко должен быть второй клик к первому, в пикселях экрана.")]
        [SerializeField, Range(5f, 120f)] private float _doubleClickRadius = 40f;

        [Header("Scatter Settling")]
        [Tooltip("Сколько секунд монете максимум на падение, потом мы её укладываем принудительно.")]
        [SerializeField, Range(0.5f, 6f)] private float _maxFlightTime = 2.5f;

        [Header("Stacking")]
        [SerializeField] private float _stackDistance = 0.03f;
        [SerializeField] private int _maxCoinsPerStack = 10;
        [SerializeField] private float _stackFormationDuration = 0.3f;

        [Header("Visual")]
        [SerializeField] private GameObject _stackHighlightPrefab; // Optional highlight for stack

        private readonly List<CoinPickupView> _allCoins = new List<CoinPickupView>();
        private readonly List<CoinStack> _stacks = new List<CoinStack>();
        private readonly List<FlyingCoin> _flyingCoins = new List<FlyingCoin>();
        private int _nextStackId = 0;

        private IAudioService _audio;
        private Camera _cachedCamera;
        private CoinPickupView _hoveredCoin;
        private float _lastClickTime = -10f;
        private Vector2 _lastClickScreenPosition;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject); // Persist across scenes
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
            HandleInput();
            UpdateFlyingCoins();
        }

        /// <summary>
        /// Одиночный клик разбрасывает нажатую монету, двойной собирает всё в стопки.
        /// Курсор захвачен игрой — тогда монеты не трогаем, иначе клик по карте
        /// разлетал бы их вместе с прицелом.
        ///
        /// Второй клик ловим по времени и экранной позиции, а не по наведению на ту же
        /// монету: после разброса она улетела из-под курсора, и проверка «монета под
        /// курсором» погасила бы жест. Клик по другой монете рядом — это новый разброс,
        /// поэтому двойным считаем только клик по пустому месту.
        /// </summary>
        private void HandleInput()
        {
            if (Cursor.lockState == CursorLockMode.Locked || !Cursor.visible)
            {
                SetHoveredCoin(null);
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                SetHoveredCoin(null);
                return;
            }

            var screenPosition = mouse.position.ReadValue();
            SetHoveredCoin(PickCoinUnderCursor(screenPosition));

            if (!mouse.leftButton.wasPressedThisFrame) return;

            bool isDouble = Time.unscaledTime - _lastClickTime <= _doubleClickThreshold
                            && (screenPosition - _lastClickScreenPosition).sqrMagnitude < _doubleClickRadius * _doubleClickRadius;

            if (isDouble && _hoveredCoin == null)
            {
                _lastClickTime = -10f;
                StackAll();
                return;
            }

            _lastClickTime = Time.unscaledTime;
            _lastClickScreenPosition = screenPosition;

            if (_hoveredCoin == null) return;

            _hoveredCoin.RequestScatter();
            TrackFlight(_hoveredCoin);
        }

        private CoinPickupView PickCoinUnderCursor(Vector2 screenPosition)
        {
            var camera = ResolveCamera();
            if (camera == null || _allCoins.Count == 0) return null;

            Ray ray = camera.ScreenPointToRay(screenPosition);

            // Ищем именно монету, а не первый попавшийся коллайдер: под монетами лежит
            // поверхность падения, и обычный SphereCast её перехватывал бы.
            var hits = Physics.SphereCastAll(ray, _hoverRayRadius, 100f,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) return null;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                var coin = hit.collider.GetComponentInParent<CoinPickupView>();
                if (coin != null) return coin;
            }

            return null;
        }

        private void SetHoveredCoin(CoinPickupView coin)
        {
            if (ReferenceEquals(_hoveredCoin, coin)) return;

            _hoveredCoin?.SetHovered(false);
            _hoveredCoin = coin;
            _hoveredCoin?.SetHovered(true);
        }

        private Camera ResolveCamera()
        {
            if (_cachedCamera == null) _cachedCamera = Camera.main;
            if (_cachedCamera == null) _cachedCamera = FindFirstObjectByType<Camera>();
            return _cachedCamera;
        }

        /// <summary>
        /// Разлетевшаяся монета укладывается, когда успокоилась или истекло время падения.
        /// Без этого куча росла бы с каждой разлетевшейся монетой, а монетка могла бы
        /// улететь за пределы зоны.
        /// </summary>
        private void UpdateFlyingCoins()
        {
            for (int i = _flyingCoins.Count - 1; i >= 0; i--)
            {
                var flying = _flyingCoins[i];
                if (flying.Coin == null)
                {
                    _flyingCoins.RemoveAt(i);
                    continue;
                }

                flying.TimeLeft -= Time.deltaTime;

                var body = flying.Coin.Body;
                bool asleep = body == null
                    || body.IsSleeping()
                    || (body.linearVelocity.sqrMagnitude < 0.0004f && body.angularVelocity.sqrMagnitude < 0.0004f);

                if (!asleep && flying.TimeLeft > 0f) continue;

                ClampToZone(flying.Coin.transform);
                flying.Coin.Settle();
                _flyingCoins.RemoveAt(i);
            }
        }

        private void TrackFlight(CoinPickupView coin)
        {
            if (coin == null) return;
            if (_flyingCoins.Exists(f => f.Coin == coin)) return;
            _flyingCoins.Add(new FlyingCoin { Coin = coin, TimeLeft = _maxFlightTime });
        }

        /// <summary>
        /// Попытаться добавить монету в существующую стопку или создать новую
        /// </summary>
        public bool TryAddCoin(CoinPickupView coin)
        {
            if (coin == null) return false;

            // Find nearest stack
            CoinStack nearestStack = null;
            float nearestDist = float.MaxValue;

            foreach (var stack in _stacks)
            {
                if (stack.Coins.Count >= _maxCoinsPerStack) continue;
                
                float dist = Vector3.Distance(coin.transform.position, stack.CenterPosition);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearestStack = stack;
                }
            }

            // If close enough to a stack, add to it
            if (nearestStack != null && nearestDist < 0.2f)
            {
                AddCoinToStack(coin, nearestStack);
                return true;
            }

            // Otherwise create new stack
            CreateNewStack(coin);
            return true;
        }

        private void AddCoinToStack(CoinPickupView coin, CoinStack stack)
        {
            stack.Coins.Add(coin);
            coin.CurrentPile = this;
            
            int index = stack.Coins.Count - 1;
            Vector3 targetPos = stack.CenterPosition + Vector3.up * (_stackDistance * index);

            // Стопка — это уложенное состояние: физику на время переноса выключаем.
            coin.MoveTo(targetPos, _stackFormationDuration, Ease.OutBack);

            // Update stack center if needed
            RecalculateStackCenter(stack);
        }

        private void CreateNewStack(CoinPickupView coin)
        {
            var stack = new CoinStack
            {
                Id = _nextStackId++,
                CenterPosition = coin.transform.position,
                Coins = new List<CoinPickupView> { coin }
            };
            
            _stacks.Add(stack);
            coin.CurrentPile = this;
            coin.Settle();
        }

        /// <summary>
        /// Рассеять все монеты в зоне (используется при пересборке кучи).
        /// Обычной кнопки нет — разброс идёт по клику на монету.
        /// </summary>
        public void ScatterAll()
        {
            if (_coinZone == null) return;

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
        /// Сложить все монеты в стопки по близости (двойной клик по разбросанным).
        /// </summary>
        public void StackAll()
        {
            if (_coinZone == null || _allCoins.Count == 0) return;

            SetHoveredCoin(null);
            _flyingCoins.Clear();
            PruneDestroyedCoins();

            // Group coins by proximity and create stacks
            var remainingCoins = new List<CoinPickupView>(_allCoins);
            _stacks.Clear();

            while (remainingCoins.Count > 0)
            {
                var baseCoin = remainingCoins[0];
                var stackCoins = new List<CoinPickupView> { baseCoin };
                remainingCoins.RemoveAt(0);

                // Find nearby coins
                for (int i = remainingCoins.Count - 1; i >= 0; i--)
                {
                    if (Vector3.Distance(baseCoin.transform.position, remainingCoins[i].transform.position) < 0.3f)
                    {
                        stackCoins.Add(remainingCoins[i]);
                        remainingCoins.RemoveAt(i);
                        
                        if (stackCoins.Count >= _maxCoinsPerStack)
                            break;
                    }
                }

                // Create stack
                var stack = new CoinStack
                {
                    Id = _nextStackId++,
                    CenterPosition = CalculateCenter(stackCoins),
                    Coins = stackCoins
                };
                _stacks.Add(stack);

                // Animate to stack positions
                for (int i = 0; i < stackCoins.Count; i++)
                {
                    var targetPos = stack.CenterPosition + Vector3.up * (_stackDistance * i);
                    stackCoins[i].CurrentPile = this;
                    stackCoins[i].MoveTo(targetPos, _stackFormationDuration, Ease.OutBack);
                }
            }
        }

        private Vector3 CalculateCenter(List<CoinPickupView> coins)
        {
            Vector3 sum = Vector3.zero;
            foreach (var c in coins)
                sum += c.transform.position;
            return sum / coins.Count;
        }

        private void RecalculateStackCenter(CoinStack stack)
        {
            if (stack.Coins.Count == 0) return;
            stack.CenterPosition = CalculateCenter(stack.Coins);
        }

        /// <summary>
        /// Монета вышла из стопки (её разбросили), но осталась в общем списке кучи.
        /// </summary>
        public void DetachFromStack(CoinPickupView coin)
        {
            RemoveCoin(coin);
        }

        /// <summary>
        /// Удалить монету из стопки. Монета остаётся на столе (её разбросали),
        /// поэтому из _allCoins она НЕ вынимается.
        /// </summary>
        public void RemoveCoin(CoinPickupView coin)
        {
            if (coin == null) return;
            foreach (var stack in _stacks)
            {
                if (stack.Coins.Remove(coin))
                {
                    // Reposition remaining coins in stack
                    for (int i = 0; i < stack.Coins.Count; i++)
                    {
                        Vector3 targetPos = stack.CenterPosition + Vector3.up * (_stackDistance * i);
                        stack.Coins[i].MoveTo(targetPos, 0.15f, Ease.OutQuad);
                    }

                    if (stack.Coins.Count == 0)
                        _stacks.Remove(stack);
                    break;
                }
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
                _stacks[i].Coins.RemoveAll(c => c == null);
        }

        /// <summary>
        /// Мгновенно создать N монет, разложив их по зоне. Без анимации.
        /// </summary>
        public int AddCoinsInstant(int count)
        {
            if (count <= 0) return 0;
            if (_coinPrefab == null || _coinZone == null)
            {
                Debug.LogWarning("[CoinPileController] Не заданы префаб монеты или зона — монеты не созданы.");
                return 0;
            }

            Vector3 center = _coinZone.bounds.center;
            Vector3 size = _coinZone.bounds.size;

            for (int i = 0; i < count; i++)
            {
                var pos = center + new Vector3(
                    Random.Range(-size.x * 0.4f, size.x * 0.4f),
                    0.1f,
                    Random.Range(-size.z * 0.4f, size.z * 0.4f));

                var coin = Instantiate(_coinPrefab, pos, Quaternion.identity, transform);
                coin.name = $"Coin_{_allCoins.Count}";
                RegisterCoin(coin);
                TryAddCoin(coin);
                // Выдача сразу кладёт монету: лежать в стопке, а не падать с высоты.
                coin.Settle();
            }
            return count;
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
                AddCoinsInstant(targetCount - _allCoins.Count);
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
                if (stack.Coins.Count > 0)
                {
                    // Save relative to coin zone
                    Vector3 localPos = _coinZone != null 
                        ? _coinZone.transform.InverseTransformPoint(stack.CenterPosition)
                        : stack.CenterPosition;
                    
                    result.Add(new CoinPileCheckpointData
                    {
                        Position = localPos,
                        CoinCount = stack.Coins.Count,
                        IsStacked = true
                    });
                }
            }

            // Also save scattered coins
            var scatteredCoins = new List<CoinPickupView>();
            foreach (var coin in _allCoins)
            {
                bool inStack = false;
                foreach (var stack in _stacks)
                {
                    if (stack.Coins.Contains(coin))
                    {
                        inStack = true;
                        break;
                    }
                }
                if (!inStack)
                    scatteredCoins.Add(coin);
            }

            if (scatteredCoins.Count > 0)
            {
                Vector3 center = CalculateCenter(scatteredCoins);
                Vector3 localPos = _coinZone != null
                    ? _coinZone.transform.InverseTransformPoint(center)
                    : center;
                
                result.Add(new CoinPileCheckpointData
                {
                    Position = localPos,
                    CoinCount = scatteredCoins.Count,
                    IsStacked = false
                });
            }

            return result;
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
                
                for (int i = 0; i < pileData.CoinCount; i++)
                {
                    var coin = Instantiate(_coinPrefab, worldPos + Vector3.up * (_stackDistance * i), Quaternion.identity, transform);
                    coin.name = $"Coin_{_allCoins.Count}";
                    coin.Settle();
                    
                    // Ensure it stays in zone
                    ClampToZone(coin.transform);
                    
                    RegisterCoin(coin);
                    
                    if (pileData.IsStacked)
                    {
                        // Will be handled by stack logic
                    }
                }
            }

            // Rebuild stacks
            if (_allCoins.Count > 0)
            {
                StackAll();
            }
        }

        private void ClampToZone(Transform coinTransform)
        {
            if (_coinZone == null) return;
            
            Bounds bounds = _coinZone.bounds;
            Vector3 pos = coinTransform.position;
            pos.x = Mathf.Clamp(pos.x, bounds.min.x, bounds.max.x);
            pos.z = Mathf.Clamp(pos.z, bounds.min.z, bounds.max.z);
            coinTransform.position = pos;
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

        private class CoinStack
        {
            public int Id;
            public Vector3 CenterPosition;
            public List<CoinPickupView> Coins = new List<CoinPickupView>();
        }

        /// <summary>Монета в полёте: ждём, пока упадёт, и кладём её на место.</summary>
        private struct FlyingCoin
        {
            public CoinPickupView Coin;
            public float TimeLeft;
        }
    }
}