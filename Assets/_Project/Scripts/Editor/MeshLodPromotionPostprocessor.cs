using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 원본이 과하게 촘촘한 모델의 <b>LOD0을 하위 단계로 갈아 끼운다</b> — 근거리 비용은
    /// LOD0이 정하는데, 에셋랩에서 보면 몇몇 에셋은 낮은 단계와 화면상 구분이 되지 않는다
    /// (docs/plans/features/에셋랩-씬-계획.md §7 결정 ⑥).
    ///
    /// <para><b>왜 Blender로 다시 굽지 않는가</b> — Unity가 임포트할 때 이미 감축본을 만들어
    /// 두었고, 그 결과물이 <b>눈으로 검증된 바로 그 메시</b>다. 같은 품질을 얻자고 밖에서
    /// 다시 깎으면 결과가 달라지고 guid·머티리얼·UV가 함께 흔들린다.</para>
    ///
    /// <para><b>인덱스 버퍼는 그대로 두고 범위만 옮긴다</b> — Mesh LOD는 한 인덱스 버퍼에
    /// 단계별 구간을 나란히 담는다(LOD0 0~7500 · LOD1 7500~11334 …). <see cref="Mesh.SetLods"/>로
    /// 시작 단계를 뒤로 밀면 <b>그려지는 삼각형</b>이 줄어든다. 버퍼 자체는 남으므로 메모리는
    /// 그대로다 — 목표가 렌더 비용이라 1차에서는 이 교환을 받아들인다.</para>
    /// </summary>
    public sealed class MeshLodPromotionPostprocessor : AssetPostprocessor
    {
        /// <summary>승격 대상 — 경로와 "새 LOD0으로 삼을 단계".</summary>
        private struct Promotion
        {
            public Promotion(string path, int level, int keepLevels = 0)
            {
                Path = path;
                Level = level;
                KeepLevels = keepLevels;
            }

            public string Path { get; }

            /// <summary>이 단계를 새 LOD0으로 쓴다. 1이면 LOD1이 LOD0이 된다.</summary>
            public int Level { get; }

            /// <summary>
            /// 승격 후 남길 단계 수 — 0이면 남는 것을 모두 쓴다. 1이면 <b>하위 단계를 버린다</b>.
            /// 승격은 남은 단계를 앞으로 당기므로, 깨지는 단계가 있으면 더 가까이서 나타난다.
            /// </summary>
            public int KeepLevels { get; }
        }

        private const string ModelRoot = "Assets/_Project/Art/Models/";

        /// <summary>
        /// 승격 표. 각 줄의 근거는 <b>에셋랩에서 단계를 강제해 눈으로 비교한 결과</b>이고,
        /// 수치는 "LOD0 → 승격 후" 삼각형 수다.
        ///
        /// <para><b>여기 넣기 전에 반드시 화면으로 확인한다.</b> 낮은 단계가 멀쩡해 보이는지는
        /// 형상에 따라 갈린다 — 덩어리진 절벽은 1/3로 깎아도 티가 안 나지만, 얇은 껍질로 된
        /// 수관은 같은 비율에서 구멍이 뚫린다.</para>
        /// </summary>
        private static readonly Promotion[] Promotions =
        {
            // 절벽 2종 — 숲 타일의 37.2 %. LOD2(786·766)가 LOD0(2,500)과 화면상 구분되지 않는다.
            new Promotion("Environment/Forest/Env_Cliff_Face_A", 2),
            new Promotion("Environment/Forest/Env_Cliff_Face_B", 2),

            // 나무 5종 — 숲 타일의 37.7 %. LOD1(약 534)까지는 실루엣이 유지된다.
            // 그 아래(320)는 수관이 뚫리므로 **단계 자체를 버린다**(KeepLevels: 1).
            // 승격은 남은 단계를 앞으로 당기므로, 두면 뚫린 메시가 더 가까이서 나타난다.
            new Promotion("Environment/Forest/Env_Tree_Conifer_A", 1, 1),
            new Promotion("Environment/Forest/Env_Tree_Conifer_B", 1, 1),
            new Promotion("Environment/Forest/Env_Tree_Conifer_C", 1, 1),
            new Promotion("Environment/Forest/Env_Tree_Broadleaf_A", 1, 1),
            new Promotion("Environment/Forest/Env_Tree_Broadleaf_B", 1, 1),

            // Train_RailTrack 은 뺐다 — LOD1(4,058)에서 레일 헤드와 침목 모서리가 뭉개지고,
            // 선로는 플레이어 발밑이라 늘 근거리에 있다 (계획서 §7 결정 ⑥).
        };

        private void OnPostprocessModel(GameObject root)
        {
            if (!TryGetPromotion(assetPath, out Promotion promotion))
            {
                return;
            }

            int level = promotion.Level;

            var promoted = new System.Collections.Generic.HashSet<Mesh>();
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || !promoted.Add(mesh) || mesh.lodCount <= level)
                {
                    continue;
                }

                int remaining = mesh.lodCount - level;
                if (promotion.KeepLevels > 0)
                {
                    remaining = Mathf.Min(remaining, promotion.KeepLevels);
                }

                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    MeshLodRange[] levels = mesh.GetLods(submesh);
                    if (levels.Length <= level)
                    {
                        continue;
                    }

                    var kept = new MeshLodRange[remaining];
                    for (int i = 0; i < kept.Length; i++)
                    {
                        kept[i] = levels[i + level];
                    }

                    mesh.SetLods(kept, submesh, UnityEngine.Rendering.MeshUpdateFlags.Default);
                }

                // 단계 수도 함께 줄인다 — 남겨 두면 뒤쪽 단계가 앞 단계를 되풀이해
                // 원거리에서 오히려 무거워진다(승격 직후 786/448/786/448 로 확인).
                mesh.lodCount = Mathf.Max(1, remaining);
            }
        }

        /// <summary>이 에셋의 승격 규칙 — 대상이 아니면 false.</summary>
        private static bool TryGetPromotion(string path, out Promotion promotion)
        {
            for (int i = 0; i < Promotions.Length; i++)
            {
                if (string.Equals(ModelRoot + Promotions[i].Path + ".fbx", path,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    promotion = Promotions[i];
                    return promotion.Level > 0;
                }
            }

            promotion = default;
            return false;
        }
    }
}
