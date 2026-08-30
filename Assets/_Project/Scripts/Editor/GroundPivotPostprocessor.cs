using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 지면에 세우는 모델의 피벗을 발밑으로 내린다 — 원본이 <b>바운즈 중심</b>을 원점으로
    /// 내보내는 탓에 프리팹 원점을 지면에 놓으면 절반이 땅에 묻히는 문제를 임포트 시점에 고친다
    /// (docs/plans/features/에셋랩-씬-계획.md §7 결정 ①).
    ///
    /// <para><b>왜 FBX를 다시 굽지 않는가</b> — 바이너리를 재수출하면 guid·머티리얼 참조·UV가
    /// 함께 흔들리고, "원본이 틀렸다"는 사실이 어디에도 남지 않는다. 여기서 고치면
    /// 원본은 그대로 두면서 보정량과 근거가 코드에 남고, 같은 원본을 다시 반입해도 자동 적용된다.</para>
    ///
    /// <para><b>트랜스폼이 아니라 메시 정점을 옮긴다</b> — 이 모델들은 루트 <b>자신</b>이
    /// 메시를 들고 있고(자식 0개), 모델 루트의 트랜스폼은 배치할 때 덮어써지므로 옮겨도
    /// 소용이 없다. 대신 각 메시의 정점을 옮기되, 축은 손으로 고르지 않고
    /// <see cref="Transform.InverseTransformVector"/>로 월드 +Y를 그 메시의 로컬 공간으로
    /// 환산한다 — 루트가 <c>scale 100 · rotX 270.02°</c>라 로컬 축과 월드 축이 다르기 때문이다.</para>
    ///
    /// <para><b>대상을 손으로 고른 이유</b> — 피벗이 바운즈 중심인 모델은 21종이지만
    /// 실측해 보면 대부분 의도이거나 이미 배치에서 보정돼 있다. 작은 돌은 반쯤 묻힌 편이
    /// 자연스럽고(<c>Env_Rock_S</c> 0.155 m), 사슴은 타일에서 이미 0.776 m 들어 올려 두었으며,
    /// 새 떼는 공중에 뜬다. <b>보정 없이 그대로 절반이 묻혀 화면에 결함으로 보이는 것만</b>
    /// 여기 넣는다.</para>
    /// </summary>
    public sealed class GroundPivotPostprocessor : AssetPostprocessor
    {
        /// <summary>이 값보다 작은 보정은 하지 않는다 — 왕복 오차로 계속 흔들리는 것을 막는다.</summary>
        private const float MinimumLift = 0.001f;

        /// <summary>
        /// 발밑 정렬 대상. 실측 근거는 각 줄의 주석이고, 값은 "묻히는 깊이 / 전체 높이"다.
        /// 새로 추가할 때는 에셋랩에서 <c>PIVOT_SUNK</c>를 확인하고 타일 배치에
        /// 손보정이 없는지 함께 본다 — 이미 보정된 것을 여기 넣으면 공중에 뜬다.
        /// </summary>
        private static readonly string[] GroundAlignedModels =
        {
            // 신호기 — 3.50 m 중 1.75 m(50 %)가 땅속이라 기둥이 통째로 보이지 않는다. 타일 6곳 전부 y=0.
            "Assets/_Project/Art/Models/Environment/Common/Env_Signal_Rusty.fbx",

            // 부서진 울타리 — 1.34 m 중 0.66 m(49 %)가 땅속. 타일 13곳 전부 보정 없음.
            "Assets/_Project/Art/Models/Environment/Common/Env_Fence_Broken.fbx",
        };

        private void OnPostprocessModel(GameObject root)
        {
            if (!IsTarget(assetPath))
            {
                return;
            }

            float lift = -LowestRendererY(root);
            if (Mathf.Abs(lift) < MinimumLift)
            {
                return;
            }

            // 같은 메시를 두 트랜스폼이 공유하면 두 번 밀린다 — 처리한 것을 기억한다.
            var lifted = new System.Collections.Generic.HashSet<Mesh>();

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null || !lifted.Add(mesh))
                {
                    continue;
                }

                // 월드 +Y로 lift 만큼 = 이 메시 로컬 공간에서 이만큼 (회전·스케일 역적용).
                Vector3 delta = filter.transform.InverseTransformVector(Vector3.up * lift);

                Vector3[] vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    vertices[i] += delta;
                }

                mesh.vertices = vertices;
                mesh.RecalculateBounds();
            }
        }

        private static bool IsTarget(string path)
        {
            for (int i = 0; i < GroundAlignedModels.Length; i++)
            {
                if (string.Equals(GroundAlignedModels[i], path, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>루트 기준 렌더러 바운즈의 밑면 Y — 렌더러가 없으면 0(보정하지 않음).</summary>
        private static float LowestRendererY(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            bool set = false;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (!set)
                {
                    bounds = renderers[i].bounds;
                    set = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            return set ? bounds.min.y : 0f;
        }
    }
}
