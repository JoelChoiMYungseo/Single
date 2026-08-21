using System;
using UnityEngine;

namespace VRGame.Samurai
{
    /// <summary>수직 대나무 숲의 진행 관리. 떨어지면 마지막으로 안전하게 발 붙였던 지점으로 되돌린다.
    ///
    /// 체크포인트를 레벨에 일일이 배치하지 않고, 플레이어가 실제로 "쉬었던" 위치를 자동으로 기록한다 —
    /// 지형이 절차적으로 생성돼도 그대로 동작하고, 어디까지 올라갔는지가 곧 진행도가 된다.
    /// 다만 너무 후하게 되돌려주면 추락의 공포가 사라지므로, 체크포인트는 일정 높이를 새로 갱신했을
    /// 때만 올라간다.</summary>
    public class BambooExploration : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SwordGorillaLocomotion player;
        [SerializeField] private Transform startPoint;

        [Header("Fall")]
        [Tooltip("이 높이 아래로 떨어지면 리스폰한다.")]
        [SerializeField] private float fallThresholdY = -12f;
        [Tooltip("리스폰 지점 위로 이만큼 띄워서 되돌린다 — 지형에 끼는 것을 막는다.")]
        [SerializeField] private float respawnLift = 0.6f;

        [Header("Checkpoint")]
        [Tooltip("직전 체크포인트보다 이만큼 더 높이 올라가야 새 체크포인트로 갱신된다.")]
        [SerializeField] private float checkpointHeightStep = 6f;
        [Tooltip("발밑 판정 거리. 이 안에 지형이 있어야 '착지했다'고 본다.")]
        [SerializeField] private float groundCheckDistance = 1.4f;
        [SerializeField] private float groundCheckRadius = 0.3f;
        [Tooltip("이 속력보다 느려야 '안정적으로 서 있다'고 판정한다.")]
        [SerializeField] private float restingSpeedThreshold = 1.5f;
        [SerializeField] private LayerMask groundMask = ~0;

        /// <summary>리스폰된 지점</summary>
        public event Action<Vector3> OnRespawned;
        /// <summary>새 체크포인트 위치</summary>
        public event Action<Vector3> OnCheckpointReached;

        public int FallCount { get; private set; }
        public float HighestY { get; private set; }
        public Vector3 CurrentCheckpoint { get; private set; }

        private Rigidbody playerBody;

        private void Awake()
        {
            playerBody = player.GetComponent<Rigidbody>();
            CurrentCheckpoint = startPoint != null ? startPoint.position : player.transform.position;
            HighestY = CurrentCheckpoint.y;
        }

        private void Update()
        {
            if (player.transform.position.y < fallThresholdY)
            {
                Respawn();
                return;
            }

            TryPromoteCheckpoint();
        }

        /// <summary>충분히 높이 올라가서 발을 붙이고 멈춘 순간에만 체크포인트를 옮긴다.</summary>
        private void TryPromoteCheckpoint()
        {
            Vector3 position = player.transform.position;

            if (position.y > HighestY)
            {
                HighestY = position.y;
            }

            if (position.y < CurrentCheckpoint.y + checkpointHeightStep)
            {
                return;
            }

            if (playerBody.linearVelocity.magnitude > restingSpeedThreshold)
            {
                return;
            }

            if (!Physics.SphereCast(position + Vector3.up * groundCheckRadius, groundCheckRadius, Vector3.down,
                    out _, groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            CurrentCheckpoint = position;
            OnCheckpointReached?.Invoke(position);
        }

        public void Respawn()
        {
            FallCount++;
            Vector3 target = CurrentCheckpoint + Vector3.up * respawnLift;
            player.Teleport(target);
            OnRespawned?.Invoke(target);
        }

        /// <summary>런을 처음부터 다시 시작한다.</summary>
        public void ResetRun()
        {
            FallCount = 0;
            CurrentCheckpoint = startPoint != null ? startPoint.position : Vector3.zero;
            HighestY = CurrentCheckpoint.y;
            player.Teleport(CurrentCheckpoint + Vector3.up * respawnLift);
        }
    }
}
