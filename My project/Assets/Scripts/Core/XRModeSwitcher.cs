using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace VRGame.Core
{
    /// <summary>헤드셋이 붙어 있으면 VR 전용 컴포넌트를, 없으면 임시(비VR) 조작 컴포넌트를 켠다.
    ///
    /// 특정 타입을 몰라도 되게 Behaviour 배열로 받는다 — 씬마다 임시 조작 방식이 달라도 이 스위처는
    /// 그대로 재사용된다. 게임 로직은 어느 쪽이 입력을 만드는지 알 필요가 없다.</summary>
    public class XRModeSwitcher : MonoBehaviour
    {
        [Tooltip("VR일 때만 켜질 컴포넌트 (헤드/손 추적 등).")]
        [SerializeField] private Behaviour[] vrOnly;
        [Tooltip("헤드셋이 없을 때만 켜질 컴포넌트 (마우스·키보드 조작).")]
        [SerializeField] private Behaviour[] placeholderOnly;
        [Tooltip("체크하면 헤드셋이 있어도 임시 조작을 강제한다 (에디터 테스트용).")]
        [SerializeField] private bool forcePlaceholder;

        [Header("World-space HUD (선택)")]
        [Tooltip("비워두면 무시한다. 채우면 VR일 때 WorldSpace(머리에 붙여서), 아니면 ScreenSpaceOverlay로 전환한다.")]
        [SerializeField] private Canvas worldSpaceHud;
        [Tooltip("worldSpaceHud가 WorldSpace일 때 레이캐스트 기준으로 쓸 카메라 (보통 Head의 Camera).")]
        [SerializeField] private Camera vrEventCamera;

        [Header("Locomotion Driver 전환 (선택)")]
        [Tooltip("비워두면 무시한다. 채우면 VR/임시 전환 시 PlayerRig가 참조할 ILocomotionDriver도 같이 바꾼다 — " +
                 "리그에 ILocomotionDriver 구현이 두 개 이상(물리 로코모션 + Placeholder) 붙어 있을 때 필요하다. " +
                 "PlayerRig.Awake의 GetComponentInChildren은 그중 아무거나 하나를 고정으로 집어서, VR 여부와 " +
                 "무관하게 항상 같은 드라이버를 보고하는 문제가 있다.")]
        [SerializeField] private PlayerRig playerRig;
        [SerializeField] private MonoBehaviour vrDriver;
        [SerializeField] private MonoBehaviour placeholderDriver;

        public bool IsVRActive { get; private set; }

        private void Awake()
        {
            IsVRActive = !forcePlaceholder && DetectVR();

            SetAll(vrOnly, IsVRActive);
            SetAll(placeholderOnly, !IsVRActive);
            ApplyHudMode();
            ApplyLocomotionDriver();

            Debug.Log(IsVRActive
                ? "[XR] VR 모드로 시작합니다."
                : "[XR] 임시 모드로 시작합니다 (헤드셋 미검출).");
        }

        /// <summary>PlayerRig.Awake()는 리그 자식에서 ILocomotionDriver 구현을 하나 찾아 고정해버린다.
        /// 구현이 하나뿐이면 문제없지만, 둘 이상이면(물리 로코모션 + Placeholder 둘 다 인터페이스를
        /// 구현) 어느 쪽이 실제로 활성 상태인지와 무관하게 먼저 발견된 쪽으로 고정된다. 이 컴포넌트가
        /// PlayerRig보다 나중에 Awake돼야(씬 빌더에서 PlayerRig를 먼저 추가) 여기서 건 override가
        /// 살아남는다.</summary>
        private void ApplyLocomotionDriver()
        {
            if (playerRig == null)
            {
                return;
            }

            var active = (IsVRActive ? vrDriver : placeholderDriver) as ILocomotionDriver;
            if (active != null)
            {
                playerRig.SetDriver(active);
            }
        }

        /// <summary>ScreenSpaceOverlay는 마우스로 화면을 덮지만 헤드셋 렌더 타겟에는 그리지 않는다.
        /// VR에서도 HUD가 보이려면 머리에 붙인 WorldSpace로 바꿔야 한다 — 데스크톱에서는 반대로
        /// Overlay가 더 읽기 쉬우므로 모드에 따라 되돌린다.</summary>
        private void ApplyHudMode()
        {
            if (worldSpaceHud == null)
            {
                return;
            }

            worldSpaceHud.renderMode = IsVRActive ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
            worldSpaceHud.worldCamera = IsVRActive ? vrEventCamera : null;
        }

        /// <summary>XR 디스플레이가 돌고 있는지 + 실제 HMD 디바이스가 있는지 둘 다 본다.
        /// isDeviceActive만 보면 로더는 올라갔지만 헤드셋이 꺼진 경우를 걸러내지 못한다.</summary>
        private static bool DetectVR()
        {
            if (!XRSettings.isDeviceActive)
            {
                return false;
            }

            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, devices);
            return devices.Count > 0;
        }

        private static void SetAll(Behaviour[] targets, bool enabled)
        {
            if (targets == null)
            {
                return;
            }

            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                {
                    targets[i].enabled = enabled;
                }
            }
        }
    }
}
