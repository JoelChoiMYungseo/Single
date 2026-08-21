using System.Collections.Generic;
using UnityEngine;
using VRGame.Survivor;

namespace VRGame.Danmaku
{
    /// <summary>DanmakuBullet 전용 오브젝트 풀. Instantiate/Destroy를 반복하면 GC 스파이크로
    /// Quest에서 프레임이 튀기 때문에, 미리 만든 총알을 비활성 상태로 재사용한다.
    /// 프리팹 1종만 다루므로 제네릭 풀 대신 전용 타입으로 좁게 구현한다.</summary>
    public class BulletPool : MonoBehaviour
    {
        [SerializeField] private DanmakuBullet bulletPrefab;
        [SerializeField] private int prewarmCount = 200;

        private readonly Queue<DanmakuBullet> inactiveBullets = new Queue<DanmakuBullet>();
        private Transform playerTarget;
        private PlayerHealth playerHealth;

        /// <summary>런 매니저가 플레이어 참조를 주입한다.</summary>
        public void Bind(Transform player, PlayerHealth health)
        {
            playerTarget = player;
            playerHealth = health;
        }

        private void Awake()
        {
            for (int i = 0; i < prewarmCount; i++)
            {
                inactiveBullets.Enqueue(CreateBullet());
            }
        }

        private DanmakuBullet CreateBullet()
        {
            DanmakuBullet bullet = Instantiate(bulletPrefab, transform);
            bullet.OnFinished -= Return;
            bullet.OnFinished += Return;
            bullet.gameObject.SetActive(false);
            return bullet;
        }

        /// <summary>대기열에 반환된 총알을 꺼내 O(1)로 재사용한다. 대기열이 비어 있으면 그때만 새로 만든다.</summary>
        public DanmakuBullet Spawn(Vector3 position, Quaternion rotation, Vector3 direction, float speed, float damage, float lifetime)
        {
            DanmakuBullet bullet = inactiveBullets.Count > 0 ? inactiveBullets.Dequeue() : CreateBullet();
            bullet.transform.SetPositionAndRotation(position, rotation);
            bullet.gameObject.SetActive(true);
            bullet.Bind(playerTarget, playerHealth);
            bullet.Launch(direction, speed, damage, lifetime);
            return bullet;
        }

        public void Return(DanmakuBullet bullet)
        {
            bullet.gameObject.SetActive(false);
            inactiveBullets.Enqueue(bullet);
        }

        /// <summary>런 재시작 시 날아다니던 탄을 전부 회수한다.</summary>
        public void DespawnAll()
        {
            var active = GetComponentsInChildren<DanmakuBullet>(includeInactive: false);
            for (int i = 0; i < active.Length; i++)
            {
                Return(active[i]);
            }
        }
    }
}
