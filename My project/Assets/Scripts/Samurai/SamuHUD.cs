using UnityEngine;
using UnityEngine.UI;

namespace VRGame.Samurai
{
    /// <summary>런 상태 표시 전용. 스스로 다른 시스템을 조회하지 않고 값을 가진 쪽이 밀어준다(Push) —
    /// OnEnable/Start에서 매니저를 찾아 읽으면 초기화 순서가 보장되지 않는다.
    ///
    /// VR에서도 읽히도록 화면 오버레이가 아니라 머리에 붙은 월드 스페이스 캔버스를 쓴다.</summary>
    public class SamuHUD : MonoBehaviour
    {
        [Header("Labels")]
        [SerializeField] private Text zoneLabel;
        [SerializeField] private Text heightLabel;
        [SerializeField] private Text comboLabel;
        [SerializeField] private Text scoreLabel;
        [SerializeField] private Text warningLabel;
        [SerializeField] private Text fallLabel;

        [Header("Warning")]
        [Tooltip("위협과의 거리가 이보다 좁아지면 경고를 띄운다.")]
        [SerializeField] private float warningGap = 12f;
        [SerializeField] private Color warningColor = new Color(1f, 0.35f, 0.3f);

        public void SetZone(int index, int total, string name)
        {
            if (zoneLabel != null)
            {
                zoneLabel.text = $"구역 {index + 1}/{total} · {name}";
            }
        }

        public void SetHeight(float height)
        {
            if (heightLabel != null)
            {
                heightLabel.text = $"고도 {height:0.0} m";
            }
        }

        public void SetCombo(int combo, int multiplier)
        {
            if (comboLabel == null)
            {
                return;
            }

            comboLabel.text = combo > 0 ? $"{combo} 연속  ×{multiplier}" : string.Empty;
        }

        public void SetScore(int score)
        {
            if (scoreLabel != null)
            {
                scoreLabel.text = $"{score:N0} 점";
            }
        }

        public void SetFalls(int falls)
        {
            if (fallLabel != null)
            {
                fallLabel.text = $"추락 {falls}";
            }
        }

        /// <summary>아래에서 올라오는 위협과의 거리. 가까워질수록 경고가 진해진다.</summary>
        public void SetHazardGap(float gap)
        {
            if (warningLabel == null)
            {
                return;
            }

            if (gap > warningGap)
            {
                warningLabel.text = string.Empty;
                return;
            }

            warningLabel.text = $"▲ 아래에서 올라온다  {Mathf.Max(0f, gap):0.0} m";

            float intensity = Mathf.InverseLerp(warningGap, 0f, gap);
            warningLabel.color = Color.Lerp(Color.white, warningColor, intensity);
        }
    }
}
