# 로코모션 드라이버 계약 (PlayerRig / ILocomotionDriver)

## 한 줄 요약
`PlayerRig`는 실제 이동 방식(반작용 힘, 자기력, 호스 반동 등)을 절대 알지 못하고 `ILocomotionDriver`라는 2개 멤버짜리 최소 계약만 안다 — 그 덕에 사무라이/서바이버/자석/타워/플런저/카트/호스클리너 등 서로 물리적으로 완전히 다른 프로토타입이, 적 AI·낙하판정·카메라 같은 공용 게임 로직을 한 줄도 안 고치고 각자 다른 로코모션을 갈아 끼운다.

## 핵심 타입 / 진입점
- `PlayerRig` — `Assets/Scripts/Core/PlayerRig.cs` — 플레이어 신원(head/hand/body)을 모으는 허브. `GetComponentInChildren<ILocomotionDriver>()`로 씬에 붙은 드라이버를 찾을 뿐, 구체 타입을 코드에 적지 않는다.
- `ILocomotionDriver` — `Assets/Scripts/Core/ILocomotionDriver.cs` — `Vector3 Velocity { get; }` + `void OnTeleported(Vector3)` 딱 2개 멤버.
- `ObservedLocomotionDriver` — `Assets/Scripts/Core/ObservedLocomotionDriver.cs` — 원본 코드를 전혀 수정하지 않고 위치 차분(속도 근사)만으로 계약을 만족시키는 관측 어댑터. Another Axiom MIT `GorillaLocomotion`이나 Auto Hand의 `AutoHandPlayer`처럼 "소스를 못 고치거나 안 고치고 싶은" 이동 패키지 위에 얹는다.
- 자체 구현 드라이버(로코모션 스스로 `ILocomotionDriver`를 구현): `SinglePlayerGorillaLocomotion`(`Locomotion/`), `SwordGorillaLocomotion`(`Samurai/`, 칼끝 반작용 변형), `MagneticLocomotion`(`Magnetic/`, 거리 역제곱 인력/척력), `PlungerLocomotion`(`Plunger/`).
- 원본 참고 코드(수정 금지, 어댑터로만 편입): `Assets/GorillaLocomotion-main/GorillaLocomotion-main/Player.cs` (Another Axiom, MIT).

## 시스템 간 관계
- `PlayerRig` → `ILocomotionDriver`: 단방향. 리그는 드라이버의 `Velocity`를 읽고 `Teleport()` 시 `OnTeleported()`를 호출할 뿐, 드라이버 내부 물리를 전혀 모른다.
- 적 예측 사격·낙하 판정·카메라 흔들림 등 "속도가 필요한" 모든 게임 로직은 `PlayerRig.Velocity`만 참조한다 — 구체 드라이버를 직접 참조하면 이 계약이 무의미해진다(신규 코드에서 절대 하지 말 것).
- `SinglePlayerGorillaLocomotion`/`SwordGorillaLocomotion`처럼 **자체 구현 드라이버**는 게임의 유일한 이동 원인이다. 반면 `ObservedLocomotionDriver`를 쓰는 게임(호스클리너 등, [domain/hose-cleaner-auto-hand-integration.md](hose-cleaner-auto-hand-integration.md) 참조)은 이동의 진짜 원인이 `PlayerRig` 바깥(`AutoHandPlayer`, 호스 반동)에 있고 `PlayerRig`는 그 결과를 관측만 하는 "관전자"다 — 두 배선은 겉보기엔 같은 계약이지만 인과관계 방향이 반대다.
- `PlayerRig.Teleport()`가 유일한 순간이동 경로. 다른 코드가 `Root.position`을 직접 대입하면 드라이버에 통보가 안 가서 다음 프레임에 거짓 속도가 계산된다.

## 기획 의도 / 역사적 맥락
리팩토링 전에는 시스템마다 플레이어 참조 타입이 달랐다 — 서바이버의 보스는 `PlaceholderPlayerController`를, 사무라이의 탐험 매니저는 `SwordGorillaLocomotion`을, 타워의 낙하 매니저는 `Rigidbody`를 직접 들고 있었다. 로코모션 하나를 바꾸려면 여러 게임의 여러 파일을 동시에 고쳐야 했다.

`ILocomotionDriver`를 도입하며 계약을 "결과(속도)"와 "통보(순간이동)" 두 가지로만 최소화한 것이 핵심 설계 결정이다. 인터페이스에 `HandRadius`나 `SwingForce` 같은 로코모션별 파라미터를 넣고 싶은 유혹이 있었지만, 그러면 물리 원리가 다른 `MagneticLocomotion`(반작용이 아니라 인력/척력)이 계약을 만족 못 하게 된다 — **"구현들이 서로 무관해질수록 계약이 잘 설계된 것"**이라는 기준으로 멤버를 계속 걸러냈다.

