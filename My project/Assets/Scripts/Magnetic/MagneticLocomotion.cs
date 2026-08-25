using UnityEngine;
using UnityEngine.XR;
using VRGame.Core;

namespace VRGame.Magnetic
{
    public enum MagneticPolarity { Attract, Repel }

    /// <summary>양손으로 금속 표면에 인력/척력을 쏘는 로코모션. 트리거로 발사하고, 그립으로 극을
    /// 바꾼다. 거리가 가까울수록 힘이 급격히 커지도록 설계했다 — 그래야 벽에 가까워질수록 "이러다
    /// 처박힌다"는 압박이 커지고, 마지막 순간 극을 못 바꾸면 그대로 고속 충돌로 이어진다.
    ///
    /// 힘 계산은 물리적으로 정확한 역제곱(1/r²)이 아니라 지수를 인스펙터로 노출한다 — 기획서가
    /// 요구한 "거리에 따라 지수적으로 커지는 느낌"은 실제 자기력 공식보다 과장된 근접 폭증이 필요해서,
    /// distanceFalloffExponent를 2보다 키우면 더 극단적인 하이스피드 연출이 나온다.
    ///
    /// 조준된 대상이 플레이어보다 가벼운 <see cref="MagneticHazard"/>면 반대로 그 물체를 플레이어
    /// 쪽으로 당긴다 — "실수로 쇳덩이를 내 얼굴로 끌어당기는" 자해 위험이 여기서 나온다.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MagneticLocomotion : MonoBehaviour, ILocomotionDriver
    {
        [Header("Hands")]
        [SerializeField] private Transform leftHandOrigin;
        [SerializeField] private Transform rightHandOrigin;
        [SerializeField] private XRNode leftHandNode = XRNode.LeftHand;
        [SerializeField] private XRNode rightHandNode = XRNode.RightHand;

        [Header("Targeting")]
        [SerializeField] private LayerMask metalMask = ~0;
        [SerializeField] private float maxRange = 40f;

        [Header("Force")]
        [Tooltip("힘 공식의 분자. forceConstant / distance^exponent")]
        [SerializeField] private float forceConstant = 40f;
        [Tooltip("2면 역제곱(물리적으로 자연스러움). 크게 줄수록 근접 시 힘이 폭증해 더 과장된 하이스피드 연출이 된다.")]
        [SerializeField] private float distanceFalloffExponent = 2.2f;
        [Tooltip("이 거리보다 가까우면 이 값으로 클램프한다 — 안 그러면 거의 0인 거리에서 힘이 무한대로 발산한다.")]
        [SerializeField] private float minDistance = 1.2f;
        [SerializeField] private float maxForce = 260f;

        [Header("Crash Coupling")]
        [Tooltip("스턴 중에는 발사를 무시한다. 비워두면 스턴 개념 없이 항상 발사 가능.")]
        [SerializeField] private CrashHandler crashHandler;

        [Header("Haptics")]
        [SerializeField] private float hapticMinAmplitude = 0.15f;
        [SerializeField] private float hapticMaxAmplitude = 0.9f;
        [SerializeField] private float hapticDuration = 0.05f;

        public MagneticPolarity LeftPolarity { get; private set; } = MagneticPolarity.Attract;
        public MagneticPolarity RightPolarity { get; private set; } = MagneticPolarity.Attract;
        public bool LeftFiring { get; private set; }
        public bool RightFiring { get; private set; }
        /// <summary>지금 어느 손이든 자기보다 가벼운 하자드를 끌어당기고 있는가 — HUD 경고에 쓴다.</summary>
        public bool IsAnyHazardAttracted => leftAttractedHazard != null || rightAttractedHazard != null;

        private Rigidbody body;
        private InputDevice leftDevice;
        private InputDevice rightDevice;

        private bool leftUseExternalInput;
        private bool rightUseExternalInput;
        private bool leftExternalFire;
        private bool rightExternalFire;

        private MagneticHazard leftAttractedHazard;
        private MagneticHazard rightAttractedHazard;

        public Vector3 Velocity => body != null ? body.linearVelocity : Vector3.zero;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = true;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void OnEnable()
        {
            leftDevice = InputDevices.GetDeviceAtXRNode(leftHandNode);
            rightDevice = InputDevices.GetDeviceAtXRNode(rightHandNode);
        }

        /// <summary>임시(비VR) 조작이 XR 입력 대신 직접 값을 넣을 때 쓴다. VR로 돌아가면 다시
        /// 꺼서 XR 그립/트리거 입력 경로로 복귀한다.</summary>
        public void SetUseExternalInput(bool isLeft, bool value)
        {
            if (isLeft) leftUseExternalInput = value;
            else rightUseExternalInput = value;
        }

        public void SetExternalFire(bool isLeft, bool firing)
        {
            if (isLeft) leftExternalFire = firing;
            else rightExternalFire = firing;
        }

        public void ToggleExternalPolarity(bool isLeft)
        {
            if (isLeft) LeftPolarity = Flip(LeftPolarity);
            else RightPolarity = Flip(RightPolarity);
        }

        private static MagneticPolarity Flip(MagneticPolarity polarity)
        {
            return polarity == MagneticPolarity.Attract ? MagneticPolarity.Repel : MagneticPolarity.Attract;
        }

        private void FixedUpdate()
        {
            RefreshDevices();

            bool leftStunned = crashHandler != null && crashHandler.IsStunned;

            ProcessHand(leftHandOrigin, leftDevice, leftUseExternalInput, leftExternalFire, leftStunned,
                LeftPolarity, ref leftAttractedHazard, out bool leftFiring);
            ProcessHand(rightHandOrigin, rightDevice, rightUseExternalInput, rightExternalFire, leftStunned,
                RightPolarity, ref rightAttractedHazard, out bool rightFiring);

            LeftFiring = leftFiring;
            RightFiring = rightFiring;
        }

        private void RefreshDevices()
        {
            if (!leftDevice.isValid) leftDevice = InputDevices.GetDeviceAtXRNode(leftHandNode);
            if (!rightDevice.isValid) rightDevice = InputDevices.GetDeviceAtXRNode(rightHandNode);
        }

        private void ProcessHand(Transform origin, InputDevice device, bool useExternal, bool externalFire,
            bool stunned, MagneticPolarity polarity, ref MagneticHazard attractedHazard, out bool isFiring)
        {
            isFiring = false;

            if (origin == null || stunned)
            {
                SetHazardAttracted(ref attractedHazard, null);
                return;
            }

            bool triggerHeld = useExternal ? externalFire : ReadTrigger(device);
            if (!triggerHeld)
            {
                SetHazardAttracted(ref attractedHazard, null);
                return;
            }

            if (!Physics.Raycast(origin.position, origin.forward, out RaycastHit hit, maxRange, metalMask,
                    QueryTriggerInteraction.Ignore))
            {
                SetHazardAttracted(ref attractedHazard, null);
                return;
            }

            isFiring = true;
            ApplyMagneticForce(origin, hit, polarity, ref attractedHazard);
            SendHaptic(device, hit.distance);
        }

        /// <summary>대상이 플레이어보다 가벼운 MagneticHazard면 그 물체를 당기고, 아니면(벽 취급)
        /// 반작용을 플레이어에게 건다. 실제 자석이라면 둘 다 서로를 당기지만, 가벼운 쪽이 압도적으로
        /// 더 많이 끌려온다는 물리적 직관을 "누가 움직이는가"라는 이분법으로 단순화했다.</summary>
        private void ApplyMagneticForce(Transform origin, RaycastHit hit, MagneticPolarity polarity,
            ref MagneticHazard attractedHazard)
        {
            float distance = Mathf.Max(minDistance, hit.distance);
            float magnitude = Mathf.Min(maxForce, forceConstant / Mathf.Pow(distance, distanceFalloffExponent));

            Vector3 towardSurface = (hit.point - origin.position).normalized;
            float sign = polarity == MagneticPolarity.Attract ? 1f : -1f;

            MagneticHazard hazard = hit.rigidbody != null ? hit.rigidbody.GetComponent<MagneticHazard>() : null;

            if (hazard != null && hazard.IsLighterThan(body.mass))
            {
                // 가벼운 쇳덩이 쪽이 날아온다 — 방향은 플레이어 쪽(towardSurface의 반대)이다.
                hazard.Body.AddForce(-towardSurface * sign * magnitude, ForceMode.Force);
                SetHazardAttracted(ref attractedHazard, hazard);
            }
            else
            {
                body.AddForce(towardSurface * sign * magnitude, ForceMode.Force);
                SetHazardAttracted(ref attractedHazard, null);
            }
        }

        private static void SetHazardAttracted(ref MagneticHazard current, MagneticHazard next)
        {
            if (current == next)
            {
                return;
            }
            if (current != null)
            {
                current.SetAttracted(false);
            }
            if (next != null)
            {
                next.SetAttracted(true);
            }
            current = next;
        }

        private static bool ReadTrigger(InputDevice device)
        {
            return device.isValid && device.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed) && pressed;
        }

        private void SendHaptic(InputDevice device, float distance)
        {
            if (!device.isValid || !device.TryGetHapticCapabilities(out HapticCapabilities caps) || !caps.supportsImpulse)
            {
                return;
            }

            float proximity = Mathf.Clamp01(1f - distance / maxRange);
            float amplitude = Mathf.Lerp(hapticMinAmplitude, hapticMaxAmplitude, proximity);
            device.SendHapticImpulse(0u, amplitude, hapticDuration);
        }

        /// <summary>PlayerRig가 순간이동시킨 뒤 통보한다. 조준 중이던 하자드의 견인 상태를 풀어야
        /// 텔레포트 직후에도 계속 당겨지는 상태로 남지 않는다.</summary>
        public void OnTeleported(Vector3 position)
        {
            SetHazardAttracted(ref leftAttractedHazard, null);
            SetHazardAttracted(ref rightAttractedHazard, null);
        }
    }
}
