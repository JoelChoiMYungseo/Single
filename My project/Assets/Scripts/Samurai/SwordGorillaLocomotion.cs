using UnityEngine;
using UnityEngine.XR;
using VRGame.Core;

namespace VRGame.Samurai
{
    /// <summary>카타나 장대높이뛰기 로코모션. 칼끝이 지형에 박히는 순간의 속도를 읽어 그 반대 방향으로
    /// 플레이어를 튕겨낸다. 고릴라 태그의 팔 젓기가 "손으로 땅을 미는" 것이라면, 이쪽은 "칼로 땅을
    /// 찍어 몸을 날리는" 것 — 같은 반작용 원리지만 도달 거리가 훨씬 길어 슬랩스틱한 과장이 나온다.
    ///
    /// 물리 조작은 전부 FixedUpdate에서만 수행한다 (RULE-03).</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SwordGorillaLocomotion : MonoBehaviour, ILocomotionDriver
    {
        [Header("References")]
        [SerializeField] private Transform katanaTip;
        [SerializeField] private Transform freeHand;
        [SerializeField] private XRNode swordHandNode = XRNode.RightHand;
        [SerializeField] private XRNode freeHandNode = XRNode.LeftHand;

        [Header("Pole Vault — 칼끝 반작용")]
        [Tooltip("칼끝 속도를 몇 배로 되돌려줄지. 1이면 찌른 속도만큼 그대로 튕겨나간다.")]
        [SerializeField] private float vaultForce = 1.35f;
        [Tooltip("표면 법선 방향으로 추가로 튕기는 양. 높을수록 위로 크게 솟는다.")]
        [SerializeField] private float bounceMultiplier = 0.85f;
        [Tooltip("한 번의 도약으로 얻을 수 있는 최대 속력 (m/s).")]
        [SerializeField] private float maxLaunchSpeed = 16f;
        [Tooltip("칼끝 판정 반경. 크면 잘 걸리지만 허공에서도 발사되는 느낌이 난다.")]
        [SerializeField] private float tipCastRadius = 0.06f;
        [Tooltip("연속 발사 방지. 칼을 박은 채로 매 프레임 힘이 누적되는 것을 막는다.")]
        [SerializeField] private float vaultCooldown = 0.2f;
        [Tooltip("이 속력 미만으로 칼끝을 움직이면 도약이 발동하지 않는다.")]
        [SerializeField] private float minVaultSpeed = 1.2f;

        [Header("Free Hand — 고릴라 팔 젓기")]
        [SerializeField] private bool enableHandSwing = true;
        [SerializeField] private float handSwingForce = 9f;
        [SerializeField] private float handCastRadius = 0.11f;

        [Header("Body")]
        [SerializeField] private float maxSpeed = 20f;
        [SerializeField] private float airDrag = 0.6f;
        [SerializeField] private LayerMask environmentMask = ~0;

        [Header("Haptics")]
        [SerializeField] private float vaultHapticAmplitude = 0.8f;
        [SerializeField] private float vaultHapticDuration = 0.12f;
        [SerializeField] private float swingHapticAmplitude = 0.25f;
        [SerializeField] private float swingHapticDuration = 0.05f;

        /// <summary>가장 최근 도약 세기 — 이펙트·사운드가 참조한다.</summary>
        public float LastVaultSpeed { get; private set; }

        private Rigidbody body;
        private Vector3 lastTipPosition;
        private Vector3 lastFreeHandPosition;
        private float nextVaultTime;
        private InputDevice swordDevice;
        private InputDevice freeDevice;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = true;
            body.isKinematic = false;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void OnEnable()
        {
            swordDevice = InputDevices.GetDeviceAtXRNode(swordHandNode);
            freeDevice = InputDevices.GetDeviceAtXRNode(freeHandNode);

            // 비활성화 동안 벌어진 위치 차이를 그대로 쓰면 첫 프레임에 거짓 속도로 발사된다.
            if (katanaTip != null) lastTipPosition = katanaTip.position;
            if (freeHand != null) lastFreeHandPosition = freeHand.position;
        }

        private void FixedUpdate()
        {
            RefreshDevices();
            TryPoleVault();

            if (enableHandSwing && freeHand != null)
            {
                TryHandSwing();
            }

            ApplyDragAndClamp();
        }

        private void RefreshDevices()
        {
            if (!swordDevice.isValid) swordDevice = InputDevices.GetDeviceAtXRNode(swordHandNode);
            if (!freeDevice.isValid) freeDevice = InputDevices.GetDeviceAtXRNode(freeHandNode);
        }

        /// <summary>칼끝이 이번 물리 스텝 동안 지나간 선분을 SphereCast로 훑어, 지형에 닿았으면
        /// 그 속도의 반대 방향으로 몸을 쏘아 올린다.</summary>
        private void TryPoleVault()
        {
            if (katanaTip == null || Time.time < nextVaultTime)
            {
                if (katanaTip != null) lastTipPosition = katanaTip.position;
                return;
            }

            Vector3 previousTip = lastTipPosition;
            Vector3 currentTip = katanaTip.position;
            lastTipPosition = currentTip;

            Vector3 travel = currentTip - previousTip;
            float distance = travel.magnitude;
            if (distance < 0.0001f)
            {
                return;
            }

            Vector3 tipVelocity = travel / Time.fixedDeltaTime;
            if (tipVelocity.magnitude < minVaultSpeed)
            {
                return;
            }

            Vector3 direction = travel / distance;
            if (!Physics.SphereCast(previousTip, tipCastRadius, direction, out RaycastHit hit,
                    distance, environmentMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            // 반작용: 찌른 속도의 반대 방향
            Vector3 launch = -tipVelocity * vaultForce;

            // 표면을 정면으로 찍을수록 법선 방향으로 더 크게 솟게 한다 — 장대높이뛰기의 "튕김".
            float intoSurface = Mathf.Max(0f, Vector3.Dot(tipVelocity, -hit.normal));
            launch += hit.normal * intoSurface * bounceMultiplier;

            if (launch.magnitude > maxLaunchSpeed)
            {
                launch = launch.normalized * maxLaunchSpeed;
            }

            body.AddForce(launch, ForceMode.VelocityChange);

            LastVaultSpeed = launch.magnitude;
            nextVaultTime = Time.time + vaultCooldown;
            SendHaptic(swordDevice, vaultHapticAmplitude, vaultHapticDuration);
        }

        /// <summary>빈손은 기존 고릴라 태그처럼 약하게 민다. 카타나만으로는 미세 조정이 어려워
        /// 착지 후 자세를 고치는 용도로 남겨둔다.</summary>
        private void TryHandSwing()
        {
            Vector3 previousHand = lastFreeHandPosition;
            Vector3 currentHand = freeHand.position;
            lastFreeHandPosition = currentHand;

            Vector3 travel = currentHand - previousHand;
            float distance = travel.magnitude;
            if (distance < 0.0001f)
            {
                return;
            }

            Vector3 direction = travel / distance;
            if (!Physics.SphereCast(previousHand, handCastRadius, direction, out _,
                    distance, environmentMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            Vector3 handVelocity = travel / Time.fixedDeltaTime;
            body.AddForce(-handVelocity * handSwingForce, ForceMode.Acceleration);
            SendHaptic(freeDevice, swingHapticAmplitude, swingHapticDuration);
        }

        private void ApplyDragAndClamp()
        {
            body.linearVelocity *= Mathf.Clamp01(1f - airDrag * Time.fixedDeltaTime);

            if (body.linearVelocity.magnitude > maxSpeed)
            {
                body.linearVelocity = body.linearVelocity.normalized * maxSpeed;
            }
        }

        public Vector3 Velocity => body != null ? body.linearVelocity : Vector3.zero;

        /// <summary>PlayerRig가 순간이동시킨 뒤 통보한다. 손 기준점을 새 위치로 다시 잡아야 한다 —
        /// 안 그러면 몸이 이동한 거리가 그대로 "칼끝이 초고속으로 움직였다"로 계산되어
        /// 리스폰 직후 플레이어가 로켓처럼 날아간다.</summary>
        public void OnTeleported(Vector3 position)
        {
            if (katanaTip != null) lastTipPosition = katanaTip.position;
            if (freeHand != null) lastFreeHandPosition = freeHand.position;
        }

        private static void SendHaptic(InputDevice device, float amplitude, float duration)
        {
            if (device.isValid && device.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
            {
                device.SendHapticImpulse(0u, amplitude, duration);
            }
        }
    }
}
