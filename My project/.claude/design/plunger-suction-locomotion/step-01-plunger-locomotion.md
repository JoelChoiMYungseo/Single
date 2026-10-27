# Step 01: PlungerLocomotion — 흡착·텐션·발사 상태머신

- **영역:** `plunger` (`Assets/Scripts/Plunger/`, 신규 폴더)
- **선행 단계:** 없음
- **후행 단계:** step-03(MonsterFaceStick), step-04(PlungerFeedback), step-05(SceneBuilder)에서 이 단계의 이벤트/공개 API를 사용

---

## 목적
양손 각각이 독립적으로 Idle → Stuck → Compressed → Launched 상태를 오간다. Trigger를 누른 채 `stickableMask` 레이어를 레이캐스트해 맞으면 그 지점에 "고정"되고(플런저 헤드는 그 월드 좌표에 박힌 것처럼 취급), 플레이어가 몸을 뒤로 빼면(= 리그와 고정점 사이 거리가 벌어지면) 그 거리로 텐션을 계산해 몸을 고정점 쪽으로 당기는 스프링 힘을 건다(F = -stiffness × 늘어난 거리, [MagneticLocomotion.ApplyMagneticForce](../../../Assets/Scripts/Magnetic/MagneticLocomotion.cs)의 힘 계산부와 동일한 자리에 들어가는 로직). Trigger를 놓는 순간 그 시점의 텐션 벡터 반대 방향으로 1회성 임펄스를 몸에 걸고 Idle로 돌아간다(재사용 가능한 상태로).

Magnetic과의 핵심 차이: Magnetic은 트리거를 쥐고 있는 동안 계속 당기는 "지속력"이고, Plunger는 트리거를 쥐고 있는 동안은 "몸이 고정점에서 너무 멀어지지 않게 잡아주는 스프링"이며 실제 추진력은 **놓는 순간 1회**만 발생한다.

---

## 에이전트 실행 지침

`/task-start`를 먼저 호출해 범위를 확정한 뒤, 아래 지시를 수행한다.

### 생성 파일
- `Assets/Scripts/Plunger/PlungerLocomotion.cs` — 신규
- `Assets/Scripts/Plunger.meta`, `Assets/Scripts/Plunger/PlungerLocomotion.cs.meta` — Unity가 자동 생성하므로 직접 만들지 않는다 (에디터가 임포트할 때 생성됨). 만약 스크립트만 텍스트로 만들고 Unity를 안 거친다면 `.meta` 누락 상태가 되니, 커밋 전 Unity 에디터를 한 번 열어 임포트되게 한다.

### 핵심 심볼
```csharp
namespace VRGame.Plunger
{
    public enum PlungerState { Idle, Stuck, Compressed, Launched }

    [RequireComponent(typeof(Rigidbody))]
    public class PlungerLocomotion : MonoBehaviour, ILocomotionDriver
    {
        // Inspector 노출 (프롬프트 요구사항): 스프링 강성, 최대 발사력, 흡착 거리
        [SerializeField] private Transform leftHandOrigin;
        [SerializeField] private Transform rightHandOrigin;
        [SerializeField] private LayerMask stickableMask;
        [SerializeField] private float maxStickDistance = 15f;   // 흡착 거리
        [SerializeField] private float springStiffness = 400f;   // 스프링 강성
        [SerializeField] private float maxLaunchForce = 30f;     // 최대 발사력 (임펄스 크기 상한)
        [SerializeField] private float maxPullDistance = 2.5f;   // 텐션 거리 클램프 — 무한정 늘어나 힘이 발산하지 않게

        public PlungerState LeftState { get; }
        public PlungerState RightState { get; }

        // step-04(PlungerFeedback)가 구독한다. isLeft: 어느 손인지. state: 전이된 새 상태.
        public event System.Action<bool, PlungerState> OnStateChanged;
        // step-03(MonsterFaceStick)이 구독한다. isLeft: 어느 손인지. anchor: 이번에 흡착된 대상의
        // Transform(레이캐스트 hit.collider.transform 그대로) — 벽이든 몬스터든 동일하게 넘긴다.
        // PlungerLocomotion은 이 Transform이 몬스터인지 벽인지 전혀 모른 채로 그냥 스프링 앵커로 쓴다
        // (MonsterFaceStick 타입을 몰라도 되게 하는 의도적 결합 축소 — ILocomotionDriver가 게임 로직
        // 구체 타입을 모르는 것과 같은 이유).
        public event System.Action<bool, Transform> OnStuck;
        // isLeft: 어느 손인지. impulse: 실제로 가해진 임펄스 벡터(방향+크기) — 햅틱 세기·오디오 타이밍에 쓴다.
        public event System.Action<bool, Vector3> OnLaunched;

        public Vector3 Velocity { get; }
        public void OnTeleported(Vector3 position);
    }
}
```

