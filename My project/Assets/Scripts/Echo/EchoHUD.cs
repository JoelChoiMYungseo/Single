using UnityEngine;
using UnityEngine.UI;

namespace VRGame.Echo
{
    /// <summary>디버그/캘리브레이션용 최소 HUD. "화면이 안 보여야 하는" 장르라 화려한 UI를 넣지
    /// 않는다 — 마이크 게인을 맞추기 위한 막대와, 몬스터가 소리를 들었는지 정도만 보여준다.</summary>
    public class EchoHUD : MonoBehaviour
    {
        [SerializeField] private Image loudnessFill;
        [SerializeField] private Text warningLabel;

        public void SetLoudness(float value01)
        {
            if (loudnessFill != null)
            {
                loudnessFill.fillAmount = value01;
            }
        }

        public void SetHeardWarning(bool heard)
        {
            if (warningLabel != null)
            {
                warningLabel.text = heard ? "괴물이 들었다" : string.Empty;
            }
        }
    }
}
