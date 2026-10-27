# Step 01: DownhillCart — 카트 물리 컨트롤러

- **영역:** `cart` (`Assets/Scripts/Cart/`, 신규 폴더)
- **선행 단계:** 없음 (step-02와 병렬 실행 가능)
- **후행 단계:** step-03(StickSteering), step-04(CartBailout), step-05(SceneBuilder)가 이 단계의 public API를 사용

---

## 목적
카트 자신의 `Rigidbody`가 물리 주체다. 네 바퀴 위치에서 아래로 SphereCast를 쏴서 지면(경사)을 감지하고, 닿아 있으면 스프링-댐퍼 힘으로 띄우면서(서스펜션) 지면 법선 방향으로 정렬시킨다 — WheelCollider 없이 "구르는 느낌"을 흉내내는 표준 아케이드 비히클 근사다. 실제 전진 가속은 중력 + 경사면 마찰로 자연스럽게 생기게 하고(장난감 카트가 언덕을 굴러 내려가는 것과 동일한 물리), `topSpeed`는 초과 시 속도 벡터를 클램프해서 지킨다. `wheelFriction`은 측면(옆으로 미끄러지는) 속도 성분을 얼마나 강하게 상쇄하는지에 쓴다 — 마찰이 낮을수록 옆으로 더 미끄러진다.

---

## 에이전트 실행 지침

`/task-start`를 먼저 호출해 범위를 확정한 뒤, 아래 지시를 수행한다.

### 생성 파일
- `Assets/Scripts/Cart/DownhillCart.cs` — 신규

### 핵심 심볼
```csharp
namespace VRGame.Cart
{
    [RequireComponent(typeof(Rigidbody))]
    public class DownhillCart : MonoBehaviour
    {
        [Header("Wheels (SphereCast 근사)")]
        [SerializeField] private Transform[] wheelPoints; // 4개 — 바퀴 위치(카트 로컬 하단 네 모서리)
        [SerializeField] private float wheelRadius = 0.35f;
        [SerializeField] private float suspensionTravel = 0.25f;
        [SerializeField] private float suspensionStiffness = 900f;
        [SerializeField] private float suspensionDamping = 60f;
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Rolling")]
        [SerializeField] private float wheelFriction = 1.5f; // 인스펙터 노출 요구사항 — 측면 속도 감쇠 계수
        [SerializeField] private float topSpeed = 25f;        // 인스펙터 노출 요구사항 — 초과 시 velocity 클램프

        public Rigidbody Body { get; private set; }
        public bool ControlEnabled { get; private set; } = true;

        // StickSteering이 매 FixedUpdate 호출한다. ControlEnabled == false면 아무 효과 없음(내부에서 무시).
        public void ApplyLateralForce(Vector3 lateralForce);

        // CartBailout이 충돌 처리 시작 시 호출해 추진/조향 계산을 끈다.
        // DownhillCart는 StickSteering을 직접 참조하지 않는다 — StickSteering이 매 프레임
        // 이 프로퍼티(ControlEnabled)를 스스로 확인해서 멈춘다.
        public void SetControlEnabled(bool value);
    }
}
```

### 동작 규칙
- `Awake()`에서 `Body = GetComponent<Rigidbody>()` 캐싱 ([컴포넌트 캐싱 원칙](../../rules/scripts.md)).
- `wheelPoints` 4개 각각에 대해 `FixedUpdate()`에서 아래로 `Physics.SphereCast(point.position, wheelRadius, Vector3.down, out hit, suspensionTravel, groundMask)`:
  - 히트하면: 압축량 = `suspensionTravel - hit.distance`, 스프링 힘 = `압축량 * suspensionStiffness - Vector3.Dot(Body.linearVelocity, hit.normal) * suspensionDamping`, `Body.AddForceAtPosition(hit.normal * 스프링힘, point.position, ForceMode.Force)`.
  - 안 히트하면: 해당 바퀴는 공중 — 이번 스텝은 그 바퀴의 서스펜션 힘 생략(자유낙하 성분 유지).
- **측면 마찰**: 카트의 `transform.right` 방향 속도 성분을 구해 `-lateral * wheelFriction`을 힘으로 가한다(옆미끄러짐 상쇄) — 이게 "바퀴가 옆으로는 안 미끄러지고 앞으로만 구른다"는 아케이드 근사의 핵심.
- **속도 상한**: `Body.linearVelocity.magnitude > topSpeed`면 `Body.linearVelocity = Body.linearVelocity.normalized * topSpeed`로 클램프 (FixedUpdate 안에서).
- `ApplyLateralForce`/서스펜션/마찰/속도클램프 전부 **`FixedUpdate()` 안에서만** 수행한다 (RULE-03).
- `ControlEnabled == false`인 동안엔 서스펜션(지면 지지)은 계속 작동하되(그래야 카트가 그냥 붕 뜨지 않음), `ApplyLateralForce` 호출은 무시한다 — 방출 이후 빈 카트가 자연스럽게 계속 굴러가거나 멈추게.

### 선행 산출물 의존성
- 없음 (첫 단계)

### 완료 판정
- [ ] `Grep "class DownhillCart" Assets/Scripts/` 로 정의 확인
- [ ] `ApplyLateralForce(Vector3)`, `SetControlEnabled(bool)` public 메서드, `Body`/`ControlEnabled` public 프로퍼티 확인
- [ ] `wheelFriction`, `topSpeed`가 `[SerializeField]`로 인스펙터에 노출되는지 확인
- [ ] 서스펜션/마찰/클램프 로직이 `FixedUpdate()` 안에 있는지 확인 (RULE-03)
- [ ] Unity 컴파일 통과

---

## 금지 사항
- `StickSteering`/`CartBailout`/씬 빌더 파일을 이 단계에서 만들지 않는다.
- `WheelCollider`를 쓰지 않는다 — README의 아키텍처 결정에 따라 SphereCast 근사로 고정.
