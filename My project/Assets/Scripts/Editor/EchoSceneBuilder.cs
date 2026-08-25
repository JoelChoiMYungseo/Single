#if UNITY_EDITOR
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRGame.Core;
using VRGame.Echo;

/// <summary>"Scream in the Dark"를 GAME5 씬에 통째로 구성한다. 어두운 미로, URP Volume 기반
/// 실명 연출, 마이크로 반응하는 몬스터, 플레이어 리그를 코드로 만들고 연결한 뒤 씬을 저장한다.</summary>
public static class EchoSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/GAME5 eho.unity";
    private const string PrefabFolder = "Assets/Prefabs/Echo";
    private const string MaterialFolder = "Assets/Materials/Echo";
    private const string VolumeProfilePath = PrefabFolder + "/EcholocationProfile.asset";

    [MenuItem("Tools/Echo/Build GAME5 Scene")]
    public static void BuildScene()
    {
        MakeAssetsUtil.EnsureFolder(PrefabFolder);
        MakeAssetsUtil.EnsureFolder(MaterialFolder);

        if (SceneManager.GetActiveScene().isDirty)
        {
            EditorSceneManager.SaveOpenScenes();
        }
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        MakeAssetsUtil.RemoveDefaultCamera();
        BuildMaze();
        BakeNavMesh();

        GameObject rig = BuildPlayerRig(out PlayerRig playerRig, out Transform head,
            out MicInputListener micListener, out Transform startPoint);

        GameObject monster = BuildMonster(rig.transform, micListener);
        BuildEcholocationVolume(micListener);
        EchoHUD hud = BuildHud(head);
        BuildRunManager(playerRig, micListener, monster.GetComponent<SoundAttractedAI>(), startPoint, hud);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log("[Echo] GAME5 씬 구성 완료. 마이크 권한을 허용하고 ▶ Play 후 소리를 질러 보세요.");
    }


    // ────────────────────────────── Environment ──────────────────────────────

    /// <summary>바깥 경계 + 내부 파티션 벽 몇 개로 이뤄진 느슨한 미로. 완전히 어두운 상태에서 길을
    /// 잃게 만드는 것이 목적이라, 복잡한 미로 생성 알고리즘 대신 손으로 배치한 몇 개의 벽으로
    /// 사각지대와 굽은 통로만 만들면 충분하다.</summary>
    private static void BuildMaze()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(34f, 1f, 34f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "GroundMat", new Color(0.08f, 0.08f, 0.09f));

        var wallsRoot = new GameObject("Walls").transform;
        Material wallMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "WallMat", new Color(0.12f, 0.12f, 0.14f));

        // 바깥 경계 4면.
        BuildWall(wallsRoot, new Vector3(0f, 2f, 17f), new Vector3(34f, 4f, 1f), wallMaterial);
        BuildWall(wallsRoot, new Vector3(0f, 2f, -17f), new Vector3(34f, 4f, 1f), wallMaterial);
        BuildWall(wallsRoot, new Vector3(17f, 2f, 0f), new Vector3(1f, 4f, 34f), wallMaterial);
        BuildWall(wallsRoot, new Vector3(-17f, 2f, 0f), new Vector3(1f, 4f, 34f), wallMaterial);

        // 내부 파티션 — 사각지대와 굽은 통로를 만드는 최소 배치.
        BuildWall(wallsRoot, new Vector3(-6f, 2f, 6f), new Vector3(1f, 4f, 16f), wallMaterial);
        BuildWall(wallsRoot, new Vector3(6f, 2f, -4f), new Vector3(1f, 4f, 18f), wallMaterial);
        BuildWall(wallsRoot, new Vector3(0f, 2f, 10f), new Vector3(14f, 4f, 1f), wallMaterial);
        BuildWall(wallsRoot, new Vector3(-2f, 2f, -10f), new Vector3(18f, 4f, 1f), wallMaterial);

        if (GameObject.Find("Directional Light") == null)
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.4f; // 어차피 Vignette/노출로 눌러버리지만, 밝을 때도 은은하게.
            lightGo.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
        }
    }

    private static void BuildWall(Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.transform.SetParent(parent, false);
        wall.transform.position = position;
        wall.transform.localScale = scale;
        wall.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    /// <summary>이 프로젝트에서 실제 NavMeshAgent를 쓰는 첫 게임이라 베이크가 필요하다. 다른 4개
    /// 게임의 적들은 전부 Transform 직접 이동이라 이 단계가 없었다.</summary>
    private static void BakeNavMesh()
    {
        var surfaceGo = new GameObject("NavMeshSurface");
        var surface = surfaceGo.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.BuildNavMesh();
    }

    // ────────────────────────────── Player ──────────────────────────────

    private static GameObject BuildPlayerRig(out PlayerRig playerRig, out Transform head,
        out MicInputListener micListener, out Transform startPoint)
    {
        var rig = new GameObject("PlayerRig");
        rig.transform.position = new Vector3(0f, 1f, -12f);

        var walk = rig.AddComponent<PlaceholderWalkRig>(); // Rigidbody 자동 부착
        var body = rig.GetComponent<Rigidbody>();
        body.mass = 70f;

        var bodyCollider = rig.AddComponent<CapsuleCollider>();
        bodyCollider.radius = 0.3f;
        bodyCollider.height = 1.7f;
        bodyCollider.center = new Vector3(0f, 0.85f, 0f);

        head = BuildHead(rig.transform);

        var walkSo = new SerializedObject(walk);
        MakeAssetsUtil.SetRef(walkSo, "head", head);
        walkSo.ApplyModifiedPropertiesWithoutUndo();

        micListener = rig.AddComponent<MicInputListener>();

        playerRig = rig.AddComponent<PlayerRig>();
        var rigSo = new SerializedObject(playerRig);
        MakeAssetsUtil.SetRef(rigSo, "root", rig.transform);
        MakeAssetsUtil.SetRef(rigSo, "head", head);
        MakeAssetsUtil.SetRef(rigSo, "body", body);
        rigSo.ApplyModifiedPropertiesWithoutUndo();

        var startGo = new GameObject("StartPoint");
        startGo.transform.position = rig.transform.position;
        startPoint = startGo.transform;

        return rig;
    }

    private static Transform BuildHead(Transform parent)
    {
        var head = new GameObject("Head") { tag = "MainCamera" };
        head.transform.SetParent(parent, false);
        head.transform.localPosition = new Vector3(0f, 1.6f, 0f);

        var camera = head.AddComponent<Camera>();
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 300f;
        head.AddComponent<AudioListener>();

        return head.transform;
    }

    // ────────────────────────────── Monster ──────────────────────────────

    private static GameObject BuildMonster(Transform player, MicInputListener micListener)
    {
        var monster = new GameObject("SoundAttractedMonster");
        monster.transform.position = new Vector3(8f, 1f, 8f);

        var agent = monster.AddComponent<NavMeshAgent>();
        agent.radius = 0.4f;
        agent.height = 2f;

        var ai = monster.AddComponent<SoundAttractedAI>();

        var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Visual";
        visual.transform.SetParent(monster.transform, false);
        visual.transform.localPosition = new Vector3(0f, 1f, 0f);
        Object.DestroyImmediate(visual.GetComponent<Collider>()); // 충돌은 NavMeshAgent의 이동만으로 충분.
        visual.GetComponent<MeshRenderer>().sharedMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "MonsterMat", new Color(0.5f, 0.05f, 0.08f));

        var so = new SerializedObject(ai);
        MakeAssetsUtil.SetRef(so, "micListener", micListener);
        MakeAssetsUtil.SetRef(so, "player", player);
        so.ApplyModifiedPropertiesWithoutUndo();

        return monster;
    }

    // ────────────────────────────── Volume ──────────────────────────────

    private static void BuildEcholocationVolume(MicInputListener micListener)
    {
        VolumeProfile profile = BuildVolumeProfile();

        var volumeGo = new GameObject("EcholocationVolume");
        var volume = volumeGo.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.weight = 1f;
        // sharedProfile에 넣어야 에셋 참조가 씬에 영구 저장된다. profile(런타임 전용 getter/setter)에
        // 넣으면 에디터에서 씬을 저장해도 유실되고, Play 진입 시 빈 프로필로 새로 생성돼 버린다.
        volume.sharedProfile = profile;

        var visuals = volumeGo.AddComponent<EcholocationVisuals>();
        var so = new SerializedObject(visuals);
        MakeAssetsUtil.SetRef(so, "micListener", micListener);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Vignette + ColorAdjustments 오버라이드만 담은 최소 프로필을 애셋으로 저장한다.
    /// EcholocationVisuals가 런타임에 두 파라미터의 값을 매 프레임 덮어쓴다.</summary>
    private static VolumeProfile BuildVolumeProfile()
    {
        var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        if (existing != null)
        {
            return existing;
        }

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.Add<Vignette>(true);
        profile.Add<ColorAdjustments>(true);

        AssetDatabase.CreateAsset(profile, VolumeProfilePath);
        return profile;
    }

    // ────────────────────────────── HUD ──────────────────────────────

    private static EchoHUD BuildHud(Transform head)
    {
        var canvasGo = new GameObject("EchoHUD", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(head, false);
        canvasGo.transform.localPosition = new Vector3(0f, -0.32f, 1.2f);
        canvasGo.transform.localScale = Vector3.one * 0.0015f;
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

        var rect = canvasGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(800f, 300f);

        canvasGo.AddComponent<VRGame.Survivor.RuntimeKoreanFont>(); // 내장 폰트에 한글 글리프가 없다

        var hud = canvasGo.AddComponent<EchoHUD>();

        Image fill = CreateBar(rect, "LoudnessBar", new Vector2(0f, -20f), new Vector2(560f, 26f));
        Text warning = MakeAssetsUtil.CreateText(rect, "WarningLabel", string.Empty, 36, TextAnchor.UpperCenter,
            new Vector2(0.5f, 1f), new Vector2(0f, -70f), color: new Color(1f, 0.3f, 0.25f));

        var so = new SerializedObject(hud);
        MakeAssetsUtil.SetRef(so, "loudnessFill", fill);
        MakeAssetsUtil.SetRef(so, "warningLabel", warning);
        so.ApplyModifiedPropertiesWithoutUndo();

        return hud;
    }

    private static Image CreateBar(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
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
        bgImage.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);
        bgImage.raycastTarget = false;

        var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(background.transform, false);

        var fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);

        var fillImage = fill.GetComponent<Image>();
        fillImage.color = new Color(0.35f, 0.85f, 1f);
        fillImage.raycastTarget = false;
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImage.fillAmount = 0f;

        return fillImage;
    }


    // ────────────────────────────── Run manager ──────────────────────────────

    private static void BuildRunManager(PlayerRig playerRig, MicInputListener micListener, SoundAttractedAI monster,
        Transform startPoint, EchoHUD hud)
    {
        var managerGo = new GameObject("EchoRunManager");
        var run = managerGo.AddComponent<EchoRunManager>();

        var so = new SerializedObject(run);
        MakeAssetsUtil.SetRef(so, "player", playerRig);
        MakeAssetsUtil.SetRef(so, "micListener", micListener);
        MakeAssetsUtil.SetRef(so, "monster", monster);
        MakeAssetsUtil.SetRef(so, "startPoint", startPoint);
        MakeAssetsUtil.SetRef(so, "hud", hud);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ────────────────────────────── Helpers ──────────────────────────────

}
#endif
