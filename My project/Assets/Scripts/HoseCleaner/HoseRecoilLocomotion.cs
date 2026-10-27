using Autohand;
using UnityEngine;

namespace VRGame.HoseCleaner
{
    /// <summary>고압 호스. 손에 쥐고 방아쇠(Squeeze)를 당기면 앞으로 물을 뿜어 먼지 몬스터를 닦는다.
    /// 2026-09-17 세션에서 8개 게임 전체를 고릴라 태그 팔스윙으로 통일하기로 결정하면서, 반작용으로
    /// 플레이어를 밀어내던 반동 추진(<see cref="ApplyRecoil"/>) 호출은 껐다 — 이동은 이제
    /// <c>SinglePlayerGorillaLocomotion</c>이 전담하고, 호스는 청소 도구로만 남는다. 메서드 자체는
    /// 지우지 않고 남겨뒀다(참고용 + 되돌리기 쉽게).
    ///
    /// Auto Hand에서 "쥐기(Grab)"와 "방아쇠(Squeeze)"는 별개다 — Grab은 쥐는 동작 자체, Squeeze는
    /// 쥔 채로 누르는 보조 버튼(대개 트리거)이다. Auto Hand 데모의 Pistol.cs가 정확히 이 조합으로
    /// 방아쇠를 구현하므로 그 패턴을 그대로 따른다. 다만 Pistol.cs는 총의 반동을 총 자신에게 주지만,
    /// 이 게임은 반동의 목적이 "총을 흔드는 것"이 아니라 "플레이어를 미는 것"이라 AutoHandPlayer.body에
    /// 힘을 가한다.</summary>
    [RequireComponent(typeof(Grabbable))]
    public class HoseRecoilLocomotion : MonoBehaviour
    {
        [Header("Aim")]
        [Tooltip("물이 나가는 방향의 기준점 — 이 Transform의 forward 반대 방향으로 플레이어를 민다.")]
        [SerializeField] private Transform nozzle;

        [Header("Recoil")]
        [Tooltip("아래를 겨눴을 때 플레이어가 공중에 뜰 만큼 강해야 한다 — 플레이어 몸무게(질량)에 맞춰 튜닝.")]
        [SerializeField] private float recoilForce = 40f;
        [Tooltip("Force = 질량 영향받는 지속 가속, VelocityChange = 질량 무시 즉각 가속.")]
        [SerializeField] private ForceMode recoilForceMode = ForceMode.Force;

        [Header("Cleaning")]
        [SerializeField] private float range = 8f;
        [Tooltip("분사구 정면 기준 이 각도(도) 안에 들어온 몬스터만 맞는다.")]
        [SerializeField] private float coneHalfAngle = 20f;
        [SerializeField] private float cleaningRatePerSecond = 40f;

        [Header("Particles")]
        [Tooltip("비워두면 이 오브젝트의 ParticleSystem을 찾는다.")]
        [SerializeField] private ParticleSystem waterJet;
        [SerializeField] private float emissionRateWhileSpraying = 120f;

        private Grabbable grabbable;
        private Hand holdingHand;
        private bool isSpraying;

        private void Awake()
        {
            grabbable = GetComponent<Grabbable>();
            if (waterJet == null)
            {
                waterJet = GetComponent<ParticleSystem>();
            }
            if (nozzle == null)
            {
                nozzle = transform;
            }
        }

        private void OnEnable()
        {
            grabbable.OnGrabEvent -= HandleGrabbed;
            grabbable.OnGrabEvent += HandleGrabbed;
            grabbable.OnReleaseEvent -= HandleReleased;
            grabbable.OnReleaseEvent += HandleReleased;
        }

        private void OnDisable()
        {
            grabbable.OnGrabEvent -= HandleGrabbed;
            grabbable.OnReleaseEvent -= HandleReleased;
            holdingHand = null;
            SetSpraying(false);
        }

        private void HandleGrabbed(Hand hand, Grabbable grabbedItem)
        {
            holdingHand = hand;
        }

        private void HandleReleased(Hand hand, Grabbable grabbedItem)
        {
            if (holdingHand == hand)
            {
                holdingHand = null;
                SetSpraying(false);
            }
        }

        // RULE-03: 물리 API(AddForce)는 FixedUpdate에서만 호출한다.
        private void FixedUpdate()
        {
            bool wantsSpray = holdingHand != null && holdingHand.IsSqueezing();
            SetSpraying(wantsSpray);

            if (!wantsSpray)
            {
                return;
            }

            // ApplyRecoil()은 더 이상 호출하지 않는다 — 이동은 SinglePlayerGorillaLocomotion이 전담.
            ApplyCleaning();
        }

        private void ApplyRecoil()
        {
            Rigidbody playerBody = AutoHandPlayer.Instance != null ? AutoHandPlayer.Instance.body : null;
            if (playerBody == null)
            {
                return;
            }

            playerBody.AddForce(-nozzle.forward * recoilForce, recoilForceMode);
        }

        /// <summary>레이캐스트 대신 거리+각도(원뿔) 검사로 판정한다 — 이 프로젝트 전체가 잡몹 피격을
        /// 콜라이더/레이캐스트가 아니라 거리 비교로 처리하는 관행(Survivor/Danmaku 등)과 일치시켰다.
        /// 물줄기가 입자 하나하나로 맞히는 게 아니라 "분사 콘 안에 있으면 계속 젖는다"는 것도 실제
        /// 물호스 체감에 더 가깝다.</summary>
        private void ApplyCleaning()
        {
            var monsters = DirtMonsterRegistry.Alive;
            if (monsters.Count == 0)
            {
                return;
            }

            float cleaningThisTick = cleaningRatePerSecond * Time.fixedDeltaTime;
            Vector3 origin = nozzle.position;
            Vector3 forward = nozzle.forward;

            for (int i = monsters.Count - 1; i >= 0; i--)
            {
                DirtMonster monster = monsters[i];
                if (monster == null || monster.IsCleaned)
                {
                    continue;
                }

                Vector3 toMonster = monster.transform.position - origin;
                float distance = toMonster.magnitude;
                if (distance > range || distance <= 0.0001f)
                {
                    continue;
                }

                float angle = Vector3.Angle(forward, toMonster);
                if (angle > coneHalfAngle)
                {
                    continue;
                }

                monster.TakeDamage(cleaningThisTick);
            }
        }

        private void SetSpraying(bool spraying)
        {
            if (spraying == isSpraying)
            {
                return;
            }
            isSpraying = spraying;

            if (waterJet == null)
            {
                return;
            }

            if (spraying)
            {
                var emission = waterJet.emission;
                emission.rateOverTime = emissionRateWhileSpraying;
                waterJet.Play();
            }
            else
            {
                waterJet.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}
