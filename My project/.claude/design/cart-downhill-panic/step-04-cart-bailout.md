# Step 04: CartBailout — 고속 충돌 감지 및 방출

- **영역:** `cart` (`Assets/Scripts/Cart/`)
- **선행 단계:** step-01(`DownhillCart.SetControlEnabled`/`Body`) 완료 필요 (step-03과는 무관 — **병렬 실행 가능**)
- **후행 단계:** step-05(SceneBuilder)가 카트↔플레이어를 묶는 `Joint`를 만들어 `seatJoint`/`playerBody` 참조를 주입한다

---

## 목적
카트가 고속으로 정면 충돌하면, 플레이어를 카트에 묶어두던 `Joint`를 파괴하고(README 아키텍처 결정 참고 — 원 프롬프트의 "Unparent" 대신 Joint 파괴 방식을 쓴다) `DownhillCart.SetControlEnabled(false)`로 조향/추진을 멈춘 뒤, 플레이어 자신의 `Rigidbody`에 위/앞 방향 임펄스를 줘서 슬랩스틱하게 날아가게 만든다.

**아키텍트 확인 필요:** 진짜 스켈레톤 래그돌(팔다리가 따로 나뒹구는 것)은 이번 스코프에 없다(README 참고) — "리그 전체가 한 덩어리로 날아간다"까지만 구현한다.

---

## 에이전트 실행 지침

`/task-start`를 먼저 호출해 범위를 확정한 뒤, 아래 지시를 수행한다.

### 생성 파일
- `Assets/Scripts/Cart/CartBailout.cs` — 신규

### 핵심 심볼
```csharp
namespace VRGame.Cart
{
    [RequireComponent(typeof(DownhillCart))]
    public class CartBailout : MonoBehaviour
    {
        [Header("Crash Detection")]
        [SerializeField] private float crashSpeedThreshold = 8f;   // 이 상대속도(m/s) 이상이어야 "사고"로 인정
        [SerializeField] private float frontalDotThreshold = 0.4f; // 카트 forward와 -충돌법선의 내적이 이 이상이어야 "정면"

        [Header("Eject")]
        [SerializeField] private float ejectForce = 12f;           // 인스펙터 노출 요구사항 — VelocityChange 크기
        [SerializeField] private Vector3 ejectDirectionLocal = new Vector3(0f, 0.6f, 0.4f); // 카트 로컬 기준 위+앞

        [Header("Seat")]
        [SerializeField] private Joint seatJoint;     // 카트↔플레이어를 묶는 조인트 — SceneBuilder가 생성해 연결
        [SerializeField] private Rigidbody playerBody; // AutoHandPlayer의 Rigidbody

        public bool HasEjected { get; private set; }
        // 이펙트/사운드 등 후속 연출용 훅 — 이번 스코프엔 구독자 없음(원 프롬프트에 "만화경 별" 등
        // 방출 이펙트 아이디어가 있었지만 Task 섹션의 3개 스크립트 요청엔 없어 훅만 열어둔다).
        public event System.Action OnEjected;
    }
}
```

### 동작 규칙
- `Awake()`에서 카트 자신의 `DownhillCart`를 `GetComponent`로 캐싱.
- `OnCollisionEnter(Collision collision)`에서:
  1. `HasEjected`가 이미 true면 무시(중복 방출 방지).
  2. `float impactSpeed = collision.relativeVelocity.magnitude;`가 `crashSpeedThreshold` 미만이면 무시 — [CrashHandler.OnCollisionEnter](../../../Assets/Scripts/Magnetic/CrashHandler.cs#L58)와 동일한 이유로 `relativeVelocity`를 쓴다(충돌 직후 velocity는 이미 솔버가 반사·마찰을 적용한 값이라 왜곡됨).
  3. `Vector3.Dot(transform.forward, -collision.GetContact(0).normal)`이 `frontalDotThreshold` 미만이면(옆·뒤 충돌) 무시.
  4. 조건 통과 시: `HasEjected = true`, `GetComponent<DownhillCart>().SetControlEnabled(false)`, `Destroy(seatJoint)`, `playerBody.AddForce(transform.TransformDirection(ejectDirectionLocal).normalized * ejectForce, ForceMode.VelocityChange)`, `OnEjected?.Invoke()`.
  5. 이 즉발성 처리는 `FixedUpdate()` 밖(`OnCollisionEnter`)에서 해도 된다 — [CrashHandler.cs의 동일 판단 근거](../../../Assets/Scripts/Magnetic/CrashHandler.cs#L55) 그대로: RULE-03은 매 프레임 누적되는 AddForce/MovePosition을 겨냥한 규칙이고, 1회성 `ForceMode.VelocityChange`는 여기 해당하지 않는다.

### 선행 산출물 의존성
- `DownhillCart.SetControlEnabled(bool)` — step-01에서 정의됨

### 완료 판정
- [ ] `Grep "class CartBailout" Assets/Scripts/` 로 정의 확인
- [ ] `relativeVelocity` 기반 충돌 속도 판정 확인 (`velocity` 아님 — CrashHandler와 동일 이유)
- [ ] 중복 방출 방지(`HasEjected` 가드) 확인
- [ ] `ejectForce`가 `[SerializeField]`로 인스펙터에 노출되는지 확인
- [ ] Unity 컴파일 통과

---

## 금지 사항
- `PlayerRig`/`ObservedLocomotionDriver` 등 Core 영역 파일을 수정하지 않는다 — README 아키텍처 결정에 따라 이번 방출 로직은 Joint 파괴 + 직접 임펄스만으로 충분하고, 새 로코모션 드라이버가 필요 없다.
- 팔다리 개별 래그돌 물리를 시도하지 않는다 — 스코프 밖(README 참고).
