using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace VRGame.Samurai
{
    /// <summary>속도 기반 절단. 칼날을 갖다 대기만 해서는 잘리지 않고, 일정 속력 이상으로 휘둘러야
    /// 베인다. "제대로 휘둘렀다"는 손맛이 이 임계값에서 나온다.
    ///
    /// 절단 평면은 칼날 축과 진행 방향의 외적으로 구한다 — 칼이 지나간 궤적면이 곧 잘린 면이다.</summary>
    public class KatanaSlicer : MonoBehaviour
    {
        [Header("Blade")]
        [SerializeField] private Transform bladeBase;
        [SerializeField] private Transform bladeTip;
        [SerializeField] private float bladeRadius = 0.05f;

        [Header("Slice Condition")]
        [Tooltip("이 속력(m/s) 이상으로 휘둘러야 잘린다. 낮추면 툭 쳐도 잘려서 손맛이 사라진다.")]
        [SerializeField] private float sliceSpeedThreshold = 3.5f;
        [Tooltip("같은 대상을 연속으로 다시 베지 않도록 하는 시간.")]
        [SerializeField] private float retargetCooldown = 0.25f;
        [SerializeField] private LayerMask sliceableMask = ~0;

        [Header("Feedback")]
        [SerializeField] private XRNode swordHandNode = XRNode.RightHand;
        [SerializeField] private float hapticAmplitude = 0.7f;
        [SerializeField] private float hapticDuration = 0.06f;

        /// <summary>절단 성공 — 베인 대상과 그때의 칼날 속력. 콤보·점수·사운드가 구독한다.</summary>
        public event System.Action<ISliceable, float> OnSliced;

        /// <summary>가장 최근 절단 속력 — 사운드 피치·이펙트 크기에 쓴다.</summary>
        public float LastSliceSpeed { get; private set; }

        private readonly Collider[] overlapBuffer = new Collider[16];
        private readonly Dictionary<ISliceable, float> recentlySliced = new Dictionary<ISliceable, float>();
        private readonly List<ISliceable> expiredKeys = new List<ISliceable>(8);
        private Vector3 lastBladeCenter;
        private InputDevice swordDevice;

        private void OnEnable()
        {
            swordDevice = InputDevices.GetDeviceAtXRNode(swordHandNode);
            lastBladeCenter = BladeCenter();
            recentlySliced.Clear();
        }

        private void FixedUpdate()
        {
            if (bladeBase == null || bladeTip == null)
            {
                return;
            }

            if (!swordDevice.isValid)
            {
                swordDevice = InputDevices.GetDeviceAtXRNode(swordHandNode);
            }

            Vector3 previousCenter = lastBladeCenter;
            Vector3 currentCenter = BladeCenter();
            lastBladeCenter = currentCenter;

            Vector3 bladeVelocity = (currentCenter - previousCenter) / Time.fixedDeltaTime;
            float speed = bladeVelocity.magnitude;

            PruneCooldowns();

            if (speed < sliceSpeedThreshold)
            {
                return;
            }

            DetectAndSlice(bladeVelocity, speed);
        }

        /// <summary>칼날 전체를 캡슐로 훑는다. 칼끝 한 점만 검사하면 손잡이 쪽으로 벤 경우를 놓친다.</summary>
        private void DetectAndSlice(Vector3 bladeVelocity, float speed)
        {
            int count = Physics.OverlapCapsuleNonAlloc(bladeBase.position, bladeTip.position, bladeRadius,
                overlapBuffer, sliceableMask, QueryTriggerInteraction.Collide);

            Vector3 bladeAxis = (bladeTip.position - bladeBase.position).normalized;
            Vector3 planeNormal = Vector3.Cross(bladeAxis, bladeVelocity.normalized);

            // 칼을 축 방향 그대로 찌른 경우엔 외적이 0에 가까워진다 — 그때는 벨 수 없다(찌르기).
            if (planeNormal.sqrMagnitude < 0.01f)
            {
                return;
            }
            planeNormal.Normalize();

            for (int i = 0; i < count; i++)
            {
                var sliceable = overlapBuffer[i].GetComponentInParent<ISliceable>();
                if (sliceable == null || !sliceable.CanBeSliced || recentlySliced.ContainsKey(sliceable))
                {
                    continue;
                }

                Vector3 contactPoint = overlapBuffer[i].ClosestPoint(BladeCenter());
                sliceable.Slice(contactPoint, planeNormal, speed);

                recentlySliced[sliceable] = Time.time + retargetCooldown;
                LastSliceSpeed = speed;
                OnSliced?.Invoke(sliceable, speed);
                SendHaptic();
            }
        }

        private void PruneCooldowns()
        {
            if (recentlySliced.Count == 0)
            {
                return;
            }

            expiredKeys.Clear();
            foreach (var pair in recentlySliced)
            {
                if (Time.time >= pair.Value)
                {
                    expiredKeys.Add(pair.Key);
                }
            }

            for (int i = 0; i < expiredKeys.Count; i++)
            {
                recentlySliced.Remove(expiredKeys[i]);
            }
        }

        private Vector3 BladeCenter()
        {
            return (bladeBase.position + bladeTip.position) * 0.5f;
        }

        private void SendHaptic()
        {
            if (swordDevice.isValid && swordDevice.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
            {
                swordDevice.SendHapticImpulse(0u, hapticAmplitude, hapticDuration);
            }
        }
    }
}
