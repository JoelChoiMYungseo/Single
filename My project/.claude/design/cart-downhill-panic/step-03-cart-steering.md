# Step 03: StickSteering — 막대로 바닥을 찍어 조향

- **영역:** `cart` (`Assets/Scripts/Cart/`)
- **선행 단계:** step-01(`DownhillCart.ApplyLateralForce`/`ControlEnabled`) 완료 필요
- **후행 단계:** step-05(SceneBuilder)가 플레이어 손에 붙이고 `cart`/`stickTip` 참조를 주입

---

## 목적
플레이어가 쥔 막대(스틱)의 끝(tip)이 바닥(경사면)에 닿아 있는 동안, 그 tip이 카트 기준 좌우로 움직인 만큼(찍어서 미는 방향의 반대)을 카트에 측면 힘으로 전달한다.

---

## 에이전트 실행 지침

`/task-start`를 먼저 호출해 범위를 확정한 뒤, 아래 지시를 수행한다.

### 생성 파일
- `Assets/Scripts/Cart/StickSteering.cs` — 신규

### 핵심 심볼
```csharp
namespace VRGame.Cart
{
    public class StickSteering : MonoBehaviour
    {
        [SerializeField] private DownhillCart cart;
        [SerializeField] private Transform stickTip;    // 막대 끝 — 그립한 손의 자식
        [SerializeField] private float tipRadius = 0.05f;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float steerForceMultiplier = 15f;
        [SerializeField] private float maxSteerForce = 40f;
    }
}
```

### 동작 규칙
- `FixedUpdate()`에서:
  1. `cart.ControlEnabled`가 false면 그냥 리턴 (방출 후엔 조향 불가).
  2. `Physics.CheckSphere(stickTip.position, tipRadius, groundMask)`로 이번 스텝에 바닥에 닿아 있는지 확인.
  3. 닿아 있으면: 이전 스텝의 `stickTip.position` 대비 이번 스텝의 위치 차이를 `Time.fixedDeltaTime`으로 나눠 속도를 구하고, 그중 `cart.transform.right` 성분만 뽑는다.
  4. 그 성분에 `-steerForceMultiplier`를 곱해(찍어서 미는 반대 방향으로 카트가 밀려나야 하므로) `Mathf.Clamp`로 `maxSteerForce` 안으로 제한한 뒤 `cart.ApplyLateralForce(카트의 right축 * 그 값)`을 호출한다.
  5. 안 닿아 있으면: 아무 힘도 가하지 않고, 다음 프레임 위치 차분이 왜곡되지 않도록 "이전 위치" 캐시만 갱신한다.
- 이전 위치 캐시는 필드에 보관 (`private Vector3 lastTipPosition;`), `OnEnable()`에서 `stickTip.position`으로 초기화 — 안 그러면 비활성 상태였다가 켜졌을 때 첫 프레임에 거짓 속도가 생긴다([ObservedLocomotionDriver.OnEnable](../../../Assets/Scripts/Core/ObservedLocomotionDriver.cs#L35)이 동일한 이유로 이렇게 하는 것과 같은 패턴).

### 선행 산출물 의존성
- `DownhillCart.ApplyLateralForce(Vector3)` / `ControlEnabled` — step-01에서 정의됨

### 완료 판정
- [ ] `Grep "class StickSteering" Assets/Scripts/` 로 정의 확인
- [ ] `cart.ControlEnabled` 체크가 있는지 확인 (방출 후 조향 계속되는 버그 방지)
- [ ] 물리 힘 적용이 `FixedUpdate()` 안에 있는지 확인 (RULE-03)
- [ ] Unity 컴파일 통과

---

## 금지 사항
- `DownhillCart.cs`를 수정하지 않는다 — 이미 노출된 API만 호출한다.
- 방출(bailout) 로직을 이 파일에 넣지 않는다 — step-04의 범위다.
