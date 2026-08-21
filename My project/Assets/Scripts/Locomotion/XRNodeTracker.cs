using UnityEngine;
using UnityEngine.XR;

namespace VRGame.Locomotion
{
    /// <summary>XR Interaction Toolkit 없이 코어 XR 모듈만으로 손 트랜스폼을 컨트롤러 포즈에 맞춘다.
    /// InputTracking이 반환하는 좌표는 트래킹 원점(플레이어 리그 루트) 기준 로컬 좌표라, 이 컴포넌트는
    /// 리그 하위의 손 오브젝트에 붙어야 한다.</summary>
    public class XRNodeTracker : MonoBehaviour
    {
        [SerializeField] private XRNode node = XRNode.LeftHand;

        private void Update()
        {
            transform.SetLocalPositionAndRotation(InputTracking.GetLocalPosition(node), InputTracking.GetLocalRotation(node));
        }
    }
}
