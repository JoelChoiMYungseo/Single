using System;
using UnityEngine;

namespace VRGame.Magnetic
{
    /// <summary>고속 충돌 감지와 "뇌정지" 스턴. 이 게임의 코미디는 전부 여기서 나온다 — 자력으로
    /// 가속해 놓고 극을 잘못 눌러 벽에 처박히는 순간, 잠깐 조작이 먹통이 되어야 "아 망했다"는
    /// 실감이 산다. 스턴 중엔 <see cref="IsStunned"/>를 MagneticLocomotion이 읽어 발사를 무시한다.
    ///
    /// <c>Collision.relativeVelocity</c>를 쓰는 이유: 충돌 직후의 Rigidbody.linearVelocity는 이미
    /// 솔버가 반사·마찰을 적용한 값이라 "얼마나 세게 박았는가"를 왜곡한다. relativeVelocity는
    /// 충돌 발생 순간의 상대 속도라 충격량 판정에 이 값을 써야 한다.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CrashHandler : MonoBehaviour
    {
        [Header("Crash Detection")]
        [Tooltip("상대 속도가 이 이상이어야 충돌로 인정한다 (m/s). 낮으면 스치기만 해도 스턴 걸린다.")]
        [SerializeField] private float crashSpeedThreshold = 6f;
        [Tooltip("연속 충돌(벽에 긁히며 여러 번 부딪히는 것)로 스턴이 겹쳐 걸리지 않게 하는 최소 간격.")]
        [SerializeField] private float crashCooldown = 0.5f;

        [Header("Stun")]
        [SerializeField] private float stunDuration = 0.9f;

        [Header("Comedic Bounce")]
        [Tooltip("충돌 법선 방향으로 튕겨나가는 정도. 0이면 안 튕기고, 1이면 들어온 속도만큼 그대로 반사.")]
        [SerializeField, Range(0f, 1.5f)] private float bounceRestitution = 0.5f;
        [Tooltip("이 속력을 넘는 충돌만 코믹 바운스를 적용한다 — 약한 충돌까지 튕기면 조작감이 미끌거린다.")]
        [SerializeField] private float bounceSpeedThreshold = 9f;

        /// <summary>충돌 순간 속력 — 카메라 흔들림·이펙트 세기에 쓴다.</summary>
        public event Action<float> OnCrashed;
        public event Action OnStunEnded;

        public bool IsStunned { get; private set; }

        private Rigidbody body;
        private float nextCrashAllowedTime;
        private float stunEndTime;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            if (IsStunned && Time.time >= stunEndTime)
            {
                IsStunned = false;
                OnStunEnded?.Invoke();
            }
        }

        // 물리 이벤트 콜백이지만 여기서는 힘을 직접 적분하지 않고 즉발성 VelocityChange만
        // 적용한다 — 이 정도는 FixedUpdate 밖에서 걸려도 안전하다 (RULE-03은 매 프레임 누적되는
        // AddForce/MovePosition 계열을 겨냥한 규칙이다).
        private void OnCollisionEnter(Collision collision)
        {
            float impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed < crashSpeedThreshold || Time.time < nextCrashAllowedTime)
            {
                return;
            }

            nextCrashAllowedTime = Time.time + crashCooldown;

            IsStunned = true;
            stunEndTime = Time.time + stunDuration;

            if (impactSpeed >= bounceSpeedThreshold)
            {
                ApplyComedicBounce(collision, impactSpeed);
            }

            OnCrashed?.Invoke(impactSpeed);
        }

        private void ApplyComedicBounce(Collision collision, float impactSpeed)
        {
            Vector3 normal = collision.GetContact(0).normal;
            Vector3 bounceDirection = Vector3.Reflect(collision.relativeVelocity.normalized, normal);
            body.linearVelocity = bounceDirection * (impactSpeed * bounceRestitution);
        }
    }
}
