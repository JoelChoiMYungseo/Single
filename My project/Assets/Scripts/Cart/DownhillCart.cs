using UnityEngine;

namespace VRGame.Cart
{
    /// <summary>브레이크 없는 마트 카트의 물리 몸통. WheelCollider 대신 바퀴 위치 4점에서 아래로
    /// SphereCast를 쏴서 지면을 감지하고, 스프링-댐퍼 힘으로 띄우는 아케이드 비히클 근사다 —
    /// 코드로만 조립하는 이 프로젝트의 방식과 궁합이 좋다(바퀴 메시·서스펜션 개별 튜닝이 필요 없음).
    /// 전진 가속은 별도 추진력 없이 중력 + 경사면으로 자연스럽게 생기고, <see cref="topSpeed"/>를
    /// 넘으면 속도를 클램프한다. <see cref="wheelFriction"/>은 옆으로 미끄러지는 속도 성분을 얼마나
    /// 강하게 상쇄하는지에 쓴다 — 바퀴가 옆으로는 안 미끄러지고 앞으로만 구르는 느낌의 핵심.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DownhillCart : MonoBehaviour
    {
        [Header("Wheels (SphereCast 근사)")]
        [SerializeField] private Transform[] wheelPoints;
        [SerializeField] private float wheelRadius = 0.35f;
        [SerializeField] private float suspensionTravel = 0.25f;
        [SerializeField] private float suspensionStiffness = 900f;
        [SerializeField] private float suspensionDamping = 60f;
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Rolling")]
        [Tooltip("옆으로 미끄러지는 속도 성분을 상쇄하는 계수 — 바퀴 마찰.")]
        [SerializeField] private float wheelFriction = 1.5f;
        [Tooltip("이 속력을 넘으면 매 FixedUpdate 속도를 클램프한다.")]
        [SerializeField] private float topSpeed = 25f;

        public Rigidbody Body { get; private set; }
        public bool ControlEnabled { get; private set; } = true;

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            // 아케이드 비히클 관례 — 롤/피치를 잠가 넘어지지 않게 하고 요(좌우 회전)만 허용한다.
            // 서스펜션이 네 바퀴를 독립적으로 밀어 올리다 보면 그 반작용만으로도 쉽게 뒤집힌다.
            Body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        /// <summary>StickSteering이 매 FixedUpdate 호출한다. ControlEnabled == false면 무시한다 —
        /// 방출(bailout) 이후엔 조향이 먹히면 안 된다. 호출 시점이 이 컴포넌트의 FixedUpdate 전이든
        /// 후든 상관없다 — Unity는 같은 물리 스텝에 걸린 AddForce를 전부 모았다가 그 스텝 끝에
        /// 한꺼번에 적분하므로 스크립트 실행 순서에 의존하지 않는다.</summary>
        public void ApplyLateralForce(Vector3 lateralForce)
        {
            if (!ControlEnabled)
            {
                return;
            }
            Body.AddForce(lateralForce, ForceMode.Force);
        }

        /// <summary>CartBailout이 충돌 처리 시작 시 호출해 추진/조향 계산을 끈다. DownhillCart는
        /// StickSteering을 직접 참조하지 않는다 — StickSteering이 매 프레임 ControlEnabled를 스스로
        /// 확인해서 멈춘다.</summary>
        public void SetControlEnabled(bool value)
        {
            ControlEnabled = value;
        }

        private void FixedUpdate()
        {
            ApplySuspension();
            ApplyWheelFriction();
            ClampTopSpeed();
        }

        private void ApplySuspension()
        {
            if (wheelPoints == null)
            {
                return;
            }

            for (int i = 0; i < wheelPoints.Length; i++)
            {
                Transform point = wheelPoints[i];
                if (point == null)
                {
                    continue;
                }

                if (!Physics.SphereCast(point.position, wheelRadius, Vector3.down, out RaycastHit hit,
                        suspensionTravel, groundMask, QueryTriggerInteraction.Ignore))
                {
                    continue; // 공중 — 이번 스텝은 이 바퀴의 서스펜션 힘 생략(자유낙하 유지).
                }

                float compression = suspensionTravel - hit.distance;
                float springForce = compression * suspensionStiffness
                    - Vector3.Dot(Body.GetPointVelocity(point.position), hit.normal) * suspensionDamping;

                Body.AddForceAtPosition(hit.normal * springForce, point.position, ForceMode.Force);
            }
        }

        /// <summary>바퀴가 옆으로는 미끄러지지 않고 앞으로만 구르는 느낌 — 카트의 right축 속도
        /// 성분을 wheelFriction 비율로 상쇄한다.</summary>
        private void ApplyWheelFriction()
        {
            Vector3 velocity = Body.linearVelocity;
            float lateralSpeed = Vector3.Dot(velocity, transform.right);
            Body.AddForce(-transform.right * (lateralSpeed * wheelFriction), ForceMode.Force);
        }

        private void ClampTopSpeed()
        {
            if (Body.linearVelocity.magnitude > topSpeed)
            {
                Body.linearVelocity = Body.linearVelocity.normalized * topSpeed;
            }
        }
    }
}
