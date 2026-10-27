using UnityEngine;
using UnityEngine.XR;

namespace VRGame.Survivor
{
    /// <summary>팔을 휘둘러 이동하는 게임인데 공격은 자동조준 원거리뿐이면 손이 할 일이 없다. 손을
    /// 빠르게 휘둘러 잡몹에게 갖다 대면 그 자체로 데미지를 준다 — 로코모션과 같은 몸짓(팔을 젓는다)이
    /// 그대로 전투 입력이 되도록, 콜라이더 없이 EnemyRegistry를 거리로 검사한다(다른 히트 판정과
    /// 동일한 이유 — 잡몹 수백 마리에 콜라이더를 달면 물리 엔진이 못 버틴다).</summary>
    public class HandPunchDamage : MonoBehaviour
    {
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;

        [Header("Punch")]
        [Tooltip("이 속도(m/s) 이상으로 손이 움직이고 있어야 펀치로 인정한다.")]
        [SerializeField] private float minPunchSpeed = 3f;
        [SerializeField] private float punchRadius = 0.35f;
        [SerializeField] private float punchDamage = 12f;
        [Tooltip("한 번 맞춘 손은 이 시간 동안 다시 못 맞춘다 — 손을 대고 있기만 해도 매 프레임 터지는 것 방지.")]
        [SerializeField] private float hitCooldown = 0.15f;

        private Vector3 lastLeftPos;
        private Vector3 lastRightPos;
        private float leftCooldown;
        private float rightCooldown;

        private void OnEnable()
        {
            if (leftHand != null) lastLeftPos = leftHand.position;
            if (rightHand != null) lastRightPos = rightHand.position;
        }

        private void Update()
        {
            ProcessHand(leftHand, ref lastLeftPos, ref leftCooldown, XRNode.LeftHand);
            ProcessHand(rightHand, ref lastRightPos, ref rightCooldown, XRNode.RightHand);
        }

        private void ProcessHand(Transform hand, ref Vector3 lastPos, ref float cooldown, XRNode node)
        {
            if (hand == null)
            {
                return;
            }

            cooldown -= Time.deltaTime;

            Vector3 current = hand.position;
            float speed = (current - lastPos).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            lastPos = current;

            if (cooldown > 0f || speed < minPunchSpeed)
            {
                return;
            }

            var enemies = EnemyRegistry.Alive;
            float radiusSqr = punchRadius * punchRadius;

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                EnemyTarget enemy = enemies[i];
                if (enemy.IsDead)
                {
                    continue;
                }

                if ((enemy.transform.position - current).sqrMagnitude > radiusSqr)
                {
                    continue;
                }

                enemy.TakeDamage(punchDamage);
                cooldown = hitCooldown;
                SendHaptic(node);
                break; // 손 하나당 한 프레임에 한 마리만 — 스윙 한 번에 무더기로 쓸리지 않게.
            }
        }

        private static void SendHaptic(XRNode node)
        {
            InputDevice device = InputDevices.GetDeviceAtXRNode(node);
            if (device.isValid && device.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
            {
                device.SendHapticImpulse(0u, 0.7f, 0.05f);
            }
        }
    }
}
