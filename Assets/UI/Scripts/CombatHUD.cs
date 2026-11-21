
using UnityEngine;
using UnityEngine.UI;

namespace OrbCombat
{
    public class CombatHUD : MonoBehaviour
    {
        [Header("HUD Images")]
        [SerializeField] private Image reloadStatus;
        [SerializeField] private Image hpBar;

        [Header("Reload Sprites")]
        [SerializeField] private Sprite readySprite;
        [SerializeField] private Sprite waitSprite;

        [Header("HP Sprites")]
        [SerializeField] private Sprite hpFull;
        [SerializeField] private Sprite hpTwo;
        [SerializeField] private Sprite hpOne;
        [SerializeField] private Sprite hpEmpty;

        public void UpdateReload(
            bool flying,
            bool returning,
            float recoveryRemaining)
        {
            if (reloadStatus == null)
                return;

            bool waiting =
                flying || returning || recoveryRemaining > 0f;

            Sprite target = waiting ? waitSprite : readySprite;

            if (target != null && reloadStatus.sprite != target)
            {
                reloadStatus.sprite = target;
            }
        }

        public void UpdateHP(int currentHP)
        {
            if (hpBar == null)
                return;

            Sprite target;

            if (currentHP >= 3)
            {
                target = hpFull;
            }
            else if (currentHP == 2)
            {
                target = hpTwo;
            }
            else if (currentHP == 1)
            {
                target = hpOne;
            }
            else
            {
                target = hpEmpty;
            }

            if (target != null && hpBar.sprite != target)
            {
                hpBar.sprite = target;
            }
        }
    }
}
