from pathlib import Path
import re, uuid
root = Path(__file__).resolve().parents[1]
ui = root/'Assets/_Project/Scripts/UI'
def read(name): return (ui/name).read_text(encoding='utf-8-sig')
def save(name,s): (ui/name).write_text(s,encoding='utf-8',newline='\n')
def replace(s,a,b):
    assert a in s, a[:150]
    return s.replace(a,b)

s=read('InventoryHud.cs')
s=replace(s,'            DrawHotbar(hotbar);','            InGameUi.BeginTooltip();\n            DrawHotbar(hotbar);')
s=replace(s,'                DrawInventoryPanel(hotbar);\n            }','                DrawInventoryPanel(hotbar);\n            }\n            if (_dragFromIndex < 0 && _dragFromEquip < 0) InGameUi.DrawTooltip();')
a=s.index('            int slotCount = hotbar.HotbarSize;',s.index('private void DrawHotbar'))
b=s.index('        private void DrawEnginePrompt',a)
s=s[:a]+'''            const float size = 72f;
            int slotCount = hotbar.HotbarSize;
            float totalWidth = slotCount * size + (slotCount - 1) * SlotGap;
            float startX = Screen.width - totalWidth - HudLayout.ScreenMargin;
            float y = Screen.height - size - HudLayout.ScreenMargin;
            bool rejecting = Time.unscaledTime < _rejectedUntilTime;
            float remaining = Mathf.Max(0, _rejectedUntilTime - Time.unscaledTime);
            float shake = Mathf.Sin(remaining * 60f) * (6f * remaining / RejectShakeSeconds);
            for (int i = 0; i < slotCount; i++)
            {
                bool rejected = rejecting && i == _rejectedSlotIndex;
                var rect = new Rect(startX + i * (size + SlotGap) + (rejected ? shake : 0), y, size, size);
                HotbarSlotView slot = hotbar.GetSlot(i);
                InGameUi.Slot(rect, slot, GetSlotLabel(slot, hotbar.StackSize), i + 1,
                    i == hotbar.SelectedIndex, rejected, _harpoonTier);
            }
        }

'''+s[b:]
s=replace(s,'GUI.Box(slotRect, GetSlotLabel(hotbar.GetSlot(i), hotbar.StackSize));','InGameUi.Slot(slotRect, hotbar.GetSlot(i), GetSlotLabel(hotbar.GetSlot(i), hotbar.StackSize),\n                    i < hotbarSize ? i + 1 : 0, i == hotbar.SelectedIndex, tier: _harpoonTier);')
for expr in ['storage.GetSlot(_storageOpenId, i)','storage.GetBundleSlot(_bundleOpenId, i)']:
    s=replace(s,f'GUI.Box(slotRect, GetSlotLabel({expr}, hotbar.StackSize));',f'InGameUi.Slot(slotRect, {expr}, GetSlotLabel({expr}, hotbar.StackSize), tier: _harpoonTier);')
s=replace(s,'GUI.Box(slotRect, label);','InGameUi.Slot(slotRect, equipped, label, equipment: part);')
s=replace(s,'GUI.Box(dragRect, GetSlotLabel(dragSlot, hotbar.StackSize));','InGameUi.Slot(dragRect, dragSlot, GetSlotLabel(dragSlot, hotbar.StackSize), tier: _harpoonTier, hover: false);')
s=replace(s,'GUI.Box(rect, "인벤토리 / 캐릭터 상태 [I·Tab 닫기] — 드래그로 재배치 · 패널 밖 드롭 = 버리기");','InGameUi.Panel(rect, "인벤토리 · 캐릭터 상태  [I·Tab 닫기]");')
s=s.replace('GUI.Box(rect, "공유 창고','InGameUi.Panel(rect, "공유 창고').replace('GUI.Box(rect, "보따리','InGameUi.Panel(rect, "보따리')
s=s.replace('착용 — 장비를 끌어다 놓기 (기획서 §6.3)','착용 — 장비를 끌어다 놓기')
s=replace(s,'HudLayout.InventoryPanelX, HudLayout.CenteredY(panelHeight)','HudLayout.InventoryX(_craftingOpen), HudLayout.CenteredY(panelHeight)')
# Each existing prompt retains its trigger, wording and input authority.
for method,key in [('DrawStoragePrompt','Interact'),('DrawBundlePrompt','Interact'),('DrawMountPrompt','Interact'),('DrawEnginePrompt','Refuel'),('DrawCarBuildPrompt','Build'),('DrawCarRecouplePrompt','Recouple'),('DrawHammerTarget','Repair'),('DrawPlankAim','Build')]:
    a=s.index('        private void '+method+'(')
    b=s.find('\n        private ',a+10)
    if b<0:b=len(s)
    chunk=s[a:b]
    chunk=chunk.replace('GUI.Label(', 'InGameUi.Prompt(')
    # Label calls in these methods all use Rect + one string and terminate on ;.
    chunk=re.sub(r'(InGameUi.Prompt\([\s\S]*?)(\);)',lambda m:m[1]+', "G_Action_'+key+'"'+m[2],chunk)
    s=s[:a]+chunk+s[b:]
