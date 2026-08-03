using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Crafting;
using Game.Gameplay.Inventory;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 제작 HUD (M5 1차) — 제작대 근접 안내("E — 제작")와 제작 창(레시피 목록·재료 보유/필요·제작 버튼).
    /// UI는 상태를 소유하지 않는다: <see cref="ICraftingStation"/>·<see cref="ILocalHotbar"/> 조회와
    /// 이벤트 구독으로만 그린다. 제작 버튼은 요청일 뿐, 확정은 호스트가 한다.
    /// </summary>
    public sealed class CraftingHud : MonoBehaviour
    {
        [SerializeField] private ResourceCatalog _catalog;

        private bool _promptVisible;
        private bool _panelOpen;

        private void OnEnable()
        {
            EventBus<CraftingPromptLocalEvent>.Subscribe(OnPrompt);
            EventBus<CraftingPanelToggledLocalEvent>.Subscribe(OnPanelToggled);
        }

        private void OnDisable()
        {
            EventBus<CraftingPromptLocalEvent>.Unsubscribe(OnPrompt);
            EventBus<CraftingPanelToggledLocalEvent>.Unsubscribe(OnPanelToggled);
        }

        private void OnPrompt(CraftingPromptLocalEvent evt)
        {
            _promptVisible = evt.InRange;
        }

        private void OnPanelToggled(CraftingPanelToggledLocalEvent evt)
        {
            _panelOpen = evt.IsOpen;
        }

        private void OnGUI()
        {
            if (_promptVisible && !_panelOpen)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 100f, Screen.height * 0.62f, 200f, 24f),
                    "<color=yellow>E — 제작</color>");
            }

            if (_panelOpen)
            {
                DrawPanel();
            }
        }

        private void DrawPanel()
        {
            if (!ServiceLocator.TryGet(out ICraftingStation station) ||
                !ServiceLocator.TryGet(out ILocalHotbar hotbar))
            {
                return;
            }

            int recipeCount = station.RecipeCount;
            float height = 80f + recipeCount * 64f;
            var rect = new Rect(Screen.width * 0.5f - 190f, Screen.height * 0.5f - height * 0.5f, 380f, height);
            GUI.Box(rect, "— 제작 (E 닫기) —");

            // 재료 판정은 순수 로직과 같은 뷰로 계산한다 — 표시와 요청 가능 여부가 항상 일치.
            var slots = new HotbarSlotView[hotbar.SlotCount];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = hotbar.GetSlot(i);
            }

            float y = rect.y + 32f;
            for (int i = 0; i < recipeCount; i++)
            {
                CraftingRecipe recipe = station.GetRecipe(i);
                if (recipe == null)
                {
                    continue;
                }

                CraftingLogic.IngredientView[] ingredients = recipe.ToIngredientViews();
                bool canCraft = CraftingLogic.CanCraft(slots, ingredients);

                string outputName = GetName(recipe.Output);
                GUI.Label(new Rect(rect.x + 16f, y, 220f, 22f),
                    $"{recipe.DisplayName}  →  {outputName} ×{recipe.OutputCount}");

                string ingredientLine = BuildIngredientLine(slots, ingredients);
                GUI.Label(new Rect(rect.x + 16f, y + 20f, 260f, 22f), ingredientLine);

                GUI.enabled = canCraft;
                if (GUI.Button(new Rect(rect.xMax - 92f, y + 6f, 76f, 30f), "제작"))
                {
                    station.RequestCraft(i);
                }

                GUI.enabled = true;
                y += 64f;
            }
        }

        private string BuildIngredientLine(HotbarSlotView[] slots, CraftingLogic.IngredientView[] ingredients)
        {
            var builder = new System.Text.StringBuilder(64);
            for (int i = 0; i < ingredients.Length; i++)
            {
                int have = HotbarLogic.CountResource(slots, ingredients[i].Type);
                int need = ingredients[i].Count;
                string color = have >= need ? "white" : "red";
                if (i > 0)
                {
                    builder.Append("  ");
                }

                builder.Append($"<color={color}>{GetName(ingredients[i].Type)} {have}/{need}</color>");
            }

            return builder.ToString();
        }

        private string GetName(ResourceType type)
        {
            return _catalog != null ? _catalog.GetDisplayName(type) : type.ToString();
        }
    }
}
