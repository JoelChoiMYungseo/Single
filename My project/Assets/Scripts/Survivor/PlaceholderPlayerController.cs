using UnityEngine;
using UnityEngine.InputSystem;
using VRGame.Core;

namespace VRGame.Survivor
{
    /// <summary>VR 리그가 붙기 전까지 쓰는 임시 이동 컨트롤러. WASD/화살표로 카메라 기준 평면 이동만
    /// 한다. 체력·자동공격·경험치는 이동 방식을 전혀 모르도록 분리해 놨다.
    ///
    /// 고릴라 로코모션과 같은 오브젝트에 얹혀 Rigidbody를 공유하므로, 활성화된 동안은 Rigidbody를
    /// 직접 Transform 이동에 안전하도록 kinematic으로 돌려놓는다 — 안 그러면 중력이 계속 끌어당기는
    /// 상태에서 매 프레임 transform.position을 덮어써서 물리와 충돌한다. XRModeSwitcher가 VR로
    /// 전환하며 이 컴포넌트를 꺼주는 순간 OnDisable에서 kinematic을 풀어 로코모션에 제어권을 돌려준다.</summary>
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(Rigidbody))]
    public class PlaceholderPlayerController : MonoBehaviour, ILocomotionDriver
    {
        [SerializeField] private float arenaHalfExtent = 28f;
        [SerializeField] private float turnSpeed = 12f;

        private PlayerStats stats;
        private Rigidbody body;

        /// <summary>가장 최근 이동 방향·속력. 보스의 예측 사격이 PlayerRig를 통해 참조한다.</summary>
        public Vector3 Velocity { get; private set; }

        public void OnTeleported(Vector3 position)
        {
            Velocity = Vector3.zero;
        }

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            body = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            body.linearVelocity = Vector3.zero;
            body.isKinematic = true;
        }

        private void OnDisable()
        {
            body.isKinematic = false;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            Vector3 input = ReadMoveInput(keyboard);
            Velocity = input * stats.MoveSpeed;

            if (input.sqrMagnitude > 0.001f)
            {
                Vector3 next = transform.position + Velocity * Time.deltaTime;
                next.x = Mathf.Clamp(next.x, -arenaHalfExtent, arenaHalfExtent);
                next.z = Mathf.Clamp(next.z, -arenaHalfExtent, arenaHalfExtent);
                transform.position = next;

                Quaternion look = Quaternion.LookRotation(input, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
            }
        }

        private static Vector3 ReadMoveInput(Keyboard keyboard)
        {
            float horizontal = 0f;
            float vertical = 0f;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horizontal -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontal += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) vertical -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) vertical += 1f;

            return new Vector3(horizontal, 0f, vertical).normalized;
        }
    }
}