s=replace(s,'GUILayout.Label(_maxHealth > 0f ?', 'InGameUi.StatusRow("G_Status_Health", _maxHealth > 0f ?')
s=replace(s,'GUILayout.Label(_temperature > 0f ?', 'InGameUi.StatusRow("G_Status_Temperature", _temperature > 0f ?')
s=replace(s,'GUILayout.Label(_maxHunger > 0f ?', 'InGameUi.StatusRow("G_Status_Hunger", _maxHunger > 0f ?')
save('InventoryHud.cs',s)

s=read('HudLayout.cs').replace('SlotGap = 6f','SlotGap = 8f').replace('PanelGap = 12f','PanelGap = 16f').replace('ScreenMargin = 12f','ScreenMargin = 24f').replace('StatusColumnHeight = 260f','StatusColumnHeight = 360f')
s=replace(s,'return Mathf.Max(ScreenMargin, InventoryPanelX - craftingWidth - PanelGap);','return Mathf.Max(ScreenMargin, (Screen.width - InventoryPanelWidth - craftingWidth - PanelGap) * 0.5f);')
s=replace(s,'        /// <summary>창의 상단 y', '''        public static float InventoryX(bool craftingOpen) => craftingOpen
            ? CraftingPanelX(392f) + 392f + PanelGap : InventoryPanelX;

        /// <summary>창의 상단 y''')
save('HudLayout.cs',s)

s=read('CraftingHud.cs')
s=replace(s,'GUI.Label(new Rect(Screen.width * 0.5f - 100f','InGameUi.Prompt(new Rect(Screen.width * 0.5f - 100f')
s=replace(s,'$"<color={UiPalette.HexFocusBrass}>E — 제작</color>");','$"<color={UiPalette.HexFocusBrass}>E — 제작</color>", "G_Action_Craft");')
s=replace(s,'contentHeight += 26f + groups[g].Count * 64f;','contentHeight += 26f;\n                    foreach (int index in groups[g]) contentHeight += RecipeHeight(station.GetRecipe(index));')
s=s.replace('const float panelWidth = 380f','const float panelWidth = 392f').replace('GUI.Box(rect,','InGameUi.Panel(rect,')
a=s.index('                    GUI.Label(new Rect(16f, y, 220f')
b=s.index('                    // 비활성 버튼',a)
s=s[:a]+'''                    string outputKey = recipe.IsHarpoonTierOutput ? "I_Item_Harpoon"
                        : recipe.IsItemOutput ? "I_Item_" + recipe.OutputItem : "I_Res_" + recipe.Output;
                    InGameUi.Image(new Rect(12, y, 48, 48), outputKey);
                    InGameUi.Label(new Rect(68, y, contentRect.width - 176, 48), BuildOutputLine(recipe));
                    for (int ingredientIndex = 0; ingredientIndex < ingredients.Length; ingredientIndex++)
                    {
                        var ingredient = ingredients[ingredientIndex];
                        int have = HotbarLogic.CountResource(slots, ingredient.Type);
                        float ix = 12 + (ingredientIndex % 2) * ((contentRect.width - 24) / 2);
                        float iy = y + 52 + (ingredientIndex / 2) * 34;
                        InGameUi.Image(new Rect(ix, iy, 32, 32), "I_Res_" + ingredient.Type);
                        string color = have >= ingredient.Count ? UiPalette.HexTextSteam : UiPalette.HexCriticalText;
                        InGameUi.Label(new Rect(ix + 36, iy, (contentRect.width - 24) / 2 - 38, 32),
                            $"{GetName(ingredient.Type)}\\n<color={color}>{have}/{ingredient.Count}</color>", true);
                    }

'''+s[b:]
s=replace(s,'GUI.Button(new Rect(contentRect.xMax - 84f, y + 6f, 76f, 30f)', 'InGameUi.Button(new Rect(contentRect.xMax - 100f, y + 4f, 92f, 40f)')
s=replace(s,'y += 64f;','y += RecipeHeight(recipe);')
s=replace(s,'        private static int GetGroup(CraftingRecipe recipe)','        private static float RecipeHeight(CraftingRecipe recipe) => 60f + Mathf.Ceil(recipe.IngredientCount / 2f) * 34f;\n\n        private static int GetGroup(CraftingRecipe recipe)')
save('CraftingHud.cs',s)

