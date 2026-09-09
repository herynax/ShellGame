// START OF FILE GlobalMetaServices.cs
using System;
using System.Collections.Generic;
using ShellGame.Core;
using ShellGame.Items;
using UnityEngine;
using Zenject;

namespace ShellGame.Meta
{
    public interface IGlobalProgressService
    {
        int TotalDeaths { get; }
        int TotalWins { get; }
    }

    public interface IUnlockManager
    {
        bool IsUnlocked(ItemDefinition item);
        IReadOnlyList<UnlockEntry> GetAllEntries();
        List<ItemDefinition> GetUnacknowledgedUnlocks();
        void AcknowledgeUnlock(ItemDefinition item);
    }

    public class GlobalProgressService : IGlobalProgressService, IInitializable, IDisposable
    {
        public int TotalDeaths { get; private set; }
        public int TotalWins { get; private set; }

        private int _sideDeathEventGeneration;
        private int _lastCountedDeathGeneration = -1;

        public void Initialize()
        {
            TotalDeaths = PlayerPrefs.GetInt("Meta_TotalDeaths", 0);
            TotalWins = PlayerPrefs.GetInt("Meta_TotalWins", 0);

            GameEvents.SideDied += OnSideDied;
            GameEvents.GameWon += OnGameWon;
        }

        public void Dispose()
        {
            GameEvents.SideDied -= OnSideDied;
            GameEvents.GameWon -= OnGameWon;
        }

        private void OnSideDied(TurnSide side)
        {
            _sideDeathEventGeneration++;
            if (side == TurnSide.Player)
                EnsureDeathCounted();
        }

        /// <summary>
        /// Идемпотентно гарантирует, что текущая смерть игрока уже учтена в
        /// TotalDeaths — можно вызвать заранее (например, из SceneLoader ДО
        /// проверки анлоков), не боясь посчитать одну и ту же смерть дважды:
        /// если OnSideDied уже отработал первым, повторный вызов ничего не
        /// изменит (сравнение generation).
        /// </summary>
        public void EnsureDeathCounted()
        {
            if (_lastCountedDeathGeneration == _sideDeathEventGeneration)
                return;

            _lastCountedDeathGeneration = _sideDeathEventGeneration;
            TotalDeaths++;
            PlayerPrefs.SetInt("Meta_TotalDeaths", TotalDeaths);
            PlayerPrefs.Save();
        }

        private void OnGameWon()
        {
            TotalWins++;
            PlayerPrefs.SetInt("Meta_TotalWins", TotalWins);
            PlayerPrefs.Save();
        }
    }

    public class UnlockManager : IUnlockManager
    {
        private readonly UnlocksConfig _config;
        private readonly IGlobalProgressService _progress;

        public UnlockManager(UnlocksConfig config, IGlobalProgressService progress)
        {
            _config = config;
            _progress = progress;
        }

        public bool IsUnlocked(ItemDefinition item)
        {
            if (_config == null) return true;
            foreach (var entry in _config.Entries)
            {
                if (entry.Item == item)
                    return entry.UnlockedByDefault || CheckCondition(entry);
            }
            return true;
        }

        public IReadOnlyList<UnlockEntry> GetAllEntries() => _config != null ? _config.Entries : new List<UnlockEntry>();

        public List<ItemDefinition> GetUnacknowledgedUnlocks()
        {
            var list = new List<ItemDefinition>();
            if (_config == null) return list;

            foreach (var entry in _config.Entries)
            {
                if (entry.UnlockedByDefault)
                    continue; // стартовые предметы не "открываются" — нечего анонсировать

                if (IsUnlocked(entry.Item))
                {
                    if (PlayerPrefs.GetInt("AckUnlock_" + entry.Item.name, 0) == 0)
                    {
                        list.Add(entry.Item);
                    }
                }
            }
            return list;
        }

        public void AcknowledgeUnlock(ItemDefinition item)
        {
            PlayerPrefs.SetInt("AckUnlock_" + item.name, 1);
            PlayerPrefs.Save();
        }

        private bool CheckCondition(UnlockEntry entry)
        {
            switch (entry.ConditionType)
            {
                case UnlockConditionType.TotalDeaths:
                    return _progress.TotalDeaths >= entry.RequiredAmount;
                case UnlockConditionType.TotalWins:
                    return _progress.TotalWins >= entry.RequiredAmount;
                default:
                    return true;
            }
        }
    }
}
// END OF FILE