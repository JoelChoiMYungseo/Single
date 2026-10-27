using UnityEngine;

namespace VRGame.Plunger
{
    /// <summary>몬스터의 머리 콜라이더에 붙는 컴포넌트. <see cref="PlungerLocomotion"/>은 자신이
    /// 무엇에 흡착됐는지 모른 채로 <see cref="PlungerLocomotion.OnStuck"/>만 쏘고, 이 컴포넌트가
    /// 그 앵커가 자기 자신인지 확인해서 반응한다 — 결합 방향은 항상 이쪽에서 로코모션 쪽으로만 향한다.</summary>
    [RequireComponent(typeof(Collider))]
    public class MonsterFaceStick : MonoBehaviour
    {
        [SerializeField] private PlungerMonster monster;
        [Tooltip("PlungerMonster.panicDuration 대신 이 값을 우선 쓴다.")]
        [SerializeField] private float blindDuration = 2.5f;
        [Tooltip("true면 자연스럽게 몬스터 쪽으로 당겨지도록 둔다. false(기본)면 얼굴에서 튕겨 나간다.")]
        [SerializeField] private bool pullPlayerTowardOnLaunch = false;

        private PlungerLocomotion boundLocomotion;
        private bool leftWasMe;
        private bool rightWasMe;

        private void Awake()
        {
            if (monster == null)
            {
                monster = GetComponentInParent<PlungerMonster>();
            }
        }

        /// <summary>SceneBuilder가 플레이어 리그를 만든 뒤 호출해 이벤트를 연결한다.</summary>
        public void Bind(PlungerLocomotion locomotion)
        {
            Unbind();

            boundLocomotion = locomotion;
            if (boundLocomotion == null)
            {
                return;
            }

            boundLocomotion.OnStuck += HandleStuck;
            boundLocomotion.OnLaunched += HandleLaunched;
        }

        private void Unbind()
        {
            if (boundLocomotion == null)
            {
                return;
            }
            boundLocomotion.OnStuck -= HandleStuck;
            boundLocomotion.OnLaunched -= HandleLaunched;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void HandleStuck(bool isLeft, Transform anchor)
        {
            if (anchor != transform && !anchor.IsChildOf(transform))
            {
                return;
            }

            if (isLeft) leftWasMe = true; else rightWasMe = true;

            if (monster != null)
            {
                monster.Blind(blindDuration);
            }
        }

        private void HandleLaunched(bool isLeft, Vector3 impulse)
        {
            bool wasMe = isLeft ? leftWasMe : rightWasMe;
            if (!wasMe)
            {
                return;
            }
            if (isLeft) leftWasMe = false; else rightWasMe = false;

            if (pullPlayerTowardOnLaunch || impulse.sqrMagnitude <= 0f)
            {
                // true: PlungerLocomotion이 이미 만든 "고정점(몬스터) 쪽으로 당겨지는" 임펄스를
                // 그대로 둔다 — 추가 처리 없음.
                return;
            }

            // 기본값(false): 몬스터 얼굴에서는 반대로 튕겨 나가야 자연스럽다. 이미 가해진 +impulse를
            // 상쇄하고 반대 방향으로 다시 걸어서(-2*impulse) 순 효과를 -impulse로 뒤집는다.
            boundLocomotion.Body.AddForce(-2f * impulse, ForceMode.VelocityChange);
        }
    }
}
