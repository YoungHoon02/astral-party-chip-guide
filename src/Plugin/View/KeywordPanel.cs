using ChipGuide.Core;
using ChipGuide.Plugin.Game;
using ChipGuide.Plugin.Runtime;
using UnityEngine;

namespace ChipGuide.Plugin.View;

internal sealed record KeywordRow(Keyword Keyword, KeywordState State, int Count, IReadOnlyList<ChipIcon> Chips);

internal static class KeywordPanel
{
    private static readonly Color OwnedInk = new(1f, 0.81f, 0.2f, 1f), OpenInk = Color.white,
        LockedInk = new(0.5f, 0.5f, 0.5f, 1f), PreviewInk = new(0.93f, 0.55f, 0.13f, 1f), GainInk = new(0.45f, 0.9f, 0.45f, 1f),
        Background = new(0.04f, 0.05f, 0.07f, 0.55f);

    private static readonly List<(RuntimeObject Icon, string Name)> Icons = new();
    private static RuntimeObject? _panel;
    private static RuntimeObject? _container;
    private static RuntimeObject? _nameTip;
    private static IntPtr _nameTipFor;
    private static IReadOnlyList<KeywordRow> _rows = Array.Empty<KeywordRow>();
    private static IReadOnlySet<Keyword> _preview = new HashSet<Keyword>();
    private static Keyword _gain;
    private static RoleWeights? _weights;
    private static RoleWeights _weightPreview = RoleWeights.Zero;
    private static int _textSize;
    private static IntPtr _iconClass;

    internal static bool Alive => GameUi.Alive(_panel);

    internal static void Show(RuntimeObject container, IReadOnlyList<KeywordRow>? rows, RoleWeights? weights, int textSize, IntPtr iconClass)
    {
        Clear();
        if (rows == null && weights == null) return;
        _container = container;
        _iconClass = iconClass;
        _rows = rows ?? Array.Empty<KeywordRow>();
        _weights = weights;
        _textSize = textSize;
        _panel = Build();
    }

    // 고르면 얻을 키워드, 막히게 될 키워드, 바뀔 역할군 가중치를 미리 보여 준다. 대상이 바뀔 때만 다시 그린다.
    internal static void Preview(Keyword gain, IReadOnlySet<Keyword> keywords, RoleWeights weights)
    {
        if (_container == null || (gain == _gain && keywords.SetEquals(_preview) && weights == _weightPreview)) return;
        _gain = gain;
        _preview = keywords;
        _weightPreview = weights;
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
            _gain = Keyword.None;
            _weights = null;
            _weightPreview = RoleWeights.Zero;
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
        var rows = new List<List<(string Text, Color Ink, IReadOnlyList<ChipIcon> Chips, bool Grayed)>>();
        if (_rows.Count > 0)
            rows.Add(new[] { ("키워드 (2종까지)", OpenInk, (IReadOnlyList<ChipIcon>)Array.Empty<ChipIcon>(), false) }.Concat(_rows.Select(Line)).ToList());
        if (_weights is { } weights)
            rows.Add(WeightLine(weights));

        var panel = RuntimeObject.New(GameUi.Require("FairyGUI", "GComponent"));
        try
        {
            var padding = _textSize * 0.6f;
            var labels = rows.Select(row => row.Select(segment => Ui.Text(segment.Text, _textSize, segment.Ink, 0f, wrap: false)).ToList()).ToList();
            var lineHeight = labels.SelectMany(row => row).Max(label => label.Get<float>("height"));
            var iconSize = lineHeight * 0.9f;
            var rowWidths = rows.Select((row, r) => row.Select((segment, i) =>
                (i > 0 ? padding * 2 : 0f) + labels[r][i].Get<float>("width") + segment.Chips.Count * (2f + iconSize)).Sum()).ToList();
            var contentWidth = rowWidths.Max();
            for (var r = 0; r < rows.Count; r++)
            {
                var y = padding + lineHeight * r;
                // 막힌 키워드에 칩 아이콘이 붙으면 줄 길이가 달라져서, 줄마다 패널 가운데에 맞춘다.
                var x = padding * 1.5f + (contentWidth - rowWidths[r]) / 2;
                for (var i = 0; i < labels[r].Count; i++)
                {
                    if (i > 0) x += padding * 2;
                    labels[r][i].Call("SetXY", x, y);
                    panel.Call("AddChild", labels[r][i]);
                    x += labels[r][i].Get<float>("width");
                    foreach (var chip in rows[r][i].Chips)
                    {
                        x += 2f;
                        var icon = Ui.ChipIcon(_iconClass, chip.Icon, iconSize, rows[r][i].Grayed);
                        icon.Call("SetXY", x, y + (lineHeight - iconSize) / 2);
                        panel.Call("AddChild", icon);
                        Icons.Add((icon, chip.Name));
                        x += iconSize;
                    }
                }
            }
            var width = contentWidth + padding * 3;
            var height = lineHeight * rows.Count + padding * 2;
            var background = Ui.Graph(width, height, Background, Math.Min(height / 2, padding * 2));
            panel.Call("AddChildAt", background, 0);
            panel.Call("SetSize", width, height);

            var container = _container!;
            var (x0, y0) = Anchor(container, width, height, padding);
            panel.Call("SetXY", x0, y0);
            container.Call("AddChild", panel);
            return panel;
        }
        catch
        {
            GameUi.Dispose(panel);
            Icons.Clear();
            throw;
        }
    }

