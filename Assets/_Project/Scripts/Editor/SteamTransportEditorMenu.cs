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

        // ── validate 함수를 의도적으로 두지 않는다 (2026-09-03 · 진단 중) ──
        //
        // 원래 여기에 [MenuItem(MenuPath, true)] Validate()가 있었고 Menu.SetChecked 로 체크 표시를
        // 갱신했다. CI(Linux · 6000.5.3f1 · batchmode)가 PlayMode 진입에서 세그폴트(signo:11)로
        // 죽는데, 스택이 ScriptCommands::Rebuild() → MenuController::GetChecked() → DoFindItem 이다.
        // 체크 상태를 쓰는 메뉴는 이 프로젝트에서 이 항목 하나뿐이라, validate 등록 자체가
        // 방아쇠인지 확인하려고 함수를 걷어냈다.
        //
        // 배치 모드 가드(5d03c0e)로는 멈추지 않았다 — 그것은 SetChecked "호출"만 막았고
        // validate 함수의 등록은 그대로였다. 원인이 확정되면 체크 표시를 어떤 방식으로
        // 되살릴지 정한다 (자동화 1차 구현 계획 §2 결정 ①).
    }
}
