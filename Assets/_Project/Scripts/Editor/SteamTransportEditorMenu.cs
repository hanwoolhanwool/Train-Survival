using Game.Systems.Networking;
using UnityEditor;

namespace Game.Editor
{
    /// <summary>
    /// 에디터 트랜스포트 토글 (M6 2차 결정 ②) — 켜면 이 에디터의 다음 플레이가 Steam 릴레이
    /// 모드로 뜬다 (EditorPrefs — 에디터 인스턴스별이라 MPPM 가상 플레이어에는 영향 없음).
    /// </summary>
    public static class SteamTransportEditorMenu
    {
        private const string MenuPath = "Game/Steam Transport (Editor)";

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            bool next = !EditorPrefs.GetBool(ActiveTransportMode.EditorPrefsKey, false);
            EditorPrefs.SetBool(ActiveTransportMode.EditorPrefsKey, next);
        }

        [MenuItem(MenuPath, true)]
        private static bool Validate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(ActiveTransportMode.EditorPrefsKey, false));
            return true;
        }
    }
}
