using UnityEngine;
using TMPro;
using DG.Tweening;

namespace ShellGame.Gameplay
{
    public sealed class MoneySystem : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _moneyText;

        private int _money;

        public int Money
        {
            get => _money;
            set
            {
                _money = value;
                UpdateMoneyText();
            }
        }

        private void Awake()
        {
            UpdateMoneyText();
        }

        private void UpdateMoneyText()
        {
            int displayedMoney = 0;

            DOTween.To(
                () => displayedMoney,
                value =>
                {
                    displayedMoney = value;
                    _moneyText.text = displayedMoney.ToString();
                },
                _money,
                1f
            )
            .SetEase(Ease.OutCubic);
        }
    }
}