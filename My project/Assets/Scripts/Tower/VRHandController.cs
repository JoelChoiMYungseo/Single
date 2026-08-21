using UnityEngine;
using UnityEngine.XR;

namespace VRGame.Tower
{
    /// <summary>물리 손. 이 프로젝트에서 가장 중요한 컴포넌트다.
    ///
    /// 구조:
    ///   추적된 컨트롤러 위치(trackedTarget) ──[ConfigurableJoint 스프링]── 물리 손(이 오브젝트)
    ///                                                                        │
    ///                                                              [FixedJoint, 잡을 때만]
    ///                                                                        │
    ///                                                                    붙잡은 블록
    ///
    /// 손을 컨트롤러 위치로 "순간이동"시키지 않고 스프링으로 끌어당기는 이유는 하나다 — 손이 돌덩이를
    /// 뚫고 들어가지 못하게 하려면 물리 엔진이 손을 막을 수 있어야 한다. Transform을 직접 옮기면
    /// 충돌 판정을 건너뛰어 손이 지형 속에 박힌다.
    ///
    /// 등반은 부수 효과로 자연히 나온다: 손이 블록에 고정되고 몸은 그 손에 스프링으로 매달려 있으므로,
    /// 실제 손을 내리면 스프링이 몸을 끌어올린다. 그 과정에서 조인트에 걸리는 힘이 곧 블록이 받는
    /// 하중이고, 그것이 UnstableBlock의 붕괴 판정 입력이 된다.
    ///
    /// 회전은 ConfigurableJoint.targetRotation 대신 각속도 제어로 처리한다 — targetRotation은
    /// 조인트 초기 프레임 기준이라 계산이 미묘하게 틀리기 쉽고, 각속도 제어가 훨씬 안정적이다.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class VRHandController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("추적된 컨트롤러 포즈를 따라가는 트랜스폼 (XRNodeTracker 또는 임시 리그가 구동).")]
        [SerializeField] private Transform trackedTarget;
        [Tooltip("플레이어 몸통 Rigidbody. 손 스프링의 상대편이자 하중 계산의 기준 질량.")]
        [SerializeField] private Rigidbody playerBody;
        [SerializeField] private XRNode handNode = XRNode.RightHand;

        [Header("Hand Body")]
        [SerializeField] private float handMass = 2.5f;
        [SerializeField] private float handDrag = 2f;
        [SerializeField] private float handAngularDrag = 4f;

        [Header("Follow Spring — 손이 컨트롤러를 따라가는 힘")]
        [Tooltip("높을수록 손이 컨트롤러를 정확히 따라가지만, 너무 높으면 지형을 뚫고 떨림이 생긴다.")]
        [SerializeField] private float positionSpring = 3000f;
        [SerializeField] private float positionDamper = 120f;
        [Tooltip("스프링이 낼 수 있는 최대 힘. 이 값이 곧 '얼마나 무거운 것까지 밀 수 있는가'다.")]
        [SerializeField] private float positionMaxForce = 1500f;

        [Header("Rotation Follow")]
        [SerializeField] private float rotationStrength = 22f;
        [SerializeField] private float maxAngularSpeed = 30f;

        [Header("Grab")]
        [SerializeField] private float grabRadius = 0.16f;
        [SerializeField] private LayerMask grabbableMask = ~0;
        [Tooltip("잡을 때 만드는 조인트가 버티는 힘. 넘으면 손이 미끄러져 놓친다.")]
        [SerializeField] private float grabJointBreakForce = 9000f;
        [SerializeField] private float grabJointBreakTorque = 9000f;

        [Header("Weight Transfer — 내 몸무게가 블록을 누르는 정도")]
        [Tooltip("1이면 플레이어 무게가 그대로 블록에 실린다. 낮추면 블록이 잘 안 부서진다.")]
        [SerializeField] private float weightTransferScale = 1f;

