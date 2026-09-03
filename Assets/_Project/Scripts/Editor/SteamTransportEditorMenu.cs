using Game.Systems.Networking;
using UnityEditor;
using UnityEngine;

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

        /// <summary>
        /// <b>배치 모드에서는 체크 표시를 건드리지 않는다 — 지우면 CI가 다시 죽는다.</b>
        ///
        /// <para><see cref="Menu.SetChecked"/>는 메뉴 UI에 체크를 그리는 API인데 배치 모드에는
        /// 그릴 메뉴가 없다. 그런데도 부르면 <b>메뉴 명령 목록을 재구축하는 도중에 그 목록을
        /// 조회</b>하게 되고, CI(Linux · 6000.5.3f1 · batchmode)에서 PlayMode 진입 시
        /// <c>MenuController::GetChecked</c> → <c>DoFindItem</c>에서 세그폴트(signo:11)가 난다.
        /// 2026-08-31 ~ 09-02 사이 CI 5회 연속 실패의 원인 후보이며, 스택은
        /// <c>EnterPlayMode</c> → <c>FinalizeReload</c> → <c>ScriptCommands::Rebuild</c>로 이어진다
        /// (자동화 1차 구현 계획 §1.2).</para>
        /// </summary>
        [MenuItem(MenuPath, true)]
        private static bool Validate()
        {
            if (!Application.isBatchMode)
            {
                Menu.SetChecked(MenuPath, EditorPrefs.GetBool(ActiveTransportMode.EditorPrefsKey, false));
            }

            return true;
        }
    }
}
