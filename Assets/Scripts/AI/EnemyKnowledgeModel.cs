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
        /// Для каждой отслеживаемой метки: если она не участвует в обмене —
        /// не трогаем; если участвует — с вероятностью Plose(D) теряем
        /// отслеживание, иначе переносим позицию по факту обмена.
        /// </summary>
        public void OnCupSwap(int slotA, int slotB, float difficultyIndex, EnemyAIConfig config)
        {
            foreach (var entry in _entries)
            {
                if (!entry.IsTracked)
                    continue; // потерянные метки не обновляются без новой информации

                // Шаг 1: метка участвует в обмене?
                if (entry.CurrentSlotIndex != slotA && entry.CurrentSlotIndex != slotB)
                    continue;

                // Шаг 2: Plose(D) = max(Pmin, Pbase - k*D)
                float pLose = config.EvaluateTrackingLossProbability(difficultyIndex);
                bool lost = Random.value < pLose;

                if (!lost)
                {
                    // Шаг 3: отслеживание продолжается, позиция обновляется согласно обмену.
                    entry.CurrentSlotIndex = entry.CurrentSlotIndex == slotA ? slotB : slotA;
                }
                else
                {
                    // Шаг 4: метка потеряна до получения новой информации.
                    entry.LastKnownSlotIndex = entry.CurrentSlotIndex;
                    entry.IsTracked = false;
                }
            }
        }

        public List<MarkerKnowledgeEntry> GetTrackedEntries()
        {
            return _entries.FindAll(e => e.IsTracked);
        }
    }
}
