using UnityEngine;
using UnityEngine.InputSystem;

namespace VRGame.Samurai
{
    /// <summary>헤드셋 없이 카타나 시스템을 시험하기 위한 임시 조작. 마우스로 시점을 돌리고,
    /// 좌클릭으로 칼을 찍고(도약), 우클릭으로 가로로 벤다.
    ///
    /// 로코모션과 슬라이서는 "손 트랜스폼이 얼마나 빨리 움직였는가"만 보기 때문에, 그 트랜스폼을
    /// 컨트롤러가 움직이든 마우스가 움직이든 동작이 완전히 같다. 그래서 VR로 넘어갈 때 이 컴포넌트만
    /// 꺼주면 되고 게임 로직은 한 줄도 바뀌지 않는다 — XRModeSwitcher가 그 전환을 자동으로 한다.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlaceholderSwordRig : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform head;
        [SerializeField] private Transform swordHand;
        [SerializeField] private Transform freeHand;

        [Header("Look")]
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private float pitchLimit = 80f;

        [Header("Hand Rest Pose (머리 기준 로컬 오프셋)")]
        [SerializeField] private Vector3 swordHandRest = new Vector3(0.3f, -0.2f, 0.45f);
        [SerializeField] private Vector3 freeHandRest = new Vector3(-0.3f, -0.2f, 0.4f);
        [SerializeField] private float handFollowSpeed = 20f;

        [Header("Thrust — 좌클릭 (칼을 찍어 도약)")]
        [SerializeField] private float thrustDistance = 1.0f;
        [SerializeField] private float thrustDuration = 0.16f;

        [Header("Slash — 우클릭 (가로베기)")]
        [SerializeField] private float slashArc = 1.2f;
        [SerializeField] private float slashDuration = 0.18f;

        [Header("Walk Assist — WASD")]
        [Tooltip("칼 도약만으로는 미세 이동이 어려워 약한 보조 이동을 남겨둔다.")]
        [SerializeField] private float walkAssistForce = 7f;

        private Rigidbody body;
        private float yaw;
        private float pitch;
        private float actionTimer;
        private bool isSlashing;
        private Vector3 moveInput;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
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
            ReadLook();
            ReadActions();
            ReadMoveInput();
            PoseHands();
        }

        private void FixedUpdate()
        {
            if (moveInput.sqrMagnitude < 0.001f)
            {
                return;
            }

            // 물리 조작은 FixedUpdate에서만 (RULE-03).
            Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * moveInput;
            body.AddForce(direction * walkAssistForce, ForceMode.Acceleration);
        }

        private void ReadLook()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
            yaw += delta.x;
            pitch = Mathf.Clamp(pitch - delta.y, -pitchLimit, pitchLimit);

            // 몸통은 좌우만, 머리는 상하만 — VR에서 HMD가 하는 역할을 흉내낸다.
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void ReadActions()
        {
            if (actionTimer > 0f)
            {
                actionTimer -= Time.deltaTime;
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                isSlashing = false;
                actionTimer = thrustDuration;
            }
            else if (mouse.rightButton.wasPressedThisFrame)
            {
                isSlashing = true;
                actionTimer = slashDuration;
            }
        }

        private void ReadMoveInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                moveInput = Vector3.zero;
                return;
            }

            float horizontal = 0f;
            float vertical = 0f;
            if (keyboard.aKey.isPressed) horizontal -= 1f;
            if (keyboard.dKey.isPressed) horizontal += 1f;
            if (keyboard.sKey.isPressed) vertical -= 1f;
            if (keyboard.wKey.isPressed) vertical += 1f;

            moveInput = new Vector3(horizontal, 0f, vertical).normalized;
        }

        /// <summary>손 위치를 직접 옮긴다. 이 이동량이 곧 칼 속도가 되어 도약·절단을 발동시킨다.</summary>
        private void PoseHands()
        {
            Vector3 swordTarget = head.TransformPoint(swordHandRest + CurrentActionOffset());
            Vector3 freeTarget = head.TransformPoint(freeHandRest);

            float step = handFollowSpeed * Time.deltaTime;
            swordHand.position = Vector3.Lerp(swordHand.position, swordTarget, step);
            freeHand.position = Vector3.Lerp(freeHand.position, freeTarget, step);

            swordHand.rotation = head.rotation;
            freeHand.rotation = head.rotation;
        }

        /// <summary>동작 진행도를 0→1→0 형태의 산 모양으로 만들어, 뻗었다가 돌아오는 궤적을 그린다.</summary>
        private Vector3 CurrentActionOffset()
        {
            if (actionTimer <= 0f)
            {
                return Vector3.zero;
            }

            float duration = isSlashing ? slashDuration : thrustDuration;
            float progress = 1f - (actionTimer / duration);
            float curve = Mathf.Sin(progress * Mathf.PI);

            if (isSlashing)
            {
                // 오른쪽에서 왼쪽으로 가로지르며 앞으로 조금 나간다.
                return new Vector3(Mathf.Lerp(slashArc * 0.5f, -slashArc * 0.5f, progress), 0f, curve * 0.25f);
            }

            return new Vector3(0f, 0f, curve * thrustDistance);
        }
    }
}
