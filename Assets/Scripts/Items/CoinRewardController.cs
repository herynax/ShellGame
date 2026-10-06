using System.Collections;
using FMODUnity;
using ShellGame.Audio;
using ShellGame.Core;
using UnityEngine;

namespace ShellGame.Items
{
    /// <summary>
    /// Контроллер награды монетами после победы над врагом.
    ///
    /// Монеты больше не летят по дуге от врага (DOPath + Settle в конце): они
    /// появляются над зоной и падают в неё, как и любая другая выдача. Точка
    /// появления, пол и физика живут в CoinPileController — здесь только счётчик
    /// и задержка между монетами, чтобы падение шло не одной стеной.
    /// </summary>
    public sealed class CoinRewardController : MonoBehaviour
    {
        public static CoinRewardController Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private CoinPickupView _coinPrefab;
        [Tooltip("Ограничение за раз, чтобы не завалить сцену физическими монетами.")]
        [SerializeField, Min(1)] private int _maxCoinsPerSpawn = 20;
        [Tooltip("Пауза между падениями, с.")]
        [SerializeField, Min(0f)] private float _spawnDelay = 0.05f;

        [Header("Sounds")]
        [SerializeField] private EventReference _coinSpawnSound;

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
        /// Спавнить монеты за победу — падением сверху зоны.
        /// </summary>
        /// <param name="coinCount">Количество монет (обычно = текущее HP игрока)</param>
        /// <param name="enemyPosition">
        /// Позиция врага. Больше не используется: раньше от неё строилась дуга
        /// полёта. Оставлена, чтобы не трогать вызов в GameManager.
        /// </param>
        /// <param name="coinZone">Зона монет (BoxCollider)</param>
        public void SpawnCoins(int coinCount, Vector3 enemyPosition, BoxCollider coinZone)
        {
            if (_isSpawning || _coinPrefab == null || _pileController == null || coinZone == null) return;
            if (coinCount <= 0) return;

            int actualCount = Mathf.Min(coinCount, _maxCoinsPerSpawn);
            StartCoroutine(SpawnCoinsRoutine(actualCount, coinZone));
        }

        private IEnumerator SpawnCoinsRoutine(int count, BoxCollider coinZone)
        {
            _isSpawning = true;

            // Зону и кучу берём заново перед выдачей: пока шла корутина, мог
            // смениться энкаунтер, и монеты полетели бы в прошлую зону.
            var pile = CoinPileController.Instance;
            if (pile == null || pile.CoinZone == null)
            {
                _isSpawning = false;
                yield break;
            }

            Vector3 soundPosition = coinZone.bounds.center;

            for (int i = 0; i < count; i++)
            {
                pile.DropCoins(1);

                if (!_coinSpawnSound.IsNull)
                    _audio?.PlayOneShot(_coinSpawnSound, soundPosition);

                if (_spawnDelay > 0f)
                    yield return new WaitForSeconds(_spawnDelay);
            }

            _isSpawning = false;
        }
    }
}