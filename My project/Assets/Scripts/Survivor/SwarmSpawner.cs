using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Survivor
{
    /// <summary>탕탕특공대식 무한 스폰. 정해진 웨이브를 끊어 내보내는 대신, 플레이어 주변 링에서
    /// 끊임없이 적을 밀어넣고 경과 시간에 따라 스폰 속도·적 능력치를 함께 올린다. 쉬는 구간이 없어야
    /// 플레이어가 계속 움직이게 된다.</summary>
    public class SwarmSpawner : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private SwarmEnemy enemyPrefab;
        [SerializeField] private ExperienceGemPool gemPool;

        [Header("Spawn Ring")]
        [SerializeField] private float spawnDistance = 22f;
        [SerializeField] private float arenaHalfExtent = 28f;

        [Header("Pacing (경과 시간 기준)")]
        [SerializeField] private float initialSpawnInterval = 0.7f;
        [SerializeField] private float minSpawnInterval = 0.08f;
        [SerializeField] private float intervalDecayPerMinute = 0.25f;
        [SerializeField] private int burstSize = 2;
        [SerializeField] private int maxConcurrentEnemies = 150;

        [Header("Enemy Scaling (분당 증가치)")]
        [SerializeField] private float baseEnemyHealth = 20f;
        [SerializeField] private float healthGainPerMinute = 14f;
        [SerializeField] private float baseEnemySpeed = 2.2f;
        [SerializeField] private float speedGainPerMinute = 0.5f;
        [SerializeField] private float maxEnemySpeed = 6.5f;
        [SerializeField] private float baseContactDamage = 8f;
        [SerializeField] private float damageGainPerMinute = 4f;

        /// <summary>잡몹 하나가 죽었을 때. 처치 수 집계 등 런 통계를 위한 훅.</summary>
        public event Action<EnemyTarget> OnEnemyKilled;

        private readonly Queue<SwarmEnemy> inactive = new Queue<SwarmEnemy>();
        private readonly List<SwarmEnemy> spawned = new List<SwarmEnemy>(256);
        private Transform playerTransform;
        private PlayerHealth playerHealth;
        private Coroutine spawnRoutine;
        private float runElapsed;

        public void Bind(Transform player, PlayerHealth health)
        {
            playerTransform = player;
            playerHealth = health;
        }

        public void BeginRun()
        {
            runElapsed = 0f;
            StopRun();
            spawnRoutine = StartCoroutine(SpawnLoop());
        }

        public void StopRun()
        {
            if (spawnRoutine != null)
            {
                StopCoroutine(spawnRoutine);
                spawnRoutine = null;
            }
        }

        /// <summary>런 재시작을 위해 필드의 적을 전부 회수한다.</summary>
        public void DespawnAll()
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i].gameObject.activeSelf)
                {
                    Recycle(spawned[i]);
                }
            }
        }

        private IEnumerator SpawnLoop()
        {
            while (true)
            {
                float minutes = runElapsed / 60f;
                float interval = Mathf.Max(minSpawnInterval, initialSpawnInterval - intervalDecayPerMinute * minutes);

                for (int i = 0; i < burstSize && EnemyRegistry.AliveCount < maxConcurrentEnemies; i++)
                {
                    SpawnOne(minutes);
                }

                // interval이 매번 달라져서 WaitForSeconds를 캐싱할 수 없다. new 로 만들면 그때마다
                // 힙 할당이 쌓이므로 프레임 누적으로 대기한다 — 할당 없음.
                float waited = 0f;
                while (waited < interval)
                {
                    waited += Time.deltaTime;
                    runElapsed += Time.deltaTime;
                    yield return null;
                }
            }
        }

        private void SpawnOne(float minutes)
        {
            SwarmEnemy enemy = inactive.Count > 0 ? inactive.Dequeue() : CreateEnemy();

            float health = baseEnemyHealth + healthGainPerMinute * minutes;
            float speed = Mathf.Min(maxEnemySpeed, baseEnemySpeed + speedGainPerMinute * minutes);
            float damage = baseContactDamage + damageGainPerMinute * minutes;

            enemy.gameObject.SetActive(true);
            enemy.ResetForSpawn(PickSpawnPosition(), playerTransform, playerHealth, health, speed, damage);
        }

        /// <summary>플레이어를 둘러싼 원 위 임의 지점. 아레나 밖으로 나가지 않게 가둔다.</summary>
        private Vector3 PickSpawnPosition()
        {
            // using System과 함께 쓰면 Random이 System.Random과 모호해진다 — 명시적으로 지정한다.
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            Vector3 offset = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * spawnDistance;
            Vector3 position = playerTransform.position + offset;

            position.x = Mathf.Clamp(position.x, -arenaHalfExtent, arenaHalfExtent);
            position.z = Mathf.Clamp(position.z, -arenaHalfExtent, arenaHalfExtent);
            position.y = 0f;
            return position;
        }

        private SwarmEnemy CreateEnemy()
        {
            SwarmEnemy enemy = Instantiate(enemyPrefab, transform);
            enemy.Target.OnDied -= HandleEnemyDied;
            enemy.Target.OnDied += HandleEnemyDied;
            enemy.gameObject.SetActive(false);
            spawned.Add(enemy);
            return enemy;
        }

        private void HandleEnemyDied(EnemyTarget target)
        {
            gemPool.Spawn(target.transform.position, target.ExperienceValue);
            OnEnemyKilled?.Invoke(target);

            var enemy = target.GetComponent<SwarmEnemy>();
            if (enemy != null)
            {
                Recycle(enemy);
            }
        }

        private void Recycle(SwarmEnemy enemy)
        {
            enemy.gameObject.SetActive(false);
            inactive.Enqueue(enemy);
        }
    }
}
