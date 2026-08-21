using System;
using UnityEngine;

namespace VRGame.Survivor
{
    /// <summary>경험치 누적과 레벨업. 레벨업은 즉시 강해지는 보상이자 업그레이드 선택 화면을 여는
    /// 트리거다.</summary>
    public class PlayerExperience : MonoBehaviour
    {
        [SerializeField] private int baseExperienceToLevel = 5;
        [SerializeField] private float levelRequirementGrowth = 1.35f;

        /// <summary>현재 경험치, 다음 레벨까지 필요량</summary>
        public event Action<int, int> OnExperienceChanged;
        /// <summary>새 레벨</summary>
        public event Action<int> OnLeveledUp;

        private int currentExperience;
        private int experienceToLevel;

        public int Level { get; private set; }

        public void ResetForRun()
        {
            Level = 1;
            currentExperience = 0;
            experienceToLevel = baseExperienceToLevel;
            OnExperienceChanged?.Invoke(currentExperience, experienceToLevel);
        }

        public void AddExperience(int amount)
        {
            currentExperience += amount;

            // 한 번에 여러 레벨이 오를 수 있다 (보스 처치 등). while로 전부 소화한다.
            while (currentExperience >= experienceToLevel)
            {
                currentExperience -= experienceToLevel;
                Level++;
                experienceToLevel = Mathf.CeilToInt(experienceToLevel * levelRequirementGrowth);
                OnLeveledUp?.Invoke(Level);
            }

            OnExperienceChanged?.Invoke(currentExperience, experienceToLevel);
        }
    }
}
