using UnityEngine;

namespace Game.Gameplay.Crafting
{
    /// <summary>
    /// 제작대가 제공하는 레시피 목록 — <b>배열 인덱스가 곧 요청 RPC 식별자</b>이므로
    /// 순서를 바꾸면 진행 중인 세션의 제작 요청이 다른 레시피로 해석된다
    /// (<see cref="Game.Gameplay.Monsters.MonsterVariantCatalog"/>와 같은 규약).
    /// </summary>
    [CreateAssetMenu(fileName = "RecipeCatalog", menuName = "Game/Recipe Catalog")]
    public sealed class RecipeCatalog : ScriptableObject
    {
        [SerializeField] private CraftingRecipe[] _recipes;

        public int Count => _recipes == null ? 0 : _recipes.Length;

        /// <summary>인덱스의 레시피. 범위 밖이면 null (호출부가 요청을 기각한다).</summary>
        public CraftingRecipe GetRecipe(int index)
        {
            if (_recipes == null || index < 0 || index >= _recipes.Length)
            {
                return null;
            }

            return _recipes[index];
        }
    }
}
