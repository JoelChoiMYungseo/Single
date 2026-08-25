using System;
using UnityEngine;

namespace VRGame.Magnetic
{
    /// <summary>손이 노릴 수 있는 금속 물체(모루 등). "N/S 극 이동"이 아니라 "자기 위험" 절반을
    /// 담당한다 — 플레이어보다 가벼운 금속을 당기면, 벽 대신 이 물체가 플레이어 얼굴로 날아온다.
    ///
    /// 누가 움직일지는 이 컴포넌트가 정하지 않는다. MagneticLocomotion이 매 발사마다
    /// <see cref="IsLighterThan"/>로 물어보고, 가벼우면 이 물체에, 무거우면(=벽 취급) 플레이어에
    /// 힘을 건다. 그래서 이 컴포넌트는 "나는 얼마나 가벼운가"만 알면 되고 조준·발사 로직은 모른다.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MagneticHazard : MonoBehaviour
    {
        [Tooltip("내 질량이 플레이어 질량의 이 비율보다 가벼워야 '당겨져 날아가는' 대상이 된다. " +
                 "1보다 크면 사실상 항상 당겨진다.")]
        [SerializeField] private float heavinessRatioThreshold = 0.6f;

        /// <summary>지금 이 프레임에 조준·견인되고 있는지 — 경고 이펙트/사운드가 구독한다.</summary>
        public event Action<bool> OnAttractionStateChanged;

        public Rigidbody Body { get; private set; }

        private bool isAttracted;

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Body.useGravity = true;
            // 견인되면 순식간에 고속이 된다 — Discrete면 플레이어를 그대로 통과해버릴 수 있다.
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        public bool IsLighterThan(float otherMass)
        {
            return Body.mass < otherMass * heavinessRatioThreshold;
        }

        /// <summary>MagneticLocomotion이 매 FixedUpdate 조준 결과를 통보한다. 값이 바뀔 때만
        /// 이벤트를 쏴서, 계속 조준 중이어도 경고 이펙트가 매 프레임 재시작되지 않게 한다.</summary>
        public void SetAttracted(bool value)
        {
            if (value == isAttracted)
            {
                return;
            }
            isAttracted = value;
            OnAttractionStateChanged?.Invoke(value);
        }
    }
}