    // 가장 높은 역할군을 강조하고, 미리보기 중이면 고른 뒤 값을 함께 보여 준다.
    private static List<(string Text, Color Ink, IReadOnlyList<ChipIcon> Chips, bool Grayed)> WeightLine(RoleWeights weights)
    {
        var top = RoleWeights.Kinds.Max(kind => weights[kind]);
        var line = new List<(string, Color, IReadOnlyList<ChipIcon>, bool)> { ("역할군 가중치", OpenInk, Array.Empty<ChipIcon>(), false) };
        foreach (var kind in RoleWeights.Kinds)
        {
            var name = RoleWeights.Name(kind);
            var added = _weightPreview[kind];
            line.Add(added > 0
                ? ($"{name} {weights[kind]}→{weights[kind] + added}", PreviewInk, Array.Empty<ChipIcon>(), false)
                : ($"{name} {weights[kind]}", weights[kind] == top ? OwnedInk : OpenInk, Array.Empty<ChipIcon>(), false));
        }
        return line;
    }

    // 창 제목 띠 안에 두면 제목과 역할군 태그를 가려서, 띠 바로 위 가운데에 둔다. 창이 화면 맨 위보다 아래서 시작할 수 있어 화면 위쪽 끝까지는 창 밖으로 올릴 수 있다.
    private static (float X, float Y) Anchor(RuntimeObject container, float width, float height, float padding)
    {
        var x = Math.Max(padding, (container.Get<float>("width") - width) / 2);
        var screenTop = container.Call("GlobalToLocal", Vector2.zero)!.Value<Vector2>().y;
        try
        {
            if (BandTop(container) is { } top) return (x, Math.Max(screenTop, top - height - padding / 2));
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"창 제목 띠 위치를 읽지 못해 창 맨 위에 둡니다: {ex.Message}");
        }
        return (x, Math.Max(screenTop, padding));
    }

    // 제목 띠 배경은 이름 없는 자식이라, 칩 획득 확률 글자를 세로로 감싸면서 창 폭 대부분을 덮는 자식을 띠로 본다. 없으면 확률 글자 위쪽을 쓴다.
    private static float? BandTop(RuntimeObject container)
    {
        var probability = container.Field("txt_Probability");
        if (probability == null || !probability.Get<bool>("visible")) return null;
        var textTop = probability.Get<float>("y");
        var textBottom = textTop + probability.Get<float>("height");
        var minWidth = container.Get<float>("width") * 0.8f;
        // 창 전체를 덮는 어두운 배경도 같은 조건에 걸리므로 높이로 거른다.
        var maxHeight = container.Get<float>("height") * 0.5f;
        float? band = null;
        var count = container.Get<int>("numChildren");
        for (var i = 0; i < count; i++)
        {
            var child = container.Call("GetChildAt", i);
            if (child == null || child.Pointer == probability.Pointer || !child.Get<bool>("visible")) continue;
            if (child.Get<float>("width") < minWidth || child.Get<float>("height") > maxHeight) continue;
            var y = child.Get<float>("y");
            if (y <= textTop && y + child.Get<float>("height") >= textBottom) band = Math.Max(band ?? y, y);
        }
        if (Plugin.Diagnostics.Value) Plugin.Logger.LogInfo($"[진단] 제목 띠 위쪽={band?.ToString() ?? "못 찾음"} 확률 글자 위쪽={textTop}");
        return band ?? Ui.TextTop(probability);
    }

    // 색만으로 구분하지 않도록 기호와 문구도 함께 바꾼다. 막혔거나 막힐 키워드에만 칩 아이콘을 붙인다.
    private static (string Text, Color Ink, IReadOnlyList<ChipIcon> Chips, bool Grayed) Line(KeywordRow row)
    {
        var name = KeywordLock.Name(row.Keyword);
        if (_preview.Contains(row.Keyword)) return ($"※ {name}  고르면 막힘", PreviewInk, row.Chips, false);
        return row.State switch
        {
            KeywordState.Owned => ($"● {name}  {row.Count}개", row.Keyword == _gain ? GainInk : OwnedInk, Array.Empty<ChipIcon>(), false),
            KeywordState.Locked => ($"× {name}  막힘", LockedInk, row.Chips, true),
            _ => ($"○ {name}", row.Keyword == _gain ? GainInk : OpenInk, Array.Empty<ChipIcon>(), false),
        };
    }
}
