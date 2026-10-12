using ChipGuide.Plugin.Runtime;
using UnityEngine;

namespace ChipGuide.Plugin.View;

internal static class Ui
{
    private const int AutoSizeBoth = 1, AutoSizeHeight = 2;
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
