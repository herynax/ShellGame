using System.Collections.Generic;
using DG.Tweening;
using ShellGame.Audio;
using ShellGame.Core;
using ShellGame.Meta;
using UnityEngine;

namespace ShellGame.Items
{
    /// <summary>
    /// Контроллер кучи монет на столе. Управляет стопками, рассеиванием и сборкой.
    /// Клик по куче = рассеять. Двойной клик = сложить в аккуратную стопку.
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

        [Header("Stacking")]
        [SerializeField] private float _stackDistance = 0.03f;
        [SerializeField] private int _maxCoinsPerStack = 10;
        [SerializeField] private float _stackFormationDuration = 0.3f;

        [Header("Visual")]
        [SerializeField] private GameObject _stackHighlightPrefab; // Optional highlight for stack

        private readonly List<CoinPickupView> _allCoins = new List<CoinPickupView>();
        private readonly List<CoinStack> _stacks = new List<CoinStack>();
        private int _nextStackId = 0;

        private IAudioService _audio;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
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

        private void Start()
        {
            // Register for clicks on this pile controller (for scatter/stack)
            // We'll use a separate collider or raycast
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
            
            coin.transform.DOKill();
            coin.transform.DOMove(targetPos, _stackFormationDuration).SetEase(Ease.OutBack);
            
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
            
            // Coin is already at the right position
        }

        /// <summary>
        /// Рассеять все монеты в куче (клик по куче)
        /// </summary>
        public void ScatterAll()
        {
            if (_coinZone == null) return;

            Vector3 zoneCenter = _coinZone.bounds.center;
            Vector3 zoneSize = _coinZone.bounds.size;

            foreach (var coin in _allCoins)
            {
                if (coin == null) continue;
                
                Vector3 randomPos = zoneCenter + new Vector3(
                    Random.Range(-zoneSize.x * 0.4f, zoneSize.x * 0.4f),
                    0,
                    Random.Range(-zoneSize.z * 0.4f, zoneSize.z * 0.4f)
                );
                randomPos.y = coin.transform.position.y;

                coin.Scatter(randomPos, 0.1f);
            }

            _stacks.Clear();
        }

        /// <summary>
        /// Сложить все монеты в аккуратные стопки (дабл-клик)
        /// </summary>
        public void StackAll()
        {
            if (_coinZone == null || _allCoins.Count == 0) return;

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
                    Vector3 targetPos = stack.CenterPosition + Vector3.up * (_stackDistance * i);
                    stackCoins[i].transform.DOKill();
                    stackCoins[i].transform.DOMove(targetPos, _stackFormationDuration).SetEase(Ease.OutBack);
                    stackCoins[i].CurrentPile = this;
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
        /// Удалить монету из стопки
        /// </summary>
        public void RemoveCoin(CoinPickupView coin)
        {
            foreach (var stack in _stacks)
            {
                if (stack.Coins.Remove(coin))
                {
                    // Reposition remaining coins in stack
                    for (int i = 0; i < stack.Coins.Count; i++)
                    {
                        Vector3 targetPos = stack.CenterPosition + Vector3.up * (_stackDistance * i);
                        stack.Coins[i].transform.DOKill();
                        stack.Coins[i].transform.DOMove(targetPos, 0.15f).SetEase(Ease.OutQuad);
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
            if (!_allCoins.Contains(coin))
                _allCoins.Add(coin);
        }

        /// <summary>
        /// Получить общее количество монет
        /// </summary>
        public int TotalCoins => _allCoins.Count;

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
    }
}