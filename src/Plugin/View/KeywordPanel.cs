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
        var lines = new List<(string Text, Color Ink, IReadOnlyList<ChipIcon> Chips, bool Grayed)> { ("키워드 (2종까지)", OpenInk, Array.Empty<ChipIcon>(), false) };
        lines.AddRange(_rows.Select(Line));

        var panel = RuntimeObject.New(GameUi.Require("FairyGUI", "GComponent"));
        try
        {
            var padding = _textSize * 0.6f;
            var labels = lines.Select(line => Ui.Text(line.Text, _textSize, line.Ink, 0f, wrap: false)).ToList();
            var lineHeight = labels.Max(label => label.Get<float>("height"));
            var textWidth = labels.Max(label => label.Get<float>("width"));
            var iconSize = lineHeight * 0.9f;
            var iconStart = padding + textWidth + padding;
            var width = lines.Max(line => line.Chips.Count == 0 ? padding + textWidth : iconStart + line.Chips.Count * (iconSize + 2f)) + padding;
            var height = lineHeight * labels.Count + padding * 2;

            panel.Call("AddChild", Ui.Graph(width, height, Background, padding));
            for (var i = 0; i < labels.Count; i++)
            {
                var y = padding + lineHeight * i;
                labels[i].Call("SetXY", padding, y);
                panel.Call("AddChild", labels[i]);
                for (var c = 0; c < lines[i].Chips.Count; c++)
                {
                    var chip = lines[i].Chips[c];
                    var icon = ChipIconLoader(chip.Icon, iconSize, lines[i].Grayed);
                    icon.Call("SetXY", iconStart + c * (iconSize + 2f), y + (lineHeight - iconSize) / 2);
                    panel.Call("AddChild", icon);
                    Icons.Add((icon, chip.Name));
                }
            }
            panel.Call("SetSize", width, height);
            panel.Call("SetXY", padding, padding);
            _container!.Call("AddChild", panel);
            return panel;
        }
        catch
        {
            GameUi.Dispose(panel);
            Icons.Clear();
            throw;
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
