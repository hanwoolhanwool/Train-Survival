using System;
using Game.Core.Logging;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// <see cref="GameLog"/>의 카테고리 필터를 켜고 끄는 창 (<c>Game/QA/Log Categories</c>).
    /// 설정은 EditorPrefs에 남으므로 에디터를 껐다 켜도, 플레이 모드를 드나들어도 유지된다.
    /// <see cref="GameLog.Error"/>는 이 필터와 무관하게 항상 출력된다.
    /// </summary>
    public sealed class GameLogWindow : EditorWindow
    {
        private static readonly LogCategory[] Categories = BuildCategoryList();

        [MenuItem("Game/QA/Log Categories")]
        private static void Open()
        {
            GetWindow<GameLogWindow>(false, "Log Categories", true).minSize = new Vector2(260f, 320f);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("출력할 로그 카테고리", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "끈 카테고리는 콘솔에 나오지 않는다. LogError는 필터와 무관하게 항상 출력된다.\n" +
                "릴리스 빌드에서는 Info·Warn 호출 자체가 컴파일에서 제거된다.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("전부 켜기"))
                {
                    GameLog.EnableAll();
                }

                if (GUILayout.Button("전부 끄기"))
                {
                    GameLog.DisableAll();
                }
            }

            EditorGUILayout.Space();

            LogCategory enabled = GameLog.Enabled;
            foreach (LogCategory category in Categories)
            {
                bool on = (enabled & category) != LogCategory.None;
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool next = EditorGUILayout.ToggleLeft(category.ToString(), on);
                    if (next != on)
                    {
                        if (next)
                        {
                            GameLog.Enable(category);
                        }
                        else
                        {
                            GameLog.Disable(category);
                        }
                    }

                    // 한 계통만 집중해서 볼 때 — 이 카테고리만 남기고 전부 끈다.
                    if (GUILayout.Button("단독", GUILayout.Width(44f)))
                    {
                        GameLog.Only(category);
                    }
                }
            }
        }

        private static LogCategory[] BuildCategoryList()
        {
            var values = (LogCategory[])Enum.GetValues(typeof(LogCategory));
            var list = new System.Collections.Generic.List<LogCategory>(values.Length);
            foreach (LogCategory value in values)
            {
                // None(0)과 All(전 비트)은 토글 대상이 아니다 — 단일 비트만 남긴다.
                int bits = (int)value;
                if (bits != 0 && (bits & (bits - 1)) == 0)
                {
                    list.Add(value);
                }
            }

            return list.ToArray();
        }
    }
}
