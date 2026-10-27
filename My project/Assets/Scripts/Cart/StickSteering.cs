using UnityEngine;

namespace VRGame.Cart
{
    /// <summary>플레이어가 쥔 막대 끝(tip)이 바닥에 닿아 있는 동안, 그 tip이 카트 기준 좌우로
    /// 움직인 만큼(찍어서 미는 반대 방향)을 카트에 측면 힘으로 전달한다. <see cref="DownhillCart"/>가
    /// 이미 노출한 API만 호출할 뿐 그 내부(서스펜션·마찰)는 전혀 모른다.</summary>
    public class StickSteering : MonoBehaviour
    {
        [SerializeField] private DownhillCart cart;
        [SerializeField] private Transform stickTip;
        [SerializeField] private float tipRadius = 0.05f;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float steerForceMultiplier = 15f;
        [SerializeField] private float maxSteerForce = 40f;

        private Vector3 lastTipPosition;

        private void OnEnable()
        {
            // 비활성 동안 벌어진 위치 차이를 그대로 쓰면 첫 프레임에 거대한 거짓 속도가 나온다
            // (ObservedLocomotionDriver.OnEnable과 동일한 이유).
            if (stickTip != null)
            {
                lastTipPosition = stickTip.position;
            }
        }

        private void FixedUpdate()
        {
            if (cart == null || stickTip == null || !cart.ControlEnabled)
            {
                if (stickTip != null)
                {
                    lastTipPosition = stickTip.position;
                }
                return;
            }

            bool touchingGround = Physics.CheckSphere(stickTip.position, tipRadius, groundMask,
                QueryTriggerInteraction.Ignore);

            if (touchingGround)
            {
                Vector3 tipVelocity = (stickTip.position - lastTipPosition) / Time.fixedDeltaTime;
                float lateralSpeed = Vector3.Dot(tipVelocity, cart.transform.right);

                float steerForce = Mathf.Clamp(-lateralSpeed * steerForceMultiplier, -maxSteerForce, maxSteerForce);
                cart.ApplyLateralForce(cart.transform.right * steerForce);
            }

            lastTipPosition = stickTip.position;
        }
    }
}
