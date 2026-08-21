using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VRGame.Survivor
{
    /// <summary>레벨업 시 게임을 멈추고 3개 중 하나를 고르게 한다. 이 잠깐의 정지가 탕탕특공대의
    /// 리듬 — 계속 쫓기다가 숨 한 번 돌리고 강해져서 다시 뛰어드는 구조다.</summary>
    public class UpgradeSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerStats stats;
        [SerializeField] private PlayerHealth health;
        [SerializeField] private PlayerExperience experience;

        [Header("UI")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Button[] choiceButtons;
        [SerializeField] private Text[] choiceTitles;
        [SerializeField] private Text[] choiceDescriptions;

        private UpgradeDefinition[] pool;
        private readonly List<UpgradeDefinition> offered = new List<UpgradeDefinition>(3);
        private readonly List<UpgradeDefinition> shuffleBuffer = new List<UpgradeDefinition>(8);
        private int pendingChoices;

        private void Awake()
        {
            pool = UpgradeDefinition.CreateDefaultPool();

            // 패널 소유자가 초기 상태를 정한다 — 패널 자신의 Awake에서 끄면 다시 켤 때 꼬인다.
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            experience.OnLeveledUp -= HandleLeveledUp;
            experience.OnLeveledUp += HandleLeveledUp;

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                int index = i;
                choiceButtons[i].onClick.RemoveAllListeners();
                choiceButtons[i].onClick.AddListener(() => Choose(index));
            }
        }

        private void OnDisable()
        {
            experience.OnLeveledUp -= HandleLeveledUp;

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                choiceButtons[i].onClick.RemoveAllListeners();
            }
        }

        private void HandleLeveledUp(int newLevel)
        {
            pendingChoices++;
            if (pendingChoices == 1)
            {
                ShowNextChoice();
            }
        }

        private void ShowNextChoice()
        {
            RollOffers();

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                bool hasOffer = i < offered.Count;
                choiceButtons[i].gameObject.SetActive(hasOffer);
                if (!hasOffer)
                {
                    continue;
                }

                choiceTitles[i].text = offered[i].Title;
                choiceDescriptions[i].text = offered[i].Description;
            }

            panel.SetActive(true);
            Time.timeScale = 0f;
        }

        /// <summary>풀에서 중복 없이 3개를 뽑는다.</summary>
        private void RollOffers()
        {
            shuffleBuffer.Clear();
            shuffleBuffer.AddRange(pool);
            offered.Clear();

            int count = Mathf.Min(choiceButtons.Length, shuffleBuffer.Count);
            for (int i = 0; i < count; i++)
            {
                int pick = Random.Range(0, shuffleBuffer.Count);
                offered.Add(shuffleBuffer[pick]);
                shuffleBuffer.RemoveAt(pick);
            }
        }

        private void Choose(int index)
        {
            if (index >= offered.Count)
            {
                return;
            }

            offered[index].Apply(stats, health);

            pendingChoices--;
            if (pendingChoices > 0)
            {
                // 한 번에 여러 레벨이 올랐으면 연달아 고르게 한다.
                ShowNextChoice();
                return;
            }

            panel.SetActive(false);
            Time.timeScale = 1f;
        }

        /// <summary>런이 끝나면 남은 선택을 버리고 시간을 되돌린다.</summary>
        public void CancelAll()
        {
            pendingChoices = 0;
            if (panel != null)
            {
                panel.SetActive(false);
            }
            Time.timeScale = 1f;
        }
    }
}
