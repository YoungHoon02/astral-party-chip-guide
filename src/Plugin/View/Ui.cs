using ChipGuide.Plugin.Runtime;
using UnityEngine;

namespace ChipGuide.Plugin.View;

internal static class Ui
{
    private const int FillScale = 1;
    private const float FadeBrightness = 0.25f;
    private const int AutoSizeBoth = 1, AutoSizeHeight = 2, VertAlignMiddle = 1, VertAlignBottom = 2;
    private static string? _font;
    private static bool _fontResolved;

    internal static RuntimeObject Graph(float width, float height, Color fill, float radius)
    {
        var graph = RuntimeObject.New(GameUi.Require("FairyGUI", "GGraph"));
        graph.Set("touchable", false);
        graph.Call("SetSize", width, height);
        graph.Get("shape")!.Call("DrawRoundRect", 0f, Color.clear, fill, radius, radius, radius, radius);
        return graph;
    }

    internal static RuntimeObject Text(string text, int size, Color color, float width, bool wrap)
    {
        var label = RuntimeObject.New(GameUi.Require("FairyGUI", "GTextField"));
        label.Set("touchable", false);
        label.Set("autoSize", wrap ? AutoSizeHeight : AutoSizeBoth);
        if (wrap) label.Set("width", width);
        var format = label.Get("textFormat")!;
        format.SetField("size", size);
        format.SetField("color", color);
        if (Font() is { } font) format.SetField("font", font);
        label.Set("textFormat", format);
        label.Set("text", text);
        label.Field("_textField")!.Call("EnsureSizeCorrect");
        return label;
    }

    // 게임 아이콘은 게임 전용 로더로만 불러와지므로 칩 카드의 아이콘 로더와 같은 클래스로 만든다.
    internal static RuntimeObject ChipIcon(IntPtr loaderClass, string url, float size, bool grayed)
    {
        var icon = RuntimeObject.New(loaderClass != IntPtr.Zero ? loaderClass : GameUi.Require("FairyGUI", "GLoader"));
        icon.Call("SetSize", size, size);
        icon.Set("fill", FillScale);
        icon.Set("url", url);
        icon.Set("grayed", grayed);
        return icon;
    }

    // 기본 흑백 처리(grayed)는 원래 어두운 칩 아이콘을 그대로 두어 검은 배경에 묻힌다. 색을 빼고 밝기를 올린 필터를 쓰고, 필터가 없으면 흑백으로 대신한다.
    internal static void Fade(RuntimeObject icon)
    {
        var filterClass = RuntimeObject.FindClass("FairyGUI", "ColorFilter");
        if (filterClass == IntPtr.Zero)
        {
            icon.Set("grayed", true);
            return;
        }
        var filter = RuntimeObject.New(filterClass);
        filter.Call("AdjustSaturation", -1f);
        filter.Call("AdjustBrightness", FadeBrightness);
        icon.Set("filter", filter);
    }

    internal static float TextTop(RuntimeObject field)
    {
        var y = field.Get<float>("y");
        var spare = Math.Max(0f, field.Get<float>("height") - field.Get<float>("textHeight"));
        return field.Get<int>("verticalAlign") switch
        {
            VertAlignMiddle => y + spare / 2,
            VertAlignBottom => y + spare,
            _ => y,
        };
    }

    // 모드 라벨도 한국어 패치가 적용된 게임 폰트를 쓰도록 공용 버튼의 글꼴을 빌린다.
    private static string? Font()
    {
        if (_fontResolved) return _font;
        _fontResolved = true;
        try
        {
            var template = RuntimeObject.StaticCall(GameUi.Require("FairyGUI", "UIPackage"), "CreateObject", "Common", "Button_Confirm_SmallRounded");
            try { _font = template?.Call("GetChild", "title")?.Get("textFormat")?.Field("font")?.String(); }
            finally { GameUi.Dispose(template); }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"게임 글꼴을 찾지 못해 기본 글꼴을 씁니다: {ex.Message}");
        }
        return _font;
    }
}
