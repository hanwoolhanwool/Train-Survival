using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Multiplayer Tools(Network Simulator) 숨김 헬퍼 오브젝트 정리 워크어라운드.
    ///
    /// 패키지의 RuntimeUpdater는 "[RuntimeUpdaterBehaviour]" GameObject를
    /// HideAndDontSave + DontDestroyOnLoad로 만들고, 플레이 종료 시 GameObject가 아니라
    /// 컴포넌트만 파괴한다 (com.unity.multiplayer.tools 2.2.9, RuntimeUpdaterBehaviour.cs).
    /// 남은 숨김 오브젝트가 에디터 teardown에서 오브젝트당 1회씩
    /// "Assertion failed on expression: 't.GetParent() == nullptr'"를 출력한다.
    ///
    /// 플레이 종료 직전에 해당 오브젝트를 GameObject째 파괴해 어설션을 막는다.
    /// 패키지가 GameObject를 파괴하도록 수정되면 이 파일은 제거한다.
    /// </summary>
    [InitializeOnLoad]
    internal static class MultiplayerToolsHiddenUpdaterCleanup
    {
        private const string HiddenUpdaterName = "[RuntimeUpdaterBehaviour]";

        static MultiplayerToolsHiddenUpdaterCleanup()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingPlayMode)
            {
                return;
            }

            // HideAndDontSave 오브젝트는 씬 루트 열거에 잡히지 않아 전체 로드 오브젝트에서 찾는다.
            GameObject[] all = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < all.Length; i++)
            {
                GameObject candidate = all[i];
                if (candidate.name == HiddenUpdaterName &&
                    (candidate.hideFlags & HideFlags.HideAndDontSave) == HideFlags.HideAndDontSave)
                {
                    // 지연 Destroy는 teardown 전에 flush되지 않으므로 즉시 파괴한다.
                    // 패키지 측의 후속 Destroy(m_Component)는 파괴된 참조에 대한 no-op이라 안전하다.
                    Object.DestroyImmediate(candidate);
                }
            }
        }
    }
}
