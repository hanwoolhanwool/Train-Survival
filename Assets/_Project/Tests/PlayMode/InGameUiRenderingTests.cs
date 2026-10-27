using System.Collections;
using System.IO;
using System.Reflection;
using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Crafting;
using Game.Gameplay.Inventory;
using Game.Gameplay.Player;
using Game.Gameplay.Train;
using Game.Gameplay.World;
using Game.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Tests.PlayMode
{
    /// <summary>Render the actual HUD components against deterministic service data.</summary>
    public sealed class InGameUiRenderingTests
    {
        private GameObject _root;
        private InventoryHud _inventory;
        private UiRepaintProbe _probe;
        private SampleInventory _hotbar;
        private Game.UI.Debugging.PerfHudView _perf;
        private bool _perfWasEnabled;
#if UNITY_EDITOR
        private uint _previousWidth, _previousHeight;
        private UiInteractionWindow _inputWindow;
#endif

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (Application.isBatchMode) Assert.Ignore("Visual HUD tests require an editor Game view; use -runTests without -batchmode.");
#if UNITY_EDITOR
            PlayModeWindow.GetRenderingResolution(out _previousWidth, out _previousHeight);
            PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "UI verification 1080p");
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Show();
#endif
            ServiceLocator.Clear();
            _root = new GameObject("UI integration fixture");
            Camera camera = _root.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.23f, 0.29f, 0.3f);
            _hotbar = new SampleInventory();
            ServiceLocator.Register<ILocalHotbar>(_hotbar);
            ServiceLocator.Register<ITrainStorage>(new SampleStorage());
            _perf = Object.FindFirstObjectByType<Game.UI.Debugging.PerfHudView>();
            if (_perf != null) { _perfWasEnabled = _perf.enabled; _perf.enabled = false; }
            _inventory = _root.AddComponent<InventoryHud>();
            _probe = _root.AddComponent<UiRepaintProbe>();
            _root.AddComponent<CoreLoopHud>();
            _root.AddComponent<SliceHud>();
            _root.AddComponent<FishingHud>();
            CraftingHud crafting = _root.AddComponent<CraftingHud>();
#if UNITY_EDITOR
            ResourceCatalog catalog = AssetDatabase.LoadAssetAtPath<ResourceCatalog>("Assets/_Project/Data/ResourceCatalog.asset");
            Set(_inventory, "_catalog", catalog);
            Set(_inventory, "_structureCatalog", AssetDatabase.LoadAssetAtPath<StructureCatalog>("Assets/_Project/Data/StructureCatalog.asset"));
            Set(crafting, "_catalog", catalog);
            ServiceLocator.Register<ICraftingStation>(new SampleStation());
#endif
            EventBus<PlayerHealthChangedEvent>.Publish(new PlayerHealthChangedEvent(0, true, 24, 100));
            EventBus<PlayerHungerChangedEvent>.Publish(new PlayerHungerChangedEvent(0, true, 32, 100, HungerStress.Hungry));
            EventBus<PlayerTemperatureChangedEvent>.Publish(new PlayerTemperatureChangedEvent(0, true, 35, TemperatureStress.Cold));
            EventBus<FuelChangedEvent>.Publish(new FuelChangedEvent(56, 100, 0.8f));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_root);
            if (_perf != null) _perf.enabled = _perfWasEnabled;
            ServiceLocator.Clear();
#if UNITY_EDITOR
            if (_inputWindow != null) _inputWindow.Close();
            if (_previousWidth > 0 && _previousHeight > 0)
                PlayModeWindow.SetCustomRenderingResolution(_previousWidth, _previousHeight, "Restored resolution");
