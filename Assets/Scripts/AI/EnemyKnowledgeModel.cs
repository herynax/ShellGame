using System.Collections.Generic;
using ShellGame.Shells;
using UnityEngine;

namespace ShellGame.AI
{
    /// <summary>Одна запись о метке во внутренней модели противника (см. таблицу Knowledge в ГДД).</summary>
    public sealed class MarkerKnowledgeEntry
    {
        public int MarkerId;

        /// <summary>Слот (Shell.SlotIndex), под которым, по мнению ИИ, сейчас находится метка.</summary>
        public int CurrentSlotIndex;

        /// <summary>Последний достоверно известный слот — сохраняется в момент потери отслеживания.</summary>
        public int LastKnownSlotIndex;

        public bool IsTracked;
    }

    /// <summary>
    /// Knowledge-модель противника. Существует только внутри ИИ и может
    /// отличаться от реального состояния игрового поля — именно на её основе
    /// принимаются все решения (ГДД: "Все решения ИИ принимаются только на
    /// основании текущего состояния Knowledge").
    /// </summary>
    public sealed class EnemyKnowledgeModel
    {
        private readonly List<MarkerKnowledgeEntry> _entries = new List<MarkerKnowledgeEntry>();

        public IReadOnlyList<MarkerKnowledgeEntry> Entries => _entries;

        public void Reset()
        {
            _entries.Clear();
        }

        /// <summary>
        /// Состояние ObserveMarkers — перед перемешиванием противник получает
        /// достоверную информацию о начальном расположении всех меток.
        /// </summary>
        public void Observe(IReadOnlyList<Shell> shells)
        {
            _entries.Clear();
            int nextId = 0;
            foreach (var shell in shells)
            {
                if (!shell.HasMarker)
                    continue;

                _entries.Add(new MarkerKnowledgeEntry
                {
                    MarkerId = nextId++,
                    CurrentSlotIndex = shell.SlotIndex,
                    LastKnownSlotIndex = shell.SlotIndex,
                    IsTracked = true,
                });
            }
        }

        /// <summary>
        /// Состояние TrackShuffle — реакция на одно событие OnCupSwap(CupA, CupB).
        /// trackingLossMultiplier — множитель на Plose(D) (0..1), которым
        /// предмет "Наркотики" в руках противника временно снижает шанс потерять
        /// метку на этом перемешивании (по умолчанию 1 — без изменений).
        /// </summary>
        public void OnCupSwap(int slotA, int slotB, float difficultyIndex, EnemyAIConfig config, float trackingLossMultiplier = 1f)
        {
            foreach (var entry in _entries)
            {
                if (!entry.IsTracked)
                    continue;

                if (entry.CurrentSlotIndex != slotA && entry.CurrentSlotIndex != slotB)
                    continue;

                float pLose = config.EvaluateTrackingLossProbability(difficultyIndex) * Mathf.Clamp01(trackingLossMultiplier);
                bool lost = Random.value < pLose;

                if (!lost)
                {
                    entry.CurrentSlotIndex = entry.CurrentSlotIndex == slotA ? slotB : slotA;
                }
                else
                {
                    entry.LastKnownSlotIndex = entry.CurrentSlotIndex;
                    entry.IsTracked = false;
                }
            }
        }

        /// <summary>Доля отслеживаемых меток от общего числа — 1, если ничего не отслеживалось (например, меток не было или ObserveMarkers ещё не было). Используется предметами, чтобы оценить, насколько врагу сейчас "нужна" информация (Монокль/подстраховка на шаффле).</summary>
        public float GetTrackedFraction()
        {
            if (_entries.Count == 0)
                return 1f;

            return (float)GetTrackedEntries().Count / _entries.Count;
        }

        public List<MarkerKnowledgeEntry> GetTrackedEntries()
        {
            return _entries.FindAll(e => e.IsTracked);
        }
    }
}
