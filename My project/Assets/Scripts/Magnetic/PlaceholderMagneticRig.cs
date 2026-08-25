using UnityEngine;
using UnityEngine.InputSystem;

namespace VRGame.Magnetic
{
    /// <summary>헤드셋 없이 자력 로코모션을 시험하기 위한 임시 조작.
    /// 좌클릭 = 왼손 발사, 우클릭 = 오른손 발사, 좌/우 Shift = 해당 손 극성 토글.
    /// 트리거/그립을 그대로 마우스 버튼/키에 매핑한 것이라, VR로 넘어갈 때 이 컴포넌트만 끄면
    /// MagneticLocomotion은 코드 변경 없이 실제 컨트롤러 입력으로 넘어간다.</summary>
    public class PlaceholderMagneticRig : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [SerializeField] private MagneticLocomotion locomotion;
        [SerializeField] private Transform leftHandOrigin;
        [SerializeField] private Transform rightHandOrigin;

        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private float pitchLimit = 85f;

        [Tooltip("머리 기준 로컬 오프셋 — 마우스로는 손을 따로 조준할 수 없어 시선 방향에 고정한다.")]
        [SerializeField] private Vector3 leftHandOffset = new Vector3(-0.25f, -0.1f, 0.3f);
        [SerializeField] private Vector3 rightHandOffset = new Vector3(0.25f, -0.1f, 0.3f);

        private float yaw;
        private float pitch;

        private void OnEnable()
        {
            yaw = transform.eulerAngles.y;
            pitch = 0f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            locomotion.SetUseExternalInput(true, true);
            locomotion.SetUseExternalInput(false, true);
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            locomotion.SetUseExternalInput(true, false);
            locomotion.SetUseExternalInput(false, false);
        }

        private void Update()
        {
            UpdateLook();
            PoseHandOrigins();
            UpdateFire();
            UpdatePolarityToggle();
        }

        /// <summary>마우스로는 두 손을 따로 조준할 수 없으니, 손 원점을 시선 방향에 고정해
        /// "보는 곳을 쏜다"로 대체한다. VR에서는 이 메서드가 아예 호출되지 않고 XRNodeTracker가
        /// 실제 컨트롤러 포즈로 이 트랜스폼들을 움직인다.</summary>
        private void PoseHandOrigins()
        {
            if (leftHandOrigin != null)
            {
                leftHandOrigin.SetPositionAndRotation(head.TransformPoint(leftHandOffset), head.rotation);
            }
            if (rightHandOrigin != null)
            {
                rightHandOrigin.SetPositionAndRotation(head.TransformPoint(rightHandOffset), head.rotation);
            }
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
            head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void UpdateFire()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            locomotion.SetExternalFire(true, mouse.leftButton.isPressed);
            locomotion.SetExternalFire(false, mouse.rightButton.isPressed);
        }

        private void UpdatePolarityToggle()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.leftShiftKey.wasPressedThisFrame)
            {
                locomotion.ToggleExternalPolarity(true);
            }
            if (keyboard.rightShiftKey.wasPressedThisFrame)
            {
                locomotion.ToggleExternalPolarity(false);
            }
        }
    }
}
