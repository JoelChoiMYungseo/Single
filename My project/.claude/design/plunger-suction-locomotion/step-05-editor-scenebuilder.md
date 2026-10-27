# Step 05: PlungerSceneBuilder — Tools/Plunger/Build Game

- **영역:** `editor` (`Assets/Scripts/Editor/`)
- **선행 단계:** step-01, 02, 03, 04 전부 완료 필요 (모든 심볼을 실제로 조립·배선한다)
- **후행 단계:** 없음 (이 계획의 마지막 단계) — 완료되면 Unity에서 `Tools/Plunger/Build Game` → Play로 실제 검증

---

## 목적
다른 6개 프로토타입과 동일한 방식으로 씬을 코드로 조립한다. 흡착 가능한 벽 몇 개, 최소 아레나, `PlungerMonster`+`MonsterFaceStick`이 붙은 테스트 몬스터 1~2마리, `AutoHandRigFactory`로 스폰한 플레이어 리그에 `PlungerLocomotion`+`PlungerFeedback`을 붙이고 배선한다. HUD는 이번 스코프에 없음(README 참고).

---

## 에이전트 실행 지침

`/task-start`를 먼저 호출해 범위를 확정한 뒤, 아래 지시를 수행한다.

### 생성 파일
- `Assets/Scripts/Editor/PlungerSceneBuilder.cs` — 신규
- 씬 파일 `Assets/Scenes/GAME7 plunger.unity` — 없으면 [TowerSceneBuilder](../../../Assets/Scripts/Editor/TowerSceneBuilder.cs)의 "씬 없으면 새로 만든다" 패턴을 그대로 따른다 ({TODO: verify} 정확한 신규 씬 생성 API는 `EditorSceneManager.NewScene`으로 TowerSceneBuilder에서 실제로 어떻게 하는지 확인 후 그대로 재사용).

### 핵심 심볼
```csharp
public static class PlungerSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/GAME7 plunger.unity";
    private const string PrefabFolder = "Assets/Prefabs/Plunger";
    private const string MaterialFolder = "Assets/Materials/Plunger";

    [MenuItem("Tools/Plunger/Build Game")]
    public static void BuildGame();
}
```

### 필수 처리 순서 (다른 빌더들과 동일한 관례)
1. `MakeAssetsUtil.EnsureFolder(PrefabFolder)` / `EnsureFolder(MaterialFolder)`
2. 대상 씬을 명시적으로 연다 ([SurvivorSceneBuilder.BuildGame](../../../Assets/Scripts/Editor/SurvivorSceneBuilder.cs#L36) 상단 패턴 그대로 — `SceneManager.GetActiveScene()`이 대상과 다르면 `EditorSceneManager.OpenScene`).
3. `MakeAssetsUtil.RemoveDefaultCamera()`
4. **"Stickable" 레이어 확인**: `int stickableLayer = LayerMask.NameToLayer("Stickable");` 결과가 `-1`이면 `Debug.LogError`로 "Project Settings > Tags and Layers에서 'Stickable' 레이어를 먼저 추가하세요"를 남기고 중단한다 ([MagneticSceneBuilder.ResolveMetalLayer](../../../Assets/Scripts/Editor/MagneticSceneBuilder.cs#L62)와 동일한 관례 — RULE-04가 해제된 상태라 레이어를 코드로 직접 추가할 수도 있지만, 이 프로젝트의 기존 6개 빌더는 전부 "레이어가 이미 있다고 가정하고 조회만" 하는 쪽을 택했으므로 그 관례를 따른다).
5. 아레나(바닥 + 벽 2~4개, 전부 `stickableLayer`)를 만든다 ([MagneticSceneBuilder.BuildArena](../../../Assets/Scripts/Editor/MagneticSceneBuilder.cs#L95) 규모 참고, 훨씬 작게: 플런저는 근거리 벽 흡착이라 8~12m 아레나면 충분).
6. 플레이어 리그:
   ```csharp
   GameObject player = AutoHandRigFactory.Spawn("Plunger", "Player", startPosition,
       disableBuiltinMovement: true, out AutoHandPlayer autoHandPlayer, out Rigidbody body);
   var locomotion = player.AddComponent<PlungerLocomotion>();
   // SetRef: leftHandOrigin/rightHandOrigin = autoHandPlayer.handLeft/handRight.transform, stickableMask = 1 << stickableLayer
   var feedback = player.AddComponent<PlungerFeedback>();
   feedback.Bind(locomotion); // 또는 SerializedObject로 참조 세팅 후 내부에서 Awake 시 자동 Bind — step-01/04 구현 방식에 맞춘다
   var playerRig = player.AddComponent<PlayerRig>();
   // SetRef: root/head/leftHand/rightHand/body
   ```
7. 몬스터 1~2마리: `PlungerMonster` + `MonsterFaceStick`을 자식(머리) 콜라이더에 붙이고 `monsterFaceStick.Bind(locomotion)` 호출. `NavMeshSurface` 베이크 필요 여부 확인 ([EchoSceneBuilder.BakeNavMesh](../../../Assets/Scripts/Editor/EchoSceneBuilder.cs#L121) 패턴 재사용).
8. `Debug.Log($"[Plunger] 구성 완료. ▶ Play 하세요.")`, `EditorSceneManager.MarkSceneDirty` + `SaveScene` + `AssetDatabase.SaveAssets()`.

### 선행 산출물 의존성
- `AutoHandRigFactory.Spawn` — 이미 존재 (이번 리팩토링 세션에서 만듦, `Assets/Scripts/Editor/AutoHandRigFactory.cs`)
- `PlungerLocomotion` — step-01
- `PlungerMonster` — step-02
- `MonsterFaceStick` — step-03
- `PlungerFeedback` — step-04
- `MakeAssetsUtil`, `PlayerRig` — 기존 공용 코드, 수정 없이 그대로 사용

### 완료 판정
- [ ] `Grep "Tools/Plunger" Assets/Scripts/Editor/` 로 메뉴 아이템 등록 확인
- [ ] Unity 컴파일 통과
- [ ] `Tools/Plunger/Build Game` 실행 시 콘솔 에러 없음
- [ ] Play 모드에서 (VR 헤드셋 연결 시) 양손 트리거로 벽에 흡착 → 당김 → 놓으면 발사되는지 실제 확인 (에이전트가 헤드셋으로 직접 확인할 수 없으므로, 이 항목은 사용자 확인 필요로 보고에 남긴다)

---

## 금지 사항
- 다른 5개 게임의 SceneBuilder 파일을 수정하지 않는다.
- HUD/점수 시스템을 추가하지 않는다 (README의 스코프 결정 참고 — 필요해지면 별도 설계).
- `.prefab`/`.unity`/`.asset` 파일을 텍스트로 직접 편집하지 않는다 — Unity가 Editor 스크립트를 통해 생성하게 한다.
