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

        [Tooltip("산출 자원 종류.")]
        [SerializeField] private ResourceType _output = ResourceType.RevolverAmmo;

        [Tooltip("제작 1회당 산출 개수.")]
        [SerializeField, Min(1)] private int _outputCount = 1;

        public string DisplayName => _displayName;

        public int IngredientCount => _ingredients == null ? 0 : _ingredients.Length;

        public ResourceType Output => _output;

        public int OutputCount => _outputCount;

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
