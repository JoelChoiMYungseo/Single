using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Samurai
{
    /// <summary>현재 구역의 예산에 맞춰 플레이어 주변에 적을 채워 넣는다. 웨이브로 끊지 않고 항상
    /// 일정 수를 유지해서, 올라가는 동안 쉬는 구간이 생기지 않게 한다.</summary>
    public class EnemySpawnDirector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SliceableEnemy enemyPrefab;
        [SerializeField] private Transform player;
        [SerializeField] private AscentZoneManager zones;

        [Header("Spawn Placement")]
        [Tooltip("플레이어로부터 이 거리의 링에서 등장한다.")]
        [SerializeField] private float spawnDistance = 15f;
        [Tooltip("등장 높이를 플레이어 기준 ±이 값만큼 흔든다.")]
        [SerializeField] private float spawnHeightSpread = 5f;
        [SerializeField] private int prewarmCount = 8;

        private readonly Queue<SliceableEnemy> inactive = new Queue<SliceableEnemy>();
        private readonly List<SliceableEnemy> allEnemies = new List<SliceableEnemy>(32);
        private int aliveCount;
        private float nextSpawnTime;

        public int AliveCount => aliveCount;

        private void Awake()
        {
            for (int i = 0; i < prewarmCount; i++)
            {
                inactive.Enqueue(CreateEnemy());
            }
        }

        private void Update()
        {
            AscentZone zone = zones.CurrentZone;

            if (aliveCount >= zone.MaxEnemies || Time.time < nextSpawnTime)
            {
                return;
            }

            nextSpawnTime = Time.time + Mathf.Max(0.2f, zone.EnemySpawnInterval);
            Spawn(zone);
        }

        private void Spawn(AscentZone zone)
        {
            SliceableEnemy enemy = inactive.Count > 0 ? inactive.Dequeue() : CreateEnemy();

            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 offset = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * spawnDistance;
            offset.y = Random.Range(-spawnHeightSpread, spawnHeightSpread);

            enemy.gameObject.SetActive(true);
            enemy.ResetForSpawn(player.position + offset, player, player.GetComponent<Rigidbody>(),
                Mathf.Max(0.1f, zone.EnemySpeedMultiplier));
            aliveCount++;
        }

        private SliceableEnemy CreateEnemy()
        {
            SliceableEnemy enemy = Instantiate(enemyPrefab, transform);
            enemy.OnDefeated -= Recycle;
            enemy.OnDefeated += Recycle;
            enemy.gameObject.SetActive(false);
            allEnemies.Add(enemy);
            return enemy;
        }

        private void Recycle(SliceableEnemy enemy)
        {
            aliveCount = Mathf.Max(0, aliveCount - 1);
            enemy.gameObject.SetActive(false);
            inactive.Enqueue(enemy);
        }

        /// <summary>런을 다시 시작할 때 필드를 비운다.</summary>
        public void DespawnAll()
        {
            for (int i = 0; i < allEnemies.Count; i++)
            {
                if (allEnemies[i].gameObject.activeSelf)
                {
                    Recycle(allEnemies[i]);
                }
            }
            aliveCount = 0;
        }
    }
}
