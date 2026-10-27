# Shopping Cart Downhill Panic — 마트 카트 다운힐

## 한 줄 요약
브레이크 없는 마트 카트에 탄 채 무한 내리막을 질주하다가, 손에 든 막대로 바닥을 찍어 방향을 틀고, 장애물에 정면충돌하면 카트에서 튕겨 나가는 VR 프로토타입.

## 원문
사용자가 제공한 "Quick GDD" 프롬프트(요약): DownhillCart(물리 기반 카트 — WheelCollider/SphereCast로 구르기, 최고 속도·바퀴 마찰 인스펙터 노출), StickSteering(손에 든 막대가 바닥을 찍는 것을 감지해 카트에 좌우 힘을 가함), CartBailout(고속 정면 충돌 감지 → VR 리그를 카트에서 분리 → 위/앞 방향 임펄스로 방출). Quest 단독구동 최적화. 트렌드 분석·수익화(스킨) 부분은 이 설계 문서의 범위 밖(코드 요청이 아님 — Task 섹션의 3개 스크립트만 스코프로 삼는다).

## 아키텍처 결정
- **카트는 별도 물리 오브젝트, 플레이어 리그는 조인트로 묶는다.** 프롬프트는 "Unparent the VR Rig from the cart"라고 썼지만, 이 프로젝트의 `PlayerRig.Body`는 항상 AutoHand 리그 자신의 `Rigidbody`를 가리켜야 다른 시스템(속도 조회 등)이 흔들리지 않는다(기존 6개 게임 전부 이 규약). 그래서 **Transform 부모-자식이 아니라 `Rigidbody` + `Joint`(카트 Rigidbody ↔ 플레이어 Rigidbody)로 "탑승"을 구현**하고, 방출(bailout)은 "Unparent" 대신 **그 Joint를 파괴**하는 것으로 대체한다. 이러면 `PlayerRig`/`ObservedLocomotionDriver` 등 기존 공용 코드를 전혀 건드릴 필요가 없다 — Core 영역 변경 없음.
- **로코모션 드라이버가 따로 필요 없다.** 탑승 중엔 조인트가 카트의 움직임을 플레이어 Rigidbody에 그대로 전달하므로 `PlayerRig.Velocity`(= `body.linearVelocity`, 기존 코드 그대로)가 이미 정확하다. 방출 후에도 마찬가지로 플레이어 자신의 Rigidbody가 계속 물리 주체다. 그래서 이번 프로토타입은 **`ILocomotionDriver`를 새로 구현하지 않는다** — 다른 6개 게임과 달리 카트/조향/방출이 전부 "플레이어 자신의 Rigidbody에 힘을 주고 받는" 문제로 환원된다.
- **바퀴는 SphereCast 기반으로 만든다** (프롬프트가 WheelCollider/SphereCast 중 선택하라고 열어둠). WheelCollider는 바퀴 메시·서스펜션을 개별 튜닝해야 하고 이 프로젝트의 "전부 코드로 조립" 방식과 궁합이 덜 좋다 — SphereCast 4점 + 스프링-댐퍼 근사가 더 가볍고 Editor 스크립트로 완결하기 쉽다.
- **"무한 내리막"은 청크 재활용 스포너로 구현한다** (원 프롬프트가 "procedurally generated ramp"라고 명시했으나 3개 핵심 시스템 목록엔 없었음 — 플레이 가능한 빌드를 만들려면 반드시 필요해서 4번째 시스템으로 추가). [BulletPool](../../../Assets/Scripts/Danmaku/BulletPool.cs)류 오브젝트 재사용 관례를 참고하되, 매 프레임이 아니라 세그먼트 하나(수십 미터)당 1회 재배치라 풀링이라기보단 단순 재배치에 가깝다.
- **"1인칭 래그돌"은 스코프를 줄인다.** 이 프로젝트의 AutoHand 리그는 스켈레톤 래그돌이 아니다(손만 개별 물리 바디, 몸통은 리그 루트 Rigidbody 하나). 팔다리가 따로 나뒹구는 진짜 래그돌은 별도의 큰 설계가 필요하므로, **이번 스코프는 "리그 루트 Rigidbody 전체가 임펄스를 맞고 날아가는" 것까지만** 구현한다. 시청자 임팩트를 위한 카메라 흔들림·슬로모션 등 연출은 후속 설계로 남긴다. **아키텍트 확인 필요.**
- **AutoHand 리그 스폰은 `AutoHandRigFactory.Spawn(..., disableBuiltinMovement: true, ...)`를 그대로 쓴다** — 카트 조인트가 유일한 이동 수단인 동안에도 Auto Hand 기본 이동이 매 프레임 속도를 덮어쓰지 않게.
- **BM/수익화(카트·도구 스킨, 결제 금액)는 이 설계의 범위 밖.** 사용자 프롬프트의 Task 섹션이 요청한 건 코드 3종뿐이라 그것만 계획한다. 스킨 시스템이 필요해지면 별도 설계로 돌린다.

## 터치 영역
| 영역 | 경로 | 역할 |
|---|---|---|
| cart (신규) | `Assets/Scripts/Cart/` | 카트 물리, 조향, 방출, 무한 램프 스포너 |
| editor | `Assets/Scripts/Editor/` | 씬 빌더 (`Tools/Cart/Build Game`) |

## 의존성 그래프
```
Cart/DownhillCart (물리 + ApplyLateralForce + ControlEnabled)  ─┬→ Cart/StickSteering
                                                                  └→ Cart/CartBailout
Cart/DownhillRampSpawner (독립 — cart Transform만 추적)          ───(직접 호출 관계 없음, 배치만 관련)

Cart/DownhillCart, StickSteering, CartBailout, DownhillRampSpawner → Editor/CartSceneBuilder
```

## 단계
1. [step-01-cart-downhillcart.md](step-01-cart-downhillcart.md) — Cart: 카트 물리 컨트롤러 (`DownhillCart`)
2. [step-02-cart-rampspawner.md](step-02-cart-rampspawner.md) — Cart: 무한 램프 스포너 (`DownhillRampSpawner`) — step-01과 병렬
3. [step-03-cart-steering.md](step-03-cart-steering.md) — Cart: 막대 조향 (`StickSteering`)
4. [step-04-cart-bailout.md](step-04-cart-bailout.md) — Cart: 충돌·방출 (`CartBailout`) — step-03과 병렬
5. [step-05-editor-scenebuilder.md](step-05-editor-scenebuilder.md) — Editor: `Tools/Cart/Build Game` 씬 빌더

## 병렬 실행 가능성
- step-01과 step-02는 서로 참조하지 않음 — **병렬 가능**
- step-01 완료 후, step-03과 step-04는 서로 무관 — **병렬 가능**
- step-05는 step-01~04 전부 완료 후 실행
