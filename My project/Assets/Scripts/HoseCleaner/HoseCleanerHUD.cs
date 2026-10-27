using UnityEngine;
using UnityEngine.UI;

namespace VRGame.HoseCleaner
{
    /// <summary>런 상태 표시 전용. 다른 4개 게임의 HUD와 동일한 원칙 — 스스로 아무것도 조회하지 않고
    /// RunManager가 값을 밀어준다(Push).</summary>
    public class HoseCleanerHUD : MonoBehaviour
    {
        [SerializeField] private Text cleanedCountLabel;
        [SerializeField] private Text messageLabel;

        public void SetCleanedCount(int cleaned, int target)
        {
            if (cleanedCountLabel != null)
            {
                cleanedCountLabel.text = $"청소 {cleaned} / {target}";
            }
        }

        public void SetMessage(string message)
        {
            if (messageLabel != null)
            {
                messageLabel.text = message;
            }
        }
    }
}
