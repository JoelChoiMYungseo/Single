using UnityEngine;
using UnityEngine.InputSystem;
using VRGame.Core;

namespace VRGame.Echo
{
    /// <summary>단순 WASD 걷기. 이 게임은 로코모션이 요청 범위 밖이다 — "룸스케일로 걸어다니며
    /// 소리를 지르는" 장르 특성상 헤드셋 자체의 머리 추적 이상으로 필요한 이동 방식이 없어서,
    /// 다른 4개 게임처럼 VR 전용 로코모션과 XRModeSwitcher 이원화를 만들지 않고 이 컨트롤러
    /// 하나만 상시 둔다. VR에서도 그대로 켜 두면 이동 반경이 부족한 환경에서 보조 이동으로 쓸 수 있다.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlaceholderWalkRig : MonoBehaviour, ILocomotionDriver
    {
        [SerializeField] private Transform head;
        [SerializeField] private float moveSpeed = 3.2f;
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private float pitchLimit = 80f;

        private Rigidbody body;
        private float yaw;
        private float pitch;

        public Vector3 Velocity => body != null ? body.linearVelocity : Vector3.zero;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = true;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void OnEnable()
        {
            yaw = transform.eulerAngles.y;
            pitch = 0f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Update()
        {
            UpdateLook();
        }

        private void FixedUpdate()
        {
            UpdateMove();
        }

        private void UpdateLook()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, -pitchLimit, pitchLimit);

            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (head != null)
            {
                head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        private void UpdateMove()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            float horizontal = 0f;
            float vertical = 0f;
            if (keyboard.aKey.isPressed) horizontal -= 1f;
            if (keyboard.dKey.isPressed) horizontal += 1f;
            if (keyboard.sKey.isPressed) vertical -= 1f;
            if (keyboard.wKey.isPressed) vertical += 1f;

            Vector3 input = new Vector3(horizontal, 0f, vertical).normalized;
            Vector3 worldDirection = transform.TransformDirection(input);
            body.linearVelocity = new Vector3(worldDirection.x * moveSpeed, body.linearVelocity.y, worldDirection.z * moveSpeed);
        }

        public void OnTeleported(Vector3 position)
        {
            // 순간이동 후 남는 관성 같은 내부 상태가 없다 — 할 일 없음.
        }
    }
}
