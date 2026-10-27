using UnityEngine;
using UnityEngine.XR;

namespace VRGame.Locomotion
{
    /// <summary>XR Interaction Toolkit 없이 코어 XR 모듈만으로 손 트랜스폼을 컨트롤러 포즈에 맞춘다.
    /// InputDevice가 반환하는 좌표는 트래킹 원점(플레이어 리그 루트) 기준 로컬 좌표라, 이 컴포넌트는
    /// 리그 하위의 손 오브젝트에 붙어야 한다.</summary>
    public class XRNodeTracker : MonoBehaviour
    {
        [SerializeField] private XRNode node = XRNode.LeftHand;

        private InputDevice device;

        // InputTracking.GetLocalPosition/Rotation은 Deprecated — InputDevice.TryGetFeatureValue로 대체.
        // 디바이스는 씬 로드 후 늦게 붙을 수 있어(헤드셋 슬립 복귀 등) 매번 재조회해서 무효화에 대비한다.
        private void Update()
        {
            if (!device.isValid)
            {
                device = InputDevices.GetDeviceAtXRNode(node);
                if (!device.isValid)
                {
                    return;
                }
            }

            if (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 position) &&
                device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rotation))
            {
                transform.SetLocalPositionAndRotation(position, rotation);
            }
        }
    }
}
