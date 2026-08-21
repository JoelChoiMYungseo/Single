using System;
using UnityEngine;

namespace VRGame.Tower
{
    /// <summary>붙잡을 수 있지만 내 몸무게를 못 버티는 블록. 이 게임의 재미가 전부 여기서 나온다.
    ///
    /// 두 단계로 무너진다 — 이 2단 구조가 핵심이다:
    ///   1) 하중 &gt; slipThreshold  → "스르륵" 미끄러지기 시작한다. 색이 붉어지고 흔들린다.
    ///      유저에게 "지금 놓지 않으면 죽는다"를 알려주는 경고 구간이며, 여기서 오는 판단 압박이
    ///      곧 긴박감이다.
    ///   2) 하중 &gt; breakThreshold 또는 미끄러짐 지속 → 완전히 빠져나온다. 붙잡은 채로 같이 추락한다.
    ///
    /// 한 번에 뚝 떨어지면 유저는 "불합리하다"고만 느낀다. 미끄러지는 구간을 먼저 보여줘야
    /// "내가 놓을 타이밍을 놓쳤다"는 자기 책임으로 읽힌다.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class UnstableBlock : MonoBehaviour, IVRGrabbable
    {
        [Header("Strength — 인스펙터에서 빠르게 조정")]
        [Tooltip("이 하중을 넘으면 미끄러지기 시작한다 (N).")]
        [SerializeField] private float slipThreshold = 900f;
        [Tooltip("이 하중을 넘으면 즉시 빠져나온다 (N).")]
        [SerializeField] private float breakThreshold = 1800f;
        [Tooltip("미끄러짐이 이 시간 이상 이어지면 하중이 낮아도 빠져나온다 (초).")]
        [SerializeField] private float slipDurationToBreak = 1.6f;
        [Tooltip("하중이 사라지면 초당 이 비율로 미끄러짐이 회복된다.")]
        [SerializeField] private float recoveryRate = 0.8f;

        [Header("Slip Motion")]
        [Tooltip("미끄러질 때 블록이 뽑혀 나오는 최대 거리 (m).")]
        [SerializeField] private float maxSlipDistance = 0.22f;
        [Tooltip("미끄러짐 진행도에 따라 블록이 떨리는 폭 (m).")]
        [SerializeField] private float shakeAmplitude = 0.012f;
        [SerializeField] private float shakeFrequency = 26f;

        [Header("Break Behaviour")]
        [Tooltip("빠져나올 때 뒤로 튀어나가는 세기 — 붙잡은 손을 배신하는 느낌을 준다.")]
        [SerializeField] private float breakEjectImpulse = 2.2f;
        [SerializeField] private float breakTorqueImpulse = 3f;

        [Header("Feedback")]
        [SerializeField] private Color safeColor = new Color(0.72f, 0.66f, 0.52f);
        [SerializeField] private Color strainedColor = new Color(0.95f, 0.25f, 0.18f);

        /// <summary>이 블록, 미끄러짐 진행도(0~1) — HUD·사운드가 경고를 띄우는 데 쓴다.</summary>
        public event Action<UnstableBlock, float> OnStrainChanged;
        /// <summary>완전히 빠져나온 블록 — TowerManager가 회수하거나 잔해로 넘긴다.</summary>
        public event Action<UnstableBlock> OnDetached;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Rigidbody body;
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock propertyBlock;
        private Vector3 anchoredLocalPosition;
        private Transform anchorParent;
        private float baseSlipThreshold;
        private float baseBreakThreshold;
        private float strain;          // 0~1, 미끄러짐 진행도
        private float slipElapsed;
        private float accumulatedLoad; // 이번 물리 스텝에 쌓인 하중
        private bool isDetached;
        private bool hasPropertyBlock;
        private int grabCount;

        public Rigidbody Body => body;
        public bool CanBeGrabbed => !isDetached;
        /// <summary>0=멀쩡함, 1=곧 빠진다. TowerManager가 탑 전체 불안정도를 볼 때 쓴다.</summary>
        public float Strain => strain;
        public bool IsDetached => isDetached;
        public bool IsHeld => grabCount > 0;

        // ────────────────────────────── Lifecycle ──────────────────────────────

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            meshRenderer = GetComponentInChildren<MeshRenderer>();
            propertyBlock = new MaterialPropertyBlock();

            // 풀에서 재사용할 때 강도 배율이 곱해져 누적되지 않도록 원본을 따로 보관한다.
            baseSlipThreshold = slipThreshold;
            baseBreakThreshold = breakThreshold;
        }

        /// <summary>TowerManager가 풀에서 꺼내 배치할 때 호출한다. 상태 초기화를 Awake가 아니라
        /// 여기서 하는 이유는 같은 인스턴스를 재사용하기 때문이다.</summary>
        public void ResetForSpawn(Vector3 position, Quaternion rotation, float strengthMultiplier)
        {
            transform.SetPositionAndRotation(position, rotation);
            anchorParent = transform.parent;
            anchoredLocalPosition = transform.localPosition;

            // 원본 기준값에서 곱한다 — 현재값에 곱하면 재사용마다 강도가 눈덩이처럼 변한다.
            slipThreshold = baseSlipThreshold * strengthMultiplier;
            breakThreshold = baseBreakThreshold * strengthMultiplier;

            strain = 0f;
            slipElapsed = 0f;
            accumulatedLoad = 0f;
            isDetached = false;
            grabCount = 0;

            // 처음에는 탑의 일부로 굳어 있다. 흔들릴 필요가 생기면 TowerManager가 깨운다.
            body.isKinematic = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;

            ApplyStrainVisual(0f);
        }

