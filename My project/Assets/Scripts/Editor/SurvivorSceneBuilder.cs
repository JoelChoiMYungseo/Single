#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using Autohand;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRGame.Core;
using VRGame.Danmaku;
using VRGame.Survivor;

/// <summary>탕탕특공대 스타일 런을 "Game1 tang" 씬에 통째로 구성한다. 프리팹·머티리얼·UI를 코드로 만들고
/// 씬에 이미 있는 오브젝트(Player / Enemy Boss / GameManger / Plan)를 찾아 재사용한다.</summary>
public static class SurvivorSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Game1 tang.unity";
    private const string PrefabFolder = "Assets/Prefabs/Survivor";
    private const string MaterialFolder = "Assets/Materials/Survivor";

    private const string EnemyPrefabPath = PrefabFolder + "/SwarmEnemy.prefab";
    private const string ProjectilePrefabPath = PrefabFolder + "/PlayerProjectile.prefab";
    private const string GemPrefabPath = PrefabFolder + "/ExperienceGem.prefab";
    private const string DanmakuPrefabPath = PrefabFolder + "/DanmakuBullet.prefab";

    [MenuItem("Tools/Survivor/Build Game")]
    public static void BuildGame()
    {
        MakeAssetsUtil.EnsureFolder(PrefabFolder);
        MakeAssetsUtil.EnsureFolder(MaterialFolder);

        // 대상 씬을 명시적으로 연다. 활성 씬에 의존하면 다른 빌더가 먼저 씬을 바꿨을 때
        // 엉뚱한 씬에 게임을 지어버린다.
        if (SceneManager.GetActiveScene().isDirty)
        {
            EditorSceneManager.SaveOpenScenes();
        }
        Scene scene = SceneManager.GetActiveScene().path == ScenePath
            ? SceneManager.GetActiveScene()
            : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // 프리팹 생성은 임시 오브젝트를 만들므로 반드시 대상 씬을 연 뒤에 한다.
        GameObject enemyPrefab = BuildEnemyPrefab();
        GameObject projectilePrefab = BuildProjectilePrefab();
        GameObject gemPrefab = BuildGemPrefab();
        GameObject danmakuPrefab = BuildDanmakuPrefab();

        // Auto Hand 리그의 Head 카메라가 유일한 카메라여야 한다 — 빈 씬 기본 카메라가 남아 있으면
        // MainCamera 태그가 둘이 되어 렌더링을 다툰다.
        MakeAssetsUtil.RemoveDefaultCamera();

        GameObject arena = SetupArena();
        GameObject player = SetupPlayer();
        GameObject boss = SetupBoss();
        GameObject manager = SetupManager();
        EnsureEventSystem();

        // AutoHandPlayer는 래퍼(player)가 아니라 중첩된 자식에 있다 — AutoHandRigFactory 클래스
        // 주석 참고. GetComponent(비재귀)가 아니라 GetComponentInChildren으로 찾아야 한다.
        Transform head = player.GetComponentInChildren<AutoHandPlayer>(true).headCamera.transform;
        HudRefs hud = BuildUI(head);

        WireEverything(player, boss, manager, hud, enemyPrefab, projectilePrefab, gemPrefab, danmakuPrefab);
        HideLeftoverObjects();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Survivor] 구성 완료 — 아레나 {arena.name}. Auto Hand 리그, VR 전용입니다. " +
                  "팔을 저어 이동하세요 (땅을 짚듯 손을 아래로 휘두르면 그 반대로 밀려납니다). ▶ Play 하세요.");
    }

    // ────────────────────────────── Prefabs ──────────────────────────────

    private static GameObject BuildEnemyPrefab()
    {
        var root = new GameObject("SwarmEnemy");
        root.transform.localScale = Vector3.one * 0.8f;

        var target = root.AddComponent<EnemyTarget>();
        root.AddComponent<SwarmEnemy>();

        var so = new SerializedObject(target);
        MakeAssetsUtil.SetFloat(so, "defaultMaxHealth", 20f);
        MakeAssetsUtil.SetInt(so, "experienceValue", 1);
        so.ApplyModifiedPropertiesWithoutUndo();

        AddVisual(root, PrimitiveType.Capsule, new Vector3(0f, 1f, 0f), MakeAssetsUtil.MakeMaterial(MaterialFolder, "EnemyMat", new Color(0.75f, 0.18f, 0.18f)));

        return MakeAssetsUtil.SavePrefab(root, EnemyPrefabPath);
    }

    private static GameObject BuildProjectilePrefab()
    {
        var root = new GameObject("PlayerProjectile");
        root.AddComponent<PlayerProjectile>();
        AddVisual(root, PrimitiveType.Sphere, Vector3.zero, MakeAssetsUtil.MakeMaterial(MaterialFolder, "ProjectileMat", new Color(1f, 0.85f, 0.2f)), Vector3.one * 0.35f);
        return MakeAssetsUtil.SavePrefab(root, ProjectilePrefabPath);
    }

    private static GameObject BuildGemPrefab()
    {
        var root = new GameObject("ExperienceGem");
        root.AddComponent<ExperienceGem>();
        AddVisual(root, PrimitiveType.Cube, new Vector3(0f, 0.3f, 0f), MakeAssetsUtil.MakeMaterial(MaterialFolder, "GemMat", new Color(0.2f, 0.9f, 0.5f)), Vector3.one * 0.3f);
        return MakeAssetsUtil.SavePrefab(root, GemPrefabPath);
    }

    private static GameObject BuildDanmakuPrefab()
    {
        var root = new GameObject("DanmakuBullet");
        root.AddComponent<DanmakuBullet>();
        AddVisual(root, PrimitiveType.Sphere, Vector3.zero, MakeAssetsUtil.MakeMaterial(MaterialFolder, "DanmakuMat", new Color(0.8f, 0.3f, 1f)), Vector3.one * 0.4f);
        return MakeAssetsUtil.SavePrefab(root, DanmakuPrefabPath);
    }

    // ────────────────────────────── Scene objects ──────────────────────────────

    private static GameObject SetupArena()
    {
        GameObject arena = FindByAnyName("Plan", "Plane", "Ground", "Arena");
        if (arena == null)
        {
            arena = GameObject.CreatePrimitive(PrimitiveType.Plane);
            arena.name = "Arena";
        }

        arena.transform.position = Vector3.zero;
        arena.transform.rotation = Quaternion.identity;
        arena.transform.localScale = new Vector3(7.2f, 1f, 7.2f); // Plane 기본 10x10 → 72x72 (arenaHalfExtent 34 여유)

        var renderer = arena.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "ArenaMat", new Color(0.16f, 0.17f, 0.2f));
        }

        return arena;
    }

    /// <summary>자체 XRNodeTracker 리그 대신 Auto Hand의 XRPlayer 프리팹을 그대로 쓴다 — 손 IK·그립
    /// 조인트를 다시 만들 이유가 없다. 로코모션은 2026-09-17 세션부터 Another Axiom의 공식 Gorilla
    /// Tag MIT 패키지(<c>AutoHandRigFactory.SpawnWithGorillaLocomotion</c> 내부에서 배선)를 쓴다.</summary>
    private static GameObject SetupPlayer()
    {
        GameObject existing = FindByAnyName("Player", "XRPlayer");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        // 리그 스폰 + 팔스윙 배선(손 참조·환경 마스크·Ignore Raycast)을 공용 헬퍼로 처리한다 —
        // Magnetic/Tower/HoseCleaner/Echo와 동일한 배선이라 AutoHandRigFactory로 승격했다.
        GameObject player = AutoHandRigFactory.SpawnWithGorillaLocomotion("Survivor", "Player", Vector3.zero,
            out AutoHandPlayer autoHandPlayer, out Rigidbody playerBody);

        EnsureComponent<PlayerStats>(player);
        EnsureComponent<PlayerHealth>(player);
        EnsureComponent<PlayerExperience>(player);
        EnsureComponent<AutoAttackWeapon>(player);

        Transform head = autoHandPlayer.headCamera.transform;
        Transform leftHand = autoHandPlayer.handLeft.transform;
        Transform rightHand = autoHandPlayer.handRight.transform;

        // 자동조준 발사 지점은 몸통 정면으로 둔다 — 조준은 플레이어가 아니라 AutoAttackWeapon이 한다.
        Transform muzzle = player.transform.Find("Muzzle");
        if (muzzle == null)
        {
            var go = new GameObject("Muzzle");
            go.transform.SetParent(player.transform, false);
            go.transform.localPosition = new Vector3(0f, 1f, 0.6f);
            muzzle = go.transform;
        }

        var weaponSo = new SerializedObject(player.GetComponent<AutoAttackWeapon>());
        MakeAssetsUtil.SetRef(weaponSo, "muzzle", muzzle);
        weaponSo.ApplyModifiedPropertiesWithoutUndo();

        // 게임 시스템이 참조할 유일한 플레이어 창구. ILocomotionDriver 구현은 SpawnWithGorillaLocomotion이
        // 이미 붙인 ObservedLocomotionDriver 하나뿐이라 PlayerRig가 알아서 정확히 찾는다. root는
        // 래퍼(player)가 아니라 autoHandPlayer.gameObject다 — 안 그러면 Position/Teleport가 스폰
        // 위치에 얼어붙는다.
        var playerRig = EnsureComponent<PlayerRig>(player);
        var rigSo = new SerializedObject(playerRig);
        MakeAssetsUtil.SetRef(rigSo, "root", autoHandPlayer.transform);
        MakeAssetsUtil.SetRef(rigSo, "head", head);
        MakeAssetsUtil.SetRef(rigSo, "leftHand", leftHand);
        MakeAssetsUtil.SetRef(rigSo, "rightHand", rightHand);
        MakeAssetsUtil.SetRef(rigSo, "body", playerBody);
        rigSo.ApplyModifiedPropertiesWithoutUndo();

        return player;
    }

    private static GameObject SetupBoss()
    {
        GameObject boss = FindByAnyName("Enemy Boss", "EnemyBoss", "Boss") ?? new GameObject("Enemy Boss");
        boss.name = "Enemy Boss";
        boss.transform.localScale = Vector3.one * 2f;
        // 런타임 스폰 시 이 y를 그대로 유지하므로 여기서 바닥에 맞춰 둔다.
        boss.transform.position = new Vector3(0f, 1f, 14f);

        EnsureComponent<EnemyTarget>(boss);
        EnsureComponent<ZombieDanmakuAI>(boss);

        var targetSo = new SerializedObject(boss.GetComponent<EnemyTarget>());
        MakeAssetsUtil.SetFloat(targetSo, "defaultMaxHealth", 1500f);
        MakeAssetsUtil.SetInt(targetSo, "experienceValue", 40);
        targetSo.ApplyModifiedPropertiesWithoutUndo();

        var bossRenderer = boss.GetComponent<MeshRenderer>();
        if (bossRenderer != null)
        {
            bossRenderer.sharedMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "BossMat", new Color(0.5f, 0.1f, 0.55f));
        }
        else if (boss.transform.Find("Visual") == null)
        {
            AddVisual(boss, PrimitiveType.Capsule, new Vector3(0f, 1f, 0f), MakeAssetsUtil.MakeMaterial(MaterialFolder, "BossMat", new Color(0.5f, 0.1f, 0.55f)));
        }

        Transform firePoint = boss.transform.Find("FirePoint");
        if (firePoint == null)
        {
            var go = new GameObject("FirePoint");
            go.transform.SetParent(boss.transform, false);
            go.transform.localPosition = new Vector3(0f, 1f, 0f);
            firePoint = go.transform;
        }

        var bossSo = new SerializedObject(boss.GetComponent<ZombieDanmakuAI>());
        MakeAssetsUtil.SetRef(bossSo, "firePoint", firePoint);
        bossSo.ApplyModifiedPropertiesWithoutUndo();

        return boss;
    }

    private static GameObject SetupManager()
    {
        GameObject manager = FindByAnyName("GameManger", "GameManager", "Managers") ?? new GameObject("GameManager");
        manager.transform.position = Vector3.zero;

        EnsureComponent<SwarmSpawner>(manager);
        EnsureComponent<ExperienceGemPool>(manager);
        EnsureComponent<BulletPool>(manager);
        EnsureComponent<UpgradeSystem>(manager);
        EnsureComponent<SurvivorRunManager>(manager);

        return manager;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
        {
            return;
        }

        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        // 이 프로젝트는 새 Input System을 쓰므로 StandaloneInputModule을 붙이면 런타임에 예외가 난다.
        go.AddComponent<InputSystemUIInputModule>();
    }

    /// <summary>이전 프로토타입에서 남은 오브젝트가 화면을 가리지 않게 꺼둔다.</summary>
    private static void HideLeftoverObjects()
    {
        string[] leftovers = { "Enemy", "SpawnPoint" };
        for (int i = 0; i < leftovers.Length; i++)
        {
            GameObject go = GameObject.Find(leftovers[i]);
            if (go != null)
            {
                go.SetActive(false);
            }
        }
    }

    // ────────────────────────────── UI ──────────────────────────────

    private struct HudRefs
    {
        public SurvivorHUD Hud;
        public GameObject UpgradePanel;
        public Button[] Buttons;
        public Text[] Titles;
        public Text[] Descriptions;
    }

    private static HudRefs BuildUI(Transform head)
    {
        GameObject existing = GameObject.Find("SurvivorCanvas");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        var canvasGo = new GameObject("SurvivorCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        // Auto Hand 리그는 VR 전용(데스크톱 폴백 없음)이라 처음부터 WorldSpace로 Head에 고정한다 —
        // HoseCleaner의 HUD와 동일한 방식.
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = head.GetComponent<Camera>();
        canvasGo.transform.SetParent(head, false);
        canvasGo.transform.localPosition = new Vector3(0f, 0f, 1.4f);
        canvasGo.transform.localRotation = Quaternion.identity;
        canvasGo.transform.localScale = Vector3.one * 0.001f;
        canvasGo.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var hud = canvasGo.AddComponent<SurvivorHUD>();
        canvasGo.AddComponent<RuntimeKoreanFont>();

        Image healthFill = CreateBar(canvasGo.transform, "HealthBar", new Vector2(0f, -40f), new Vector2(520f, 34f),
            new Color(0.15f, 0.05f, 0.05f, 0.85f), new Color(0.9f, 0.25f, 0.25f), TextAnchor.UpperCenter);
        Text healthLabel = CreateText(healthFill.transform.parent, "HealthLabel", "100 / 100", 20, TextAnchor.MiddleCenter,
            new Vector2(0f, 0f), new Vector2(520f, 34f));

        Image xpFill = CreateBar(canvasGo.transform, "XpBar", new Vector2(0f, -84f), new Vector2(520f, 20f),
            new Color(0.05f, 0.1f, 0.05f, 0.85f), new Color(0.3f, 0.85f, 0.45f), TextAnchor.UpperCenter);

        Text levelLabel = CreateAnchoredText(canvasGo.transform, "LevelLabel", "Lv.1", 34, TextAnchor.UpperLeft,
            new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(240f, 46f));
        Text timerLabel = CreateAnchoredText(canvasGo.transform, "TimerLabel", "00:00", 42, TextAnchor.UpperRight,
            new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(240f, 52f));
        Text killLabel = CreateAnchoredText(canvasGo.transform, "KillLabel", "처치 0", 26, TextAnchor.UpperRight,
            new Vector2(1f, 1f), new Vector2(-40f, -96f), new Vector2(240f, 40f));

        // ── 업그레이드 선택 패널 ──
        GameObject upgradePanel = CreateFullscreenPanel(canvasGo.transform, "UpgradePanel", new Color(0f, 0f, 0f, 0.82f));
        CreateAnchoredText(upgradePanel.transform, "UpgradeTitle", "레벨 업!  강화를 선택하세요", 44, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(900f, 60f));

        var buttons = new Button[3];
        var titles = new Text[3];
        var descriptions = new Text[3];
        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * 380f;
            BuildChoiceButton(upgradePanel.transform, i, x, out buttons[i], out titles[i], out descriptions[i]);
        }

        // ── 게임오버 패널 ──
        GameObject gameOverPanel = CreateFullscreenPanel(canvasGo.transform, "GameOverPanel", new Color(0.12f, 0f, 0f, 0.88f));
        CreateAnchoredText(gameOverPanel.transform, "GameOverTitle", "사망", 72, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0f, -280f), new Vector2(900f, 90f));
        Text gameOverLabel = CreateAnchoredText(gameOverPanel.transform, "GameOverLabel", string.Empty, 34, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0f, -400f), new Vector2(900f, 220f));

        var hudSo = new SerializedObject(hud);
        MakeAssetsUtil.SetRef(hudSo, "healthFill", healthFill);
        MakeAssetsUtil.SetRef(hudSo, "healthLabel", healthLabel);
        MakeAssetsUtil.SetRef(hudSo, "experienceFill", xpFill);
        MakeAssetsUtil.SetRef(hudSo, "levelLabel", levelLabel);
        MakeAssetsUtil.SetRef(hudSo, "timerLabel", timerLabel);
        MakeAssetsUtil.SetRef(hudSo, "killLabel", killLabel);
        MakeAssetsUtil.SetRef(hudSo, "gameOverPanel", gameOverPanel);
        MakeAssetsUtil.SetRef(hudSo, "gameOverLabel", gameOverLabel);
        hudSo.ApplyModifiedPropertiesWithoutUndo();

        upgradePanel.SetActive(false);
        gameOverPanel.SetActive(false);

        return new HudRefs
        {
            Hud = hud,
            UpgradePanel = upgradePanel,
            Buttons = buttons,
            Titles = titles,
            Descriptions = descriptions,
        };
    }

    private static void BuildChoiceButton(Transform parent, int index, float x, out Button button, out Text title, out Text description)
    {
        var go = new GameObject($"Choice_{index}", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(340f, 300f);

        var image = go.GetComponent<Image>();
        image.color = new Color(0.16f, 0.18f, 0.24f, 0.98f);
        image.sprite = UiSprite();
        image.type = Image.Type.Sliced;

        button = go.GetComponent<Button>();
        button.targetGraphic = image;

        title = CreateAnchoredText(go.transform, "Title", "제목", 32, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(300f, 50f));
        description = CreateAnchoredText(go.transform, "Description", "설명", 24, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(300f, 120f));
    }

    private static GameObject CreateFullscreenPanel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        go.GetComponent<Image>().color = color;
        return go;
    }

    private static Image CreateBar(Transform parent, string name, Vector2 anchoredPosition, Vector2 size,
        Color backgroundColor, Color fillColor, TextAnchor anchor)
    {
        var background = new GameObject(name, typeof(RectTransform), typeof(Image));
        background.transform.SetParent(parent, false);

        var bgRect = background.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0.5f, 1f);
        bgRect.anchorMax = new Vector2(0.5f, 1f);
        bgRect.pivot = new Vector2(0.5f, 1f);
        bgRect.anchoredPosition = anchoredPosition;
        bgRect.sizeDelta = size;

        var bgImage = background.GetComponent<Image>();
        bgImage.color = backgroundColor;
        bgImage.raycastTarget = false;
        bgImage.sprite = UiSprite();

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(background.transform, false);

        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);

        var fillImage = fill.GetComponent<Image>();
        fillImage.color = fillColor;
        fillImage.raycastTarget = false;
        // fillAmount는 스프라이트가 있는 Filled 타입에서만 제대로 동작한다.
        fillImage.sprite = UiSprite();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 1f;

        return fillImage;
    }

    private static Text CreateText(Transform parent, string name, string content, int fontSize, TextAnchor alignment,
        Vector2 anchoredPosition, Vector2 size)
    {
        return CreateAnchoredText(parent, name, content, fontSize, alignment, new Vector2(0.5f, 0.5f), anchoredPosition, size);
    }

    private static Text CreateAnchoredText(Transform parent, string name, string content, int fontSize, TextAnchor alignment,
        Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var text = go.GetComponent<Text>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        // Arial은 최신 Unity에 없다. 내장 대체 폰트를 쓴다.
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return text;
    }

    // ────────────────────────────── Wiring ──────────────────────────────

    private static void WireEverything(GameObject player, GameObject boss, GameObject manager, HudRefs hud,
        GameObject enemyPrefab, GameObject projectilePrefab, GameObject gemPrefab, GameObject danmakuPrefab)
    {
        var stats = player.GetComponent<PlayerStats>();
        var health = player.GetComponent<PlayerHealth>();
        var experience = player.GetComponent<PlayerExperience>();
        var autoAttack = player.GetComponent<AutoAttackWeapon>();

        var spawner = manager.GetComponent<SwarmSpawner>();
        var gemPool = manager.GetComponent<ExperienceGemPool>();
        var bulletPool = manager.GetComponent<BulletPool>();
        var upgrades = manager.GetComponent<UpgradeSystem>();
        var run = manager.GetComponent<SurvivorRunManager>();

        var weaponSo = new SerializedObject(autoAttack);
        MakeAssetsUtil.SetRef(weaponSo, "projectilePrefab", projectilePrefab.GetComponent<PlayerProjectile>());
        weaponSo.ApplyModifiedPropertiesWithoutUndo();

        var spawnerSo = new SerializedObject(spawner);
        MakeAssetsUtil.SetRef(spawnerSo, "enemyPrefab", enemyPrefab.GetComponent<SwarmEnemy>());
        MakeAssetsUtil.SetRef(spawnerSo, "gemPool", gemPool);
        // 난이도 상향 — GameManager는 씬에 이미 있으면 재사용되므로(EnsureComponent), C# 기본값을
        // 바꾸는 것만으론 이미 지어진 씬에 반영되지 않는다. 재빌드할 때마다 적용되도록 명시적으로 밀어넣는다.
        MakeAssetsUtil.SetInt(spawnerSo, "maxConcurrentEnemies", 220);
        MakeAssetsUtil.SetInt(spawnerSo, "burstSize", 3);
        MakeAssetsUtil.SetFloat(spawnerSo, "minSpawnInterval", 0.05f);
        MakeAssetsUtil.SetFloat(spawnerSo, "intervalDecayPerMinute", 0.35f);
        MakeAssetsUtil.SetFloat(spawnerSo, "healthGainPerMinute", 20f);
        MakeAssetsUtil.SetFloat(spawnerSo, "speedGainPerMinute", 0.7f);
        MakeAssetsUtil.SetFloat(spawnerSo, "maxEnemySpeed", 8f);
        MakeAssetsUtil.SetFloat(spawnerSo, "damageGainPerMinute", 6f);
        MakeAssetsUtil.SetFloat(spawnerSo, "spawnDistance", 26f);
        MakeAssetsUtil.SetFloat(spawnerSo, "arenaHalfExtent", 34f);
        spawnerSo.ApplyModifiedPropertiesWithoutUndo();

        var gemSo = new SerializedObject(gemPool);
        MakeAssetsUtil.SetRef(gemSo, "gemPrefab", gemPrefab.GetComponent<ExperienceGem>());
        gemSo.ApplyModifiedPropertiesWithoutUndo();

        var bulletSo = new SerializedObject(bulletPool);
        MakeAssetsUtil.SetRef(bulletSo, "bulletPrefab", danmakuPrefab.GetComponent<DanmakuBullet>());
        bulletSo.ApplyModifiedPropertiesWithoutUndo();

        var upgradeSo = new SerializedObject(upgrades);
        MakeAssetsUtil.SetRef(upgradeSo, "stats", stats);
        MakeAssetsUtil.SetRef(upgradeSo, "health", health);
        MakeAssetsUtil.SetRef(upgradeSo, "experience", experience);
        MakeAssetsUtil.SetRef(upgradeSo, "panel", hud.UpgradePanel);
        MakeAssetsUtil.SetObjectArray(upgradeSo, "choiceButtons", hud.Buttons);
        MakeAssetsUtil.SetObjectArray(upgradeSo, "choiceTitles", hud.Titles);
        MakeAssetsUtil.SetObjectArray(upgradeSo, "choiceDescriptions", hud.Descriptions);
        upgradeSo.ApplyModifiedPropertiesWithoutUndo();

        var bossAiSo = new SerializedObject(boss.GetComponent<ZombieDanmakuAI>());
        MakeAssetsUtil.SetRef(bossAiSo, "bulletPool", bulletPool);
        MakeAssetsUtil.SetRef(bossAiSo, "playerTarget", player.transform);
        bossAiSo.ApplyModifiedPropertiesWithoutUndo();

        var runSo = new SerializedObject(run);
        MakeAssetsUtil.SetRef(runSo, "playerTransform", player.transform);
        MakeAssetsUtil.SetRef(runSo, "playerStats", stats);
        MakeAssetsUtil.SetRef(runSo, "playerHealth", health);
        MakeAssetsUtil.SetRef(runSo, "playerExperience", experience);
        MakeAssetsUtil.SetRef(runSo, "autoAttack", autoAttack);
        MakeAssetsUtil.SetRef(runSo, "swarmSpawner", spawner);
        MakeAssetsUtil.SetRef(runSo, "gemPool", gemPool);
        MakeAssetsUtil.SetRef(runSo, "bulletPool", bulletPool);
        MakeAssetsUtil.SetRef(runSo, "upgradeSystem", upgrades);
        MakeAssetsUtil.SetRef(runSo, "hud", hud.Hud);
        MakeAssetsUtil.SetRef(runSo, "bossObject", boss);
        MakeAssetsUtil.SetRef(runSo, "bossTarget", boss.GetComponent<EnemyTarget>());
        // 난이도 상향 — 보스를 더 자주, 더 오래 버티게 등장시킨다.
        MakeAssetsUtil.SetFloat(runSo, "firstBossAtSeconds", 45f);
        MakeAssetsUtil.SetFloat(runSo, "bossIntervalSeconds", 70f);
        MakeAssetsUtil.SetFloat(runSo, "bossHealth", 2200f);
        MakeAssetsUtil.SetFloat(runSo, "bossHealthGrowth", 1.75f);
        runSo.ApplyModifiedPropertiesWithoutUndo();

        // Auto Hand 리그의 손은 "LeftHand"/"RightHand"라는 이름으로 player 바로 밑에 있지 않다 —
        // AutoHandPlayer가 들고 있는 실제 참조로 가져온다. AutoHandPlayer 자신도 래퍼(player)가
        // 아니라 중첩된 자식에 있으므로 GetComponentInChildren으로 찾아야 한다.
        var autoHandPlayer = player.GetComponentInChildren<AutoHandPlayer>(true);
        Transform leftHandTransform = autoHandPlayer.handLeft.transform;
        Transform rightHandTransform = autoHandPlayer.handRight.transform;

        // VR에는 마우스가 없다 — 업그레이드 카드를 손으로 직접 짚어서 고르게 한다.
        var touch = EnsureComponent<VRUpgradeButtonTouch>(player);
        var touchSo = new SerializedObject(touch);
        MakeAssetsUtil.SetRef(touchSo, "leftHand", leftHandTransform);
        MakeAssetsUtil.SetRef(touchSo, "rightHand", rightHandTransform);
        MakeAssetsUtil.SetObjectArray(touchSo, "choiceButtons", hud.Buttons);
        touchSo.ApplyModifiedPropertiesWithoutUndo();

        // 팔을 휘둘러 이동하는 게임이니, 그 손을 빠르게 휘둘러 잡몹에 대면 그대로 근접 데미지가 된다.
        var punch = EnsureComponent<HandPunchDamage>(player);
        var punchSo = new SerializedObject(punch);
        MakeAssetsUtil.SetRef(punchSo, "leftHand", leftHandTransform);
        MakeAssetsUtil.SetRef(punchSo, "rightHand", rightHandTransform);
        punchSo.ApplyModifiedPropertiesWithoutUndo();
    }

    // ────────────────────────────── Helpers ──────────────────────────────

    private static GameObject FindByAnyName(params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            GameObject found = GameObject.Find(names[i]);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    private static T EnsureComponent<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    private static GameObject AddVisual(GameObject parent, PrimitiveType shape, Vector3 localPosition, Material material, Vector3 localScale = default)
    {
        GameObject visual = GameObject.CreatePrimitive(shape);
        visual.name = "Visual";
        visual.transform.SetParent(parent.transform, false);
        visual.transform.localPosition = localPosition;
        visual.transform.localScale = localScale == default ? Vector3.one : localScale;
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.GetComponent<MeshRenderer>().sharedMaterial = material;
        return visual;
    }

    /// <summary>Unity 내장 UI 스프라이트. Image에 스프라이트가 없으면 Filled 타입이 그려지지 않는다.</summary>
    private static Sprite UiSprite()
    {
        return AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
    }





}
#endif
