using UnityEngine;

namespace VRGame.Survivor
{
    /// <summary>탕탕특공대식 잡몹의 "움직이는 쪽". 수십~수백 마리가 동시에 존재하므로 NavMeshAgent도
    /// Rigidbody도 쓰지 않고 Transform으로 플레이어를 향해 직진한다. 평지 아레나에서는 경로 탐색이
    /// 필요 없고, 서로 겹쳐 몰려드는 모습이 오히려 이 장르의 압박감을 만든다.
    /// 체력·피격은 EnemyTarget이 담당한다.</summary>
    [RequireComponent(typeof(EnemyTarget))]
    public class SwarmEnemy : MonoBehaviour
    {
        [Header("Contact Damage")]
        [SerializeField] private float contactRange = 1.1f;
        [SerializeField] private float contactInterval = 0.8f;

        private EnemyTarget enemyTarget;
        private Transform target;
        private PlayerHealth targetHealth;
        private float moveSpeed;
        private float contactDamage;
        private float nextContactTime;

        public EnemyTarget Target => enemyTarget;

        private void Awake()
        {
            enemyTarget = GetComponent<EnemyTarget>();
        }

        /// <summary>풀에서 꺼내 활성화한 직후 호출. 시간이 지날수록 스포너가 더 높은 수치를 넘긴다.</summary>
        public void ResetForSpawn(Vector3 position, Transform playerTarget, PlayerHealth playerHealth, float health, float speed, float damage)
        {
            transform.position = position;
            target = playerTarget;
            targetHealth = playerHealth;
            moveSpeed = speed;
            contactDamage = damage;
            nextContactTime = 0f;
            enemyTarget.ResetHealth(health);
        }

        private void Update()
        {
            if (enemyTarget.IsDead || target == null)
            {
                return;
            }

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            if (distance > contactRange)
            {
                Vector3 direction = toTarget / distance;
                transform.position += direction * moveSpeed * Time.deltaTime;
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
                return;
            }

            TryContactDamage();
        }

        private void TryContactDamage()
        {
            if (Time.time < nextContactTime || targetHealth == null)
            {
                return;
            }

            nextContactTime = Time.time + contactInterval;
            targetHealth.TakeDamage(contactDamage);
        }
    }
}
