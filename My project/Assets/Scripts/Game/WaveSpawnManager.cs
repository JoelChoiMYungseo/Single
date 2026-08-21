using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRGame.AI;

namespace VRGame.Game
{
    /// <summary>웨이브마다 좀비 수와 난이도(속도/체력)를 함께 올려 스폰한다. Instantiate/Destroy 반복
    /// 대신 오브젝트 풀을 재사용하고, 웨이브 종료는 폴링이 아니라 ZombieAI.OnDied 이벤트로 감지한다.</summary>
    public class WaveSpawnManager : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private ZombieAI zombiePrefab;
        [SerializeField] private Transform[] spawnPoints;
        [SerializeField] private Transform playerTarget;

        [Header("Wave Tuning")]
        [SerializeField] private int initialZombiesPerWave = 3;
        [SerializeField] private int zombiesIncreasePerWave = 2;
        [SerializeField] private float delayBetweenWaves = 5f;

        [Header("Difficulty Scaling (per wave)")]
        [SerializeField] private float healthMultiplierPerWave = 0.15f;
        [SerializeField] private float speedMultiplierPerWave = 0.05f;
        [SerializeField] private float speedMultiplierCap = 2f;

        [Header("Spawn Pacing")]
        [SerializeField] private float spawnInterval = 0.35f;
        [SerializeField] private int maxConcurrentZombies = 20;

        /// <summary>waveIndex, 이번 웨이브 총 좀비 수</summary>
        public event Action<int, int> OnWaveStarted;
        /// <summary>waveIndex</summary>
        public event Action<int> OnWaveCleared;

        private readonly List<ZombieAI> pool = new List<ZombieAI>();
        private WaitForSeconds waveDelay;
        private WaitForSeconds spawnTick;
        private Coroutine nextWaveRoutine;
        private Coroutine spawnRoutine;
        private int aliveInWave;
        private int waveIndex;
        private bool stopped;

        private void Awake()
        {
            waveDelay = new WaitForSeconds(delayBetweenWaves);
            spawnTick = new WaitForSeconds(spawnInterval);
        }

        private void Start()
        {
            BeginWave(1);
        }

        private void OnDestroy()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                pool[i].OnDied -= HandleZombieDied;
            }
        }

        /// <summary>플레이어 사망 등 외부 상태에 따라 스폰을 완전히 멈춘다. PlayerHealth 같은 시스템이
        /// 아직 없어서 지금은 공개 메서드로만 훅을 열어둔다.</summary>
        public void StopSpawning()
        {
            stopped = true;
            if (spawnRoutine != null) StopCoroutine(spawnRoutine);
            if (nextWaveRoutine != null) StopCoroutine(nextWaveRoutine);
        }

        private void BeginWave(int index)
        {
            if (stopped)
            {
                return;
            }

            waveIndex = index;
            int count = initialZombiesPerWave + (index - 1) * zombiesIncreasePerWave;
            aliveInWave = count;

            float healthMult = 1f + healthMultiplierPerWave * (index - 1);
            float speedMult = Mathf.Min(speedMultiplierCap, 1f + speedMultiplierPerWave * (index - 1));

            OnWaveStarted?.Invoke(index, count);

            if (spawnRoutine != null)
            {
                StopCoroutine(spawnRoutine);
            }
            spawnRoutine = StartCoroutine(SpawnWaveRoutine(count, healthMult, speedMult));
        }

        private IEnumerator SpawnWaveRoutine(int count, float healthMult, float speedMult)
        {
            for (int i = 0; i < count; i++)
            {
                while (ActiveZombieCount() >= maxConcurrentZombies)
                {
                    yield return null;
                }

                SpawnOne(healthMult, speedMult);
                yield return spawnTick;
            }
            spawnRoutine = null;
        }

        private int ActiveZombieCount()
        {
            int count = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].gameObject.activeSelf)
                {
                    count++;
                }
            }
            return count;
        }

        private void SpawnOne(float healthMult, float speedMult)
        {
            Transform point = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
            ZombieAI zombie = GetPooledZombie();
            zombie.gameObject.SetActive(true);
            zombie.ResetForSpawn(point.position, point.rotation, playerTarget, healthMult, speedMult);
        }

        private ZombieAI GetPooledZombie()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].gameObject.activeSelf)
                {
                    return pool[i];
                }
            }

            ZombieAI created = Instantiate(zombiePrefab);
            created.gameObject.SetActive(false);
            created.OnDied += HandleZombieDied;
            pool.Add(created);
            return created;
        }

        private void HandleZombieDied(ZombieAI zombie)
        {
            aliveInWave--;
            if (aliveInWave > 0 || stopped)
            {
                return;
            }

            OnWaveCleared?.Invoke(waveIndex);

            if (nextWaveRoutine != null)
            {
                StopCoroutine(nextWaveRoutine);
            }
            nextWaveRoutine = StartCoroutine(StartNextWaveAfterDelay());
        }

        private IEnumerator StartNextWaveAfterDelay()
        {
            yield return waveDelay;
            nextWaveRoutine = null;
            BeginWave(waveIndex + 1);
        }
    }
}
