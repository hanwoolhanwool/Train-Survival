using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 한글 폰트 아틀라스 정비 — <b>구워 둔 글리프를 비우고, 소스 TTF가 필요한 글자를 전부
    /// 공급할 수 있는지 검증</b>한다.
    ///
    /// <para><b>왜 비우는가.</b> 이 폰트 에셋은 <c>Atlas Population Mode: Dynamic</c>이라
    /// 런타임에 필요한 글자를 소스 TTF에서 알아서 굽는다. 그래서 에셋에 구워 둔 글리프는
    /// <b>필수 데이터가 아니라 캐시</b>다. 그런데 그 캐시가 커밋되면서 문제가 생겼다 —
    /// TMP가 빌드·재직렬화 시점에 동적 데이터를 지우고(<c>m_ClearDynamicDataOnBuild: 1</c>)
    /// 플레이 중에는 새 글자를 채워 넣어, <b>같은 파일이 계속 오간다.</b>
    /// 실제로 이 파일은 커밋 14회 · 저장소 이력 47 MB를 썼고, 그중 두 번은 "다시 굽는다"였다.</para>
    ///
    /// <para><b>비워도 되는 근거</b>는 적중률이다 — 게임이 쓰는 한글 약 757자 중 구워져 있던 것은
    /// 108자(14 %)뿐이고, 나머지 649자는 이미 런타임에 굽고 있었다. 즉 캐시를 지워도
    /// 달라지는 것은 그 14 %의 첫 표시 시점뿐이다.</para>
    ///
    /// <para>출력은 명령에 대한 응답이라 카테고리 필터에 걸리면 안 되므로
    /// 규약(architecture-rules.md §3 "예외 둘")에 따라 <see cref="Debug"/>를 그대로 쓴다.</para>
    /// </summary>
    public static class FontAtlasMaintenance
    {
        private const string FontAssetPath = "Assets/_Project/Art/Fonts/F_NotoSansKR_SDF.asset";

        /// <summary>검증할 글자 목록 파일 경로를 넘기는 인자 (CLI).</summary>
        private const string CharsetArgument = "-charsetFile";

        [MenuItem("Game/QA/Font/아틀라스 비우기")]
        public static void ClearAtlas()
        {
            TMP_FontAsset font = Load();
            if (font == null)
            {
                return;
            }

            int before = font.characterTable.Count;
            font.ClearFontAssetData(true);

            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();

            Debug.Log($"[FontAtlas] 아틀라스를 비웠다 — 글자 {before} → {font.characterTable.Count}. " +
                      "런타임이 소스 TTF에서 필요한 글자를 굽는다.");
        }

        /// <summary>
        /// 소스 TTF 가 지정한 글자를 전부 공급할 수 있는지 확인한다.
        /// <b>확인만 하고 아틀라스는 다시 비운다</b> — 캐시를 남기지 않는 것이 이 정비의 목적이다.
        /// </summary>
        [MenuItem("Game/QA/Font/공급 가능 여부 검증")]
        public static void VerifyCoverage()
        {
            string charsetPath = GetCommandLineArg(CharsetArgument);
            if (string.IsNullOrEmpty(charsetPath))
            {
                charsetPath = EditorUtility.OpenFilePanel("검증할 글자 목록", string.Empty, "txt");
            }

            if (string.IsNullOrEmpty(charsetPath) || !File.Exists(charsetPath))
            {
                Debug.LogError($"[FontAtlas] 글자 목록 파일이 없다 — {charsetPath}");
                return;
            }

            TMP_FontAsset font = Load();
            if (font == null)
            {
                return;
            }

            string charset = File.ReadAllText(charsetPath, Encoding.UTF8).Trim();
            Debug.Log($"[FontAtlas] 검증 시작 — 글자 {charset.Length}자 · 아틀라스 현재 {font.characterTable.Count}자");

            font.ClearFontAssetData(true);

            bool all = font.TryAddCharacters(charset, out string missing);
            int added = font.characterTable.Count;

            if (all && string.IsNullOrEmpty(missing))
            {
                Debug.Log($"[FontAtlas] 통과 — 소스 TTF 가 {charset.Length}자를 전부 공급한다 (구워진 글자 {added}자). " +
                          "동적 모드에서 런타임 표시가 보장된다.");
            }
            else
            {
                Debug.LogError($"[FontAtlas] 실패 — 공급 못 하는 글자 {missing?.Length ?? 0}자: {missing}");
            }

            // 검증 때문에 구워진 글리프를 남기면 이 정비의 목적이 사라진다.
            font.ClearFontAssetData(true);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssets();

            Debug.Log($"[FontAtlas] 아틀라스를 다시 비웠다 — 글자 {font.characterTable.Count}자.");
        }

        private static TMP_FontAsset Load()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (font == null)
            {
                Debug.LogError($"[FontAtlas] 폰트 에셋을 찾지 못했다 — {FontAssetPath}");
            }

            return font;
        }

        private static string GetCommandLineArg(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
