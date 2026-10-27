using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using VRGame.Core;

namespace VRGame.HoseCleaner
{
    /// <summary>런 관리 허브. 주기적으로 Dirt Monster를 스폰하고, 청소된 수를 세어 목표에 도달하면
    /// 런을 끝낸다. 안전망(추락 리스폰)과 재시작은 Echo/Survivor RunManager와 같은 최소 구성.</summary>
    public class HoseCleanerRunManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerRig player;
        [SerializeField] private HoseCleanerHUD hud;
        [SerializeField] private DirtMonster monsterPrefab;
        [SerializeField] private Transform startPoint;

        [Header("Spawning")]
        [SerializeField] private float spawnInterval = 4f;
        [SerializeField] private float spawnRadius = 12f;
        [SerializeField] private int maxConcurrentMonsters = 6;

        [Header("Goal")]
        [SerializeField] private int cleanedTarget = 15;

        [Tooltip("이 높이 아래로 떨어지면 시작 지점으로 되돌린다.")]
        [SerializeField] private float killPlaneY = -20f;
        [SerializeField] private Key restartKey = Key.R;

        private readonly List<DirtMonster> activeMonsters = new List<DirtMonster>(8);
        private int cleanedCount;
        private float nextSpawnTime;
        private bool runComplete;

        private void Start()
        {
            hud.SetCleanedCount(cleanedCount, cleanedTarget);
            hud.SetMessage(string.Empty);
        }

        private void Update()
        {
            if (player.Position.y < killPlaneY)
            {
                ResetRun();
                return;
            }

            if (Keyboard.current != null && Keyboard.current[restartKey].wasPressedThisFrame)
            {
                ResetRun();
                return;
            }

            if (runComplete)
            {
                return;
            }

            PruneDeadMonsters();

            if (Time.time >= nextSpawnTime && activeMonsters.Count < maxConcurrentMonsters)
            {
                nextSpawnTime = Time.time + spawnInterval;
                SpawnMonster();
            }
        }

        /// <summary>DirtMonster는 청소되면 Destroy(delay)로 스스로 사라진다 — 그 시점을 이벤트로 잡아
        /// 카운트를 늘리고, 파괴된 뒤에도 목록에 null로 남지 않게 매 프레임 걸러낸다.</summary>
        private void PruneDeadMonsters()
        {
            for (int i = activeMonsters.Count - 1; i >= 0; i--)
            {
                if (activeMonsters[i] == null)
                {
                    activeMonsters.RemoveAt(i);
                }
            }
        }

        private void SpawnMonster()
        {
            Vector3 randomOffset = Random.insideUnitSphere * spawnRadius;
            randomOffset.y = 0f;
            Vector3 desired = startPoint.position + randomOffset;

            if (!NavMesh.SamplePosition(desired, out NavMeshHit hit, spawnRadius, NavMesh.AllAreas))
            {
                return;
            }

            DirtMonster monster = Instantiate(monsterPrefab, hit.position, Quaternion.identity);
            monster.SetPlayer(player.Root);
            monster.OnCleaned += HandleMonsterCleaned;
            activeMonsters.Add(monster);
        }

        private void HandleMonsterCleaned(DirtMonster monster)
        {
            monster.OnCleaned -= HandleMonsterCleaned;
            cleanedCount++;
            hud.SetCleanedCount(cleanedCount, cleanedTarget);

            if (cleanedCount >= cleanedTarget)
            {
                runComplete = true;
                hud.SetMessage($"청소 완료! {restartKey} 키로 다시 도전");
            }
        }

        public void ResetRun()
        {
            for (int i = 0; i < activeMonsters.Count; i++)
            {
                if (activeMonsters[i] != null)
                {
                    activeMonsters[i].OnCleaned -= HandleMonsterCleaned;
                    Destroy(activeMonsters[i].gameObject);
                }
            }
            activeMonsters.Clear();

            cleanedCount = 0;
            runComplete = false;
            nextSpawnTime = Time.time + spawnInterval;

            hud.SetCleanedCount(cleanedCount, cleanedTarget);
            hud.SetMessage(string.Empty);

            player.Teleport(startPoint.position);
        }
    }
}
