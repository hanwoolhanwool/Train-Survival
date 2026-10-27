using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Game.Gameplay.Inventory;
using Game.Gameplay.Train;
using Game.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class InGameUiArtTests
    {
        private static InGameUiArt Load() => Resources.Load<InGameUiArt>("InGameUiArt");

        [Test]
        public void Resource_catalog_contains_every_delivered_sprite_once()
        {
            InGameUiArt art = Load();
            Assert.That(art, Is.Not.Null);
            Assert.That(art.Entries.Count, Is.EqualTo(97));
            var keys = new HashSet<string>();
            foreach (var entry in art.Entries)
            {
                Assert.That(keys.Add(entry.Key), Is.True, entry.Key);
                Assert.That(entry.Sprite, Is.Not.Null, entry.Key);
                Assert.That(entry.Sprite.name, Is.EqualTo(entry.Key));
                Assert.That(art.Find(entry.Key), Is.SameAs(entry.Sprite));
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(entry.Sprite));
                Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite), entry.Key);
                Assert.That(importer.mipmapEnabled, Is.False, entry.Key);
                Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), entry.Key);
                Assert.That(entry.Sprite.rect.size, Is.EqualTo(new Vector2(entry.Sprite.texture.width, entry.Sprite.texture.height)), entry.Key);
            }
            foreach (string path in Directory.GetFiles("Assets/_Project/Art/Textures/UI/InGame", "*.png", SearchOption.AllDirectories))
                Assert.That(keys.Contains(Path.GetFileNameWithoutExtension(path)), Is.True, path);
        }

        private static IEnumerable ResourceCases()
        {
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
                if (type != ResourceType.None) yield return new TestCaseData(type);
        }

        [Test]
        public void Construction_icons_cover_exactly_the_placeable_catalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StructureCatalog>("Assets/_Project/Data/StructureCatalog.asset");
            int count = 0;
            foreach (StructureKind kind in Enum.GetValues(typeof(StructureKind)))
            {
                Sprite sprite = Load().Structure(kind);
                Assert.That(sprite != null, Is.EqualTo(catalog.IsPlaceable(kind)), kind.ToString());
                if (sprite != null)
                {
                    Assert.That(sprite.name, Is.EqualTo(InGameUiArt.StructureKey(kind)));
                    Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(512, 512)));
                    count++;
                }
            }
            Assert.That(count, Is.EqualTo(9));
        }

        [TestCaseSource(nameof(ResourceCases))]
        public void Every_resource_maps_to_its_own_image(ResourceType resource)
        {
            var slot = new HotbarSlotView(HotbarItemType.Resource, 7, resource);
            Assert.That(Load().Item(slot).name, Is.EqualTo("I_Res_" + resource));
            Assert.That(InGameUiArt.QuantityLabel(slot), Is.EqualTo("7"));
        }

        private static IEnumerable EquipmentCases()
        {
            foreach (HotbarItemType type in Enum.GetValues(typeof(HotbarItemType)))
                if (type != HotbarItemType.None && type != HotbarItemType.Resource) yield return new TestCaseData(type);
        }

        [TestCaseSource(nameof(EquipmentCases))]
        public void Every_equipment_maps_to_its_image_without_showing_internal_count(HotbarItemType type)
        {
            var slot = new HotbarSlotView(type, 135);
            Assert.That(Load().Item(slot).name, Is.EqualTo("I_Item_" + type));
            Assert.That(InGameUiArt.QuantityLabel(slot), Is.Empty);
        }

        [Test]
        public void Icon_UI_font_sizes_are_in_the_loading_warmup()
        {
            foreach (float height in new[] { 1080f, 1440f, 2160f })
            {
                int[] sizes = Game.UI.Loading.UiWarmupText.FontSizes(height);
                CollectionAssert.Contains(sizes, InGameUi.SmallFontSize);
                CollectionAssert.Contains(sizes, InGameUi.LabelFontSize);
                CollectionAssert.Contains(sizes, 32);
            }
        }

        [Test]
        public void Empty_slots_do_not_draw_a_resource_or_quantity()
        {
            Assert.That(Load().Item(default), Is.Null);
            var depleted = new HotbarSlotView(HotbarItemType.Resource, 0, ResourceType.Wood);
            Assert.That(Load().Item(depleted), Is.Null);
            Assert.That(InGameUiArt.QuantityLabel(depleted), Is.Empty);
        }

        [TestCase("U_SlotBase")]
        [TestCase("U_SlotOutline")]
        [TestCase("U_PanelBody")]
        [TestCase("U_PanelHeader")]
        [TestCase("U_ControlFace")]
        [TestCase("U_GaugeTrack")]
        [TestCase("U_GaugeFill")]
        public void Resizable_parts_keep_their_sliced_borders(string key)
        {
            Sprite sprite = Load().Find(key);
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.border.sqrMagnitude, Is.GreaterThan(0), key);
        }
    }
}
