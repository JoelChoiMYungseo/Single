using UnityEngine;
using UnityEngine.InputSystem;
using VRGame.Core;

namespace VRGame.Survivor
{
    /// <summary>VR 리그가 붙기 전까지 쓰는 임시 이동 컨트롤러. WASD/화살표로 카메라 기준 평면 이동만
    /// 한다. 나중에 VR 로코모션으로 교체할 때는 이 컴포넌트만 제거하면 된다 — 체력·자동공격·경험치는
    /// 이동 방식을 전혀 모르도록 분리해 놨다.</summary>
    [RequireComponent(typeof(PlayerStats))]
    public class PlaceholderPlayerController : MonoBehaviour, ILocomotionDriver
    {
        [SerializeField] private float arenaHalfExtent = 28f;
        [SerializeField] private float turnSpeed = 12f;

        private PlayerStats stats;

        /// <summary>가장 최근 이동 방향·속력. 보스의 예측 사격이 PlayerRig를 통해 참조한다.</summary>
        public Vector3 Velocity { get; private set; }

        public void OnTeleported(Vector3 position)
        {
            Velocity = Vector3.zero;
        }

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
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
