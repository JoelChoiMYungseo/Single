using System;
using UnityEngine;

namespace VRGame.Samurai
{
    /// <summary>아래에서 계속 올라오는 위협(안개/조수). 추락하지 않아도 가만히 있으면 잡히므로
    /// 플레이어를 끊임없이 위로 밀어낸다 — 이 프로토타입의 "긴박감"을 만드는 핵심 장치다.
    ///
    /// 잡히면 체크포인트로 되돌리고 위협을 한참 아래로 되돌려 숨 돌릴 틈을 준다. 그러지 않으면
    /// 리스폰 직후 다시 잡혀서 빠져나올 수 없는 죽음의 고리가 생긴다.</summary>
    public class RisingHazard : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform player;
        [SerializeField] private BambooExploration exploration;
        [SerializeField] private AscentZoneManager zones;
        [Tooltip("올라오는 위협을 눈으로 보여줄 오브젝트 (평면/박스).")]
        [SerializeField] private Transform hazardVisual;

        [Header("Tuning")]
        [SerializeField] private float startY = -8f;
        [Tooltip("플레이어 발밑이 이 거리 안으로 들어오면 잡힌 것으로 본다.")]
        [SerializeField] private float catchMargin = 0.6f;
        [Tooltip("잡힌 뒤 체크포인트보다 이만큼 아래에서 다시 올라온다.")]
        [SerializeField] private float resetDepthBelowCheckpoint = 16f;

        /// <summary>플레이어와 위협 표면의 높이 차 — HUD가 경고를 띄우는 데 쓴다.</summary>
        public float Gap { get; private set; }
        public float SurfaceY { get; private set; }

        /// <summary>위협에 잡혀 되돌아간 순간.</summary>
        public event Action OnCaught;

        private void Start()
        {
            SurfaceY = startY;
            ApplyVisual();
        }

        private void Update()
        {
            float riseSpeed = zones != null ? zones.CurrentZone.HazardRiseSpeed : 0.3f;
            SurfaceY += riseSpeed * Time.deltaTime;

            Gap = player.position.y - SurfaceY;
            ApplyVisual();

            if (Gap <= catchMargin)
            {
                Catch();
            }
        }

        private void Catch()
        {
            exploration.Respawn();
            SurfaceY = exploration.CurrentCheckpoint.y - resetDepthBelowCheckpoint;
            ApplyVisual();
            OnCaught?.Invoke();
        }

        /// <summary>런을 처음부터 다시 시작할 때 위협도 되돌린다.</summary>
        public void ResetHazard()
        {
            SurfaceY = startY;
            ApplyVisual();
        }

        private void ApplyVisual()
        {
            if (hazardVisual == null)
            {
                return;
            }

            Vector3 position = hazardVisual.position;
            position.y = SurfaceY;
            hazardVisual.position = position;
        }
    }
}
