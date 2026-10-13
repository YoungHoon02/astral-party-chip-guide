using ChipGuide.Plugin.Game;
using ChipGuide.Plugin.Runtime;
using UnityEngine;

namespace ChipGuide.Plugin.View;

internal sealed record CoreChip(ChipIcon Chip, bool Owned);

// 내 캐릭터의 코어 칩을 확인 버튼 아래에 늘어놓고, 가진 칩만 컬러로 보여 빌드 진행 상황을 한눈에 보이게 한다.
internal static class CoreStrip
{
    private static readonly Color Background = new(0.02f, 0.02f, 0.03f, 0.9f), TipFill = new(0.04f, 0.05f, 0.07f, 0.92f), Glow = new(1f, 0.6f, 0.1f, 1f);
    private const float BlinkPeriod = 1.6f, GlowMin = 0.15f, GlowMax = 0.85f;

    private sealed record Slot(RuntimeObject Icon, RuntimeObject Glow, string Name);

    private static readonly List<Slot> Icons = new();
    private static RuntimeObject? _strip;
    private static RuntimeObject? _container;
    private static RuntimeObject? _tip;
    private static IntPtr _tipFor;
    private static int _textSize;
    private static float _gap;

    internal static bool Alive => GameUi.Alive(_strip);

    internal static void Show(RuntimeObject container, IReadOnlyList<CoreChip>? chips, int textSize, IntPtr iconClass)
    {
        Clear();
        var button = container.Field("btn_Select");
        if (Plugin.Diagnostics.Value)
            Plugin.Logger.LogInfo($"[진단] 코어 칩 줄: 칩 {chips?.Count.ToString() ?? "끔"}개({string.Join(",", chips?.Select(c => c.Chip.Name) ?? Array.Empty<string>())}), 확인 버튼={(button == null ? "없음" : $"({button.Get<float>("x")},{button.Get<float>("y")}) {button.Get<float>("width")}x{button.Get<float>("height")}")}, 창={container.Get<float>("width")}x{container.Get<float>("height")}");
        if (chips == null || chips.Count == 0 || button == null) return;
        _container = container;
        _textSize = textSize;

        var strip = RuntimeObject.New(GameUi.Require("FairyGUI", "GComponent"));
        try
        {
            var buttonHeight = button.Get<float>("height");
            var iconSize = buttonHeight > 0 ? buttonHeight * 0.6f : textSize * 2.2f;
            var padding = iconSize * 0.2f;
            _gap = iconSize * 0.15f;
            var x = padding * 1.5f;
            foreach (var chip in chips)
            {
                var glowSize = iconSize + padding;
                var glow = Ui.Graph(glowSize, glowSize, Glow, glowSize / 2);
                glow.Call("SetXY", x - padding / 2, padding / 2);
                glow.Set("visible", false);
                strip.Call("AddChild", glow);
                var icon = Ui.ChipIcon(iconClass, chip.Chip.Icon, iconSize, grayed: false);
                if (!chip.Owned) Ui.Fade(icon);
                icon.Call("SetXY", x, padding);
                strip.Call("AddChild", icon);
                Icons.Add(new Slot(icon, glow, chip.Chip.Name));
                x += iconSize + padding;
            }
            var width = x + padding * 0.5f;
            var height = iconSize + padding * 2;
            strip.Call("AddChildAt", Ui.Graph(width, height, Background, height / 2), 0);
            strip.Call("SetSize", width, height);
            // 칩 창은 확인 버튼 바닥 아래를 잘라내서 창에 붙이면 보이지 않는다. 화면 최상위에 붙이고 위치만 버튼에 맞춘다.
            // 최상위 자식은 sortingOrder 순으로 그려지고 게임 창들은 값이 커서, 기본값 0이면 손패 카드 밑에 깔린다.
            var host = GameUi.Root() ?? container;
            strip.Set("sortingOrder", TopSortingOrder(host) + 1);
            host.Call("AddChild", strip);
            _strip = strip;
            Place();
            if (Plugin.Diagnostics.Value)
                Plugin.Logger.LogInfo($"[진단] 코어 칩 줄 위치=({strip.Get<float>("x")},{strip.Get<float>("y")}) 크기={width}x{height} 보임={GameUi.Visible(strip)} 그리기 순서={strip.Get<int>("sortingOrder")}");
        }
        catch
        {
            GameUi.Dispose(strip);
            Icons.Clear();
            _container = null;
            throw;
        }
    }

