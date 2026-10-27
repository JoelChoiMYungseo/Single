using System;
using UnityEngine;
using UnityEngine.AI;
using VRGame.Combat;

namespace VRGame.HoseCleaner
{
    /// <summary>플레이어를 쫓아오는 잡몹. 체력 대신 "더러움(Dirtiness)"을 들고 있다가 호스에 맞으면
    /// 줄어들고, 0이 되면 "청소됨"으로 처치된다. 추적 자체는 Echo의 SoundAttractedAI와 같은
    /// NavMeshAgent 패턴 — 이 프로젝트에서 NavMesh를 쓰는 두 번째 게임이라 새 패턴을 만들 이유가 없다.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class DirtMonster : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxDirtiness = 100f;
        [SerializeField] private float chaseSpeed = 2.2f;
        [Tooltip("SetDestination 호출 간격 — 매 프레임 부르면 NavMesh 쿼리 비용이 쌓인다.")]
        [SerializeField] private float destinationUpdateInterval = 0.2f;

        public event Action<DirtMonster> OnCleaned;

        public bool IsCleaned { get; private set; }
        public float Dirtiness01 => maxDirtiness <= 0f ? 0f : currentDirtiness / maxDirtiness;

        private NavMeshAgent agent;
        private Transform player;
        private float currentDirtiness;
        private float nextDestinationUpdateTime;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.speed = chaseSpeed;
            currentDirtiness = maxDirtiness;
        }

        private void OnEnable()
        {
            DirtMonsterRegistry.Register(this);
        }

        private void OnDisable()
        {
            DirtMonsterRegistry.Unregister(this);
        }

        /// <summary>스포너가 생성 직후 호출. 몬스터 스스로 플레이어를 찾지 않게 한다.</summary>
        public void SetPlayer(Transform playerTarget)
        {
            player = playerTarget;
        }

        private void Update()
        {
            if (IsCleaned || player == null)
            {
                return;
            }

            if (Time.time < nextDestinationUpdateTime)
            {
                return;
            }
            nextDestinationUpdateTime = Time.time + destinationUpdateInterval;

            agent.SetDestination(player.position);
        }

        public void TakeDamage(float amount)
        {
            if (IsCleaned)
            {
                return;
            }

            currentDirtiness -= amount;
            if (currentDirtiness > 0f)
            {
                return;
            }

            currentDirtiness = 0f;
            IsCleaned = true;
            agent.isStopped = true;

            OnCleaned?.Invoke(this);

            // 폭발·파티클 없이 조용히 사라진다 — 청소 판정이 "씻겨나간다"는 느낌을 이미 준다.
            // 그 프레임에 아직 참조하는 코드(HUD 카운트 등)가 있을 수 있어 한 프레임 늦게 파괴.
            Destroy(gameObject, 0.1f);
        }
    }
}
