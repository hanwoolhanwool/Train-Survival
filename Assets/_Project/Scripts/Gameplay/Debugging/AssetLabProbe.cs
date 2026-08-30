using UnityEngine;

namespace Game.Gameplay.Debugging
{
    /// <summary>
    /// 씬에 놓인 인스턴스에서 계측값을 읽는다 — 판정은 <see cref="AssetLabDiagnostics"/>가 한다.
    /// 프리팹 에셋이 아니라 <b>실제로 켜진 인스턴스</b>를 재는 이유는, 변주(ScatterSlot)로
    /// 꺼진 자식이 화면에 없는데도 예산에 잡히는 일을 피하기 위해서다.
    /// </summary>
    public static class AssetLabProbe
    {
        /// <summary>
        /// 대상과 그 자식 전체를 훑어 바운즈·삼각형·머티리얼을 집계한다.
        /// <paramref name="activeOnly"/>가 참이면 꺼진 오브젝트를 세지 않는다.
        /// </summary>
        public static AssetMeasurement Measure(GameObject root, bool activeOnly = true)
        {
            if (root == null)
            {
                return default;
            }

            var renderers = root.GetComponentsInChildren<Renderer>(!activeOnly);
            int triangles = 0;
            int materialCount = 0;
            int missingMaterials = 0;
            int rendererCount = 0;
            bool castsShadow = false;
            var materials = new System.Collections.Generic.HashSet<Material>();
            Bounds bounds = default;
            bool boundsSet = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || (activeOnly && !r.enabled))
                {
                    continue;
                }

                rendererCount++;
                if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
                {
                    castsShadow = true;
                }

                var shared = r.sharedMaterials;
                for (int m = 0; m < shared.Length; m++)
                {
                    if (shared[m] == null)
                    {
                        missingMaterials++;
                    }
                    else
                    {
                        materials.Add(shared[m]);
                    }
                }

                triangles += TriangleCountOf(r);

                if (!boundsSet)
                {
                    bounds = r.bounds;
                    boundsSet = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            materialCount = materials.Count;

            // 지면 기준은 루트 원점 — 루트를 y=0에 놓았을 때 밑면이 어디 오는지가 관심사다.
            float groundOffset = boundsSet ? bounds.min.y - root.transform.position.y : 0f;

            MeasureLod(root, activeOnly, out int lodLevels, out int lowestLodTriangles);

            return new AssetMeasurement(
                triangles,
                materialCount,
                missingMaterials,
                rendererCount,
                boundsSet ? bounds.size : Vector3.zero,
                groundOffset,
                root.GetComponentInChildren<Collider>(!activeOnly) != null,
                lodLevels,
                lowestLodTriangles,
                castsShadow);
        }

        /// <summary>
        /// LOD 실태 — <b>두 갈래를 모두 본다</b>. <see cref="LODGroup"/>은 오브젝트를 통째로
        /// 바꾸고, Unity 6의 <b>Mesh LOD</b>는 메시 하나가 단계를 품는다
        /// (<see cref="Mesh.lodCount"/>). 이 프로젝트의 배경 에셋은 후자를 쓰므로
        /// LODGroup만 찾으면 "LOD가 없다"고 잘못 읽는다.
        /// </summary>
        private static void MeasureLod(GameObject root, bool activeOnly, out int levels, out int lowestTriangles)
        {
            levels = 1;
            lowestTriangles = 0;

            LODGroup group = root.GetComponentInChildren<LODGroup>(!activeOnly);
            if (group != null)
            {
                levels = Mathf.Max(levels, group.lodCount);
            }

            var counted = new System.Collections.Generic.HashSet<Mesh>();
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(!activeOnly))
            {
                if (renderer == null || (activeOnly && !renderer.enabled))
                {
                    continue;
                }

                Mesh mesh = MeshOf(renderer);
                if (mesh == null || !counted.Add(mesh))
                {
                    continue;
                }

                levels = Mathf.Max(levels, mesh.lodCount);

                int last = Mathf.Max(0, mesh.lodCount - 1);
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    lowestTriangles += (int)(mesh.GetIndexCount(s, last) / 3);
                }
            }
        }

        /// <summary>
        /// 단계별 삼각형 수 — <c>[0]</c>이 LOD0이다. LOD가 없으면 길이 1.
        /// 감축이 실제로 얼마나 됐는지는 이 배열로만 보인다(1,000 → 534 → 320 식).
        /// </summary>
        public static int[] LodTriangleCounts(GameObject root, bool activeOnly = true)
        {
            if (root == null)
            {
                return new int[] { 0 };
            }

            var meshes = new System.Collections.Generic.List<Mesh>();
            int levels = 1;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(!activeOnly))
            {
                if (renderer == null || (activeOnly && !renderer.enabled))
                {
                    continue;
                }

                Mesh mesh = MeshOf(renderer);
                if (mesh == null || meshes.Contains(mesh))
                {
                    continue;
                }

                meshes.Add(mesh);
                levels = Mathf.Max(levels, mesh.lodCount);
            }

            var counts = new int[levels];
            for (int i = 0; i < meshes.Count; i++)
            {
                Mesh mesh = meshes[i];
                for (int lod = 0; lod < levels; lod++)
                {
                    // 단계가 모자란 메시는 마지막 단계를 계속 쓴다 — 실제 렌더도 그렇다.
                    int level = Mathf.Min(lod, Mathf.Max(0, mesh.lodCount - 1));
                    for (int s = 0; s < mesh.subMeshCount; s++)
                    {
                        counts[lod] += (int)(mesh.GetIndexCount(s, level) / 3);
                    }
                }
            }

            return counts;
        }

        /// <summary>
        /// 대상의 모든 렌더러에 LOD 단계를 강제한다 — <paramref name="lod"/>가 음수면 자동으로 되돌린다.
        /// Mesh LOD는 화면 점유 비율로 단계를 고르므로, 가만히 보고 있으면 LOD1·2를 볼 수 없다.
        /// </summary>
        public static void ForceLod(GameObject root, int lod)
        {
            if (root == null)
            {
                return;
            }

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.forceMeshLod = (short)Mathf.Clamp(lod, -1, short.MaxValue);
            }
        }

        /// <summary>렌더러가 그리는 메시 — 스킨드와 메시 필터 둘 다 받는다.</summary>
        private static Mesh MeshOf(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                return skinned.sharedMesh;
            }

            return renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
        }

        /// <summary>렌더러가 실제로 그리는 메시의 삼각형 수 — 인덱스가 아니라 삼각형 기준(LOD0).</summary>
        private static int TriangleCountOf(Renderer renderer)
        {
            Mesh mesh = MeshOf(renderer);
            if (mesh == null)
            {
                return 0;
            }

            int total = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                total += (int)(mesh.GetIndexCount(s) / 3);
            }

            return total;
        }

        /// <summary>
        /// 대상을 화면에 꽉 채우는 궤도 거리 — 12 m 나무와 30 cm 돌을 같은 거리에서 보면
        /// 한쪽은 화면 밖, 한쪽은 점이 된다.
        /// </summary>
        public static float FramingDistance(Vector3 size, float verticalFovDegrees, float margin = 1.35f)
        {
            float radius = Mathf.Max(size.magnitude * 0.5f, 0.05f);
            float halfFov = Mathf.Max(verticalFovDegrees, 1f) * 0.5f * Mathf.Deg2Rad;
            return radius / Mathf.Tan(halfFov) * margin;
        }
    }
}
