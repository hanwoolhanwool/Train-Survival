using Game.Gameplay.Inventory;
using UnityEngine;

namespace Game.Gameplay.Crafting
{
    /// <summary>
    /// 제작 레시피 1종 (기획서 §6.1 탄약 제작, §7.3 요리) — 재료 목록과 산출물.
    /// 수치는 전부 데이터 — 밸런싱은 에셋 수정만으로 한다 (개발 가이드 §3).
    /// </summary>
    [CreateAssetMenu(fileName = "CraftingRecipe", menuName = "Game/Crafting Recipe")]
    public sealed class CraftingRecipe : ScriptableObject
    {
        [System.Serializable]
        public sealed class Ingredient
        {
            [SerializeField] private ResourceType _type = ResourceType.Wood;
            [SerializeField, Min(1)] private int _count = 1;

            public ResourceType Type => _type;

            public int Count => _count;
        }

        [Tooltip("제작 목록에 표시할 이름 (예: 권총탄 ×4).")]
        [SerializeField] private string _displayName = "권총탄";

        [SerializeField] private Ingredient[] _ingredients;

        [Tooltip("산출 자원 종류 (무기 산출 레시피에서는 무시된다).")]
        [SerializeField] private ResourceType _output = ResourceType.RevolverAmmo;

        [Tooltip("제작 1회당 산출 개수.")]
        [SerializeField, Min(1)] private int _outputCount = 1;

        [Tooltip("무기·도구 산출 (M5 2차) — None이 아니면 자원 대신 이 아이템 1개를 첫 빈 칸에 지급한다.")]
        [SerializeField] private HotbarItemType _outputItem = HotbarItemType.None;

        [Tooltip("요구 제작 지점 (M5 4차) — 기본 = 제작대/기관차, 요리는 화덕. 지점이 근처에 없으면 목록에서 빠진다.")]
        [SerializeField] private CraftStationKind _station = CraftStationKind.Workbench;

        [Tooltip("집게 승급 산출 (M5 5차) — 0 = 미사용. 1 이상이면 인벤토리 산출 없이 집게 등급을 이 값으로 올린다.")]
        [SerializeField, Range(0, 3)] private int _outputHarpoonTier;

        public string DisplayName => _displayName;

        public int IngredientCount => _ingredients == null ? 0 : _ingredients.Length;

        public ResourceType Output => _output;

        public int OutputCount => _outputCount;

        public HotbarItemType OutputItem => _outputItem;

        /// <summary>요구 제작 지점 종류 — 유효 지점 판정은 CraftingStation이 한다.</summary>
        public CraftStationKind Station => _station;

        /// <summary>무기·도구 산출 레시피인가 — 산출 경로가 자원 적재와 다르다 (빈 칸 1개 필요).</summary>
        public bool IsItemOutput => _outputItem != HotbarItemType.None && _outputItem != HotbarItemType.Resource;

        /// <summary>목표 집게 등급 (M5 5차). 0이면 승급 레시피가 아니다.</summary>
        public int OutputHarpoonTier => _outputHarpoonTier;

        /// <summary>
        /// 집게 승급 레시피인가 — 산출이 인벤토리가 아니라 <b>플레이어 상태</b>다.
        /// 핫바 아이템을 늘리지 않으므로 "집게는 1번 슬롯·버릴 수 없다"(기획서 §3.4)와 충돌하지 않는다.
        /// </summary>
        public bool IsHarpoonTierOutput => _outputHarpoonTier > 0;

        /// <summary>인덱스의 재료. 범위 밖이면 null.</summary>
        public Ingredient GetIngredient(int index)
        {
            if (_ingredients == null || index < 0 || index >= _ingredients.Length)
            {
                return null;
            }

            return _ingredients[index];
        }

        /// <summary>순수 로직(<see cref="CraftingLogic"/>) 입력용 재료 배열 — 호출부가 캐시한다.</summary>
        public CraftingLogic.IngredientView[] ToIngredientViews()
        {
            var views = new CraftingLogic.IngredientView[IngredientCount];
            for (int i = 0; i < views.Length; i++)
            {
                views[i] = new CraftingLogic.IngredientView(_ingredients[i].Type, _ingredients[i].Count);
            }

            return views;
        }
    }
}
