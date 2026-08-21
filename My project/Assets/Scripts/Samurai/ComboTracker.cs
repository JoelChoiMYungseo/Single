using System;
using UnityEngine;

namespace VRGame.Samurai
{
    /// <summary>연속 절단 콤보와 점수. 짧은 시간 안에 계속 베면 배율이 올라가므로, 안전하게 한 그루씩
    /// 베는 것보다 위험을 안고 몰아서 베는 쪽이 이득이 된다 — 긴박감에 보상을 붙이는 장치다.</summary>
    public class ComboTracker : MonoBehaviour
    {
        [SerializeField] private KatanaSlicer slicer;
        [Tooltip("이 시간 안에 다시 베지 않으면 콤보가 끊긴다.")]
        [SerializeField] private float comboWindow = 2.5f;
        [SerializeField] private int maxMultiplier = 8;

        /// <summary>콤보, 배율, 총점</summary>
        public event Action<int, int, int> OnChanged;
        /// <summary>콤보가 끊긴 순간의 마지막 콤보 수</summary>
        public event Action<int> OnComboBroken;

        public int Combo { get; private set; }
        public int Score { get; private set; }
        public int Multiplier => Mathf.Clamp(1 + Combo / 3, 1, maxMultiplier);
        public int BestCombo { get; private set; }

        private float comboExpireTime;

        private void OnEnable()
        {
            slicer.OnSliced -= HandleSliced;
            slicer.OnSliced += HandleSliced;
        }

        private void OnDisable()
        {
            slicer.OnSliced -= HandleSliced;
        }

        private void Update()
        {
            if (Combo <= 0 || Time.time < comboExpireTime)
            {
                return;
            }

            int broken = Combo;
            Combo = 0;
            OnComboBroken?.Invoke(broken);
            OnChanged?.Invoke(Combo, Multiplier, Score);
        }

        private void HandleSliced(ISliceable target, float speed)
        {
            // 배율은 이번 절단을 포함한 값으로 계산한다 — 먼저 콤보를 올린 뒤 점수를 더한다.
            Combo++;
            if (Combo > BestCombo)
            {
                BestCombo = Combo;
            }

            Score += target.ScoreValue * Multiplier;
            comboExpireTime = Time.time + comboWindow;
            OnChanged?.Invoke(Combo, Multiplier, Score);
        }

        public void ResetRun()
        {
            Combo = 0;
            Score = 0;
            BestCombo = 0;
            OnChanged?.Invoke(Combo, Multiplier, Score);
        }
    }
}