        [Header("Stuck Recovery")]
        [Tooltip("손이 컨트롤러에서 이만큼 벌어지면 지형에 박힌 것으로 보고 강제로 되돌린다.")]
        [SerializeField] private float maxHandDistance = 0.55f;
        [SerializeField] private float snapBackCooldown = 0.25f;

        [Header("Haptics")]
        [SerializeField] private float grabHapticAmplitude = 0.55f;
        [SerializeField] private float grabHapticDuration = 0.07f;

        [Header("Input")]
        [Tooltip("체크하면 XR 그립 입력 대신 SetGrip()으로 외부에서 제어한다 (임시 조작용).")]
        [SerializeField] private bool useExternalGrip;

        /// <summary>지금 무언가를 붙잡고 있는가 — 등반 판정·HUD가 참조한다.</summary>
        public bool IsGripping { get; private set; }
        public bool HasGrabbedObject => grabbedJoint != null;
        /// <summary>지금 붙잡고 있는 대상. 잡은 블록의 미끄러짐 상태를 HUD가 읽는 데 쓴다.</summary>
        public IVRGrabbable GrabbedTarget => grabbedTarget;
        /// <summary>이번 스텝에 붙잡은 대상에 실린 하중 크기.</summary>
        public float CurrentLoad { get; private set; }

        private readonly Collider[] overlapBuffer = new Collider[12];
        private Rigidbody handBody;
        private ConfigurableJoint followJoint;
        private FixedJoint grabbedJoint;
        private IVRGrabbable grabbedTarget;
        private InputDevice device;
        private Quaternion followStartLocalRotation;
        private float nextSnapBackTime;
        private bool externalGrip;

        // ────────────────────────────── Lifecycle ──────────────────────────────

        private void Awake()
        {
            // 인스펙터/프리팹 세팅에 의존하지 않고 코드로 확정한다.
            handBody = GetComponent<Rigidbody>();
            handBody.mass = handMass;
            handBody.useGravity = false;
            handBody.isKinematic = false;
            handBody.linearDamping = handDrag;
            handBody.angularDamping = handAngularDrag;
            handBody.interpolation = RigidbodyInterpolation.Interpolate;
            handBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            handBody.maxAngularVelocity = maxAngularSpeed;

            BuildFollowJoint();
        }

        private void OnEnable()
        {
            device = InputDevices.GetDeviceAtXRNode(handNode);
        }

        private void OnDisable()
        {
            ReleaseGrab();
        }

        /// <summary>손을 몸통에 스프링으로 매단다. connectedAnchor를 목표 지점으로 옮기고
        /// targetPosition은 0으로 두는 것이 "조인트가 목표를 따라가게" 만드는 정석 패턴이다.</summary>
        private void BuildFollowJoint()
        {
            followJoint = gameObject.AddComponent<ConfigurableJoint>();
            followJoint.connectedBody = playerBody;
            followJoint.autoConfigureConnectedAnchor = false;
            followJoint.anchor = Vector3.zero;
            followJoint.connectedAnchor = Vector3.zero;

            followJoint.xMotion = ConfigurableJointMotion.Free;
            followJoint.yMotion = ConfigurableJointMotion.Free;
            followJoint.zMotion = ConfigurableJointMotion.Free;
            followJoint.angularXMotion = ConfigurableJointMotion.Free;
            followJoint.angularYMotion = ConfigurableJointMotion.Free;
            followJoint.angularZMotion = ConfigurableJointMotion.Free;

            var drive = new JointDrive
            {
                positionSpring = positionSpring,
                positionDamper = positionDamper,
                maximumForce = positionMaxForce,
            };
            followJoint.xDrive = drive;
            followJoint.yDrive = drive;
            followJoint.zDrive = drive;

            followJoint.targetPosition = Vector3.zero;
            followStartLocalRotation = transform.localRotation;
        }

        // ────────────────────────────── Physics step ──────────────────────────────

