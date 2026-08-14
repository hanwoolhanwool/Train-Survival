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

        // 표시 그룹 (M5 6차 발견성) — 승급 줄이 최상단 그룹이라 발견성이 먼저 풀린다.
        private static readonly string[] GroupTitles = { "집게 강화", "탄약", "무기·장비", "요리" };

        private bool _promptVisible;
        private bool _panelOpen;
        private Vector2 _scrollPosition;

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

            // 유효 지점이 근처에 없는 레시피는 목록에서 뺀다 (M5 4차 — 요리는 화덕 근처에서만).
            // 표시 순서만 그룹화한다 (M5 6차 발견성) — 요청은 기존 레시피 인덱스를 그대로 쓴다
            // (인덱스 = RPC 식별자 규약과 무관).
            int recipeCount = station.RecipeCount;
            var groups = new System.Collections.Generic.List<int>[GroupTitles.Length];
            for (int g = 0; g < groups.Length; g++)
            {
                groups[g] = new System.Collections.Generic.List<int>();
            }

            for (int i = 0; i < recipeCount; i++)
            {
                CraftingRecipe recipe = station.GetRecipe(i);
                if (recipe != null && station.IsRecipeAvailable(i))
                {
                    groups[GetGroup(recipe)].Add(i);
                }
            }

            float contentHeight = 0f;
            for (int g = 0; g < groups.Length; g++)
            {
                if (groups[g].Count > 0)
                {
                    contentHeight += 26f + groups[g].Count * 64f;
                }
            }

            // 패널 높이 상한 = 화면 높이의 70 % — 목록이 넘치면 스크롤이 받는다 (5차 1차 검증 차단 원인).
            float panelHeight = Mathf.Min(48f + contentHeight, Screen.height * 0.7f);
            // 좌측 상단 고정 — 제작 창이 열리면 인벤토리가 화면 가운데에 함께 열리므로(M7 3차 검증
            // 개선 요청) 중앙을 비워 준다. 재료를 보면서 제작할 수 있게 하는 배치다.
            var rect = new Rect(24f, 24f, 380f, panelHeight);
            GUI.Box(rect, "— 제작 (E·I·Tab·Esc 닫기) —");

            // 재료 판정은 순수 로직과 같은 뷰로 계산한다 — 표시와 요청 가능 여부가 항상 일치.
            var slots = new HotbarSlotView[hotbar.SlotCount];
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = hotbar.GetSlot(i);
            }

            var viewRect = new Rect(rect.x, rect.y + 32f, rect.width, panelHeight - 48f);
            var contentRect = new Rect(0f, 0f, rect.width - 24f, contentHeight);
            _scrollPosition = GUI.BeginScrollView(viewRect, _scrollPosition, contentRect);

            float y = 0f;
            for (int g = 0; g < groups.Length; g++)
            {
                if (groups[g].Count == 0)
                {
                    continue;
                }

                GUI.Label(new Rect(8f, y, 300f, 22f), $"<b>— {GroupTitles[g]} —</b>");
                y += 26f;

                for (int r = 0; r < groups[g].Count; r++)
                {
                    int recipeIndex = groups[g][r];
                    CraftingRecipe recipe = station.GetRecipe(recipeIndex);
                    CraftingLogic.IngredientView[] ingredients = recipe.ToIngredientViews();
                    bool canCraft = CraftingLogic.CanCraft(slots, ingredients);
                    string blockedLabel = "재료 부족";

                    // 집게 승급은 집게를 실제로 갖고 있어야 한다 (M6 검증 후속 — 서버 게이트와 동일 조건).
                    if (recipe.IsHarpoonTierOutput && !HotbarLogic.ContainsItem(slots, HotbarItemType.Harpoon))
                    {
                        canCraft = false;
                        blockedLabel = "집게 없음";
                    }

                    GUI.Label(new Rect(16f, y, 220f, 22f), BuildOutputLine(recipe));

                    string ingredientLine = BuildIngredientLine(slots, ingredients);
                    GUI.Label(new Rect(16f, y + 20f, 260f, 22f), ingredientLine);

                    // 비활성 버튼이 "없는 것"처럼 보이지 않게 사유를 라벨로 말해준다 (M5 6차).
                    GUI.enabled = canCraft;
                    if (GUI.Button(new Rect(contentRect.xMax - 84f, y + 6f, 76f, 30f), canCraft ? "제작" : blockedLabel))
                    {
                        station.RequestCraft(recipeIndex);
                    }

                    GUI.enabled = true;
                    y += 64f;
                }
            }

            GUI.EndScrollView();
        }

        /// <summary>
        /// 표시 그룹 (M5 6차) — 분류는 레시피에서 <b>파생</b>한다 (신규 데이터 없음):
        /// 승급 → 집게 강화, 화덕 → 요리, 아이템 산출 → 무기·장비, 그 외 → 탄약.
        /// </summary>
        private static int GetGroup(CraftingRecipe recipe)
        {
            if (recipe.IsHarpoonTierOutput)
            {
                return 0;
            }

            if (recipe.Station == CraftStationKind.Campfire)
            {
                return 3;
            }

            return recipe.IsItemOutput ? 2 : 1;
        }

        /// <summary>산출 표기 — 산출 종류마다 경로가 달라 표기도 다르다 (자원 적재 / 아이템 1개 / 집게 승급).</summary>
        private string BuildOutputLine(CraftingRecipe recipe)
        {
            // 집게 승급 (M5 5차)은 인벤토리 산출이 없다 — 무엇으로 바뀌는지를 보여준다.
            if (recipe.IsHarpoonTierOutput)
            {
                return $"{recipe.DisplayName}  →  {HotbarItemLabels.GetHarpoonLabel(recipe.OutputHarpoonTier)}";
            }

            // 무기 산출 레시피는 아이템 1개 지급 — 자원 산출과 표기가 다르다 (M5 2차).
            return recipe.IsItemOutput
                ? $"{recipe.DisplayName}  →  {HotbarItemLabels.GetLabel(recipe.OutputItem)} ×1"
                : $"{recipe.DisplayName}  →  {GetName(recipe.Output)} ×{recipe.OutputCount}";
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
