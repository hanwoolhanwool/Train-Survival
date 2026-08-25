using System;
using System.Collections.Generic;
using Game.Core.Logging;
using Game.Gameplay.Crafting;
using Game.Gameplay.Inventory;
using Game.Gameplay.Train;
using Game.Systems.Loading;
using UnityEngine;

namespace Game.UI.Loading
{
    /// <summary>
    /// B 묶음 — HUD의 첫 그리기 비용을 로딩 뒤로 옮긴다 —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §5.4.
    ///
    /// <para><b>두 가지를 겹쳐 쓴다</b>:</para>
    /// <list type="number">
    /// <item><description><b>글리프 선요청</b> — <c>Font.RequestCharactersInTexture</c>로
    /// 필요한 문자를 미리 굽는다. 부작용이 없다.</description></item>
    /// <item><description><b>화면 밖 1회 그리기</b> — 글리프만으로는 IMGUI 내부의 스타일·레이아웃
    /// 캐시가 안 채워진다. 그래서 실제 문자열로 <c>GUI.Box</c>/<c>GUI.Label</c>을 화면 밖에
    /// 한 번 그린다.</description></item>
    /// </list>
    ///
    /// <para><b>규칙 둘, 어기면 실제 UI가 오작동한다</b>(§5.4②):</para>
    /// <list type="bullet">
    /// <item><description><b><c>Repaint</c>일 때만 그린다.</b> 입력 이벤트에서 그리면
    /// 워밍업이 클릭·드래그 판정을 먹는다 — 창고 드래그가 망가진다.</description></item>
    /// <item><description><b>실제 패널 코드를 재사용하지 않는다.</b> 문자열만 받아 그린다.
    /// 패널 코드에 워밍업 분기를 심으면 그 분기가 언젠가 진짜 화면에 샌다.</description></item>
    /// </list>
    ///
    /// <para><b>씬을 타지 않는다.</b> 계획 §3.1은 글리프를 ①에, 패널 워밍업을 ③에 뒀지만,
    /// 실제로 이 컴포넌트는 HUD 컴포넌트를 <b>전혀 만지지 않으므로</b>(문자열만 그린다)
    /// 인게임 씬을 기다릴 이유가 없다. 그래서 Boot에 두고 <b>B 묶음으로 한 번에</b> 돈다 —
    /// 가중치 표(§4.1)의 "③ UI 워밍업 0.10"이 그대로 맞는다.</para>
    ///
    /// <para><b>모든 일이 <c>OnGUI</c> 안에서 일어난다.</b> <c>GUI.skin</c>은 <c>OnGUI</c> 밖에서는
    /// 의미가 없기 때문이다 — 실제 HUD가 쓰는 바로 그 폰트를 집으려면 여기서 집어야 한다.
    /// <see cref="Advance"/>는 "한 프레임 몫을 허락한다"만 하고 물러난다.</para>
    /// </summary>
    public sealed class UiWarmupStep : SessionPreloadStepBehaviour
    {
        /// <summary>화면 밖 — 여기 그린 것은 어떤 해상도에서도 보이지 않는다.</summary>
        private static readonly Rect OffScreen = new Rect(-10000f, -10000f, 400f, 200f);

        [Header("문자를 모을 곳")]
        [SerializeField] private ResourceCatalog _resources;
        [SerializeField] private StructureCatalog _structures;
        [SerializeField] private RecipeCatalog _recipes;

        [SerializeField]
        [TextArea(2, 6)]
        [Tooltip("카탈로그에 없는 HUD 고정 문구. 빠뜨려도 그 글자의 첫 그리기 값만 남을 뿐 고장나지 않는다.")]
        private string _phrases;

        private int[] _sizes;
        private string _text;

        private int _total;
        private int _done;

        /// <summary>이번 프레임에 한 단위를 처리해도 되는가 — <see cref="Advance"/>가 켜고 <c>OnGUI</c>가 끈다.</summary>
        private bool _budget;

        public override PreloadPhase Phase => PreloadPhase.AfterSceneLoad;

