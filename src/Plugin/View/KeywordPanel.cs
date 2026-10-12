using ChipGuide.Core;
using ChipGuide.Plugin.Game;
using ChipGuide.Plugin.Runtime;
using UnityEngine;

namespace ChipGuide.Plugin.View;

internal sealed record KeywordRow(Keyword Keyword, KeywordState State, int Count, IReadOnlyList<ChipIcon> Chips);

internal static class KeywordPanel
{
    private const int FillScale = 1;

    private static readonly Color OwnedInk = new(1f, 0.81f, 0.2f, 1f), OpenInk = Color.white,
        LockedInk = new(0.5f, 0.5f, 0.5f, 1f), PreviewInk = new(0.93f, 0.55f, 0.13f, 1f),
        Background = new(0.04f, 0.05f, 0.07f, 0.85f);

    private static readonly List<(RuntimeObject Icon, string Name)> Icons = new();
    private static RuntimeObject? _panel;
    private static RuntimeObject? _container;
    private static RuntimeObject? _nameTip;
    private static IntPtr _nameTipFor;
    private static IReadOnlyList<KeywordRow> _rows = Array.Empty<KeywordRow>();
    private static IReadOnlySet<Keyword> _preview = new HashSet<Keyword>();
    private static int _textSize;

    internal static bool Alive => GameUi.Alive(_panel);

    internal static void Show(RuntimeObject container, IReadOnlyList<KeywordRow> rows, int textSize)
    {
        Clear();
        _container = container;
        _rows = rows;
        _textSize = textSize;
        _panel = Build();
    }

    // 고르면 막히게 될 키워드를 미리 보여 준다. 마우스가 옮겨질 때만 다시 그린다.
    internal static void Preview(IReadOnlySet<Keyword> keywords)
    {
        if (_container == null || keywords.SetEquals(_preview)) return;
        _preview = keywords;
        Dispose();
        _panel = Build();
    }

    internal static bool UnderPointer(RuntimeObject root) => Alive && GameUi.IsUnderPointer(root, _panel!);

    internal static void UpdateHover(RuntimeObject root)
    {
        if (!Alive) return;
        var hovered = Icons.FirstOrDefault(icon => GameUi.IsUnderPointer(root, icon.Icon));
        if (hovered.Icon == null)
        {
            HideNameTip();
            return;
        }
        if (_nameTipFor == hovered.Icon.Pointer && GameUi.Alive(_nameTip)) return;
        HideNameTip();
        _nameTip = Ui.Text(hovered.Name, _textSize, Color.white, 0f, wrap: false);
        var x = hovered.Icon.Get<float>("x");
        var y = hovered.Icon.Get<float>("y") + hovered.Icon.Get<float>("height");
        _nameTip.Call("SetXY", x, y);
        _panel!.Call("AddChild", _nameTip);
        _nameTipFor = hovered.Icon.Pointer;
    }

    internal static void Clear()
    {
        try { Dispose(); }
        finally
        {
            _container = null;
            _preview = new HashSet<Keyword>();
        }
    }

    private static void Dispose()
    {
        try { GameUi.Dispose(_panel); }
        finally
        {
            _panel = null;
            _nameTip = null;
            _nameTipFor = IntPtr.Zero;
            Icons.Clear();
        }
    }

    private static void HideNameTip()
    {
        GameUi.Dispose(_nameTip);
        _nameTip = null;
        _nameTipFor = IntPtr.Zero;
    }

