using UnityEngine;

namespace VRGame.Core
{
    /// <summary>외부 로코모션 패키지를 코드 수정 없이 붙이기 위한 어댑터.
    ///
    /// 이 컴포넌트는 로코모션에게 아무것도 요구하지 않는다. 그냥 리그가 실제로 어디로 얼마나
    /// 움직였는지 밖에서 관측해서 속도를 보고한다. 그래서 Another Axiom의 MIT GorillaLocomotion처럼
    /// transform.position을 직접 쓰는 구현이든, Rigidbody에 힘을 주는 구현이든, 심지어 소스가 없는
    /// 에셋스토어 패키지든 그대로 얹을 수 있다 — ILocomotionDriver를 구현하도록 남의 코드를
    /// 고칠 필요가 없다.
    ///
    /// 트레이드오프: 관측이므로 한 프레임 늦다. 예측 사격처럼 정밀함이 필요한 곳에는 충분하지만,
    /// 그 프레임의 정확한 속도가 필요하면 로코모션이 직접 ILocomotionDriver를 구현하는 편이 낫다.</summary>
    public class ObservedLocomotionDriver : MonoBehaviour, ILocomotionDriver
    {
        [Tooltip("관측 대상. 비우면 이 오브젝트의 트랜스폼을 본다.")]
        [SerializeField] private Transform observed;
        [Tooltip("있으면 위치 차분 대신 이 값을 쓴다 — 더 정확하다.")]
        [SerializeField] private Rigidbody observedBody;
        [Tooltip("위치 차분으로 계산한 속도를 매끄럽게 하는 정도. 0이면 평활화 없음.")]
        [SerializeField] private float smoothing = 0.25f;

        public Vector3 Velocity { get; private set; }

        private Transform target;
        private Vector3 lastPosition;

        private void Awake()
        {
            target = observed != null ? observed : transform;
            lastPosition = target.position;
        }

        private void OnEnable()
        {
            // 비활성 동안 벌어진 위치 차이를 그대로 쓰면 첫 프레임에 거대한 거짓 속도가 나온다.
            lastPosition = target.position;
            Velocity = Vector3.zero;
        }

        private void FixedUpdate()
        {
            if (observedBody != null && !observedBody.isKinematic)
            {
                Velocity = observedBody.linearVelocity;
                lastPosition = target.position;
                return;
            }

            Vector3 raw = (target.position - lastPosition) / Time.fixedDeltaTime;
            lastPosition = target.position;

            Velocity = smoothing > 0f
                ? Vector3.Lerp(Velocity, raw, 1f - Mathf.Exp(-Time.fixedDeltaTime / smoothing))
                : raw;
        }

        public void OnTeleported(Vector3 position)
        {
            lastPosition = position;
            Velocity = Vector3.zero;
        }
    }
}
