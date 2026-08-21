using UnityEngine;
using UnityEngine.UI;

namespace VRGame.Survivor
{
    /// <summary>런 상태 표시 전용. 스스로 다른 시스템을 조회하지 않고, 값을 가진 쪽이 밀어준다(Push).
    /// OnEnable/Start에서 매니저를 찾아 읽으면 초기화 순서가 보장되지 않기 때문이다.</summary>
    public class SurvivorHUD : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private Image healthFill;
        [SerializeField] private Text healthLabel;

        [Header("Experience")]
        [SerializeField] private Image experienceFill;
        [SerializeField] private Text levelLabel;

        [Header("Run")]
        [SerializeField] private Text timerLabel;
        [SerializeField] private Text killLabel;

        [Header("Game Over")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private Text gameOverLabel;

        public void SetHealth(float current, float max)
        {
            if (healthFill != null)
            {
                healthFill.fillAmount = max <= 0f ? 0f : current / max;
            }
            if (healthLabel != null)
            {
                healthLabel.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            }
        }

        public void SetExperience(int current, int required)
        {
            if (experienceFill != null)
            {
                experienceFill.fillAmount = required <= 0 ? 0f : (float)current / required;
            }
        }

        public void SetLevel(int level)
        {
            if (levelLabel != null)
            {
                levelLabel.text = $"Lv.{level}";
            }
        }

        public void SetTime(float seconds)
        {
            if (timerLabel == null)
            {
                return;
            }

            int minutes = Mathf.FloorToInt(seconds / 60f);
            int remainder = Mathf.FloorToInt(seconds % 60f);
            timerLabel.text = $"{minutes:00}:{remainder:00}";
        }

        public void SetKills(int kills)
        {
            if (killLabel != null)
            {
                killLabel.text = $"처치 {kills}";
            }
        }

        public void ShowGameOver(float survivedSeconds, int kills, int level)
        {
            if (gameOverLabel != null)
            {
                int minutes = Mathf.FloorToInt(survivedSeconds / 60f);
                int remainder = Mathf.FloorToInt(survivedSeconds % 60f);
                gameOverLabel.text = $"생존 {minutes:00}:{remainder:00}\n처치 {kills}  ·  Lv.{level}\n\nR 키로 재시작";
            }
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
            }
        }

        public void HideGameOver()
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(false);
            }
        }
    }
}