        private void FixedUpdate()
        {
            if (trackedTarget == null || playerBody == null)
            {
                return;
            }

            RefreshDevice();
            UpdateFollowTarget();
            UpdateRotation();
            ResolveStuckHand();
            UpdateGripState();
            TransferLoadToGrabbedTarget();
        }

        private void RefreshDevice()
        {
            if (!useExternalGrip && !device.isValid)
            {
                device = InputDevices.GetDeviceAtXRNode(handNode);
            }
        }

        /// <summary>목표 지점을 몸통 로컬 좌표로 변환해 조인트에 꽂는다. 몸통이 움직이거나 회전해도
        /// 손이 상대 위치를 유지한다.</summary>
        private void UpdateFollowTarget()
        {
            followJoint.connectedAnchor = playerBody.transform.InverseTransformPoint(trackedTarget.position);
        }

        /// <summary>각속도로 회전을 맞춘다. 최단 회전축을 구해 한 스텝에 도달할 속도를 넣고 상한을 둔다.</summary>
        private void UpdateRotation()
        {
            Quaternion delta = trackedTarget.rotation * Quaternion.Inverse(handBody.rotation);
            delta.ToAngleAxis(out float angleDegrees, out Vector3 axis);

            // ToAngleAxis는 0~360을 반환한다. 180을 넘으면 반대로 도는 게 더 짧다.
            if (angleDegrees > 180f)
            {
                angleDegrees -= 360f;
            }

            if (Mathf.Approximately(angleDegrees, 0f) || float.IsNaN(axis.x))
            {
                return;
            }

            Vector3 angularVelocity = axis.normalized * (angleDegrees * Mathf.Deg2Rad / Time.fixedDeltaTime);
            handBody.angularVelocity = Vector3.ClampMagnitude(angularVelocity * (rotationStrength * Time.fixedDeltaTime), maxAngularSpeed);
        }

        /// <summary>손이 블록 안에 박히면 스프링이 절대 이길 수 없는 상태가 된다. 일정 거리 이상
        /// 벌어지면 물리를 포기하고 컨트롤러 위치로 강제 복귀시킨다 — VR에서 이 안전장치가 없으면
        /// 손이 지형에 삼켜진 채 게임이 진행 불가능해진다.</summary>
        private void ResolveStuckHand()
        {
            if (HasGrabbedObject || Time.time < nextSnapBackTime)
            {
                return;
            }

            if (Vector3.Distance(handBody.position, trackedTarget.position) <= maxHandDistance)
            {
                return;
            }

            handBody.position = trackedTarget.position;
            handBody.rotation = trackedTarget.rotation;
            handBody.linearVelocity = Vector3.zero;
            handBody.angularVelocity = Vector3.zero;
            nextSnapBackTime = Time.time + snapBackCooldown;
        }

        // ────────────────────────────── Grab ──────────────────────────────

        private void UpdateGripState()
        {
            bool gripping = ReadGrip();

            if (gripping && !IsGripping)
            {
                TryGrab();
            }
            else if (!gripping && IsGripping)
            {
                ReleaseGrab();
            }

            IsGripping = gripping;

            // 조인트가 끊어졌거나 대상이 부서졌으면 잡은 상태를 정리한다.
            if (grabbedJoint == null && grabbedTarget != null)
            {
                ClearGrabReferences();
            }
            else if (grabbedTarget != null && !grabbedTarget.CanBeGrabbed)
            {
                ReleaseGrab();
            }
        }

        private bool ReadGrip()
        {
            if (useExternalGrip)
            {
                return externalGrip;
            }

            if (!device.isValid)
            {
                return false;
            }

            // 그립 버튼을 우선 보고, 없으면 트리거로 대체한다 (컨트롤러 프로파일 차이 흡수).
            if (device.TryGetFeatureValue(CommonUsages.gripButton, out bool grip) && grip)
            {
                return true;
            }
            return device.TryGetFeatureValue(CommonUsages.triggerButton, out bool trigger) && trigger;
        }

