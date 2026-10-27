# 플런저 흡착·발사 로코모션 (Plunger Suction & Launch)

## 한 줄 요약
양손 플런저로 벽/몬스터 얼굴에 흡착 → 몸을 당겨 텐션을 만든 뒤 놓으면 그 반대 방향으로 슬링샷처럼 발사되는, 7번째 VR 프로토타입.

## 원문
사용자가 제공한 프롬프트(요약): VRPlungerController(흡착 상태머신 Idle→Stuck→Compressed→Launched, 당긴 거리로 텐션 계산, 놓으면 반대 방향 임펄스), MonsterFaceStick(몬스터 얼굴에 꽂으면 일시 실명+패닉, 플레이어를 당기거나 튕겨냄), PlungerFeedback(압축/발사 시 햅틱, 흡착/발사 효과음). Quest 단독구동 최적화, 스프링 강성/최대 발사력/흡착 거리는 인스펙터 노출.

## 아키텍처 결정
- **로코모션은 `ILocomotionDriver` 구현체로 만든다** (새 게임 로직이 필요할 때 `PlayerRig`만 참조하면 되게) — [MagneticLocomotion.cs](../../../Assets/Scripts/Magnetic/MagneticLocomotion.cs)가 정확히 같은 골격(양손 XR 트리거 입력 → 레이캐스트 → 물리 힘)이라 이걸 뼈대로 복제·변형한다. 다른 점: Magnetic은 즉시-지속 인력이고, Plunger는 "고정→당김→해제 시 1회 임펄스"라는 3단 상태가 더 있다.
- **몬스터 얼굴 흡착은 레이캐스트 히트의 컴포넌트 존재 여부로 분기한다** (Magnetic이 `hit.rigidbody.GetComponent<MagneticHazard>()`로 "가벼운 쇳덩이인가"를 판별하는 것과 동일한 패턴) — `MonsterFaceStick`이 있으면 벽 흡착이 아니라 몬스터 흡착 분기로 간다.
- **몬스터는 이 프로토타입 전용으로 최소 하나 새로 만든다** (`PlungerMonster`) — 기존 `SoundAttractedAI`(Echo)/`DirtMonster`(HoseCleaner)는 각자 프로토타입 전용 로직이 섞여 있어 재사용 불가. NavMeshAgent 추적 + `Blind(duration)`만 있는 최소 버전으로 만든다. **본 프롬프트엔 몬스터 AI 스펙이 없어 최소 골격으로 가정 — 아키텍트 확인 필요.**
- **HUD는 이번 스코프에서 제외.** 요청한 3개 시스템 + 플레이 가능한 씬 구성까지가 목표. 텐션 게이지 등 UI는 메커닉이 검증된 뒤 별도 설계로 돌린다.
- **AutoHand 리그 스폰은 `AutoHandRigFactory.Spawn(..., disableBuiltinMovement: true, ...)`를 그대로 쓴다** — Plunger 반동이 유일한 이동 수단이라 다른 반동 기반 게임들(Survivor/Samurai/Magnetic)과 동일하게 Auto Hand 기본 이동을 꺼야 한다.
- **"Stickable" 레이어를 새로 추가해야 한다** — Magnetic의 "Metal" 레이어와 동일한 방식(ProjectSettings/TagManager에 레이어 존재를 가정하고 `LayerMask.NameToLayer`로 조회). RULE-04는 이미 해제된 상태라 SceneBuilder가 직접 추가해도 되지만, 없으면 `LayerMask.NameToLayer`가 -1을 반환하므로 step-05에서 존재를 확인/생성하는 처리가 필요.

## 터치 영역
| 영역 | 경로 | 역할 |
|---|---|---|
| plunger (신규) | `Assets/Scripts/Plunger/` | 로코모션, 몬스터, 흡착 인터랙션, 피드백 |
| editor | `Assets/Scripts/Editor/` | 씬 빌더 (`Tools/Plunger/Build Game`) |

## 의존성 그래프
```
Plunger/PlungerLocomotion (상태머신 + 이벤트)  ─┐
Plunger/PlungerMonster (추적 + Blind)          ─┼→ Plunger/MonsterFaceStick
                                                 │
Plunger/PlungerLocomotion (이벤트)  ────────────┴→ Plunger/PlungerFeedback

Plunger/PlungerLocomotion, PlungerMonster, MonsterFaceStick, PlungerFeedback → Editor/PlungerSceneBuilder
```

## 단계
1. [step-01-plunger-locomotion.md](step-01-plunger-locomotion.md) — Plunger: 흡착·텐션·발사 상태머신 (`ILocomotionDriver`)
2. [step-02-plunger-monster.md](step-02-plunger-monster.md) — Plunger: 최소 추적 몬스터 + `Blind()`
3. [step-03-plunger-facestick.md](step-03-plunger-facestick.md) — Plunger: 몬스터 얼굴 흡착 분기 (`MonsterFaceStick`)
4. [step-04-plunger-feedback.md](step-04-plunger-feedback.md) — Plunger: 햅틱·오디오 피드백 (`PlungerFeedback`)
5. [step-05-editor-scenebuilder.md](step-05-editor-scenebuilder.md) — Editor: `Tools/Plunger/Build Game` 씬 빌더

## 병렬 실행 가능성
- step-01과 step-02는 서로 독립 — **병렬 가능**
- step-03은 step-01, step-02 완료 후 실행 (둘 다 참조)
- step-04는 step-01 완료 후 실행 (이벤트만 참조, step-02/03과 무관 — step-01 이후 step-03과 **병렬 가능**)
- step-05는 step-01~04 전부 완료 후 실행
