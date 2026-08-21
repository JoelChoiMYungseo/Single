using System;
using UnityEngine;

namespace VRGame.Samurai
{
    /// <summary>공중을 떠다니며 플레이어에게 달려드는 적. 체력 개념 없이 "제대로 휘두르면 한 번에
    /// 갈린다" — 대신 몸통박치기로 플레이어를 크게 밀어내서, 데미지가 아니라 추락 위험으로 위협한다.
    /// 수직 레벨에서는 밀려나는 것 자체가 가장 무서운 공격이다.</summary>
    public class SliceableEnemy : MonoBehaviour, ISliceable
    {
        [Header("Movement")]
        [SerializeField] private float baseMoveSpeed = 3.2f;
        [SerializeField] private float turnSpeed = 6f;
        [Tooltip("플레이어 눈높이보다 이만큼 위를 노리고 접근한다 — 칼이 닿는 높이로 붙게 만든다.")]
        [SerializeField] private float approachHeightOffset = 0.4f;

        [Header("Body Slam")]
        [SerializeField] private float contactRange = 1.4f;
        [SerializeField] private float knockbackForce = 11f;
        [SerializeField] private float knockbackLift = 4f;
        [SerializeField] private float contactCooldown = 1.6f;

        [Header("Slice Reward")]
        [SerializeField] private int scoreValue = 120;

        [Header("Pieces")]
        [SerializeField] private GameObject piecePrefab;
        [SerializeField] private float pieceSpread = 3.5f;
        [SerializeField] private float pieceLifetime = 6f;

        /// <summary>베여서 처치됨 — 스포너가 풀로 회수한다.</summary>
        public event Action<SliceableEnemy> OnDefeated;

        private Transform target;
        private Rigidbody targetBody;
        private float moveSpeed;
        private float nextContactTime;
        private bool isDead;

        public bool CanBeSliced => !isDead;
        public int ScoreValue => scoreValue;

        /// <summary>풀에서 꺼내 활성화한 직후 호출. 구역이 올라갈수록 스포너가 더 빠른 속도를 넘긴다.</summary>
        public void ResetForSpawn(Vector3 position, Transform playerTarget, Rigidbody playerBody, float speedMultiplier)
        {
            transform.position = position;
            target = playerTarget;
            targetBody = playerBody;
            moveSpeed = baseMoveSpeed * speedMultiplier;
            nextContactTime = 0f;
            isDead = false;
        }

        private void Update()
        {
            if (isDead || target == null)
            {
                return;
            }

            Vector3 aim = target.position + Vector3.up * approachHeightOffset;
            Vector3 toTarget = aim - transform.position;
            float distance = toTarget.magnitude;

            if (distance < 0.001f)
            {
                return;
            }

            Vector3 direction = toTarget / distance;

            if (distance > contactRange)
            {
                transform.position += direction * moveSpeed * Time.deltaTime;
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(direction, Vector3.up), turnSpeed * Time.deltaTime);
                return;
            }

            TryBodySlam(direction);
        }

        /// <summary>플레이어를 밀어내는 것은 물리 조작이지만, 대상은 이 오브젝트가 아니라 플레이어의
        /// Rigidbody다. 접촉 판정 자체는 거리 비교라 Update에서 해도 무해하고, 힘은 즉발 임펄스
        /// 한 번으로 끝나므로 프레임레이트에 따라 누적되지 않는다.</summary>
        private void TryBodySlam(Vector3 direction)
        {
            if (Time.time < nextContactTime || targetBody == null)
            {
                return;
            }

            nextContactTime = Time.time + contactCooldown;

            Vector3 push = direction * knockbackForce + Vector3.up * knockbackLift;
            targetBody.AddForce(push, ForceMode.VelocityChange);
        }

        public void Slice(Vector3 point, Vector3 planeNormal, float sliceSpeed)
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            SpawnHalves(point, planeNormal, sliceSpeed);
            OnDefeated?.Invoke(this);
        }

        /// <summary>절단면을 기준으로 양쪽으로 두 조각을 날린다. 반대 방향으로 튀어야 "갈렸다"고 읽힌다.</summary>
        private void SpawnHalves(Vector3 point, Vector3 planeNormal, float sliceSpeed)
        {
            if (piecePrefab == null)
            {
                return;
            }

            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 offset = planeNormal * (0.18f * side);
                GameObject piece = Instantiate(piecePrefab, point + offset, transform.rotation);
                piece.transform.localScale = Vector3.one * 0.45f;

                var pieceBody = piece.GetComponent<Rigidbody>();
                if (pieceBody == null)
                {
                    pieceBody = piece.AddComponent<Rigidbody>();
                }

                pieceBody.AddForce(planeNormal * side * (pieceSpread + sliceSpeed * 0.2f), ForceMode.VelocityChange);
                pieceBody.AddTorque(UnityEngine.Random.insideUnitSphere * 6f, ForceMode.VelocityChange);

                Destroy(piece, pieceLifetime);
            }
        }
    }
}
