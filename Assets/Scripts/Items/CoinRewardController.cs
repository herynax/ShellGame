using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using FMODUnity;
using ShellGame.Audio;
using ShellGame.Core;
using ShellGame.Items;
using UnityEngine;

namespace ShellGame.Items
{
    /// <summary>
    /// Контроллер награды монетами после победы над врагом.
    /// Спавнит монеты у врага, анимирует их полет в зону монет.
    /// </summary>
    public sealed class CoinRewardController : MonoBehaviour
    {
        public static CoinRewardController Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private CoinPickupView _coinPrefab;
        [SerializeField] private int _maxCoinsPerSpawn = 20; // Limit for performance
        [SerializeField] private float _spawnDelay = 0.05f;
        [SerializeField] private float _flyDuration = 0.8f;
        [SerializeField] private Ease _flyEase = Ease.OutCubic;
        [SerializeField] private float _flyHeight = 1.5f; // Arc height
        [SerializeField] private float _spawnRadius = 0.3f; // Initial spawn spread

        [Header("Sounds")]
        [SerializeField] private EventReference _coinSpawnSound;
        [SerializeField] private EventReference _coinLandSound;

        private CoinPileController _pileController;
        private IAudioService _audio;
        private bool _isSpawning;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
            {
                Destroy(gameObject);
                return;
            }

            _pileController = CoinPileController.Instance;
            
            if (!ServiceLocator.TryGet<IAudioService>(out _audio))
            {
                _audio = new FMODAudioService();
                ServiceLocator.Register(_audio);
            }
        }

        /// <summary>
        /// Спавнить монеты за победу. Монеты анимируются от позиции врага к зоне монет.
        /// </summary>
        /// <param name="coinCount">Количество монет (обычно = текущее HP игрока)</param>
        /// <param name="enemyPosition">Позиция врага</param>
        /// <param name="coinZone">Зона монет (BoxCollider)</param>
        public void SpawnCoins(int coinCount, Vector3 enemyPosition, BoxCollider coinZone)
        {
            if (_isSpawning || _coinPrefab == null || _pileController == null || coinZone == null) return;
            if (coinCount <= 0) return;

            int actualCount = Mathf.Min(coinCount, _maxCoinsPerSpawn);
            StartCoroutine(SpawnCoinsRoutine(actualCount, enemyPosition, coinZone));
        }

        private IEnumerator SpawnCoinsRoutine(int count, Vector3 enemyPosition, BoxCollider coinZone)
        {
            _isSpawning = true;

            Vector3 zoneCenter = coinZone.bounds.center;
            Vector3 zoneSize = coinZone.bounds.size;

            for (int i = 0; i < count; i++)
            {
                // Spawn near enemy
                Vector3 spawnPos = enemyPosition + new Vector3(
                    Random.Range(-_spawnRadius, _spawnRadius),
                    0.5f,
                    Random.Range(-_spawnRadius, _spawnRadius)
                );

                var coin = Instantiate(_coinPrefab, spawnPos, Quaternion.identity, _pileController.transform);
                coin.name = $"RewardCoin_{i}";
                
                _pileController.RegisterCoin(coin);

                // Animate to random position in coin zone
                Vector3 targetPos = zoneCenter + new Vector3(
                    Random.Range(-zoneSize.x * 0.4f, zoneSize.x * 0.4f),
                    0.1f,
                    Random.Range(-zoneSize.z * 0.4f, zoneSize.z * 0.4f)
                );

                // Arc flight
                Vector3 midPoint = Vector3.Lerp(spawnPos, targetPos, 0.5f);
                midPoint.y += _flyHeight;

                coin.transform.DOKill();
                coin.transform.DOPath(new[] { spawnPos, midPoint, targetPos }, _flyDuration, PathType.CatmullRom)
                    .SetEase(_flyEase)
                    .OnComplete(() =>
                    {
                        _pileController.TryAddCoin(coin);
                        if (!_coinLandSound.IsNull)
                            _audio?.PlayOneShot(_coinLandSound, targetPos);
                    });

                if (!_coinSpawnSound.IsNull)
                    _audio?.PlayOneShot(_coinSpawnSound, spawnPos);

                if (_spawnDelay > 0f)
                    yield return new WaitForSeconds(_spawnDelay);
            }

            _isSpawning = false;
        }

        /// <summary>
        /// Мгновенно добавить монеты (для тестов или магазина)
        /// </summary>
        public void AddCoinsInstant(int count, BoxCollider coinZone)
        {
            if (_coinPrefab == null || _pileController == null || coinZone == null) return;

            Vector3 zoneCenter = coinZone.bounds.center;
            Vector3 zoneSize = coinZone.bounds.size;

            for (int i = 0; i < count; i++)
            {
                Vector3 targetPos = zoneCenter + new Vector3(
                    Random.Range(-zoneSize.x * 0.4f, zoneSize.x * 0.4f),
                    0.1f,
                    Random.Range(-zoneSize.z * 0.4f, zoneSize.z * 0.4f)
                );

                var coin = Instantiate(_coinPrefab, targetPos, Quaternion.identity, _pileController.transform);
                coin.name = $"InstantCoin_{i}";

                _pileController.RegisterCoin(coin);
                _pileController.TryAddCoin(coin);
            }
        }

        /// <summary>
        /// Добавить N монет в кучу (используется магазином при продаже).
        /// Делегирует CoinPileController, чтобы не дублировать логику зоны.
        /// </summary>
        public int GiveCoins(int count)
        {
            return _pileController != null ? _pileController.AddCoinsInstant(count) : 0;
        }
    }
}