#endif
            yield return null;
        }

        [UnityTest]
        public IEnumerator Hud_and_inventory_render_with_imported_sprites()
        {
            Assert.That(InGameUi.Art.Entries.Count, Is.EqualTo(97));
            yield return Capture("hud");
            Set(_inventory, "_panelOpen", true);
            yield return Capture("inventory");
            Assert.That(_probe.Repaints, Is.GreaterThan(0), "The actual OnGUI render loop must run.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Construction_selection_and_target_render_with_their_icons()
        {
            foreach (StructureKind kind in System.Enum.GetValues(typeof(StructureKind)))
            {
                if (InGameUi.Art.Structure(kind) == null) continue;
                EventBus<HammerTargetLocalEvent>.Publish(new HammerTargetLocalEvent(true, TrainPartKind.Car, 1,
                    default, 100, 100, false, true, kind, 50, false, false, 0, 0));
                for (int frame = 0; frame < 3; frame++) yield return null;
                if (kind == StructureKind.Heater || kind == StructureKind.Tent)
                    yield return Capture("construction-" + kind);
            }
            EventBus<HammerTargetLocalEvent>.Publish(new HammerTargetLocalEvent(true, TrainPartKind.Structure, 7,
                StructureKind.Storage, 35, 100, true, false, default, 0, false, true, 5, 0.55f));
            yield return Capture("construction-target");
            Assert.That(_probe.Repaints, Is.GreaterThan(0));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Crafting_and_inventory_render_together_without_GUI_errors()
        {
            EventBus<CraftingPanelToggledLocalEvent>.Publish(new CraftingPanelToggledLocalEvent(true));
            yield return Capture("crafting-inventory");
            Assert.That(_probe.Repaints, Is.GreaterThan(0));
            Assert.That(HudLayout.InventoryX(true), Is.GreaterThanOrEqualTo(HudLayout.CraftingPanelX(392) + 392 + HudLayout.PanelGap));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Storage_and_bundle_render_the_same_item_icons()
        {
            Set(_inventory, "_storageOpenId", 7);
            yield return Capture("storage");
            Set(_inventory, "_storageOpenId", -1);
            Set(_inventory, "_bundleOpen", true);
            Set(_inventory, "_bundleOpenId", 8UL);
            yield return Capture("bundle");
            LogAssert.NoUnexpectedReceived();
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator Icon_slots_keep_swap_equip_and_disabled_drop_behaviour()
        {
            Set(_inventory, "_panelOpen", true);
            _inventory.enabled = false;
            _inputWindow = ScriptableObject.CreateInstance<UiInteractionWindow>();
            UiInteractionWindow input = _inputWindow;
            input.Target = _inventory;
            input.position = new Rect(100, 100, 1000, 800);
            input.ShowUtility();
            for (int i = 0; i < 6; i++) yield return null;
            float x = input.FirstSlotCenter.x;
            float top = input.FirstSlotCenter.y - 54 - 32;
            yield return Send(input, EventType.MouseDown, new Vector2(x, top + 54 + 32));
            yield return Send(input, EventType.MouseUp, new Vector2(x + 72, top + 54 + 32));
            Assert.That(_hotbar.Swap, Is.EqualTo(new Vector2Int(0, 1)));
            yield return Send(input, EventType.MouseDown, new Vector2(x + 4 * 72, top + 154 + 32));
            yield return Send(input, EventType.MouseUp, new Vector2(x + 72, top + 330 + 32));
            Assert.That(_hotbar.Equipped, Is.EqualTo(9));
            yield return Send(input, EventType.MouseDown, new Vector2(x + 3 * 72, top + 54 + 32));
            yield return Send(input, EventType.MouseUp, new Vector2(8, 8));
            Assert.That(_hotbar.Drops, Is.Zero, "Image drag must not enable the disabled drop operation.");
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator Send(UiInteractionWindow window, EventType type, Vector2 position)
        {
            window.SendEvent(new Event { type = type, mousePosition = position, button = 0 });
            yield return null;
        }
#endif

        private static IEnumerator Capture(string name)
        {
            // CaptureScreenshot is deferred by Unity; avoid WaitForEndOfFrame which stalls in batch mode.
            for (int frame = 0; frame < 12; frame++) yield return null;
            string directory = Path.GetFullPath("output/ui-integration");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, name + ".png");
            if (File.Exists(path)) File.Delete(path);
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                ScreenCapture.CaptureScreenshot(path);
                for (int frame = 0; frame < 100 && !File.Exists(path); frame++) yield return new WaitForSecondsRealtime(0.1f);
                Assert.That(File.Exists(path), Is.True, "Unity did not save the HUD capture.");
            }
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private sealed class SampleInventory : ILocalHotbar
        {
            public Vector2Int Swap = new Vector2Int(-1, -1);
            public int Equipped = -1;
            public int Drops;
            private readonly HotbarSlotView[] _slots =
            {
                new HotbarSlotView(HotbarItemType.Harpoon, 1),
                new HotbarSlotView(HotbarItemType.Revolver, 1),
                new HotbarSlotView(HotbarItemType.Hammer, 1),
                new HotbarSlotView(HotbarItemType.Resource, 12, ResourceType.Wood),
                new HotbarSlotView(HotbarItemType.Resource, 8, ResourceType.CookedMeal),
                new HotbarSlotView(HotbarItemType.Resource, 25, ResourceType.Scrap),
                new HotbarSlotView(HotbarItemType.Resource, 8, ResourceType.Niter),
                new HotbarSlotView(HotbarItemType.Resource, 21, ResourceType.RevolverAmmo),
                new HotbarSlotView(HotbarItemType.Bundle, 135),
                new HotbarSlotView(HotbarItemType.WinterParka, 1),
                new HotbarSlotView(HotbarItemType.FishingRod, 1),
                new HotbarSlotView(HotbarItemType.Resource, 2, ResourceType.Fish),
                default, default, default
            };
            public int SlotCount => _slots.Length;
            public int HotbarSize => 5;
            public int StackSize => 30;
            public int SelectedIndex => 0;
            public HotbarSlotView GetSlot(int index) => _slots[index];
            public HotbarSlotView GetEquipmentSlot(int partIndex) => partIndex == 0 ? new HotbarSlotView(HotbarItemType.FurHood, 1) : default;
            public void RequestSwap(int a, int b) => Swap = new Vector2Int(a, b);
            public void RequestDrop(int slotIndex, int amount) => Drops++;
            public void RequestEquip(int slotIndex) => Equipped = slotIndex;
            public void RequestUnequip(int partIndex) { }
        }

        private sealed class SampleStorage : ITrainStorage
        {
            public int SlotsPerStorage => 10;
            public HotbarSlotView GetSlot(int storageId, int slotIndex) => slotIndex < 3
                ? new HotbarSlotView(HotbarItemType.Resource, slotIndex + 2, ResourceType.RareMetal) : default;
            public HotbarSlotView GetBundleSlot(ulong id, int index) => GetSlot(0, index);
            public int GetBundleSlotCount(ulong id) => 5;
            public void RequestTransfer(int id, byte from, int fromIndex, byte to, int toIndex) { }
            public void RequestBundleTransfer(ulong id, byte from, int fromIndex, byte to, int toIndex) { }
            public void ServerAllocateBlock(int id) { }
            public void ServerReleaseBlock(int id, StorageReleaseMode mode) { }
        }

#if UNITY_EDITOR
        private sealed class SampleStation : ICraftingStation
        {
            private readonly CraftingRecipe[] _recipes =
            {
                AssetDatabase.LoadAssetAtPath<CraftingRecipe>("Assets/_Project/Data/Recipe_HarpoonTier2.asset"),
                AssetDatabase.LoadAssetAtPath<CraftingRecipe>("Assets/_Project/Data/Recipe_HarpoonTier3.asset"),
                AssetDatabase.LoadAssetAtPath<CraftingRecipe>("Assets/_Project/Data/Recipe_RevolverAmmo.asset"),
                AssetDatabase.LoadAssetAtPath<CraftingRecipe>("Assets/_Project/Data/Recipe_WinterParka.asset"),
                AssetDatabase.LoadAssetAtPath<CraftingRecipe>("Assets/_Project/Data/Recipe_CookedMeal.asset")
            };
            public int RecipeCount => _recipes.Length;
            public bool IsLocalPlayerInRange => true;
            public CraftingRecipe GetRecipe(int index) => _recipes[index];
            public bool IsRecipeAvailable(int index) => true;
            public void RequestCraft(int recipeIndex) { }
        }
#endif
    }

    public sealed class UiRepaintProbe : MonoBehaviour
    {
        public int Repaints { get; private set; }
        private void OnGUI()
        {
            if (Event.current.type == EventType.Repaint) Repaints++;
        }
    }

#if UNITY_EDITOR
    public sealed class UiInteractionWindow : EditorWindow
    {
        public InventoryHud Target;
        public Vector2 FirstSlotCenter;
        private static readonly MethodInfo Draw = typeof(InventoryHud).GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.NonPublic);
        private void OnGUI()
        {
            if (Target == null) return;
            FirstSlotCenter = new Vector2(HudLayout.InventoryPanelX + 52, HudLayout.CenteredY(548) + 86);
            Draw.Invoke(Target, null);
        }
    }
#endif
}
