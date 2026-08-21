using System;
using UnityEngine;

namespace VRGame.Survivor
{
    /// <summary>플레이어 자동공격 탄. 적에게 콜라이더를 달지 않기 위해 물리 대신 EnemyRegistry를
    /// 거리로 검사한다 — 콜라이더 달린 오브젝트 150개를 매 프레임 움직이면 물리 엔진이 정적 트리를
    /// 계속 재구성해서 그것만으로 프레임이 무너진다.</summary>
    public class PlayerProjectile : MonoBehaviour
    {
        [SerializeField] private float hitRadius = 0.6f;

        public event Action<PlayerProjectile> OnFinished;

        private Vector3 direction;
        private float speed;
        private float damage;
        private float lifetime;
        private float elapsed;
        private int remainingPierce;

        public void Launch(Vector3 travelDirection, float travelSpeed, float hitDamage, float maxLifetime, int pierce)
        {
            direction = travelDirection.normalized;
            speed = travelSpeed;
            damage = hitDamage;
            lifetime = maxLifetime;
            elapsed = 0f;
            remainingPierce = pierce;
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
            CheckHit();
        }

        private void CheckHit()
        {
            var enemies = EnemyRegistry.Alive;
            float radiusSqr = hitRadius * hitRadius;
            Vector3 position = transform.position;

            // 피격으로 적이 죽으면 그 자리에서 목록이 줄어든다. 뒤에서부터 돌아 인덱스가 밀리지 않게 한다.
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                if (i >= enemies.Count)
                {
                    continue;
                }

                EnemyTarget enemy = enemies[i];
                if (enemy.IsDead)
                {
                    continue;
                }

                Vector3 delta = enemy.transform.position - position;
                delta.y = 0f;

                if (delta.sqrMagnitude > radiusSqr)
                {
                    continue;
                }

                enemy.TakeDamage(damage);

                remainingPierce--;
                if (remainingPierce < 0)
                {
                    OnFinished?.Invoke(this);
                    return;
                }
            }
        }
    }
}