        // ────────────────────────────── IVRGrabbable ──────────────────────────────

        public Vector3 GetGripPoint(Vector3 handPosition)
        {
            // 손이 블록 표면의 가장 가까운 점을 잡게 한다 — 중심으로 빨려들면 손이 블록 속에 박힌다.
            Collider blockCollider = GetComponent<Collider>();
            return blockCollider != null ? blockCollider.ClosestPoint(handPosition) : handPosition;
        }

        public void OnGrabbed(VRHandController hand)
        {
            grabCount++;
        }

        public void OnReleased(VRHandController hand)
        {
            grabCount = Mathf.Max(0, grabCount - 1);
        }

        public void ApplyLoad(Vector3 force, Vector3 point)
        {
            if (isDetached)
            {
                return;
            }

            // 여러 손이 같은 블록을 잡을 수 있으므로 스텝 단위로 합산하고 FixedUpdate에서 소비한다.
            accumulatedLoad += force.magnitude;
        }

        // ────────────────────────────── Physics step ──────────────────────────────

        private void FixedUpdate()
        {
            if (isDetached)
            {
                return;
            }

            EvaluateLoad(accumulatedLoad);
            accumulatedLoad = 0f;
        }

        private void EvaluateLoad(float load)
        {
            if (load >= breakThreshold)
            {
                Detach();
                return;
            }

            if (load > slipThreshold)
            {
                // 미끄러짐 속도는 초과분에 비례한다 — 아슬아슬하게 넘으면 천천히, 확 넘으면 급격히.
                float excess = (load - slipThreshold) / Mathf.Max(1f, breakThreshold - slipThreshold);
                slipElapsed += Time.fixedDeltaTime;
                strain = Mathf.Clamp01(strain + excess * Time.fixedDeltaTime / Mathf.Max(0.05f, slipDurationToBreak));

                if (slipElapsed >= slipDurationToBreak || strain >= 1f)
                {
                    Detach();
                    return;
                }
            }
            else
            {
                slipElapsed = Mathf.Max(0f, slipElapsed - Time.fixedDeltaTime);
                strain = Mathf.Max(0f, strain - recoveryRate * Time.fixedDeltaTime);
            }

            ApplySlipTransform();
            ApplyStrainVisual(strain);
            OnStrainChanged?.Invoke(this, strain);
        }

        /// <summary>미끄러진 만큼 블록을 뽑아내고 떨림을 얹는다. kinematic 상태이므로 Transform을
        /// 직접 옮겨도 물리 시뮬레이션과 싸우지 않는다.</summary>
        private void ApplySlipTransform()
        {
            if (strain <= 0.0001f)
            {
                transform.localPosition = anchoredLocalPosition;
                return;
            }

            float shake = Mathf.Sin(Time.time * shakeFrequency) * shakeAmplitude * strain;

            // 블록의 로컬 +Z(길이 방향)로 뽑혀 나오면서 아래로 조금 처진다.
            Vector3 slip = new Vector3(shake, -0.02f * strain, maxSlipDistance * strain);
            transform.localPosition = anchoredLocalPosition + slip;
        }

        private void ApplyStrainVisual(float value)
        {
            if (meshRenderer == null)
            {
                return;
            }

            if (value <= 0.001f)
            {
                if (hasPropertyBlock)
                {
                    // 정상 상태로 돌아왔으면 프로퍼티 블록을 비워 배칭을 되살린다.
                    propertyBlock.Clear();
                    meshRenderer.SetPropertyBlock(propertyBlock);
                    hasPropertyBlock = false;
                }
                return;
            }

            propertyBlock.SetColor(BaseColorId, Color.Lerp(safeColor, strainedColor, value));
            meshRenderer.SetPropertyBlock(propertyBlock);
            hasPropertyBlock = true;
        }

        // ────────────────────────────── Detach ──────────────────────────────

        /// <summary>블록이 탑에서 완전히 빠져나온다. 붙잡고 있던 손의 조인트는 대상이 갑자기
        /// 동적 물체가 되면서 자연히 딸려오고, 무게 때문에 플레이어를 아래로 끌어당긴다.</summary>
        public void Detach()
        {
            if (isDetached)
            {
                return;
            }

            isDetached = true;
            strain = 1f;

            body.isKinematic = false;
            body.WakeUp();

            // 손 반대쪽으로 튀어나가게 해서 "배신당한" 감각을 만든다.
            body.AddForce(-transform.forward * breakEjectImpulse, ForceMode.VelocityChange);
            body.AddTorque(UnityEngine.Random.insideUnitSphere * breakTorqueImpulse, ForceMode.VelocityChange);

            ApplyStrainVisual(1f);
            OnDetached?.Invoke(this);
        }

        /// <summary>TowerManager가 시뮬레이션 예산에 맞춰 블록을 깨우거나 재운다.</summary>
        public void SetSimulated(bool simulated)
        {
            if (isDetached)
            {
                return;
            }

            body.isKinematic = !simulated;
            if (!simulated)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                transform.localPosition = anchoredLocalPosition;
            }
        }

        /// <summary>강도 배율을 다시 지정한다 (풀 재사용 시 원본 값이 누적 곱해지는 것을 막기 위해
        /// TowerManager가 기준값을 관리하고 여기에 절대값을 넣는다).</summary>
        public void SetThresholds(float slip, float breakForce)
        {
            slipThreshold = slip;
            breakThreshold = breakForce;
        }

        public void GetBaseThresholds(out float slip, out float breakForce)
        {
            slip = slipThreshold;
            breakForce = breakThreshold;
        }
    }
}
