using Game.Gameplay.Monsters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 스탬피드 들소 <b>외형 배선</b>의 건전성 (대초원 지역 구현 계획 §7).
    ///
    /// <para>이 계기가 필요한 이유는 팔레트와 같다 — <b>배선이 끊겨도 아무 신호가 없다.</b>
    /// 변종 외형이 안 물리면 예외도 로그도 없이 <b>색만 다른 기본 몬스터 30마리</b>가 오고,
    /// 그것은 화면에서 "무리"가 아니라 "몬스터 여럿"으로 읽힌다(§7.2).</para>
    ///
    /// <para><b>깨지지 않아야 할 선</b> — 네트워크 프리팹을 늘리지 않으므로
    /// <c>GlobalObjectIdHash</c>가 그대로여야 하고, 기본 외형 폴백이 살아 있어야
    /// 다른 네 지역의 변종이 종전대로 나온다.</para>
    /// </summary>
    public sealed class GrasslandStampedeVisualAssetTests
    {
        private const string MonsterPrefabPath = "Assets/_Project/Prefabs/Monster.prefab";
        private const string StampedeSettingsPath = "Assets/_Project/Data/MonsterSettings_Stampede.asset";
        private const string StampedeEventPath = "Assets/_Project/Data/StampedeSettings.asset";
        private const string BisonMeshPath = "Assets/_Project/Art/Meshes/Mesh_Env_Bison.asset";

        /// <summary>동시 상한 12마리 × 이 값이 타일 예산 30,000의 절반 안이어야 한다 (계획 §7.1 ①).</summary>
        private const int BisonTriangleBudget = 1200;

        private static GameObject LoadMonster()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPrefabPath);
            Assert.IsNotNull(prefab, $"몬스터 프리팹이 없다: {MonsterPrefabPath}");
            return prefab;
        }

        /// <summary>
        /// <c>MonsterGrabTarget</c>을 타입으로 부르지 않는다 — 이 컴포넌트는 <c>NetworkBehaviour</c>
        /// 파생이라 EditMode 테스트 어셈블리가 <c>Unity.Netcode.Runtime</c>을 참조해야 한다.
        /// 계기 하나 때문에 테스트 어셈블리의 의존을 늘리지 않는다.
        /// </summary>
        private static SerializedObject GrabTargetOf(GameObject prefab)
        {
            Component[] components = prefab.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].GetType().Name == "MonsterGrabTarget")
                {
                    return new SerializedObject(components[i]);
                }
            }

            Assert.Fail("Monster 프리팹에 MonsterGrabTarget 이 없다");
            return null;
        }

        [Test]
        public void 들소_외형이_스탬피드_변종에_물려_있다()
        {
            SerializedObject so = GrabTargetOf(LoadMonster());
            SerializedProperty visuals = so.FindProperty("_variantVisuals");
            Assert.IsNotNull(visuals, "_variantVisuals 필드가 없다");
            Assert.AreEqual(1, visuals.arraySize, "지금 전용 외형이 있는 변종은 들소뿐이다");

            SerializedProperty entry = visuals.GetArrayElementAtIndex(0);
            var settings = AssetDatabase.LoadAssetAtPath<MonsterSettings>(StampedeSettingsPath);
            Assert.IsNotNull(settings, $"들소 변종 설정이 없다: {StampedeSettingsPath}");
            Assert.AreSame(settings, entry.FindPropertyRelative("_settings").objectReferenceValue,
                "전용 외형이 들소 변종을 가리키지 않는다 — 색만 다른 기본 몬스터가 온다");

            var root = entry.FindPropertyRelative("_root").objectReferenceValue as GameObject;
            Assert.IsNotNull(root, "들소 메시 루트가 비었다");
            Assert.IsFalse(root.activeSelf, "들소 외형은 기본으로 꺼져 있어야 한다 — 스폰 때 켜진다");
        }

        [Test]
        public void 기본_외형_폴백이_살아_있다()
        {
            // 폴백이 끊기면 다른 네 지역의 변종이 통째로 사라진다.
            SerializedObject so = GrabTargetOf(LoadMonster());
            Assert.IsNotNull(so.FindProperty("_fallbackVisual").objectReferenceValue,
                "기본 외형이 비었다 — 들소가 아닌 변종이 보이지 않는다");
        }

        [Test]
        public void 두_외형_모두_그로기_틴트_대상이다()
        {
            // 기절 노란색이 안 칠해지면 "지금 무력화 상태다"가 들소에게만 안 보인다.
            SerializedObject so = GrabTargetOf(LoadMonster());
            SerializedProperty tints = so.FindProperty("_tintRenderers");
            Assert.AreEqual(2, tints.arraySize);
            for (int i = 0; i < tints.arraySize; i++)
            {
                Assert.IsNotNull(tints.GetArrayElementAtIndex(i).objectReferenceValue, $"틴트 렌더러 {i} 가 비었다");
            }
        }

        [Test]
        public void 들소_메시가_예산_안에_있다()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(BisonMeshPath);
            Assert.IsNotNull(mesh, $"들소 메시가 없다: {BisonMeshPath}");

            int tris = mesh.triangles.Length / 3;
            Assert.LessOrEqual(tris, BisonTriangleBudget, "동시 상한 12마리 × 이 값이 타일 예산의 절반 안이어야 한다");
            Assert.LessOrEqual(tris * 12, 15000, "동시 12마리 합계");
        }

        [Test]
        public void 무리_뒤로_끌리는_먼지가_있다()
        {
            // 지면이 조용하면 "무리"가 아니라 "몬스터 여럿"으로 읽힌다 (계획 §7.2 ③).
            SerializedObject so = GrabTargetOf(LoadMonster());
            var root = so.FindProperty("_variantVisuals").GetArrayElementAtIndex(0)
                .FindPropertyRelative("_root").objectReferenceValue as GameObject;
            ParticleSystem[] dust = root.GetComponentsInChildren<ParticleSystem>(true);
            Assert.AreEqual(1, dust.Length, "들소 외형 아래에 먼지 입자가 하나 있어야 한다");

            ParticleSystemRenderer renderer = dust[0].GetComponent<ParticleSystemRenderer>();
            Assert.IsNotNull(renderer.sharedMaterial, "먼지 머티리얼이 비었다");
            Assert.AreEqual(ParticleSystemSimulationSpace.World, dust[0].main.simulationSpace,
                "월드 공간이 아니면 먼지가 들소를 따라다녀 '끌리는' 것으로 안 보인다");
        }

        [Test]
        public void 들소_외형은_네트워크_오브젝트를_늘리지_않는다()
        {
            // 변종마다 프리팹을 만들면 NetworkPrefabs 목록이 늘고 GlobalObjectIdHash 가 갈린다.
            GameObject prefab = LoadMonster();
            int networkObjects = 0;
            Component[] components = prefab.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].GetType().Name == "NetworkObject")
                {
                    networkObjects++;
                }
            }

            Assert.AreEqual(1, networkObjects, "몬스터 프리팹의 NetworkObject 는 루트 하나뿐이다");
        }

        [Test]
        public void 스탬피드_대역이_계획이_쓴_자와_같다()
        {
            // 팔레트 계기(4~9 m)와 이벤트 데이터가 어긋나면 검사한 대역이 실제와 다른 곳이 된다.
            var settings = AssetDatabase.LoadAssetAtPath<StampedeSettings>(StampedeEventPath);
            if (settings == null)
            {
                Assert.Ignore($"스탬피드 이벤트 설정을 {StampedeEventPath} 에서 찾지 못했다");
            }

            Assert.AreEqual(4f, settings.MinLateralOffset, 1e-4f);
            Assert.AreEqual(9f, settings.MaxLateralOffset, 1e-4f);
            Assert.AreEqual(30, settings.TotalCount);
            Assert.AreEqual(12, settings.MaxAlive);
        }
    }
}