    private static RuntimeObject Build()
    {
        var segments = new List<(string Text, Color Ink, IReadOnlyList<ChipIcon> Chips, bool Grayed)> { ("키워드 (2종까지)", OpenInk, Array.Empty<ChipIcon>(), false) };
        segments.AddRange(_rows.Select(Line));

        var panel = RuntimeObject.New(GameUi.Require("FairyGUI", "GComponent"));
        try
        {
            var padding = _textSize * 0.6f;
            var labels = segments.Select(segment => Ui.Text(segment.Text, _textSize, segment.Ink, 0f, wrap: false)).ToList();
            var lineHeight = labels.Max(label => label.Get<float>("height"));
            var iconSize = lineHeight * 0.9f;
            var height = lineHeight + padding * 2;

            var x = padding * 1.5f;
            for (var i = 0; i < labels.Count; i++)
            {
                if (i > 0) x += padding * 2;
                labels[i].Call("SetXY", x, padding);
                panel.Call("AddChild", labels[i]);
                x += labels[i].Get<float>("width");
                foreach (var chip in segments[i].Chips)
                {
                    x += 2f;
                    var icon = ChipIconLoader(chip.Icon, iconSize, segments[i].Grayed);
                    icon.Call("SetXY", x, padding + (lineHeight - iconSize) / 2);
                    panel.Call("AddChild", icon);
                    Icons.Add((icon, chip.Name));
                    x += iconSize;
                }
            }
            var width = x + padding * 1.5f;
            var background = Ui.Graph(width, height, Background, height / 2);
            // 배경도 터치를 받아야 아이콘 사이로 마우스가 지나가도 패널 위로 인식된다.
            background.Set("touchable", true);
            panel.Call("AddChildAt", background, 0);
            panel.Call("SetSize", width, height);

            var containerWidth = _container!.Get<float>("width");
            var top = TitleTop(_container) is { } title ? title - height - padding * 0.5f : padding;
            panel.Call("SetXY", Math.Max(padding, (containerWidth - width) / 2), Math.Max(0f, top));
            _container.Call("AddChild", panel);
            return panel;
        }
        catch
        {
            GameUi.Dispose(panel);
            Icons.Clear();
            throw;
        }
    }

    // 창 제목("강화칩을 선택해주세요")은 이름 없는 글자 요소라, 칩 목록보다 위에 있는 글자 중 가장 위의 것을 제목으로 본다.
    private static float? TitleTop(RuntimeObject container)
    {
        try
        {
            var listTop = container.Field("list_Relic")?.Get<float>("y") ?? float.MaxValue;
            float? top = null;
            var count = container.Get<int>("numChildren");
            for (var i = 0; i < count; i++)
            {
                var child = container.Call("GetChildAt", i);
                if (child == null || child.TypeName is not ("GTextField" or "GRichTextField") || !child.Get<bool>("visible")) continue;
                var y = child.Get<float>("y");
                if (y < listTop && (top == null || y < top)) top = y;
            }
            return top;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"칩 선택 창 제목 위치를 읽지 못해 창 맨 위에 둡니다: {ex.Message}");
            return null;
        }
    }

    // 마우스를 올렸을 때 이름을 보여 주려면 아이콘만 터치를 받아야 한다. 배경과 글자는 터치를 받지 않는다.
    private static RuntimeObject ChipIconLoader(string url, float size, bool grayed)
    {
        var icon = RuntimeObject.New(GameUi.Require("FairyGUI", "GLoader"));
        icon.Call("SetSize", size, size);
        icon.Set("fill", FillScale);
        icon.Set("url", url);
        icon.Set("grayed", grayed);
        return icon;
    }

    // 색만으로 구분하지 않도록 기호와 문구도 함께 바꾼다. 막혔거나 막힐 키워드에만 칩 아이콘을 붙인다.
    private static (string Text, Color Ink, IReadOnlyList<ChipIcon> Chips, bool Grayed) Line(KeywordRow row)
    {
        var name = KeywordLock.Name(row.Keyword);
        if (_preview.Contains(row.Keyword)) return ($"※ {name}  고르면 막힘", PreviewInk, row.Chips, false);
        return row.State switch
        {
            KeywordState.Owned => ($"● {name}  {row.Count}개", OwnedInk, Array.Empty<ChipIcon>(), false),
            KeywordState.Locked => ($"× {name}  막힘", LockedInk, row.Chips, true),
            _ => ($"○ {name}", OpenInk, Array.Empty<ChipIcon>(), false),
        };
    }
}
