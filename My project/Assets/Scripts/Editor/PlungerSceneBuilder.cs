#if UNITY_EDITOR
using System.IO;
using Autohand;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using VRGame.Core;
using VRGame.Plunger;

/// <summary>"Plunger Suction & Launch"를 GAME7 씬에 통째로 구성한다. 흡착 가능한 벽 4면 + 바닥,
/// 얼굴 흡착 판정이 달린 테스트 몬스터 1마리, Auto Hand 플레이어 리그를 코드로 만들고 연결한다.
/// HUD·점수 시스템은 이번 스코프에 없다(설계 문서 .claude/design/plunger-suction-locomotion 참고).</summary>
public static class PlungerSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/GAME7 plunger.unity";
    private const string PrefabFolder = "Assets/Prefabs/Plunger";
    private const string MaterialFolder = "Assets/Materials/Plunger";

    [MenuItem("Tools/Plunger/Build Game")]
    public static void BuildGame()
    {
        MakeAssetsUtil.EnsureFolder(PrefabFolder);
        MakeAssetsUtil.EnsureFolder(MaterialFolder);

        int stickableLayer = MakeAssetsUtil.ResolveLayer("Plunger", "Stickable");

        if (SceneManager.GetActiveScene().isDirty)
        {
            EditorSceneManager.SaveOpenScenes();
        }
        Scene scene = OpenOrCreateScene();

        MakeAssetsUtil.RemoveDefaultCamera();
        MakeAssetsUtil.ClearPreviousBuild("Ground", "Walls", "Monster", "Player", "XRPlayer", "NavMeshSurface");

        BuildArena(stickableLayer);
        BakeNavMesh();

        GameObject player = BuildPlayer(stickableLayer, out PlungerLocomotion locomotion);
        BuildMonster(stickableLayer, player.transform, locomotion);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[Plunger] GAME7 씬 구성 완료. Stickable 레이어={stickableLayer}. " +
                  "양손 트리거로 벽/몬스터 얼굴에 흡착 → 몸을 뒤로 빼서 당김 → 놓으면 발사됩니다. ▶ Play 하세요.");
    }

    private static Scene OpenOrCreateScene()
    {
        if (File.Exists(ScenePath))
        {
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        Scene created = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EditorSceneManager.SaveScene(created, ScenePath);
        return created;
    }

    // ────────────────────────────── Environment ──────────────────────────────

    private static void BuildArena(int stickableLayer)
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.layer = stickableLayer;
        ground.transform.position = new Vector3(0f, -0.5f, 0f);
        ground.transform.localScale = new Vector3(40f, 1f, 40f);
        ground.GetComponent<MeshRenderer>().sharedMaterial =
            MakeAssetsUtil.MakeMaterial(MaterialFolder, "GroundMat", new Color(0.2f, 0.22f, 0.2f));

        var wallsRoot = new GameObject("Walls").transform;
        Material wallMaterial = MakeAssetsUtil.MakeMaterial(MaterialFolder, "WallMat", new Color(0.35f, 0.3f, 0.25f));

        BuildWall(wallsRoot, new Vector3(0f, 5f, 20f), Vector3.zero, new Vector3(30f, 10f, 1f), wallMaterial, stickableLayer);
        BuildWall(wallsRoot, new Vector3(0f, 5f, -20f), Vector3.zero, new Vector3(30f, 10f, 1f), wallMaterial, stickableLayer);
        BuildWall(wallsRoot, new Vector3(20f, 5f, 0f), new Vector3(0f, 90f, 0f), new Vector3(40f, 10f, 1f), wallMaterial, stickableLayer);
        BuildWall(wallsRoot, new Vector3(-20f, 5f, 0f), new Vector3(0f, 90f, 0f), new Vector3(40f, 10f, 1f), wallMaterial, stickableLayer);
    }

    private static void BuildWall(Transform parent, Vector3 position, Vector3 eulerRotation, Vector3 scale,
        Material material, int layer)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.layer = layer;
        wall.transform.SetParent(parent, false);
        wall.transform.position = position;
        wall.transform.eulerAngles = eulerRotation;
        wall.transform.localScale = scale;
        wall.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    /// <summary>PlungerMonster가 NavMeshAgent로 추적하려면 베이크된 NavMesh가 필요하다 —
    /// Echo/HoseCleaner와 동일한 방식.</summary>
    private static void BakeNavMesh()
    {
        var surfaceGo = new GameObject("NavMeshSurface");
        var surface = surfaceGo.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.BuildNavMesh();
    }

    // ────────────────────────────── Player ──────────────────────────────

    private static GameObject BuildPlayer(int stickableLayer, out PlungerLocomotion locomotion)
    {
        GameObject player = AutoHandRigFactory.Spawn("Plunger", "Player", new Vector3(0f, 1f, -15f),
            disableBuiltinMovement: true, out AutoHandPlayer autoHandPlayer, out Rigidbody body);

        Transform head = autoHandPlayer.headCamera.transform;
        Transform leftHand = autoHandPlayer.handLeft.transform;
        Transform rightHand = autoHandPlayer.handRight.transform;

        // [RequireComponent(typeof(Rigidbody))] — Rigidbody는 래퍼(player)가 아니라
        // autoHandPlayer.gameObject에 있으므로 반드시 거기 붙여야 그 Rigidbody를 재사용한다.
        locomotion = autoHandPlayer.gameObject.AddComponent<PlungerLocomotion>();
        var locomotionSo = new SerializedObject(locomotion);
        MakeAssetsUtil.SetRef(locomotionSo, "leftHandOrigin", leftHand);
        MakeAssetsUtil.SetRef(locomotionSo, "rightHandOrigin", rightHand);
        MakeAssetsUtil.SetLayerMask(locomotionSo, "stickableMask", 1 << stickableLayer);
        locomotionSo.ApplyModifiedPropertiesWithoutUndo();

        // 아트(오디오 클립)가 아직 없어 소스만 배선해둔다 — 클립을 비워도 PlungerFeedback은
        // null 체크 후 조용히 스킵하므로 안전하다.
        AudioSource leftAudio = leftHand.gameObject.AddComponent<AudioSource>();
        leftAudio.playOnAwake = false;
        AudioSource rightAudio = rightHand.gameObject.AddComponent<AudioSource>();
        rightAudio.playOnAwake = false;

        var feedback = player.AddComponent<PlungerFeedback>();
        var feedbackSo = new SerializedObject(feedback);
        MakeAssetsUtil.SetRef(feedbackSo, "leftHandAudio", leftAudio);
        MakeAssetsUtil.SetRef(feedbackSo, "rightHandAudio", rightAudio);
        feedbackSo.ApplyModifiedPropertiesWithoutUndo();
        feedback.Bind(locomotion);

        // root는 래퍼(player)가 아니라 autoHandPlayer.gameObject다 — PlungerLocomotion이 실제로
        // 그 오브젝트의 Rigidbody를 움직이므로, 래퍼를 넣으면 Position/Teleport가 스폰 위치에 얼어붙는다.
        var playerRig = player.AddComponent<PlayerRig>();
        var rigSo = new SerializedObject(playerRig);
        MakeAssetsUtil.SetRef(rigSo, "root", autoHandPlayer.transform);
        MakeAssetsUtil.SetRef(rigSo, "head", head);
        MakeAssetsUtil.SetRef(rigSo, "leftHand", leftHand);
        MakeAssetsUtil.SetRef(rigSo, "rightHand", rightHand);
        MakeAssetsUtil.SetRef(rigSo, "body", body);
        rigSo.ApplyModifiedPropertiesWithoutUndo();

        return player;
    }

    // ────────────────────────────── Monster ──────────────────────────────

    private static void BuildMonster(int stickableLayer, Transform player, PlungerLocomotion locomotion)
    {
        var monsterGo = new GameObject("Monster");
        monsterGo.transform.position = new Vector3(0f, 1f, 10f);

        // PlungerMonster에 [RequireComponent(typeof(NavMeshAgent))]가 있어 AddComponent 시 자동으로
        // 같이 붙는다 — 따로 AddComponent<NavMeshAgent>를 호출하면 중복 추가 경고가 난다.
        var monster = monsterGo.AddComponent<PlungerMonster>();
        var agent = monsterGo.GetComponent<NavMeshAgent>();
        agent.radius = 0.5f;
        agent.height = 2f;

        var monsterSo = new SerializedObject(monster);
        MakeAssetsUtil.SetRef(monsterSo, "target", player);
        monsterSo.ApplyModifiedPropertiesWithoutUndo();

        var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Visual";
        visual.transform.SetParent(monsterGo.transform, false);
        visual.transform.localPosition = new Vector3(0f, 1f, 0f);
        Object.DestroyImmediate(visual.GetComponent<Collider>()); // 충돌은 아래 Face 콜라이더만으로 판정한다.
        visual.GetComponent<MeshRenderer>().sharedMaterial =
            MakeAssetsUtil.MakeMaterial(MaterialFolder, "MonsterMat", new Color(0.5f, 0.08f, 0.08f));

        // 얼굴(흡착 판정용) — 별도 콜라이더를 앞머리에 둬서 플런저 레이가 정확히 여기를 맞아야
        // MonsterFaceStick이 반응한다. Stickable 레이어에 둬야 PlungerLocomotion의 레이캐스트가 잡는다.
        var faceGo = new GameObject("Face", typeof(SphereCollider));
        faceGo.layer = stickableLayer;
        faceGo.transform.SetParent(monsterGo.transform, false);
        faceGo.transform.localPosition = new Vector3(0f, 1.6f, 0.4f);
        faceGo.GetComponent<SphereCollider>().radius = 0.25f;

        var faceStick = faceGo.AddComponent<MonsterFaceStick>();
        var faceStickSo = new SerializedObject(faceStick);
        MakeAssetsUtil.SetRef(faceStickSo, "monster", monster);
        faceStickSo.ApplyModifiedPropertiesWithoutUndo();
        faceStick.Bind(locomotion);
    }
}
#endif