    // selected는 선택한 카드의 칩 이름이다. 그 칩이 코어면 줄의 같은 아이콘 뒤에서 주황 빛이 천천히 점멸해 어느 코어를 집는지 보여 준다.
    internal static void Update(RuntimeObject root, string? selected)
    {
        if (!Alive) return;
        Place();
        var pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2 * Mathf.PI / BlinkPeriod);
        foreach (var slot in Icons)
        {
            var on = slot.Name == selected;
            if (slot.Glow.Get<bool>("visible") != on) slot.Glow.Set("visible", on);
            if (on) slot.Glow.Set("alpha", GlowMin + (GlowMax - GlowMin) * pulse);
        }
        var hovered = Icons.FirstOrDefault(slot => GameUi.IsUnderPointer(root, slot.Icon));
        if (hovered == null)
        {
            HideTip();
            return;
        }
        if (_tipFor == hovered.Icon.Pointer && GameUi.Alive(_tip)) return;
        HideTip();
        _tip = CreateTip(hovered.Icon, hovered.Name);
        _tipFor = hovered.Icon.Pointer;
    }

    internal static void Clear()
    {
        try { GameUi.Dispose(_strip); }
        finally
        {
            _strip = null;
            _container = null;
            _tip = null;
            _tipFor = IntPtr.Zero;
            Icons.Clear();
        }
    }

    // 창이 열릴 때 확인 버튼도 함께 움직일 수 있어 매 프레임 버튼 바로 아래 가운데에 다시 맞춘다.
    private static void Place()
    {
        var button = _container!.Field("btn_Select");
        var parent = _strip!.Get("parent");
        if (button == null || parent == null) return;
        var global = button.Call("LocalToGlobal", new Vector2(button.Get<float>("width") / 2, button.Get<float>("height")))!.Value<Vector2>();
        var anchor = parent.Call("GlobalToLocal", global)!.Value<Vector2>();
        _strip.Call("SetXY", anchor.x - _strip.Get<float>("width") / 2, anchor.y + _gap);
    }

    private static int TopSortingOrder(RuntimeObject host)
    {
        var top = 0;
        var count = host.Get<int>("numChildren");
        for (var i = 0; i < count; i++)
            if (host.Call("GetChildAt", i) is { } child) top = Math.Max(top, child.Get<int>("sortingOrder"));
        return top;
    }

    // 줄 아래는 화면 끝이라 이름은 아이콘 위에 띄운다.
    private static RuntimeObject CreateTip(RuntimeObject icon, string name)
    {
        var label = Ui.Text(name, _textSize, Color.white, 0f, wrap: false);
        var tip = RuntimeObject.New(GameUi.Require("FairyGUI", "GComponent"));
        try
        {
            tip.Set("touchable", false);
            var padding = label.Get<float>("height") * 0.3f;
            var width = label.Get<float>("width") + padding * 2;
            var height = label.Get<float>("height") + padding;
            tip.Call("AddChild", Ui.Graph(width, height, TipFill, height / 2));
            label.Call("SetXY", padding, padding / 2);
            tip.Call("AddChild", label);
            tip.Call("SetSize", width, height);
            tip.Call("SetXY", icon.Get<float>("x") + (icon.Get<float>("width") - width) / 2, -height - padding / 2);
            _strip!.Call("AddChild", tip);
            return tip;
        }
        catch
        {
            GameUi.Dispose(label);
            GameUi.Dispose(tip);
            throw;
        }
    }

    private static void HideTip()
    {
        GameUi.Dispose(_tip);
        _tip = null;
        _tipFor = IntPtr.Zero;
    }
}