### 물리 규칙 (RULE-03 — 이번 세션 신규 발견/확인 사항 아님, 기존 규칙 재확인)
- 텐션 스프링 힘(`Rigidbody.AddForce`)은 **`FixedUpdate()`에서만** 적용한다. [MagneticLocomotion.FixedUpdate](../../../Assets/Scripts/Magnetic/MagneticLocomotion.cs#L111)와 동일한 자리.
- 놓는 순간의 1회성 임펄스도 `FixedUpdate()` 안에서, 트리거 해제를 감지한 그 스텝에 적용한다 (`ForceMode.VelocityChange` 또는 `ForceMode.Impulse` 중 하나 선택 — 질량 의존 여부에 따라 골라 스펙에 남긴다).

### XR 입력 참고
- Magnetic과 동일하게 `UnityEngine.XR.InputDevices.GetDeviceAtXRNode` + `TryGetFeatureValue(CommonUsages.triggerButton, ...)`로 트리거를 읽는다 ([MagneticLocomotion.ReadTrigger](../../../Assets/Scripts/Magnetic/MagneticLocomotion.cs#L206) 참조).
- 햅틱은 이 스크립트가 직접 보내지 않는다 — `OnStateChanged`/`OnLaunched` 이벤트로 **step-04(PlungerFeedback)에 위임**한다. (Magnetic은 로코모션이 직접 햅틱을 보내지만, 이번엔 프롬프트가 피드백을 별도 매니저로 분리 요청했으므로 그 경계를 지킨다.)

### 선행 산출물 의존성
- 없음 (첫 단계)

### 완료 판정
- [ ] `Grep "class PlungerLocomotion" Assets/Scripts/` 로 정의 확인
- [ ] `PlungerLocomotion : MonoBehaviour, ILocomotionDriver` 로 인터페이스 구현 확인 (`Grep "ILocomotionDriver" Assets/Scripts/Plunger/`)
- [ ] `OnStateChanged`, `OnStuck`, `OnLaunched` 이벤트가 public으로 노출되어 있는지 확인 (다음 단계들이 구독해야 함)
- [ ] `PlungerLocomotion.cs`가 `VRGame.Plunger` 밖의 어떤 타입도 (MonsterFaceStick 등) 직접 참조하지 않는지 확인 — 몬스터 여부는 이벤트 구독 쪽(step-03)이 판단한다
- [ ] springStiffness/maxLaunchForce/maxStickDistance가 `[SerializeField]`로 인스펙터에 노출되는지 확인
- [ ] Unity 컴파일 통과 (콘솔 에러 없음)

---

## 금지 사항
- 이 단계에서 몬스터/피드백/씬 빌더 파일을 만들지 않는다 — 각각 step-02~05의 범위다.
- 햅틱·오디오를 이 파일 안에서 직접 재생하지 않는다 — 이벤트로만 통보한다 (역할 분리가 프롬프트의 명시적 요구사항).
