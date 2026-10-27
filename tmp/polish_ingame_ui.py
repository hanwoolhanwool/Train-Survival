from pathlib import Path
import re
root=Path(__file__).resolve().parents[1]
ui=root/'Assets/_Project/Scripts/UI'
def edit(name,fn):
 p=ui/name;s=p.read_text(encoding='utf-8-sig');p.write_text(fn(s),encoding='utf-8',newline='\n')
def inventory(s):
 s=s.replace('화면 중앙 하단 핫바','화면 우하단 핫바')
 s=s.replace('InGameUi.BeginTooltip();','InGameUi.BeginTooltip();\n            InGameUi.BeginPrompts();')
 s=s.replace('            DrawMountPrompt();','            DrawMountPrompt();\n            InGameUi.DrawPrompts();')
 s=re.sub(r'InGameUi.Prompt\(new Rect\(Screen.width[^\n]+?\),\s*', 'InGameUi.QueuePrompt(',s)
 s=s.replace('$"<color={color}>{partName}: {healthText}{action}</color>", "G_Action_Repair");', '$"<color={color}>{partName}: {healthText}{action}</color>",\n                _hammerTarget.DemolishProgress > 0 ? "G_Action_Demolish" : "G_Action_Repair",\n                _hammerTarget.DemolishProgress > 0 ? _hammerTarget.DemolishProgress : -1);')
 s=s.replace('$"{where} — {action}", "G_Action_Build");','$"{where} — {action}", _plankAim.EmptySlot ? "G_Action_Build" : "G_Action_Demolish",\n                _plankAim.RemoveProgress > 0 ? _plankAim.RemoveProgress : -1);')
 return s
edit('InventoryHud.cs',inventory)
def core(s):
 s=s.replace('        private GUIStyle _bannerStyle;\n','')
 a=s.index('        /// <summary>\n        /// 상태 단계에 맞는 색으로 한 줄 그리기')
 b=s.index('        /// <summary>\n        /// 사건 배너',a)
 s=s[:a]+s[b:]
 s=s.replace('            EnsureBannerStyle();','            float y = HudLayout.BannerSlotRect(0).y;')
 s=s.replace('                InGameUi.Prompt(HudLayout.BannerSlotRect(i), _visibleBanners[i].Text,','                Rect rect = HudLayout.BannerSlotRect(i);\n                rect.y = y;\n                y += InGameUi.Prompt(rect, _visibleBanners[i].Text,')
 s=s.replace('? "G_System_Confirm" : "G_System_Warning");','? "G_System_Confirm" : "G_System_Warning") + HudLayout.BannerGap;')
 a=s.index('        private void EnsureBannerStyle()')
 return s[:a]+'    }\n}\n'
edit('CoreLoopHud.cs',core)
edit('HudLayout.cs',lambda s:s.replace('BannerHeight = 30f','BannerHeight = 56f').replace('인벤토리는 화면 중앙, 제작 창은 <b>그 왼쪽에 붙는다</b>. 좁은 화면에서 제작 창이\n    /// 화면 밖으로 나가지 않도록 왼쪽 여백에서 멈춘다(겹치더라도 잘려 보이지는 않게).','인벤토리는 단독일 때 중앙, 제작 창과 함께 열릴 때는 <b>두 창 묶음을 중앙에 배치</b>한다.'))
def craft(s):
 s=s.replace('92f, 40f','96f, 40f')
 s=s.replace('                    string outputKey =', '                    InGameUi.Mask(new Rect(8, y, contentRect.width - 16, RecipeHeight(recipe) - 6),\n                        "U_ControlFace", UiPalette.PanelBackdrop);\n                    string outputKey =')
 s=s.replace('return $"{recipe.DisplayName}  →  {HotbarItemLabels.GetHarpoonLabel(recipe.OutputHarpoonTier)}";','return $"{HotbarItemLabels.GetHarpoonLabel(recipe.OutputHarpoonTier)} 강화";')
 s=s.replace('$"{recipe.DisplayName}  →  {HotbarItemLabels.GetLabel(recipe.OutputItem)} ×1"','$"{HotbarItemLabels.GetLabel(recipe.OutputItem)} ×1"')
 s=s.replace('$"{recipe.DisplayName}  →  {GetName(recipe.Output)} ×{recipe.OutputCount}"','$"{GetName(recipe.Output)} ×{recipe.OutputCount}"')
 a=s.index('        private string BuildIngredientLine(')
 b=s.index('        private string GetName(',a)
 return s[:a]+s[b:]
edit('CraftingHud.cs',craft)
def fishing(s):
 s=s.replace('        private GUIStyle _waitStyle;\n        private GUIStyle _biteStyle;\n','')
 s=s.replace('            EnsureStyles();\n\n','')
 # Keep bite alert prominent as before, but display the action glyph alongside it.
 s=s.replace('InGameUi.Prompt(rect, "입질!  왼쪽 클릭", "G_Action_FishBite");','InGameUi.Prompt(rect, "<size=32><b>입질!  왼쪽 클릭</b></size>", "G_Action_FishBite");')
 a=s.index('        private void EnsureStyles()')
 return s[:a]+'    }\n}\n'
edit('FishingHud.cs',fishing)
def boss(s):
 s=s.replace('        private static readonly Color BarBackColor = UiPalette.PanelBackdrop;\n','')
 a=s.index('        private static void DrawSolid(')
 b=s.index('        private void EnsureStyle()',a)
 return s[:a]+s[b:]
edit('BossHudView.cs',boss)
edit('Loading/UiWarmupText.cs',lambda s:s.replace('InGameUi.LabelFontSize };','InGameUi.LabelFontSize, 32 };'))
print('Polished HUD sizing, contextual prompts, font warmup and recipe labels.')