        /// <summary>임시(비VR) 조작에서 그립을 외부에서 넣는다.</summary>
        public void SetGrip(bool value)
        {
            externalGrip = value;
        }

        /// <summary>입력 출처를 전환한다. 임시 리그가 켜질 때 스스로 이걸 호출하므로, 모드가 바뀌면
        /// 그립 경로도 함께 따라간다 — 한쪽만 바뀌면 그립이 아예 먹지 않는다.</summary>
        public void SetUseExternalGrip(bool value)
        {
            useExternalGrip = value;
            if (value)
            {
                externalGrip = false;
            }
        }

        private void TryGrab()
        {
            int count = Physics.OverlapSphereNonAlloc(handBody.position, grabRadius, overlapBuffer,
                grabbableMask, QueryTriggerInteraction.Ignore);

            IVRGrabbable best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var candidate = overlapBuffer[i].GetComponentInParent<IVRGrabbable>();
                if (candidate == null || !candidate.CanBeGrabbed)
                {
                    continue;
                }

                float distance = Vector3.Distance(handBody.position, overlapBuffer[i].ClosestPoint(handBody.position));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            if (best == null)
            {
                return;
            }

            AttachTo(best);
        }

        private void AttachTo(IVRGrabbable target)
        {
            grabbedTarget = target;

            grabbedJoint = gameObject.AddComponent<FixedJoint>();
            grabbedJoint.connectedBody = target.Body; // null이면 월드에 고정된다 (정적 손잡이)
            grabbedJoint.autoConfigureConnectedAnchor = false;
            grabbedJoint.anchor = Vector3.zero;

            Vector3 gripPoint = target.GetGripPoint(handBody.position);
            grabbedJoint.connectedAnchor = target.Body != null
                ? target.Body.transform.InverseTransformPoint(gripPoint)
                : gripPoint;

            grabbedJoint.breakForce = grabJointBreakForce;
            grabbedJoint.breakTorque = grabJointBreakTorque;
            grabbedJoint.enableCollision = true;

            target.OnGrabbed(this);
            SendHaptic();
        }

        public void ReleaseGrab()
        {
            if (grabbedTarget != null)
            {
                grabbedTarget.OnReleased(this);
            }

            if (grabbedJoint != null)
            {
                Destroy(grabbedJoint);
            }

            ClearGrabReferences();
        }

        private void ClearGrabReferences()
        {
            grabbedJoint = null;
            grabbedTarget = null;
            CurrentLoad = 0f;
        }

        /// <summary>조인트에 실제로 걸린 힘과 플레이어 무게를 합쳐 대상에 통보한다.
        /// 조인트 힘만 보면 매달린 직후 한 스텝은 0에 가까워 붕괴가 늦게 시작되므로,
        /// 몸무게를 명시적으로 더해 "매달리는 순간부터" 하중이 걸리게 한다.</summary>
        private void TransferLoadToGrabbedTarget()
        {
            if (grabbedTarget == null || grabbedJoint == null)
            {
                return;
            }

            Vector3 jointForce = grabbedJoint.currentForce;
            Vector3 weight = Physics.gravity * playerBody.mass * weightTransferScale;
            Vector3 total = jointForce + weight;

            CurrentLoad = total.magnitude;
            grabbedTarget.ApplyLoad(total, handBody.position);
        }

        /// <summary>조인트가 breakForce를 넘어 끊어졌을 때 Unity가 호출한다 — 손이 미끄러진 상황.</summary>
        private void OnJointBreak(float breakForce)
        {
            ReleaseGrab();
            SendHaptic();
        }

        private void SendHaptic()
        {
            if (device.isValid && device.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
            {
                device.SendHapticImpulse(0u, grabHapticAmplitude, grabHapticDuration);
            }
        }
    }
}
