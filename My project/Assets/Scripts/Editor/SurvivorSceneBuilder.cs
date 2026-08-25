#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
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

        GameObject arena = SetupArena();
        GameObject player = SetupPlayer();
        GameObject boss = SetupBoss();
        GameObject manager = SetupManager();
        SetupCamera(player.transform);
        EnsureEventSystem();

        HudRefs hud = BuildUI();

        WireEverything(player, boss, manager, hud, enemyPrefab, projectilePrefab, gemPrefab, danmakuPrefab);
        HideLeftoverObjects();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Survivor] 구성 완료 — 아레나 {arena.name}, 플레이 버튼을 눌러 WASD로 이동하세요.");
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
        arena.transform.localScale = new Vector3(6f, 1f, 6f); // Plane 기본 10x10 → 60x60

        var renderer = arena.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "ArenaMat", new Color(0.16f, 0.17f, 0.2f));
        }

        return arena;
    }

    private static GameObject SetupPlayer()
    {
        GameObject player = FindByAnyName("Player") ?? new GameObject("Player");
        player.name = "Player";

        EnsureComponent<PlayerStats>(player);
        EnsureComponent<PlayerHealth>(player);
        EnsureComponent<PlayerExperience>(player);
        EnsureComponent<AutoAttackWeapon>(player);
        EnsureComponent<PlaceholderPlayerController>(player);

        // 사용자가 이미 메쉬를 붙여 놨으면 그대로 쓰고, 바닥에 걸치도록 높이만 맞춘다.
        bool hasOwnVisual = player.GetComponent<MeshRenderer>() != null;
        if (hasOwnVisual)
        {
            player.transform.position = new Vector3(0f, 0.5f, 0f);
            player.GetComponent<MeshRenderer>().sharedMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "PlayerMat", new Color(0.25f, 0.6f, 1f));
        }
        else
        {
            player.transform.position = Vector3.zero;
            if (player.transform.Find("Visual") == null)
            {
                AddVisual(player, PrimitiveType.Capsule, new Vector3(0f, 1f, 0f), MakeAssetsUtil.MakeMaterial(MaterialFolder, "PlayerMat", new Color(0.25f, 0.6f, 1f)));
            }
        }

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

        // 게임 시스템이 참조할 유일한 플레이어 창구. 이 게임은 Transform 이동이라 body는 비운다 —
        // 나중에 물리 기반 로코모션으로 바꾸면 body만 채우면 되고 게임 코드는 안 바뀐다.
        var playerRig = EnsureComponent<PlayerRig>(player);
        var rigSo = new SerializedObject(playerRig);
        MakeAssetsUtil.SetRef(rigSo, "root", player.transform);
        // 탑다운 게임이라 머리·손 개념이 없다. 비워 두는 것이 가짜 참조를 넣는 것보다 정직하다.
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

    private static void SetupCamera(Transform player)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            camera = go.AddComponent<Camera>();
        }

        camera.transform.SetParent(null);
        var follow = EnsureComponent<TopDownCameraFollow>(camera.gameObject);
        var so = new SerializedObject(follow);
        MakeAssetsUtil.SetRef(so, "target", player);
        so.ApplyModifiedPropertiesWithoutUndo();
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

    private static HudRefs BuildUI()
    {
        GameObject existing = GameObject.Find("SurvivorCanvas");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        var canvasGo = new GameObject("SurvivorCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
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
        runSo.ApplyModifiedPropertiesWithoutUndo();
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
