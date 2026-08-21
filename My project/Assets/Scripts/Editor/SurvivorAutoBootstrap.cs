#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>씬 구성을 자동으로 한 번만 실행하는 부트스트랩. 사용자가 메뉴를 직접 누르지 않아도
/// 에디터가 스크립트를 로드하는 시점에 SurvivorSceneBuilder를 돌려 플레이 가능한 상태로 만든다.
///
/// 한 번 실행되면 EditorPrefs에 표시를 남겨 다시는 동작하지 않는다. 즉 매 도메인 리로드마다
/// 씬을 덮어쓰지 않는다. 역할을 다한 뒤에는 이 파일을 삭제해도 무방하며, 다시 구성하고 싶으면
/// Tools > Survivor > Build Game 메뉴를 쓰면 된다.</summary>
public static class SurvivorAutoBootstrap
{
    private const string DoneKey = "VRGame.Survivor.AutoBootstrapDone.v1";

    [InitializeOnLoadMethod]
    private static void OnLoad()
    {
        if (EditorPrefs.GetBool(DoneKey, false))
        {
            return;
        }

        // 도메인 리로드 도중에는 에셋 데이터베이스가 아직 준비되지 않았을 수 있다. 한 틱 미룬다.
        EditorApplication.delayCall += RunOnce;
    }

    private static void RunOnce()
    {
        if (EditorPrefs.GetBool(DoneKey, false))
        {
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            // 플레이 중이거나 아직 컴파일 중이면 씬을 건드리지 않는다. 다음 로드 때 다시 시도한다.
            return;
        }

        EditorPrefs.SetBool(DoneKey, true);

        try
        {
            SurvivorSceneBuilder.BuildGame();
            Debug.Log("[Survivor] 자동 구성 완료 — ▶ Play 를 누르고 WASD 로 이동하세요.");
        }
        catch (System.Exception exception)
        {
            // 도메인 리로드 중 예외가 새어나가면 에디터 초기화가 깨진다. 로그로만 남긴다.
            Debug.LogError($"[Survivor] 자동 구성 실패: {exception}");
        }
    }

    [MenuItem("Tools/Survivor/Reset Auto Bootstrap Flag")]
    private static void ResetFlag()
    {
        EditorPrefs.DeleteKey(DoneKey);
        Debug.Log("[Survivor] 부트스트랩 플래그를 초기화했습니다. 다음 스크립트 리로드 때 다시 구성됩니다.");
    }
}
#endif
