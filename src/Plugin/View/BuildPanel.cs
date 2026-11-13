using ChipGuide.Core;
using ChipGuide.Plugin.Runtime;
using UnityEngine;

namespace ChipGuide.Plugin.View;

internal static class BuildPanel
{
    // 제목이 띠 가운데 정렬이라, 오른쪽 끝에서 이 폭 안에 두면 제목 글자와 겹치지 않는다.
    private const float MaxWidthRatio = 0.3f;
    private static readonly string[] SideButtons = { "btn_OpenExpr", "btn_OpenChat", "btn_MapTool", "btn_MapChat" };

    private static RuntimeObject? _panel;

    internal static bool Alive => GameUi.Alive(_panel);

    // 빌드 설명은 띠 안쪽 오른쪽이라는 자리에 뜻이 있고 같은 정보가 글자 블럭으로도 보여, 띠를 못 찾으면 물러날 자리 없이 그리지 않는다.
    internal static void Show(RuntimeObject container, IReadOnlyList<BuildLine>? builds, int textSize)
    {
        RuntimeObject? panel = null;
        try
        {
            Clear();
            if (builds == null || builds.Count == 0) return;
            if (TitleBand.Find(container) is not { } band)
            {
                if (Plugin.Diagnostics.Value) Plugin.Logger.LogInfo("[진단] 제목 띠를 찾지 못해 빌드 설명을 그리지 않습니다");
                return;
            }
            panel = RuntimeObject.New(GameUi.Require("FairyGUI", "GComponent"));
            panel.Set("touchable", false);
            var labels = new List<RuntimeObject>();
            foreach (var build in builds)
            {
                var label = Ui.Text(build.Label, textSize, Color.white, 0f, wrap: false);
                panel.Call("AddChild", label);
                labels.Add(label);
            }
            var lineHeight = labels.Max(label => label.Get<float>("height"));
            var tagGap = textSize * 0.3f;
            var lineGap = textSize * 0.2f;
            var maxWidth = container.Get<float>("width") * MaxWidthRatio;
            float x = 0f, y = 0f, width = 0f;
            for (var i = 0; i < builds.Count; i++)
            {
                var labelWidth = labels[i].Get<float>("width");
                var groupWidth = lineHeight + tagGap + labelWidth;
                var start = x > 0f ? x + textSize : 0f;
                if (x > 0f && start + groupWidth > maxWidth)
                {
                    start = 0f;
                    y += lineHeight + lineGap;
                }
                var tag = BuildTag.Create(builds[i].Letter, lineHeight, textSize);
                panel.Call("AddChild", tag);
                tag.Call("SetXY", start, y);
                labels[i].Call("SetXY", start + lineHeight + tagGap, y + (lineHeight - labels[i].Get<float>("height")) / 2);
                x = start + groupWidth;
                width = Math.Max(width, x);
            }
            var height = y + lineHeight;
            panel.Call("SetSize", width, height);
            panel.Call("SetXY", RightLimit(container, band.Top, band.Bottom) - textSize * KeywordPanel.PaddingRatio - width, (band.Top + band.Bottom - height) / 2);
            container.Call("AddChild", panel);
            _panel = panel;
        }
        catch (Exception ex)
        {
            GameUi.Dispose(panel);
            Plugin.Logger.LogWarning($"빌드 설명을 그리지 못했습니다: {ex.Message}");
        }
    }

    internal static void Clear()
    {
        try { GameUi.Dispose(_panel); }
        finally { _panel = null; }
    }

    // 게임의 표정·채팅 버튼 줄이 칩 선택 창 위에 떠서 띠 오른쪽 끝을 덮으므로, 띠와 겹치는 버튼의 왼쪽에서 멈춘다.
    private static float RightLimit(RuntimeObject container, float top, float bottom)
    {
        var right = container.Get<float>("width");
        try
        {
            if (GameUi.Root() is not { } root || GameUi.FindWindow(root, "ExpressionWindow")?.Get("contentPane")?.Field("com_Interact_PC") is not { } buttons)
                return right;
            foreach (var name in SideButtons)
            {
                var button = buttons.Field(name);
                if (!GameUi.Visible(button)) continue;
                var a = ToLocal(container, button!, Vector2.zero);
                var b = ToLocal(container, button!, new Vector2(button!.Get<float>("width"), button.Get<float>("height")));
                if (Math.Max(a.y, b.y) > top && Math.Min(a.y, b.y) < bottom) right = Math.Min(right, Math.Min(a.x, b.x));
            }
        }
        catch (Exception ex)
        {
            if (Plugin.Diagnostics.Value) Plugin.Logger.LogInfo($"[진단] 오른쪽 버튼 위치를 읽지 못해 띠 오른쪽 끝에 붙입니다: {ex.Message}");
        }
        return right;
    }

    private static Vector2 ToLocal(RuntimeObject container, RuntimeObject target, Vector2 point) =>
        container.Call("GlobalToLocal", target.Call("LocalToGlobal", point)!.Value<Vector2>())!.Value<Vector2>();
}
