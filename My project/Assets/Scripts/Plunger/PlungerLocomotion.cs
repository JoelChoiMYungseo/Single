using System;
using UnityEngine;
using UnityEngine.XR;
using VRGame.Core;

namespace VRGame.Plunger
{
    public enum PlungerState { Idle, Stuck, Compressed, Launched }

    /// <summary>양손 플런저로 벽(또는 몬스터 얼굴)에 흡착한 뒤, 몸을 뒤로 빼서 텐션을 만들고 트리거를
    /// 놓으면 그 반대(=고정점 쪽) 방향으로 슬링샷처럼 발사되는 로코모션. 흡착 중엔 고정점으로 몸을
    /// 당기는 스프링 힘이 지속되고(F = k·거리), 놓는 순간 그 시점의 방향·거리 기준으로 1회성 임펄스를
    /// 준다 — 활을 당겼다 놓는 것과 같은 방향(고정점 쪽으로) 이다.
    ///
    /// 이 스크립트는 자신이 무엇에 흡착됐는지(벽인지 몬스터인지) 전혀 모른다 — <see cref="OnStuck"/>로
    /// 흡착 대상의 Transform만 통보하고, 그게 몬스터인지 판단하는 건 그 이벤트를 구독하는 쪽
    /// (MonsterFaceStick)의 몫이다. 햅틱·오디오도 직접 재생하지 않고 이벤트로만 통보한다
    /// (PlungerFeedback이 소비) — 이 프로젝트에서 로코모션과 프레젠테이션을 분리하는 관례.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlungerLocomotion : MonoBehaviour, ILocomotionDriver
    {
        [Header("Hands")]
        [SerializeField] private Transform leftHandOrigin;
        [SerializeField] private Transform rightHandOrigin;
        [SerializeField] private XRNode leftHandNode = XRNode.LeftHand;
        [SerializeField] private XRNode rightHandNode = XRNode.RightHand;

        [Header("Targeting")]
        [SerializeField] private LayerMask stickableMask = ~0;
        [Tooltip("흡착 거리 — 이 범위 밖의 표면엔 플런저가 닿지 않는다.")]
        [SerializeField] private float maxStickDistance = 15f;

        [Header("Spring")]
        [Tooltip("스프링 강성. 고정점에서 멀어질수록(당길수록) 이 값에 비례해 몸을 고정점 쪽으로 당긴다.")]
        [SerializeField] private float springStiffness = 400f;
        [Tooltip("놓는 순간 가해지는 임펄스의 최대 크기.")]
        [SerializeField] private float maxLaunchForce = 30f;
        [Tooltip("텐션 계산에 쓰는 거리 상한 — 무한정 늘어나 지속 스프링 힘이 발산하지 않게 클램프.")]
        [SerializeField] private float maxPullDistance = 2.5f;
        [Tooltip("이 거리 이상 벌어져야 Stuck에서 Compressed로 전이한다.")]
        [SerializeField] private float compressThreshold = 0.05f;

        public PlungerState LeftState => leftState;
        public PlungerState RightState => rightState;

        /// <summary>PlungerFeedback이 구독한다. isLeft: 어느 손인지. state: 전이된 새 상태.</summary>
        public event Action<bool, PlungerState> OnStateChanged;
        /// <summary>MonsterFaceStick이 구독한다. isLeft: 어느 손인지. anchor: 이번에 흡착된 대상의
        /// Transform(레이캐스트 hit.collider.transform 그대로) — 벽이든 몬스터든 이 스크립트는 구분하지 않는다.</summary>
        public event Action<bool, Transform> OnStuck;
        /// <summary>PlungerFeedback·MonsterFaceStick이 구독한다. isLeft: 어느 손인지.
        /// impulse: 실제로 가해진 임펄스 벡터(방향+크기).</summary>
        public event Action<bool, Vector3> OnLaunched;

        private Rigidbody body;
        private InputDevice leftDevice;
        private InputDevice rightDevice;

        private bool leftTriggerHeld;
        private bool rightTriggerHeld;
        private Vector3 leftStuckPoint;
        private Vector3 rightStuckPoint;
        private PlungerState leftState;
        private PlungerState rightState;

        public Vector3 Velocity => body != null ? body.linearVelocity : Vector3.zero;
        /// <summary>MonsterFaceStick이 "튕겨 나감" 연출(발사 임펄스 반전)에 쓴다.</summary>
        public Rigidbody Body => body;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            leftDevice = InputDevices.GetDeviceAtXRNode(leftHandNode);
            rightDevice = InputDevices.GetDeviceAtXRNode(rightHandNode);
        }

