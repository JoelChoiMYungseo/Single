using System;
using UnityEngine;

namespace VRGame.Survivor
{
    /// <summary>적이 죽은 자리에 떨어지는 경험치. 플레이어의 자석 범위 안에 들어오면 빨려온다.
    /// 젬을 주우려면 위험지대로 들어가야 하므로, 이 장르 특유의 "먹으러 갈까 말까" 압박을 만든다.</summary>
    public class ExperienceGem : MonoBehaviour
    {
        [SerializeField] private float flySpeed = 12f;
        [SerializeField] private float collectDistance = 0.6f;

        /// <summary>주워진 젬 자신 — 풀 회수용.</summary>
        public event Action<ExperienceGem> OnCollected;

        private Transform target;
        private PlayerStats targetStats;
        private int amount;
        private bool isAttracted;

        public int Amount => amount;

        public void ResetForSpawn(Vector3 position, Transform playerTarget, PlayerStats playerStats, int experienceAmount)
        {
            transform.position = position;
            target = playerTarget;
            targetStats = playerStats;
            amount = experienceAmount;
            isAttracted = false;
        }

        private void Update()
        {
            if (target == null)
            {
                return;
            }

            Vector3 toPlayer = target.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;

            // 한 번 끌리기 시작하면 자석 범위를 벗어나도 계속 따라간다 — 안 그러면 경계에서 덜덜 떨린다.
            if (!isAttracted && distance <= targetStats.PickupRadius)
            {
                isAttracted = true;
            }

            if (!isAttracted)
            {
                return;
            }

            if (distance <= collectDistance)
            {
                OnCollected?.Invoke(this);
                return;
            }

            transform.position += (toPlayer / distance) * flySpeed * Time.deltaTime;
        }
    }
}