`ObservedLocomotionDriver`는 애초에 "Another Axiom의 MIT GorillaLocomotion처럼 소스를 고치기 싫은 외부 패키지"를 염두에 두고 설계됐다(클래스 주석에 명시). 이 설계 의도는 나중에 Auto Hand(`AutoHandPlayer`) 통합에서 실제로 검증됐다 — 전혀 다른 외부 패키지인데도 같은 어댑터가 그대로 통했다.

## 숨은 규칙 / 암묵지
- **인터페이스에 멤버를 추가하기 전에 "이게 없는 구현도 나올 수 있나?"를 먼저 물을 것.** 지금 3개 드라이버(팔스윙 반작용/칼끝 반작용/자기력)가 물리적으로 완전히 무관한데도 2개 멤버만으로 전부 만족된다는 게 이 계약이 건강하다는 증거다. 새 멤버가 특정 드라이버에서만 의미 있다면 그건 `ILocomotionDriver`가 아니라 그 드라이버 자신의 public 필드로 가야 한다.
- **`OnTeleported`를 구현 안 하면 컴파일이 안 되게 강제한 것이 의도적이다.** 손/칼끝 기반 드라이버는 전부 "지난 프레임 위치와의 차이"로 속도를 계산하기 때문에, 순간이동 직후 기준점을 리셋 안 하면 "몸이 순식간에 이동했다"를 "칼끝이 초고속으로 움직였다"로 오인해 플레이어가 로켓처럼 튕겨나간다. 새 드라이버를 만들 때 이 훅을 대충 비워두면 리스폰마다 이 버그가 재현된다.
- **원본 알고리즘 전체를 포팅하지 않는다.** `GorillaLocomotion-main/Player.cs` 원본은 손을 표면에 고정하는 앵커링 + 슬라이드 + 언스틱까지 포함한 정교한 시스템(약 300줄)이지만, 이 프로젝트의 `SinglePlayerGorillaLocomotion`/`SwordGorillaLocomotion`은 그중 "손(또는 칼끝) 속도의 반대 방향으로 힘을 가한다"는 반작용 원리 하나만 뽑아 `Rigidbody.AddForce`로 재구현했다. 원본의 나머지 복잡도는 Gorilla Tag 자체의 정체성("손이 완벽히 고정되는 느낌")을 위한 것이지 이 프로젝트에 필요한 게 아니다. 새 프로토타입에 외부 MIT/에셋 코드를 가져올 때도 전체를 포팅하지 말고 "왜 재밌는가"에 대한 물리적 원리 한 줄부터 찾을 것.
- **드라이버가 없어도 `PlayerRig`는 죽지 않는다.** `Awake()`에서 `GetComponentInChildren<ILocomotionDriver>()`가 null이면 경고만 남기고 `Velocity`는 0을 반환한다 — 씬 세팅이 덜 끝난 상태에서도 나머지 시스템(적 AI, UI 등)을 테스트할 수 있게 하기 위한 의도적 설계다.
- `ObservedLocomotionDriver`를 쓸지, 드라이버가 스스로 `ILocomotionDriver`를 구현할지는 "원본 소스를 고칠 수 있는가/고치고 싶은가"로 결정한다. 자체 구현이 가능하면 한 프레임 지연이 없어 예측 사격 등 정밀도가 필요한 곳에 유리하다(`ObservedLocomotionDriver`는 위치 차분 기반이라 항상 한 프레임 늦다).

## 수정 시 주의
- 새 프로토타입에 새 로코모션을 추가할 때: (1) 물리 원리가 기존 3개(반작용/자기력)와 겹치는지 먼저 확인 — 겹치면 파라미터만 다른 변형(Sword가 Single의 변형이듯)으로 만드는 게 낫다. (2) `[RequireComponent(typeof(Rigidbody))]` 계열 드라이버는 반드시 실제 물리 바디가 있는 GameObject에 붙여야 한다 — Auto Hand 통합 시 이 지점에서 실제로 버그가 났다([domain/hose-cleaner-auto-hand-integration.md](hose-cleaner-auto-hand-integration.md) 참조).
- `PlayerRig.Velocity`를 참조하는 새 게임 로직(예측 사격, 낙하 데미지 등)을 짤 때 구체 드라이버 타입을 캐스팅해서 우회하지 말 것 — 그 순간 이 계약 전체의 존재 의미가 사라지고, 그 로직만 특정 프로토타입에 종속된다.
- 이 문서는 계약 자체(범용 원리)를 다룬다. 특정 외부 패키지(Auto Hand)와의 실제 통합에서 나온 구체적 함정들(런타임 전용 프로퍼티, `useMovement` 등)은 [domain/hose-cleaner-auto-hand-integration.md](hose-cleaner-auto-hand-integration.md)에 있다 — 두 문서는 서로 다른 층을 다루므로 함께 읽을 것.
