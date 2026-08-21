using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using VRGame.Locomotion;

namespace VRGame.Samurai
{
    /// <summary>헤드셋이 붙어 있으면 VR 추적을, 없으면 마우스·키보드 임시 조작을 켠다.
    ///
    /// 게임 로직(도약·절단·리스폰)은 어느 쪽이 손을 움직이는지 전혀 모른다. 그래서 VR 전환에
    /// 필요한 작업이 "이 스위처가 어느 컴포넌트를 켜는가" 하나로 끝난다 — 씬을 다시 만들거나
    /// 스크립트를 고칠 필요가 없다.</summary>
    public class RigModeSwitcher : MonoBehaviour
    {
        [Header("VR")]
        [Tooltip("헤드셋 추적을 담당하는 컴포넌트들 (머리 + 양손).")]
        [SerializeField] private XRNodeTracker[] xrTrackers;

        [Header("Placeholder")]
        [SerializeField] private PlaceholderSwordRig placeholderRig;

        [Header("Override")]
        [Tooltip("체크하면 헤드셋이 연결돼 있어도 임시 조작을 강제한다 (에디터 테스트용).")]
        [SerializeField] private bool forcePlaceholder;

        public bool IsVRActive { get; private set; }

        private void Awake()
        {
            IsVRActive = !forcePlaceholder && DetectVR();
            ApplyMode();
        }

        /// <summary>XR 디스플레이가 실제로 돌고 있는지 확인한다. XRSettings.isDeviceActive만 보면
        /// 로더는 올라갔지만 헤드셋이 꺼진 경우를 걸러내지 못해, 추적 디바이스 존재도 함께 본다.</summary>
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

        private void ApplyMode()
        {
            for (int i = 0; i < xrTrackers.Length; i++)
            {
                if (xrTrackers[i] != null)
                {
                    xrTrackers[i].enabled = IsVRActive;
                }
            }

            if (placeholderRig != null)
            {
                placeholderRig.enabled = !IsVRActive;
            }

            Debug.Log(IsVRActive
                ? "[Samurai] VR 모드 — 헤드셋 추적으로 카타나를 조작합니다."
                : "[Samurai] 임시 모드 — 마우스 이동=시점, 좌클릭=칼 찍기(도약), 우클릭=베기, WASD=보조 이동.");
        }
    }
}