        private void FixedUpdate()
        {
            RefreshDevices();

            ProcessHand(true, leftHandOrigin, leftDevice, ref leftTriggerHeld, ref leftStuckPoint, ref leftState);
            ProcessHand(false, rightHandOrigin, rightDevice, ref rightTriggerHeld, ref rightStuckPoint, ref rightState);
        }

        private void RefreshDevices()
        {
            if (!leftDevice.isValid) leftDevice = InputDevices.GetDeviceAtXRNode(leftHandNode);
            if (!rightDevice.isValid) rightDevice = InputDevices.GetDeviceAtXRNode(rightHandNode);
        }

        private void ProcessHand(bool isLeft, Transform origin, InputDevice device, ref bool wasTriggerHeld,
            ref Vector3 stuckPoint, ref PlungerState state)
        {
            if (origin == null)
            {
                return;
            }

            bool triggerHeld = ReadTrigger(device);

            if (state == PlungerState.Idle)
            {
                if (triggerHeld && !wasTriggerHeld && TryFindStickTarget(origin, out RaycastHit hit))
                {
                    stuckPoint = hit.point;
                    SetState(isLeft, PlungerState.Stuck, ref state);
                    OnStuck?.Invoke(isLeft, hit.collider.transform);
                }
            }
            else
            {
                if (triggerHeld)
                {
                    ApplyTension(isLeft, stuckPoint, ref state);
                }
                else
                {
                    Launch(isLeft, stuckPoint, ref state);
                }
            }

            wasTriggerHeld = triggerHeld;
        }

        private bool TryFindStickTarget(Transform origin, out RaycastHit hit)
        {
            return Physics.Raycast(origin.position, origin.forward, out hit, maxStickDistance, stickableMask,
                QueryTriggerInteraction.Ignore);
        }

        /// <summary>흡착 중 지속되는 스프링 힘 — 고정점에서 멀어질수록(당길수록) 몸을 고정점 쪽으로
        /// 끌어당긴다. 실제 플런저를 당겼을 때의 저항감과 동일한 방향.</summary>
        private void ApplyTension(bool isLeft, Vector3 stuckPoint, ref PlungerState state)
        {
            Vector3 toAnchor = stuckPoint - body.position;
            float distance = Mathf.Min(toAnchor.magnitude, maxPullDistance);

            PlungerState next = distance >= compressThreshold ? PlungerState.Compressed : PlungerState.Stuck;
            if (next != state)
            {
                SetState(isLeft, next, ref state);
            }

            if (distance <= 0.0001f)
            {
                return;
            }

            body.AddForce(toAnchor.normalized * (distance * springStiffness), ForceMode.Force);
        }

        /// <summary>트리거를 놓는 순간 — 그 시점의 고정점 방향으로 1회성 임펄스를 준다. 활시위를
        /// 당겼다 놓는 것과 같은 방향(당겨진 반대, 즉 고정점 쪽)이라 "슬링샷"이 된다.</summary>
        private void Launch(bool isLeft, Vector3 stuckPoint, ref PlungerState state)
        {
            Vector3 toAnchor = stuckPoint - body.position;
            float distance = Mathf.Min(toAnchor.magnitude, maxPullDistance);
            Vector3 impulse = distance > 0.0001f
                ? toAnchor.normalized * Mathf.Min(distance * springStiffness, maxLaunchForce)
                : Vector3.zero;

            if (impulse.sqrMagnitude > 0f)
            {
                body.AddForce(impulse, ForceMode.VelocityChange);
            }

            SetState(isLeft, PlungerState.Launched, ref state);
            OnLaunched?.Invoke(isLeft, impulse);
            SetState(isLeft, PlungerState.Idle, ref state);
        }

        private void SetState(bool isLeft, PlungerState newState, ref PlungerState state)
        {
            state = newState;
            OnStateChanged?.Invoke(isLeft, newState);
        }

        private static bool ReadTrigger(InputDevice device)
        {
            return device.isValid && device.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed) && pressed;
        }

        /// <summary>순간이동 직후 통보 — 흡착된 채로 리스폰되면 엉뚱한 방향으로 당겨지므로 양손 다 놓는다.</summary>
        public void OnTeleported(Vector3 position)
        {
            if (leftState != PlungerState.Idle)
            {
                SetState(true, PlungerState.Idle, ref leftState);
            }
            if (rightState != PlungerState.Idle)
            {
                SetState(false, PlungerState.Idle, ref rightState);
            }
            leftTriggerHeld = false;
            rightTriggerHeld = false;
        }
    }
}
