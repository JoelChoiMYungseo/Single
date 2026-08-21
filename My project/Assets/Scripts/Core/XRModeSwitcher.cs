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

        public bool IsVRActive { get; private set; }

        private void Awake()
        {
            IsVRActive = !forcePlaceholder && DetectVR();

            SetAll(vrOnly, IsVRActive);
            SetAll(placeholderOnly, !IsVRActive);

            Debug.Log(IsVRActive
                ? "[XR] VR 모드로 시작합니다."
                : "[XR] 임시 모드로 시작합니다 (헤드셋 미검출).");
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