s=read('CoreLoopHud.cs')
s=replace(s,'            DrawBanners();','            DrawBanners();\n            DrawAmmoLine();')
s=replace(s,'            DrawAmmoLine();\n            DrawKillLine();','            DrawKillLine();')
s=replace(s,'GUILayout.Label(\n                $"Day','InGameUi.StatusRow(cycle.Phase == DayPhase.Night ? "G_Status_Night" : "G_Status_Day",\n                $"Day')
s=replace(s,'DrawStatusLine(UiStatusLevel.Critical, text);','InGameUi.StatusRow("G_Status_Health", text, _health / _maxHealth, UiPalette.CriticalText);')
s=replace(s,'DrawFaded(text, RestingAlpha);','InGameUi.StatusRow("G_Status_Health", text, _health / _maxHealth, alpha: RestingAlpha);')
s=replace(s,'DrawStatusLine(UiStatusLevel.Critical, $"연료 0 — 감속 중!");','InGameUi.StatusRow("G_Status_Fuel", "연료 0 — 감속 중!", 0, UiPalette.CriticalText);')
s=replace(s,'DrawFaded($"연료 {_fuel:F0} / {_fuelCapacity:F0}  (-{_fuelConsumptionPerSecond:F2}/s)", RestingAlpha);','InGameUi.StatusRow("G_Status_Fuel", $"연료 {_fuel:F0} / {_fuelCapacity:F0}  (-{_fuelConsumptionPerSecond:F2}/s)", _fuel / _fuelCapacity, alpha: RestingAlpha);')
s=replace(s,'DrawStatusLine(UiStatusLevel.Critical, $"이탈 칸 {_detachedCars}");','InGameUi.StatusRow("G_Status_DetachedCar", $"이탈 칸 {_detachedCars}", tint: UiPalette.CriticalText);')
s=replace(s,'GUILayout.Label($"{_ammoWeaponName}  {state}  ·  예비 {_reserveRounds}");','InGameUi.Prompt(new Rect(Screen.width - 392 - HudLayout.ScreenMargin, Screen.height - 136, 392, 32),\n                $"{_ammoWeaponName}  {state}  ·  예비 {_reserveRounds}", _reloading ? "G_Action_Reload" : "G_Status_Ammo");')
s=replace(s,'DrawFaded($"처치 {_killCount}", RestingAlpha);','InGameUi.StatusRow("G_System_Confirm", $"처치 {_killCount}", alpha: RestingAlpha);')
s=replace(s,'DrawFaded($"<color={UiPalette.HexSafeText}>{_buffText}</color>", alpha);','InGameUi.StatusRow(_warmthBuffSeconds > 0 ? "G_Status_Warmth" : "G_Status_Regen",\n                $"<color={UiPalette.HexSafeText}>{_buffText}</color>", tint: UiPalette.SafeText, alpha: alpha);')
s=replace(s,'DrawFaded($"<color={UiPalette.StatusHex(level)}>{text}{suffix}</color>", alpha);','InGameUi.StatusRow("G_Status_Hunger", $"<color={UiPalette.StatusHex(level)}>{text}{suffix}</color>",\n                _maxHunger > 0 ? _hunger / _maxHunger : 0, UiPalette.StatusTextColor(level), alpha);')
s=replace(s,'DrawFaded($"<color={UiPalette.StatusHex(UiStatusLevel.Alert)}>{text}{suffix}</color>", alpha);','InGameUi.StatusRow(_temperatureStress == TemperatureStress.Heat ? "G_Status_Heat" : "G_Status_Cold",\n                $"<color={UiPalette.HexAlertText}>{text}{suffix}</color>", tint: UiPalette.AlertFill, alpha: alpha);')
s=replace(s,'GUI.Label(HudLayout.BannerSlotRect(i), _visibleBanners[i].Text, _bannerStyle);','InGameUi.Prompt(HudLayout.BannerSlotRect(i), _visibleBanners[i].Text,\n                    _visibleBanners[i].Priority == HudBannerPriority.Notice ? "G_System_Confirm" : "G_System_Warning");')
save('CoreLoopHud.cs',s)