        /// <summary>크기마다 둘 — 글리프 요청 한 번, 화면 밖 그리기 한 번.</summary>
        public override int Total
        {
            get
            {
                if (_sizes == null)
                {
                    BuildPlan();
                }

                return _total;
            }
        }

        public override int Done => _done;

        public override void Advance()
        {
            _budget = true;
        }

        private void OnGUI()
        {
            if (!_budget || Event.current.type != EventType.Repaint)
            {
                return;
            }

            _budget = false;

            if (_sizes == null || _done >= _total)
            {
                return;
            }

            int unit = _done;
            if (unit < _sizes.Length)
            {
                RequestGlyphs(_sizes[unit]);
            }
            else
            {
                PaintOffScreen(_sizes[unit - _sizes.Length]);
            }

            _done++;

            if (_done >= _total)
            {
                GameLog.Info(LogCategory.Ui, $"UI 워밍업 완료: {_sizes.Length}개 크기 · {_text.Length}자");
            }
        }

        /// <summary>부작용 없는 쪽 — 아틀라스에 글리프만 채운다.</summary>
        private void RequestGlyphs(int size)
        {
            Font font = GUI.skin == null ? null : GUI.skin.font;
            if (font == null || string.IsNullOrEmpty(_text))
            {
                return;
            }

            font.RequestCharactersInTexture(_text, size);
            font.RequestCharactersInTexture(_text, size, FontStyle.Bold);
        }

        /// <summary>
        /// 스타일·레이아웃 캐시까지 채우는 쪽 — <b>실제로 한 번 그린다.</b>
        /// 화면 밖이고 <c>Repaint</c>이므로 보이지도, 입력을 먹지도 않는다.
        /// </summary>
        private void PaintOffScreen(int size)
        {
            if (string.IsNullOrEmpty(_text))
            {
                return;
            }

            var style = new GUIStyle(GUI.skin.label) { fontSize = size, richText = true };
            var boxStyle = new GUIStyle(GUI.skin.box) { fontSize = size };

            GUI.Box(OffScreen, _text, boxStyle);
            GUI.Label(OffScreen, _text, style);
        }

        private void BuildPlan()
        {
            _sizes = UiWarmupText.FontSizes(Screen.height);
            _text = new string(UiWarmupText.Collect(GatherSources()));
            _total = _sizes.Length * 2;
            _done = 0;
        }

        /// <summary>
        /// 화면에 뜰 문자열을 모은다 — <b>표시명은 카탈로그가 소유하므로 카탈로그에서 가져온다.</b>
        /// 여기에 문자열을 베껴 적으면 카탈로그가 바뀔 때 조용히 뒤처진다.
        /// </summary>
        private IEnumerable<string> GatherSources()
        {
            if (!string.IsNullOrEmpty(_phrases))
            {
                yield return _phrases;
            }

            foreach (ResourceType type in (ResourceType[])Enum.GetValues(typeof(ResourceType)))
            {
                yield return _resources == null ? type.ToString() : _resources.GetDisplayName(type);
            }

            foreach (HotbarItemType type in (HotbarItemType[])Enum.GetValues(typeof(HotbarItemType)))
            {
                yield return HotbarItemLabels.GetLabel(type);
            }

            // 집게는 등급별로 표기가 다르다 — 승급 뒤에 처음 뜨는 글자가 남지 않게 함께 굽는다.
            for (int tier = 1; tier <= 3; tier++)
            {
                yield return HotbarItemLabels.GetHarpoonLabel(tier);
            }

            if (_structures != null)
            {
                for (int i = 0; i < _structures.EntryCount; i++)
                {
                    if (_structures.TryGetKindAt(i, out StructureKind kind))
                    {
                        yield return _structures.GetDisplayName(kind);
                    }
                }
            }

            if (_recipes != null)
            {
                for (int i = 0; i < _recipes.Count; i++)
                {
                    CraftingRecipe recipe = _recipes.GetRecipe(i);
                    if (recipe != null)
                    {
                        yield return recipe.DisplayName;
                    }
                }
            }
        }
    }
}
