using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 선로 메시를 절차로 다시 만든다 — 원본 <c>Train_RailTrack.fbx</c>가 7,668 tris이고
    /// <b>51개 타일 전부에 1개씩</b> 들어가 총 391k를 먹는데, 정작 형태는 도상·침목·레일
    /// 세 덩어리의 반복이라 그만한 폴리곤이 필요 없다
    /// (docs/plans/features/에셋랩-씬-계획.md §7 결정 ⑥).
    ///
    /// <para><b>왜 LOD가 아니라 원본 교체인가</b> — 선로는 플레이어 발밑이라 늘 근거리에 있어
    /// LOD1(4,058)에서 레일 헤드와 침목 모서리가 뭉개지는 것을 받을 수 없다. 그래서 이 에셋만은
    /// 하위 LOD 승격 대상에서 빼고 형태를 다시 만든다.</para>
    ///
    /// <para><b>치수는 원본 실측값이다</b>(모델 로컬 = 실제 미터 — 이 FBX만 스케일 규약이 다르다).
    /// 아래 상수를 고치고 메뉴를 다시 실행하면 메시가 갱신된다.</para>
    /// </summary>
    public static class RailTrackMeshBuilder
    {
        private const string MeshPath = "Assets/_Project/Art/Meshes/Mesh_Train_RailTrack.asset";

        // ── 원본 실측 치수 (모델 로컬: x=폭 · y=길이 · z=높이) ──

        /// <summary>길이 — 원본 bbox y −17.336~17.336.</summary>
        private const float Length = 34.672f;

        /// <summary>도상 바닥 폭 — 원본 z 0.00 에서 4.58.</summary>
        private const float BallastBottomWidth = 4.58f;

        /// <summary>도상 어깨 폭 — 원본 z 0.16~0.68 에서 6.46.</summary>
        private const float BallastTopWidth = 6.44f;

        /// <summary>도상이 넓어지는 높이 — 원본 z 0.12 부터 6.4 대.</summary>
        private const float BallastFlareHeight = 0.14f;

        /// <summary>도상 상면 높이 — 침목이 얹히는 면.</summary>
        private const float BallastTopHeight = 1.06f;

        /// <summary>상면에서 살짝 좁아진다 — 원본 z 0.96~1.04 에서 6.3.</summary>
        private const float BallastCrownWidth = 6.30f;

        /// <summary>침목 반폭 — 원본 z 1.08~1.24 에서 x ±2.72.</summary>
        private const float SleeperHalfWidth = 2.72f;

        /// <summary>침목 두께(길이 방향) — 원본 평균 0.374.</summary>
        private const float SleeperDepth = 0.38f;

        /// <summary>침목 높이 — 원본 z 1.06~1.26.</summary>
        private const float SleeperHeight = 0.20f;

        /// <summary>침목 간격 — 원본 실측 0.954.</summary>
        private const float SleeperSpacing = 0.954f;

        /// <summary>레일 안쪽 면 x — 원본 z 1.30~1.46 에서 1.771~2.208.</summary>
        private const float RailInnerX = 1.775f;

        /// <summary>레일 바깥 면 x.</summary>
        private const float RailOuterX = 2.210f;

        /// <summary>레일 높이 — 침목 위에 얹힌다.</summary>
        private const float RailHeight = 0.16f;

        // ── 리눅스 에디터(= CI)에서는 이 메뉴를 등록하지 않는다 ──
        //
        // 이 어트리뷰트 한 줄이 2026-08-31 ~ 09-03 CI 전면 실패의 원인이다. 리눅스 컨테이너의
        // batchmode 에디터에서 PlayMode 에 진입하면 도메인 리로드 끝에 메뉴를 재구축하다가
        // 세그폴트(signo:11)로 죽는다 — ScriptCommands::Rebuild() 안에서 MonoMenuItem 을
        // 조회하거나(DoFindItem) 해제할 때(~MonoMenuItem) 양쪽에서 터진다.
        //
        // 이등분으로 확정했다 (자동화 1차 구현 계획 §1.8):
        //   bd115b7 + 주석 한 줄 → 통과 · 065d253(이 파일 추가) → 죽음 ·
        //   메뉴 경로만 Game/QA 로 이동 → 죽음 · 이 어트리뷰트만 주석 → 통과.
        // 즉 방아쇠는 새 하위 메뉴가 아니라 **메뉴 항목이 하나 늘어난 것 자체**다.
        // Windows 에디터에서는 같은 코드가 멀쩡하고 PlayMode 테스트도 11/11 통과한다.
        //
        // 메서드는 남으므로 필요하면 -executeMethod 로 부를 수 있다.
        // 원인은 Unity 쪽에 있어 이 가드는 회피다 — 버전을 올릴 때 걷어낼 수 있는지 다시 본다.
#if !UNITY_EDITOR_LINUX
        [MenuItem("Game/Art/Rebuild Rail Track Mesh")]
#endif
        public static void Rebuild()
        {
            Mesh mesh = Build();
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);

            if (existing != null)
            {
                // 에셋을 새로 만들면 guid가 바뀌어 프리팹 참조가 끊긴다 — 내용만 갈아 끼운다.
                existing.Clear();
                existing.SetVertices(new List<Vector3>(mesh.vertices));
                existing.SetNormals(new List<Vector3>(mesh.normals));
                existing.SetUVs(0, new List<Vector2>(mesh.uv));
                existing.SetTriangles(mesh.triangles, 0);
                existing.RecalculateBounds();
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, MeshPath);
                existing = mesh;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[RailTrackMeshBuilder] {MeshPath} — {existing.triangles.Length / 3} tris "
                + $"· {existing.vertexCount} verts · bounds {existing.bounds.size:F2}");
        }

        /// <summary>도상 + 침목 + 레일 2줄을 하나의 메시로 만든다.</summary>
        public static Mesh Build()
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            float halfLength = Length * 0.5f;

            // ── 도상 — 아래가 좁고 어깨에서 넓어졌다가 상면에서 살짝 좁아지는 사다리꼴 ──
            // 옆면만 만든다. 바닥은 지면 아래(배치 y −0.50)라 보이지 않는다.
            AddBallast(verts, norms, uvs, tris, halfLength);

            // ── 침목 ── 원본과 같은 간격으로 깐다. 아랫면은 도상에 묻히므로 뺀다.
            int sleeperCount = Mathf.FloorToInt(Length / SleeperSpacing);
            float span = (sleeperCount - 1) * SleeperSpacing;
            float start = -span * 0.5f;
            for (int i = 0; i < sleeperCount; i++)
            {
                float y = start + i * SleeperSpacing;
                AddBox(verts, norms, uvs, tris,
                    new Vector3(-SleeperHalfWidth, y - SleeperDepth * 0.5f, BallastTopHeight),
                    new Vector3(SleeperHalfWidth, y + SleeperDepth * 0.5f, BallastTopHeight + SleeperHeight),
                    skipBottom: true);
            }

            // ── 레일 2줄 ── 침목 위에 통으로 얹는다. 아랫면은 침목에 가려 뺀다.
            float railBottom = BallastTopHeight + SleeperHeight;
            AddBox(verts, norms, uvs, tris,
                new Vector3(-RailOuterX, -halfLength, railBottom),
                new Vector3(-RailInnerX, halfLength, railBottom + RailHeight),
                skipBottom: true);
            AddBox(verts, norms, uvs, tris,
                new Vector3(RailInnerX, -halfLength, railBottom),
                new Vector3(RailOuterX, halfLength, railBottom + RailHeight),
                skipBottom: true);

            var mesh = new Mesh { name = "Mesh_Train_RailTrack" };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>도상 — 단면이 사다리꼴인 띠. 양 끝 마구리와 좌우 경사면만 만든다.</summary>
        private static void AddBallast(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs,
            List<int> tris, float halfLength)
        {
            float b = BallastBottomWidth * 0.5f;
            float s = BallastTopWidth * 0.5f;
            float c = BallastCrownWidth * 0.5f;

            // 단면 윤곽 (x, z) — 왼쪽 아래에서 시계 방향으로 위를 돌아 오른쪽 아래까지.
            var section = new[]
            {
                new Vector2(-b, 0f),
                new Vector2(-s, BallastFlareHeight),
                new Vector2(-c, BallastTopHeight),
                new Vector2(c, BallastTopHeight),
                new Vector2(s, BallastFlareHeight),
                new Vector2(b, 0f),
            };

            // 옆면 — 단면을 길이 방향으로 훑는다.
            for (int i = 0; i < section.Length - 1; i++)
            {
                Vector2 p0 = section[i];
                Vector2 p1 = section[i + 1];
                Vector3 a = new Vector3(p0.x, -halfLength, p0.y);
                Vector3 bb = new Vector3(p1.x, -halfLength, p1.y);
                Vector3 cc = new Vector3(p1.x, halfLength, p1.y);
                Vector3 d = new Vector3(p0.x, halfLength, p0.y);
                AddQuad(verts, norms, uvs, tris, a, bb, cc, d);
            }

            // 양 끝 마구리 — 이웃 타일과 맞물리지만 이음매가 벌어질 때를 대비해 막는다.
            AddFan(verts, norms, uvs, tris, section, -halfLength, Vector3.down);
            AddFan(verts, norms, uvs, tris, section, halfLength, Vector3.up);
        }

        /// <summary>단면 윤곽을 삼각형 부채꼴로 막는다(마구리).</summary>
        private static void AddFan(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs,
            List<int> tris, Vector2[] section, float y, Vector3 normal)
        {
            int baseIndex = verts.Count;
            for (int i = 0; i < section.Length; i++)
            {
                verts.Add(new Vector3(section[i].x, y, section[i].y));
                norms.Add(normal);
                uvs.Add(new Vector2(section[i].x, section[i].y) * UvPerMeter);
            }

            bool flip = normal.y > 0f;
            for (int i = 1; i < section.Length - 1; i++)
            {
                if (flip)
                {
                    tris.Add(baseIndex);
                    tris.Add(baseIndex + i);
                    tris.Add(baseIndex + i + 1);
                }
                else
                {
                    tris.Add(baseIndex);
                    tris.Add(baseIndex + i + 1);
                    tris.Add(baseIndex + i);
                }
            }
        }

        /// <summary>축 정렬 상자. <paramref name="skipBottom"/>이면 아랫면을 만들지 않는다.</summary>
        private static void AddBox(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs,
            List<int> tris, Vector3 min, Vector3 max, bool skipBottom)
        {
            // z가 높이인 좌표계 — 윗면은 +z, 아랫면은 −z다.
            AddQuad(verts, norms, uvs, tris,
                new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z));

            if (!skipBottom)
            {
                AddQuad(verts, norms, uvs, tris,
                    new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z),
                    new Vector3(max.x, min.y, min.z), new Vector3(min.x, min.y, min.z));
            }

            AddQuad(verts, norms, uvs, tris,
                new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z));
            AddQuad(verts, norms, uvs, tris,
                new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z));
            AddQuad(verts, norms, uvs, tris,
                new Vector3(min.x, max.y, min.z), new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z), new Vector3(min.x, max.y, max.z));
            AddQuad(verts, norms, uvs, tris,
                new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z), new Vector3(max.x, min.y, max.z));
        }

        /// <summary>
        /// 1 m 당 UV — 원본 텍스처는 나무·금속 조각이 뒤섞인 아틀라스라 <b>부위별로 노릴 영역이
        /// 없다</b>(전체가 같은 갈색 톤). 그래서 면마다 0~1을 주는 대신 <b>월드 크기에 비례해</b>
        /// 타일링한다 — 34 m 면에 텍스처를 한 번만 늘이면 조각이 뭉개져 흐려진다.
        /// </summary>
        private const float UvPerMeter = 0.45f;

        /// <summary>
        /// 사각형 하나 — 면 노멀을 정점마다 그대로 주어 각진 음영을 유지하고,
        /// UV는 면이 놓인 평면의 두 축을 월드 크기 그대로 쓴다.
        /// </summary>
        private static void AddQuad(List<Vector3> verts, List<Vector3> norms, List<Vector2> uvs,
            List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i0 = verts.Count;
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;

            verts.Add(a);
            verts.Add(b);
            verts.Add(c);
            verts.Add(d);
            for (int i = 0; i < 4; i++)
            {
                norms.Add(normal);
            }

            // 노멀이 가장 약한 두 축을 UV 축으로 삼는다 — 면을 그 평면에 눕혀 투영하는 것과 같다.
            Vector3 n = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            AddProjectedUv(uvs, a, n);
            AddProjectedUv(uvs, b, n);
            AddProjectedUv(uvs, c, n);
            AddProjectedUv(uvs, d, n);

            tris.Add(i0);
            tris.Add(i0 + 1);
            tris.Add(i0 + 2);
            tris.Add(i0);
            tris.Add(i0 + 2);
            tris.Add(i0 + 3);
        }

        private static void AddProjectedUv(List<Vector2> uvs, Vector3 p, Vector3 absNormal)
        {
            if (absNormal.z >= absNormal.x && absNormal.z >= absNormal.y)
            {
                uvs.Add(new Vector2(p.x, p.y) * UvPerMeter);      // 윗면·아랫면
            }
            else if (absNormal.x >= absNormal.y)
            {
                uvs.Add(new Vector2(p.y, p.z) * UvPerMeter);      // 좌우 옆면
            }
            else
            {
                uvs.Add(new Vector2(p.x, p.z) * UvPerMeter);      // 앞뒤 마구리
            }
        }
    }
}
