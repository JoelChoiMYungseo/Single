using System;
using UnityEngine;

namespace VRGame.Cart
{
    /// <summary>카트가 고속으로 정면 충돌하면 플레이어를 카트에 묶어두던 Joint를 파괴하고
    /// (Transform 부모/자식이 아니라 물리 조인트로 "탑승"을 구현했으므로 방출도 그 조인트를
    /// 없애는 것으로 대체한다), 조향/추진을 멈춘 뒤 플레이어 자신의 Rigidbody에 위/앞 방향
    /// 임펄스를 줘서 날려보낸다. 스켈레톤 래그돌(팔다리 개별 물리)은 이번 스코프에 없다 —
    /// 리그 전체가 한 덩어리로 날아가는 것까지만 구현한다.</summary>
    [RequireComponent(typeof(DownhillCart))]
    public class CartBailout : MonoBehaviour
    {
        [Header("Crash Detection")]
        [Tooltip("상대 속도가 이 이상이어야 사고로 인정한다 (m/s).")]
        [SerializeField] private float crashSpeedThreshold = 8f;
        [Tooltip("카트 forward와 -충돌법선의 내적이 이 이상이어야 \"정면\" 충돌로 인정한다.")]
        [SerializeField] private float frontalDotThreshold = 0.4f;

        [Header("Eject")]
        [Tooltip("인스펙터 노출 요구사항 — 방출 임펄스(VelocityChange) 크기.")]
        [SerializeField] private float ejectForce = 12f;
        [SerializeField] private Vector3 ejectDirectionLocal = new Vector3(0f, 0.6f, 0.4f);

        [Header("Seat")]
        [Tooltip("카트↔플레이어를 묶는 조인트 — SceneBuilder가 생성해 연결한다.")]
        [SerializeField] private Joint seatJoint;
        [Tooltip("AutoHandPlayer의 Rigidbody — 방출 후 물리 주체가 된다.")]
        [SerializeField] private Rigidbody playerBody;

        public bool HasEjected { get; private set; }

        /// <summary>이펙트·사운드 등 후속 연출용 훅 — 이번 스코프엔 구독자 없음.</summary>
        public event Action OnEjected;

        private DownhillCart cart;

        private void Awake()
        {
            cart = GetComponent<DownhillCart>();
        }

        // relativeVelocity를 쓰는 이유: 충돌 직후의 Rigidbody.linearVelocity는 이미 솔버가 반사·마찰을
        // 적용한 값이라 "얼마나 세게 박았는가"를 왜곡한다 — CrashHandler.OnCollisionEnter와 동일한 근거.
        // 이 즉발성 처리는 FixedUpdate 밖(OnCollisionEnter)에서 해도 된다 — RULE-03은 매 프레임
        // 누적되는 AddForce/MovePosition을 겨냥한 규칙이고, 1회성 ForceMode.VelocityChange는 해당 없다.
        private void OnCollisionEnter(Collision collision)
        {
            if (HasEjected)
            {
                return;
            }

            float impactSpeed = collision.relativeVelocity.magnitude;
            if (impactSpeed < crashSpeedThreshold)
            {
                return;
            }

            float frontalDot = Vector3.Dot(transform.forward, -collision.GetContact(0).normal);
            if (frontalDot < frontalDotThreshold)
            {
                return;
            }

            Eject();
        }

        private void Eject()
        {
            HasEjected = true;

            cart.SetControlEnabled(false);

            if (seatJoint != null)
            {
                Destroy(seatJoint);
                seatJoint = null;
            }

            if (playerBody != null)
            {
                Vector3 direction = transform.TransformDirection(ejectDirectionLocal).normalized;
                playerBody.AddForce(direction * ejectForce, ForceMode.VelocityChange);
            }

            OnEjected?.Invoke();
        }
    }
}