s=read('FishingHud.cs')
s=replace(s,'GUI.Label(rect, "입질!  클릭", _biteStyle);','InGameUi.Prompt(rect, "입질!  왼쪽 클릭", "G_Action_FishBite");')
s=replace(s,'GUI.Label(rect, "…기다리는 중", _waitStyle);','InGameUi.Prompt(rect, "…기다리는 중", "I_Item_FishingRod");')
s=replace(s,'GUI.Label(rect, $"생선 {_catchCount}마리", _waitStyle);','InGameUi.Prompt(rect, $"생선 +{_catchCount}", "I_Res_Fish");')
save('FishingHud.cs',s)
s=read('FrostbiteHudView.cs').replace('GUI.Label(rect, builder.ToString(), _labelStyle);','InGameUi.Prompt(rect, builder.ToString(), "G_Status_Frostbite");')
save('FrostbiteHudView.cs',s)
s=read('BossHudView.cs')
a=s.index('            DrawSolid(back, BarBackColor);')
b=s.index('            string phaseLabel',a)
s=s[:a]+'''            InGameUi.Gauge(back, _current / _max, BarFillColor);

'''+s[b:]
save('BossHudView.cs',s)
s=read('SliceHud.cs')
s=s.replace('GUILayout.Label($"자원 {_resourceTotal}");','InGameUi.StatusRow("G_Action_Grab", $"팀 획득 자원 {_resourceTotal}");')
s=s.replace('GUILayout.Label($"<color={UiPalette.HexAlertText}>경고','InGameUi.StatusRow("G_System_Warning", $"<color={UiPalette.HexAlertText}>경고')
s=s.replace('GUILayout.Label($"<color={UiPalette.HexCriticalText}>플레이어','InGameUi.StatusRow("G_Status_DetachedCar", $"<color={UiPalette.HexCriticalText}>플레이어')
s=s.replace('GUI.Label(new Rect(Screen.width * 0.5f - 180f','InGameUi.Prompt(new Rect(Screen.width * 0.5f - 180f')
s=s.replace('$"<color={UiPalette.HexCriticalText}>{_grabRejectMessage}</color>");','$"<color={UiPalette.HexCriticalText}>{_grabRejectMessage}</color>", "G_System_Cancel");')
save('SliceHud.cs',s)

def meta(path,folder=False):
    p=Path(str(path)+'.meta')
    if not p.exists(): p.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'+('folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n' if folder else ''),encoding='utf-8')
for p in [ui/'InGameUi.cs',ui/'InGameUiArt.cs']:meta(p)
resources=root/'Assets/_Project/Resources'
resources.mkdir(exist_ok=True)
meta(resources,True)
guid=re.search(r'guid: (\w+)',Path(str(ui/'InGameUiArt.cs')+'.meta').read_text())[1]
asset=resources/'InGameUiArt.asset'
yaml=f'''%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}
  m_Name: InGameUiArt
  m_EditorClassIdentifier:
  _entries:
'''
for png in sorted((root/'Assets/_Project/Art/Textures/UI/InGame').rglob('*.png')):
    guid=re.search(r'guid: (\w+)',Path(str(png)+'.meta').read_text())[1]
    yaml+=f'  - Key: {png.stem}\n    Sprite: {{fileID: 21300000, guid: {guid}, type: 3}}\n'
asset.write_text(yaml,encoding='utf-8')
meta(asset)
print('Integrated UI sprites and HUD rendering.')
