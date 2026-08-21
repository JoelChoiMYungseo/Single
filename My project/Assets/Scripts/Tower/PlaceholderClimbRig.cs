using UnityEngine;
using UnityEngine.InputSystem;

namespace VRGame.Tower
{
    /// <summary>헤드셋 없이 등반 물리를 시험하기 위한 임시 조작.
    ///
    /// 매핑의 핵심 아이디어: 손 목표 지점을 머리에 붙여 둔다. 그러면 "위를 보고 → 잡고 → 아래를 보면"
    /// 손 목표가 내려가면서 스프링이 몸을 끌어올린다 — VR에서 팔을 뻗어 잡고 당기는 동작과 물리적으로
    /// 동일한 입력이 된다. 덕분에 VRHandController는 한 줄도 바뀌지 않는다.</summary>
    public class PlaceholderClimbRig : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftHandTarget;
        [SerializeField] private Transform rightHandTarget;
        [SerializeField] private VRHandController leftHand;
        [SerializeField] private VRHandController rightHand;

        [Header("Look")]
        [SerializeField] private float mouseSensitivity = 0.11f;
        [SerializeField] private float pitchLimit = 85f;

        [Header("Reach")]
        [SerializeField] private float handSpread = 0.26f;
        [SerializeField] private float handDrop = 0.12f;
        [SerializeField] private float minReach = 0.35f;
        [SerializeField] private float maxReach = 0.8f;
        [SerializeField] private float reachStep = 0.9f;

        private float yaw;
        private float pitch;
        private float reach = 0.6f;

        private void OnEnable()
        {
            yaw = transform.eulerAngles.y;
            pitch = 0f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // 임시 모드에서는 그립을 이 스크립트가 넣어준다. 입력 출처 전환을 여기서 해야
            // 모드 전환과 그립 경로가 어긋나지 않는다.
            leftHand.SetUseExternalGrip(true);
            rightHand.SetUseExternalGrip(true);
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // VR로 돌아가면 XR 그립 입력을 다시 쓴다.
            leftHand.SetUseExternalGrip(false);
            rightHand.SetUseExternalGrip(false);
        }

        private void Update()
        {
            UpdateLook();
            UpdateReach();
            UpdateGrip();
            PoseHandTargets();
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

            // 몸통은 좌우만, 머리는 상하만 — VR에서 HMD가 하는 역할을 대체한다.
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void UpdateReach()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            float input = 0f;
            if (keyboard.wKey.isPressed) input += 1f;
            if (keyboard.sKey.isPressed) input -= 1f;

            reach = Mathf.Clamp(reach + input * reachStep * Time.unscaledDeltaTime, minReach, maxReach);
        }

        private void UpdateGrip()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            leftHand.SetGrip(mouse.leftButton.isPressed);
            rightHand.SetGrip(mouse.rightButton.isPressed);
        }

        /// <summary>손 목표를 머리 기준 로컬 오프셋에 둔다. 실제 이동은 VRHandController의
        /// 스프링이 처리하므로 여기서는 목표만 옮긴다.</summary>
        private void PoseHandTargets()
        {
            leftHandTarget.position = head.TransformPoint(new Vector3(-handSpread, -handDrop, reach));
            rightHandTarget.position = head.TransformPoint(new Vector3(handSpread, -handDrop, reach));

            leftHandTarget.rotation = head.rotation;
            rightHandTarget.rotation = head.rotation;
        }
    }
}
