using UnityEngine;

namespace VRGame.Survivor
{
    /// <summary>임시 플레이어용 탑다운 추적 카메라. VR 리그로 교체할 때는 이 컴포넌트를 지우고
    /// 카메라를 리그 하위로 옮기면 된다.</summary>
    public class TopDownCameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 24f, -14f);
        [SerializeField] private float smoothing = 8f;

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desired = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desired, smoothing * Time.deltaTime);
            transform.rotation = Quaternion.LookRotation(target.position - transform.position, Vector3.up);
        }
    }
}
