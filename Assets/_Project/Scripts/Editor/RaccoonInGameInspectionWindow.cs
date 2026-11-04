using Game.Gameplay.Debugging;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public sealed class RaccoonInGameInspectionWindow : EditorWindow
    {
        [MenuItem("Tools/Train Survival/Raccoon/In-game Inspection")]
        public static void Open() => GetWindow<RaccoonInGameInspectionWindow>("라쿤 인게임 확인");

        private void OnGUI()
        {
            EditorGUILayout.LabelField("라쿤 최신 수정본 · 실제 플레이", EditorStyles.boldLabel);
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Boot에서 플레이한 뒤 게임을 시작하세요. 실제 스폰된 플레이어를 확인합니다.", MessageType.Info);
                return;
            }
            var preview = Object.FindFirstObjectByType<RaccoonInGameInspection>();
            if (preview == null)
            {
                if (GUILayout.Button("외부 확인 시점 열기")) new GameObject("Raccoon in-game inspection (editor only)").AddComponent<RaccoonInGameInspection>();
                return;
            }
            preview.Inspect = EditorGUILayout.Toggle("외부 확인 시점", preview.Inspect);
            preview.OrbitYaw = EditorGUILayout.Slider("좌우", preview.OrbitYaw, -180f, 180f);
            preview.Pitch = EditorGUILayout.Slider("상하", preview.Pitch, -15f, 65f);
            preview.Distance = EditorGUILayout.Slider("거리", preview.Distance, 1.5f, 7f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("정면")) preview.OrbitYaw = 180f;
            if (GUILayout.Button("사선")) preview.OrbitYaw = 150f;
            if (GUILayout.Button("후면")) preview.OrbitYaw = 0f;
            EditorGUILayout.EndHorizontal();
            if (preview.Target != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(preview.Target.UseLatest ? "현재: 최신 메시 + 변형 보정" : "현재: 이전 인게임 메시");
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("이전 비교")) preview.Target.SetRevision(false);
                if (GUILayout.Button("최신 적용")) preview.Target.SetRevision(true);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("보정 정점", preview.Target.CorrectedVertices.ToString());
            }
            else EditorGUILayout.HelpBox("로컬 플레이어 스폰을 기다리는 중입니다.", MessageType.Info);
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Game 창을 클릭하면 기존 입력으로 이동·장비 사용이 가능합니다. 외부 확인 시점을 끄면 원래 1인칭으로 돌아갑니다. 이 창의 비교 선택은 저장되지 않습니다.", MessageType.Info);
        }
    }
}
