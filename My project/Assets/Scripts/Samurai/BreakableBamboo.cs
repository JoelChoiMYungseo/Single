using UnityEngine;

namespace VRGame.Samurai
{
    /// <summary>베이는 대나무. 한 번 베고 끝나는 게 아니라 벤 높이만큼 그루터기가 남고 윗동강이
    /// 물리적으로 쓰러진다 — 같은 대나무를 여러 번 토막 낼 수 있어야 "썰었다"는 쾌감이 산다.</summary>
    public class BreakableBamboo : MonoBehaviour, ISliceable
    {
        [Header("Shape")]
        [Tooltip("루트 원점(밑동)에서 위로 뻗는 전체 높이.")]
        [SerializeField] private float height = 6f;
        [SerializeField] private float radius = 0.14f;
        [SerializeField] private Transform stalk;

        [Header("Slicing")]
        [Tooltip("이보다 짧게 남는 그루터기는 만들지 않는다 — 밑동을 베면 통째로 쓰러진다.")]
        [SerializeField] private float minStumpHeight = 0.35f;
        [Tooltip("잘려나갈 조각이 이보다 짧으면 자르지 않는다 (끝동만 스치는 경우).")]
        [SerializeField] private float minPieceHeight = 0.25f;

        [Header("Slice Reward")]
        [SerializeField] private int scoreValue = 20;

        [Header("Flying Piece")]
        [SerializeField] private GameObject piecePrefab;
        [Tooltip("절단 속력이 조각을 밀어내는 세기로 환산되는 비율.")]
        [SerializeField] private float knockbackMultiplier = 0.3f;
        [SerializeField] private float spinTorque = 4f;
        [SerializeField] private float pieceLifetime = 8f;

        private CapsuleCollider bodyCollider;
        private float currentHeight;

        public bool CanBeSliced => currentHeight > minStumpHeight + minPieceHeight;
        public int ScoreValue => scoreValue;

        private void Awake()
        {
            // 인스펙터 세팅에 의존하지 않고 코드로 확정한다.
            bodyCollider = GetComponent<CapsuleCollider>();
            if (bodyCollider == null)
            {
                bodyCollider = gameObject.AddComponent<CapsuleCollider>();
            }
            bodyCollider.direction = 1; // Y축
            bodyCollider.isTrigger = false;

            currentHeight = height;
            ApplyHeight();
        }

        public void Slice(Vector3 point, Vector3 planeNormal, float sliceSpeed)
        {
            if (!CanBeSliced)
            {
                return;
            }

            float cutHeight = Mathf.Clamp(point.y - transform.position.y, minStumpHeight, currentHeight - minPieceHeight);
            float pieceHeight = currentHeight - cutHeight;

            SpawnFlyingPiece(cutHeight, pieceHeight, planeNormal, sliceSpeed);

            currentHeight = cutHeight;
            ApplyHeight();
        }

        /// <summary>잘려나간 윗동강을 물리 오브젝트로 띄운다. 절단 평면 방향으로 밀고 회전을 줘서
        /// 툭 떨어지는 대신 팽그르르 날아가게 만든다.</summary>
        private void SpawnFlyingPiece(float cutHeight, float pieceHeight, Vector3 planeNormal, float sliceSpeed)
        {
            if (piecePrefab == null)
            {
                return;
            }

            Vector3 pieceCenter = transform.position + Vector3.up * (cutHeight + pieceHeight * 0.5f);
            GameObject piece = Instantiate(piecePrefab, pieceCenter, transform.rotation);

            // 프리팹 원기둥은 높이 2 기준이므로 절반으로 나눠 스케일을 맞춘다.
            piece.transform.localScale = new Vector3(radius * 2f, pieceHeight * 0.5f, radius * 2f);

            var pieceBody = piece.GetComponent<Rigidbody>();
            if (pieceBody == null)
            {
                pieceBody = piece.AddComponent<Rigidbody>();
            }
            pieceBody.isKinematic = false;
            pieceBody.useGravity = true;

            pieceBody.AddForce(planeNormal * sliceSpeed * knockbackMultiplier, ForceMode.VelocityChange);
            pieceBody.AddTorque(Random.insideUnitSphere * spinTorque, ForceMode.VelocityChange);

            Destroy(piece, pieceLifetime);
        }

        /// <summary>남은 높이에 맞춰 시각 메쉬와 콜라이더를 함께 줄인다.</summary>
        private void ApplyHeight()
        {
            bodyCollider.height = currentHeight;
            bodyCollider.radius = radius;
            bodyCollider.center = new Vector3(0f, currentHeight * 0.5f, 0f);

            if (stalk != null)
            {
                stalk.localScale = new Vector3(radius * 2f, currentHeight * 0.5f, radius * 2f);
                stalk.localPosition = new Vector3(0f, currentHeight * 0.5f, 0f);
            }
        }
    }
}
