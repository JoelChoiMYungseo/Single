using System.Collections.Generic;
using UnityEngine;

namespace VRGame.Survivor
{
    /// <summary>탕탕특공대의 핵심 — 조준·발사를 플레이어가 하지 않는다. 가장 가까운 적을 향해
    /// 자동으로 계속 쏘고, 플레이어는 "어디로 피할지"만 결정한다. VR 리그로 교체해도 이 컴포넌트는
    /// 플레이어 루트에 그대로 얹으면 동작한다.</summary>
    [RequireComponent(typeof(PlayerStats))]
    public class AutoAttackWeapon : MonoBehaviour
    {
        [Header("Projectile")]
        [SerializeField] private PlayerProjectile projectilePrefab;
        [SerializeField] private Transform muzzle;
        [SerializeField] private float projectileSpeed = 18f;
        [SerializeField] private float projectileLifetime = 2f;
        [SerializeField] private int prewarmCount = 48;

        [Header("Multi-shot")]
        [SerializeField] private float spreadAnglePerExtraShot = 9f;

        private readonly Queue<PlayerProjectile> inactive = new Queue<PlayerProjectile>();
        private PlayerStats stats;
        private Transform poolRoot;
        private float nextFireTime;
        private bool isFiring;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            if (muzzle == null)
            {
                muzzle = transform;
            }

            // 탄을 플레이어 자식으로 두면 플레이어가 움직일 때 같이 끌려간다. 별도 루트에 담는다.
            poolRoot = new GameObject("PlayerProjectilePool").transform;

            for (int i = 0; i < prewarmCount; i++)
            {
                inactive.Enqueue(CreateProjectile());
            }
        }

        private void OnDestroy()
        {
            if (poolRoot != null)
            {
                Destroy(poolRoot.gameObject);
            }
        }

        public void SetFiring(bool value)
        {
            isFiring = value;
        }

        private void Update()
        {
            if (!isFiring || Time.time < nextFireTime)
            {
                return;
            }

            EnemyTarget target = EnemyRegistry.FindNearest(transform.position, stats.AttackRange);
            if (target == null)
            {
                return;
            }

            nextFireTime = Time.time + 1f / stats.AttacksPerSecond;
            FireAt(target.transform.position);
        }

        private void FireAt(Vector3 targetPosition)
        {
            Vector3 origin = muzzle.position;
            Vector3 baseDirection = targetPosition - origin;
            baseDirection.y = 0f;
            baseDirection.Normalize();

            int shots = Mathf.Max(1, stats.ProjectileCount);
            float damage = stats.Damage;

            for (int i = 0; i < shots; i++)
            {
                float offset = shots <= 1 ? 0f : (i - (shots - 1) * 0.5f) * spreadAnglePerExtraShot;
                Vector3 direction = Quaternion.Euler(0f, offset, 0f) * baseDirection;

                PlayerProjectile projectile = inactive.Count > 0 ? inactive.Dequeue() : CreateProjectile();
                projectile.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(direction, Vector3.up));
                projectile.gameObject.SetActive(true);
                projectile.Launch(direction, projectileSpeed, damage, projectileLifetime, pierce: 0);
            }
        }

        private PlayerProjectile CreateProjectile()
        {
            PlayerProjectile projectile = Instantiate(projectilePrefab, poolRoot);
            projectile.OnFinished -= Recycle;
            projectile.OnFinished += Recycle;
            projectile.gameObject.SetActive(false);
            return projectile;
        }

        private void Recycle(PlayerProjectile projectile)
        {
            projectile.gameObject.SetActive(false);
            inactive.Enqueue(projectile);
        }
    }
}
