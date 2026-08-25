using UnityEngine;
using UnityEngine.UI;

namespace VRGame.Magnetic
{
    /// <summary>런 상태 표시 전용. 값을 가진 쪽이 밀어준다(Push) — OnEnable/Start에서 매니저를
    /// 조회하지 않는다. VR에서도 읽히도록 머리에 붙은 월드 스페이스 캔버스를 쓴다.</summary>
    public class MagneticHUD : MonoBehaviour
    {
        [Header("Labels")]
        [SerializeField] private Text speedLabel;
        [SerializeField] private Text leftPolarityLabel;
        [SerializeField] private Text rightPolarityLabel;
        [SerializeField] private Text crashLabel;
        [SerializeField] private Text warningLabel;

        [Header("Colors")]
        [SerializeField] private Color attractColor = new Color(0.35f, 0.65f, 1f);
        [SerializeField] private Color repelColor = new Color(1f, 0.45f, 0.35f);
        [SerializeField] private Color warningColor = new Color(1f, 0.3f, 0.25f);

        public void SetSpeed(float speed)
        {
            if (speedLabel != null)
            {
                speedLabel.text = $"{speed:0.0} m/s";
            }
        }

        public void SetPolarity(bool isLeft, MagneticPolarity polarity)
        {
            Text label = isLeft ? leftPolarityLabel : rightPolarityLabel;
            if (label == null)
            {
                return;
            }

            bool isAttract = polarity == MagneticPolarity.Attract;
            label.text = isAttract ? "N (당김)" : "S (밀침)";
            label.color = isAttract ? attractColor : repelColor;
        }

        public void SetCrashCount(int count)
        {
            if (crashLabel != null)
            {
                crashLabel.text = $"충돌 {count}";
            }
        }

        public void SetWarning(bool hazardIncoming)
        {
            if (warningLabel == null)
            {
                return;
            }

            warningLabel.text = hazardIncoming ? "위험! 쇳덩이가 날아온다" : string.Empty;
            warningLabel.color = warningColor;
        }
    }
}
