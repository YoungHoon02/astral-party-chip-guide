using ChipGuide.Core;
using ChipGuide.Plugin.Runtime;
using UnityEngine;

namespace ChipGuide.Plugin.View;

internal sealed record KeywordRow(Keyword Keyword, KeywordState State, int Count);

internal static class KeywordPanel
{
    private static readonly Color OwnedInk = new(1f, 0.81f, 0.2f, 1f), OpenInk = Color.white,
        LockedInk = new(0.5f, 0.5f, 0.5f, 1f), PreviewInk = new(0.93f, 0.55f, 0.13f, 1f),
        Background = new(0.04f, 0.05f, 0.07f, 0.85f);

    private static RuntimeObject? _panel;
    private static RuntimeObject? _container;
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
        GameUi.Dispose(_panel);
        _panel = Build();
    }

    internal static void Clear()
    {
        try { GameUi.Dispose(_panel); }
        finally
        {
            _panel = null;
            _container = null;
            _preview = new HashSet<Keyword>();
        }
    }

    private static RuntimeObject Build()
    {
        var lines = new List<(string Text, Color Ink)> { ("키워드 (2종까지)", OpenInk) };
        lines.AddRange(_rows.Select(Line));

        var panel = RuntimeObject.New(GameUi.Require("FairyGUI", "GComponent"));
        try
        {
            panel.Set("touchable", false);
            var labels = lines.Select(line => Ui.Text(line.Text, _textSize, line.Ink, 0f, wrap: false)).ToList();
            var padding = _textSize * 0.6f;
            var lineHeight = labels.Max(label => label.Get<float>("height"));
            var width = labels.Max(label => label.Get<float>("width")) + padding * 2;
            var height = lineHeight * labels.Count + padding * 2;
            panel.Call("AddChild", Ui.Graph(width, height, Background, padding));
            for (var i = 0; i < labels.Count; i++)
            {
                labels[i].Call("SetXY", padding, padding + lineHeight * i);
                panel.Call("AddChild", labels[i]);
            }
            panel.Call("SetSize", width, height);
            panel.Call("SetXY", padding, padding);
            _container!.Call("AddChild", panel);
            return panel;
        }
        catch
        {
            GameUi.Dispose(panel);
            throw;
        }
    }

    // 색만으로 구분하지 않도록 기호와 문구도 함께 바꾼다.
    private static (string Text, Color Ink) Line(KeywordRow row)
    {
        var name = KeywordLock.Name(row.Keyword);
        if (_preview.Contains(row.Keyword)) return ($"※ {name}  고르면 막힘", PreviewInk);
        return row.State switch
        {
            KeywordState.Owned => ($"● {name}  {row.Count}개", OwnedInk),
            KeywordState.Locked => ($"× {name}  막힘", LockedInk),
            _ => ($"○ {name}", OpenInk),
        };
    }
}
