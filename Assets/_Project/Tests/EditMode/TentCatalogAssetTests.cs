using Game.Gameplay.Train;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 천막 카탈로그 배선 계기 (천막 계획 1차).
    ///
    /// <b>왜 자산을 테스트하는가</b> — 이 배선이 끊기면 <b>오류도 예외도 로그도 없이</b> 천막이
    /// 사라지거나 그늘이 안 든다. 설치 목록에서 빠지거나(placeable), 크기 조절이 사라지거나(resizable),
    /// 안쪽이 막히거나(occupancy), 칸 전체가 시원해진다(shelterScope). 사막 세그먼트 팔레트가
    /// 같은 이유로 계기를 달았던 것과 같은 종류의 위험이다.
    /// </summary>
    public sealed class TentCatalogAssetTests
    {
        private const string CatalogPath = "Assets/_Project/Data/StructureCatalog.asset";
        private const string TentPrefabPath = "Assets/_Project/Prefabs/Structures/Structure_Tent.prefab";

        private static StructureCatalog LoadCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StructureCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, $"건축물 카탈로그가 {CatalogPath}에 없다");
            return catalog;
        }

        [Test]
        public void 천막이_카탈로그에_등재되어_있다()
        {
            StructureCatalog catalog = LoadCatalog();
            bool found = false;
            for (int i = 0; i < catalog.EntryCount; i++)
            {
                if (catalog.TryGetKindAt(i, out StructureKind kind) && kind == StructureKind.Tent)
                {
                    found = true;
                    break;
                }
            }

            Assert.IsTrue(found, "천막 엔트리가 없으면 설치 목록에 뜨지 않는다");
        }

        [Test]
        public void 천막은_설치할_수_있고_그늘을_준다()
        {
            StructureCatalog catalog = LoadCatalog();

            Assert.IsTrue(catalog.IsPlaceable(StructureKind.Tent),
                "설치 불가면 R 순환에서 빠져 사막을 건널 수단이 없어진다");
            Assert.IsTrue(catalog.ProvidesShade(StructureKind.Tent),
                "그늘이 없으면 천막이 아니다");
            Assert.IsFalse(catalog.ProvidesHeat(StructureKind.Tent),
                "지붕은 햇빛을 가릴 뿐 난방은 하지 않는다 — 건축물 1종 = 역할 1개");
        }

        [Test]
        public void 천막은_크기를_끌어서_정하고_값은_기둥_넷이_정한다()
        {
            StructureCatalog catalog = LoadCatalog();

            Assert.IsTrue(catalog.IsResizable(StructureKind.Tent),
                "가변 크기가 꺼지면 우클릭 2회 경로를 타지 않는다");
            Assert.AreEqual(0f, catalog.GetCostPerCell(StructureKind.Tent),
                "셀당 비용이 켜지면 넓이 비례로 돌아가 큰 차양이 감당 못 할 값이 된다 (결정 ⑤′)");
            Assert.Greater(catalog.GetBuildCost(StructureKind.Tent, 0), 0,
                "한 채 값이 0이면 천막이 공짜가 된다");
        }

        [Test]
        public void 천막의_효과는_발자국_안에서만_든다()
        {
            StructureCatalog catalog = LoadCatalog();

            Assert.AreEqual(ShelterScope.Footprint, catalog.GetShelterScope(StructureKind.Tent),
                "Car 범위면 천막 옆에 서 있어도 시원해진다");
        }

        [Test]
        public void 천막은_네_기둥만_막는다()
        {
            StructureCatalog catalog = LoadCatalog();

            Assert.AreEqual(StructureOccupancy.Corners, catalog.GetOccupancy(StructureKind.Tent),
                "Solid면 안쪽이 막혀 난방기·제작대를 넣을 수 없다");
        }

        [Test]
        public void 기존_건축물의_점유와_효과_범위는_그대로다()
        {
            // 천막 작업이 기존 8종을 건드리지 않았다는 계기 — 완료 기준 10번.
            StructureCatalog catalog = LoadCatalog();
            StructureKind[] existing =
            {
                StructureKind.Dome, StructureKind.Heater, StructureKind.Storage,
                StructureKind.Workbench, StructureKind.Campfire, StructureKind.Purifier,
                StructureKind.Furnace, StructureKind.MountedGun, StructureKind.Turret,
            };

            foreach (StructureKind kind in existing)
            {
                Assert.AreEqual(StructureOccupancy.Solid, catalog.GetOccupancy(kind),
                    $"{kind}의 점유가 바뀌면 기존 설치 판정이 달라진다");
                Assert.AreEqual(ShelterScope.Car, catalog.GetShelterScope(kind),
                    $"{kind}의 효과 범위가 바뀌면 난방 판정이 달라진다");
                Assert.IsFalse(catalog.IsResizable(kind),
                    $"{kind}는 고정 크기여야 한다");
            }
        }

        [Test]
        public void 천막_프리팹이_배선되어_있고_뷰를_갖췄다()
        {
            StructureCatalog catalog = LoadCatalog();
            GameObject prefab = catalog.GetViewPrefab(StructureKind.Tent);

            Assert.IsNotNull(prefab, $"뷰 프리팹이 비면 천막이 보이지 않는다 ({TentPrefabPath})");
            Assert.IsNotNull(prefab.GetComponent<StructureView>(),
                "루트에 StructureView가 없으면 스포너가 스폰을 포기한다");

            var footprintView = prefab.GetComponent<IStructureFootprintView>();
            Assert.IsNotNull(footprintView,
                "발자국 뷰가 없으면 어떤 크기로 지어도 프리팹 원본 크기로 선다");
        }

        [Test]
        public void 천막_프리팹에는_콜라이더가_없다()
        {
            StructureCatalog catalog = LoadCatalog();
            GameObject prefab = catalog.GetViewPrefab(StructureKind.Tent);
            Assert.IsNotNull(prefab);

            Collider[] colliders = prefab.GetComponentsInChildren<Collider>(includeInactive: true);
            Assert.AreEqual(0, colliders.Length,
                "천막은 덮되 막지 않는다 — 콜라이더가 있으면 안쪽을 걸어 다닐 수 없다");
        }
    }
}
