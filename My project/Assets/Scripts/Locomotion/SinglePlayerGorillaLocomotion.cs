using UnityEngine;
using UnityEngine.XR;
using VRGame.Core;

namespace VRGame.Locomotion
{
    /// <summary>
    /// 고릴라 태그 스타일 팔 스윙 로코모션. 손이 이동한 궤적을 SphereCast로 검사해
    /// 주변 지형에 "닿았다"고 판단되면 그 이동 반대 방향으로 몸에 힘을 가한다
    /// (손으로 공간을 밀어내는 반작용 = 스윙). 버튼 입력이 아니라 순수 움직임 기반이라
    /// 계속 팔을 저어야만 이동이 유지되는 긴박감을 만든다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SinglePlayerGorillaLocomotion : MonoBehaviour, ILocomotionDriver
    {
        [Header("Hand References")]
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;

        [Header("Movement Tuning")]
        [SerializeField] private float handCastRadius = 0.12f;
        [SerializeField] private float swingForce = 14f;
        [SerializeField] private float maxSpeed = 9f;
        [SerializeField] private float airDrag = 1.2f;
        [SerializeField] private LayerMask locomotionMask = ~0;

        [Header("Haptics")]
        [SerializeField] private float hapticAmplitude = 0.3f;
        [SerializeField] private float hapticDuration = 0.05f;

        private Rigidbody rb;
        private Vector3 lastLeftPos;
        private Vector3 lastRightPos;
        private InputDevice leftDevice;
        private InputDevice rightDevice;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        private void OnEnable()
        {
            leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

            // 재활성화 시 오래된 위치 기준 그대로 쓰면 첫 프레임에 순간이동한 것 같은
            // 거짓 속도가 계산되므로 현재 손 위치로 다시 맞춘다.
            lastLeftPos = leftHand.position;
            lastRightPos = rightHand.position;
        }

        private void FixedUpdate()
        {
            if (!leftDevice.isValid)
            {
                leftDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            }
            if (!rightDevice.isValid)
            {
                rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            }

            ProcessHand(leftHand, ref lastLeftPos, leftDevice);
            ProcessHand(rightHand, ref lastRightPos, rightDevice);

            rb.linearVelocity *= Mathf.Clamp01(1f - airDrag * Time.fixedDeltaTime);

            if (rb.linearVelocity.magnitude > maxSpeed)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
            }
        }

        private void ProcessHand(Transform hand, ref Vector3 lastPos, InputDevice device)
        {
            Vector3 currentPos = hand.position;
            Vector3 delta = currentPos - lastPos;
            float distance = delta.magnitude;

            if (distance > 0.0001f)
            {
                Vector3 direction = delta / distance;

                if (Physics.SphereCast(lastPos, handCastRadius, direction, out _, distance, locomotionMask))
                {
                    Vector3 handVelocity = delta / Time.fixedDeltaTime;
                    rb.AddForce(-handVelocity * swingForce, ForceMode.Acceleration);
                    SendHaptic(device);
                }
            }

            lastPos = currentPos;
        }

        private void SendHaptic(InputDevice device)
        {
            if (device.isValid && device.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
            {
                device.SendHapticImpulse(0u, hapticAmplitude, hapticDuration);
            }
        }

        public Vector3 Velocity => rb != null ? rb.linearVelocity : Vector3.zero;

        /// <summary>PlayerRig가 순간이동시킨 뒤 통보한다. 손 기준점을 새 위치로 재동기화하지 않으면
        /// 몸이 이동한 거리가 그대로 "손이 초고속으로 움직였다"로 계산되어 리스폰 직후
        /// 플레이어가 로켓처럼 날아간다.</summary>
        public void OnTeleported(Vector3 position)
        {
            if (leftHand != null) lastLeftPos = leftHand.position;
            if (rightHand != null) lastRightPos = rightHand.position;
        }
    }
}
