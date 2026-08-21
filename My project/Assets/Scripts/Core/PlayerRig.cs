using UnityEngine;

namespace VRGame.Core
{
    /// <summary>플레이어의 신원(identity)을 한 곳에 모은 컴포넌트. 세 프로토타입의 모든 게임 시스템은
    /// 구체적인 로코모션 타입이 아니라 이것을 참조한다.
    ///
    /// 왜 이 층이 필요한가: 리팩토링 전에는 시스템마다 참조 타입이 달랐다. 서바이버의 보스는
    /// PlaceholderPlayerController를, 사무라이의 탐험 매니저는 SwordGorillaLocomotion을,
    /// 타워의 낙하 매니저는 Rigidbody를 직접 들고 있었다. 그래서 로코모션을 바꾸려면 세 게임의
    /// 여러 파일을 동시에 고쳐야 했다.
    ///
    /// 이제는 로코모션 컴포넌트만 갈아 끼우면 된다 — 게임 로직은 한 줄도 바뀌지 않는다.
    /// 외부 패키지(예: Another Axiom의 MIT GorillaLocomotion)를 넣을 때도 그 코드를 수정하거나
    /// 참조할 필요가 없다. ObservedLocomotionDriver가 바깥에서 속도를 관측해 주기 때문이다.</summary>
    public class PlayerRig : MonoBehaviour
    {
        [Header("Rig Parts")]
        [Tooltip("비우면 이 오브젝트가 루트가 된다.")]
        [SerializeField] private Transform root;
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightHand;

        [Header("Physics")]
        [Tooltip("물리 기반 로코모션이면 채운다. Transform 이동 방식이면 비워도 된다.")]
        [SerializeField] private Rigidbody body;

        public Transform Root => root != null ? root : transform;
        public Transform Head => head;
        public Transform LeftHand => leftHand;
        public Transform RightHand => rightHand;
        public Rigidbody Body => body;

        public Vector3 Position => Root.position;

        /// <summary>물리든 Transform 이동이든 하나의 값으로 읽는다. Rigidbody가 있으면 그쪽을 믿고,
        /// 없으면 드라이버가 보고한 값을 쓴다.</summary>
        public Vector3 Velocity
        {
            get
            {
                if (body != null && !body.isKinematic)
                {
                    return body.linearVelocity;
                }
                return driver != null ? driver.Velocity : Vector3.zero;
            }
        }

        private ILocomotionDriver driver;

        private void Awake()
        {
            // 리그에 붙어 있는 로코모션 구현을 찾는다. 없어도 동작하지만(속도 0), 순간이동 시
            // 내부 상태 리셋 통보를 받을 대상이 없으므로 경고를 남긴다.
            driver = GetComponentInChildren<ILocomotionDriver>(true);
            if (driver == null)
            {
                Debug.LogWarning($"[PlayerRig] {name}에 ILocomotionDriver 구현이 없습니다 — " +
                                 "속도가 항상 0으로 보고되고 리스폰 후 상태 리셋이 되지 않습니다.");
            }
        }

        /// <summary>리스폰·체크포인트 복귀 등에 쓰는 유일한 순간이동 경로.
        /// 속도를 지우지 않으면 떨어지던 가속도를 그대로 안고 다시 떨어지고, 드라이버에 통보하지
        /// 않으면 팔스윙류 구현이 거짓 속도를 만들어 플레이어를 튕겨낸다.</summary>
        public void Teleport(Vector3 position)
        {
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = position;
            }

            Root.position = position;
            driver?.OnTeleported(position);
        }

        /// <summary>로코모션을 런타임에 교체할 때 쓴다 (VR/임시 모드 전환 등).</summary>
        public void SetDriver(ILocomotionDriver newDriver)
        {
            driver = newDriver;
        }
    }
}
