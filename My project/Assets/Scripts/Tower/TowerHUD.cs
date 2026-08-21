using UnityEngine;
using UnityEngine.UI;

namespace VRGame.Tower
{
    /// <summary>런 상태 표시 전용. 값을 가진 쪽이 밀어준다(Push) — UI가 스스로 매니저를 조회하면
    /// 초기화 순서가 보장되지 않는다. VR에서도 읽히도록 머리에 붙은 월드 스페이스 캔버스를 쓴다.</summary>
    public class TowerHUD : MonoBehaviour
    {
        [SerializeField] private Text heightLabel;
        [SerializeField] private Text bestLabel;
        [SerializeField] private Text stateLabel;
        [SerializeField] private Text budgetLabel;

        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color warningColor = new Color(1f, 0.75f, 0.2f);
        [SerializeField] private Color dangerColor = new Color(1f, 0.3f, 0.25f);

        public void SetHeight(float height)
        {
            if (heightLabel != null)
            {
                heightLabel.text = $"{height:0.0} m";
            }
        }

        public void SetBest(float best)
        {
            if (bestLabel != null)
            {
                bestLabel.text = $"최고 {best:0.0} m";
            }
        }

        /// <summary>디버그용 — 지금 몇 개의 블록이 실제로 시뮬레이션되고 있는지. Quest에서
        /// 프레임이 떨어질 때 이 숫자가 예산 안에 있는지 바로 확인할 수 있다.</summary>
        public void SetBudget(int simulated, int limit)
        {
            if (budgetLabel != null)
            {
                budgetLabel.text = $"물리 블록 {simulated}/{limit}";
            }
        }

        /// <summary>잡은 블록이 미끄러지는 중이면 경고, 슬로우모션 중이면 그걸 우선 표시한다.</summary>
        public void SetState(bool bulletTime, float heldStrain)
        {
            if (stateLabel == null)
            {
                return;
            }

            if (bulletTime)
            {
                stateLabel.text = "붙잡아!";
                stateLabel.color = dangerColor;
                return;
            }

            if (heldStrain > 0.05f)
            {
                stateLabel.text = heldStrain > 0.6f ? "빠진다!" : "미끄러진다";
                stateLabel.color = Color.Lerp(warningColor, dangerColor, heldStrain);
                return;
            }

            stateLabel.text = string.Empty;
            stateLabel.color = normalColor;
        }
    }
}
