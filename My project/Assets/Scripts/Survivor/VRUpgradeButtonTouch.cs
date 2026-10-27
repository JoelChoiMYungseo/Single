using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace VRGame.Survivor
{
    /// <summary>업그레이드 패널은 마우스 클릭을 전제로 만들어졌는데 VR에는 마우스가 없다. 대신 손을
    /// 카드에 직접 갖다 대는 것으로 선택한다 — 팔을 저어 이동하는 게임이라 "손으로 건드려서 고른다"가
    /// 별도 포인터 시스템을 새로 만드는 것보다 몸에 붙는 상호작용이다.</summary>
    public class VRUpgradeButtonTouch : MonoBehaviour
    {
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;
        [SerializeField] private Button[] choiceButtons;
        [SerializeField] private float touchRadius = 0.18f;
        [SerializeField] private float retriggerCooldown = 0.4f;

        private float nextAllowedTime;

        private void Update()
        {
            // Time.timeScale이 0이어도 Update는 계속 돌아서 선택 패널이 떠 있는 동안에도 반응한다.
            if (Time.unscaledTime < nextAllowedTime || choiceButtons == null)
            {
                return;
            }

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                Button button = choiceButtons[i];
                if (button == null || !button.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (IsHandTouching(leftHand, button.transform) || IsHandTouching(rightHand, button.transform))
                {
                    nextAllowedTime = Time.unscaledTime + retriggerCooldown;
                    button.onClick.Invoke();
                    return;
                }
            }
        }

        private bool IsHandTouching(Transform hand, Transform target)
        {
            if (hand == null)
            {
                return false;
            }

            bool touching = Vector3.Distance(hand.position, target.position) <= touchRadius;
            if (touching)
            {
                SendHaptic(hand == leftHand ? XRNode.LeftHand : XRNode.RightHand);
            }
            return touching;
        }

        private static void SendHaptic(XRNode node)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid && device.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
            {
                device.SendHapticImpulse(0u, 0.5f, 0.08f);
            }
        }
    }
}
