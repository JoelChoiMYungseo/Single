using System;
using UnityEngine;
using VRGame.Survivor;

namespace VRGame.Danmaku
{
    /// <summary>보스가 쏘는 탄막. Rigidbody도 Collider도 쓰지 않고 Transform으로 직진하며, 명중 판정은
    /// 물리 대신 플레이어 한 명과의 거리로만 검사한다. 화면에 탄이 수백 발 떠 있어도 검사 비용이
    /// 탄 개수에만 비례하므로 Quest에서도 90fps를 유지할 수 있다.</summary>
    public class DanmakuBullet : MonoBehaviour
    {
        [SerializeField] private float hitRadius = 0.45f;

        public event Action<DanmakuBullet> OnFinished;

        private Transform target;
        private PlayerHealth targetHealth;
        private Vector3 direction;
        private float speed;
        private float damage;
        private float lifetime;
        private float elapsed;

        /// <summary>풀이 스폰 시점에 플레이어 참조를 넣어준다. 탄이 스스로 플레이어를 찾지 않게 한다.</summary>
        public void Bind(Transform playerTarget, PlayerHealth playerHealth)
        {
            target = playerTarget;
            targetHealth = playerHealth;
        }

        public void Launch(Vector3 travelDirection, float travelSpeed, float hitDamage, float maxLifetime)
        {
            direction = travelDirection.normalized;
            speed = travelSpeed;
            damage = hitDamage;
            lifetime = maxLifetime;
            elapsed = 0f;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= lifetime)
            {
                OnFinished?.Invoke(this);
                return;
            }

            transform.position += direction * speed * Time.deltaTime;

            if (target == null)
            {
                return;
            }

            Vector3 delta = target.position - transform.position;
            delta.y = 0f;

            if (delta.sqrMagnitude <= hitRadius * hitRadius)
            {
                targetHealth.TakeDamage(damage);
                OnFinished?.Invoke(this);
            }
        }
    }
}
