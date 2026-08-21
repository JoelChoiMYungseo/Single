using System;
using UnityEngine;
using UnityEngine.AI;
using VRGame.Combat;

namespace VRGame.AI
{
    /// <summary>플레이어를 계속 추적하는 좀비. 생존 시간이 길어질수록 속도가 올라가
    /// "도망칠 수 없다"는 압박감을 준다. 오브젝트 풀 재사용을 전제로 하므로 모든 상태는
    /// Awake가 아니라 ResetForSpawn에서 초기화한다.</summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class ZombieAI : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;

        [Header("Speed Ramp")]
        [SerializeField] private float initialSpeed = 1.5f;
        [SerializeField] private float maxSpeed = 5f;
        [SerializeField] private float speedRampPerSecond = 0.15f;

        [Header("Tracking")]
        [SerializeField] private float destinationUpdateInterval = 0.2f;
        [SerializeField] private float attackRange = 1.2f;

        public event Action<ZombieAI> OnDied;

        private NavMeshAgent agent;
        private Transform target;
        private float currentHealth;
        private float aliveTime;
        private float nextDestinationUpdateTime;
        private float healthMultiplier;
        private float speedMultiplier;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            agent.stoppingDistance = attackRange;
            // 같은 지점(플레이어)으로 몰리는 좀비들이 서로 밀착해 떨지 않도록 개체마다 회피 우선순위를 다르게 준다.
            agent.avoidancePriority = UnityEngine.Random.Range(1, 99);
        }

        /// <summary>스폰 매니저가 풀에서 꺼내 활성화한 뒤 호출한다. 위치 이동은 NavMeshAgent.Warp로
        /// 처리해 내부 NavMesh 상태가 어긋나지 않게 한다. healthMultiplier/speedMultiplier는 웨이브가
        /// 진행될수록 스폰 매니저가 올려서 넘기는 난이도 배율이다.</summary>
        public void ResetForSpawn(Vector3 position, Quaternion rotation, Transform playerTarget, float healthMultiplier, float speedMultiplier)
        {
            target = playerTarget;
            this.healthMultiplier = healthMultiplier;
            this.speedMultiplier = speedMultiplier;
            currentHealth = maxHealth * healthMultiplier;
            aliveTime = 0f;
            nextDestinationUpdateTime = 0f;

            agent.Warp(position);
            transform.rotation = rotation;
            agent.speed = initialSpeed * speedMultiplier;
        }

        private void Update()
        {
            if (target == null)
            {
                return;
            }

            aliveTime += Time.deltaTime;
            float rampedSpeed = initialSpeed * speedMultiplier + speedRampPerSecond * aliveTime;
            agent.speed = Mathf.Min(maxSpeed * speedMultiplier, rampedSpeed);

            if (Time.time >= nextDestinationUpdateTime)
            {
                agent.SetDestination(target.position);
                nextDestinationUpdateTime = Time.time + destinationUpdateInterval;
            }
        }

        public void TakeDamage(float amount)
        {
            if (currentHealth <= 0f)
            {
                return;
            }

            currentHealth -= amount;
            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            OnDied?.Invoke(this);
            gameObject.SetActive(false);
        }
    }
}
