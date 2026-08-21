using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Survivor
{
    /// <summary>경험치 젬 풀. 잡몹이 초당 수십 마리씩 죽으므로 Instantiate/Destroy를 그대로 쓰면
    /// GC 스파이크가 난다.</summary>
    public class ExperienceGemPool : MonoBehaviour
    {
        [SerializeField] private ExperienceGem gemPrefab;
        [SerializeField] private int prewarmCount = 64;

        private readonly Queue<ExperienceGem> inactive = new Queue<ExperienceGem>();
        private Transform playerTransform;
        private PlayerStats playerStats;
        private PlayerExperience playerExperience;

        /// <summary>런 매니저가 플레이어 참조를 넣어준다. 젬이 스스로 플레이어를 찾지 않게 한다.</summary>
        public void Bind(Transform player, PlayerStats stats, PlayerExperience experience)
        {
            playerTransform = player;
            playerStats = stats;
            playerExperience = experience;
        }

        private void Awake()
        {
            for (int i = 0; i < prewarmCount; i++)
            {
                inactive.Enqueue(CreateGem());
            }
        }

        private ExperienceGem CreateGem()
        {
            ExperienceGem gem = Instantiate(gemPrefab, transform);
            gem.OnCollected -= HandleCollected;
            gem.OnCollected += HandleCollected;
            gem.gameObject.SetActive(false);
            return gem;
        }

        public void Spawn(Vector3 position, int amount)
        {
            ExperienceGem gem = inactive.Count > 0 ? inactive.Dequeue() : CreateGem();
            gem.gameObject.SetActive(true);
            gem.ResetForSpawn(position, playerTransform, playerStats, amount);
        }

        private void HandleCollected(ExperienceGem gem)
        {
            playerExperience.AddExperience(gem.Amount);
            gem.gameObject.SetActive(false);
            inactive.Enqueue(gem);
        }

        /// <summary>런 재시작 시 필드에 남은 젬을 전부 회수한다.</summary>
        public void DespawnAll()
        {
            var gems = GetComponentsInChildren<ExperienceGem>(includeInactive: false);
            for (int i = 0; i < gems.Length; i++)
            {
                gems[i].gameObject.SetActive(false);
                inactive.Enqueue(gems[i]);
            }
        }
    }
}
