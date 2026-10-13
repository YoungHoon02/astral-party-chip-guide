using ChipGuide.Core;
using ChipGuide.Plugin.Runtime;
using UnityEngine;

namespace ChipGuide.Plugin.View;

internal sealed record CardView(RuntimeObject Item, Advice? Advice, string? Warning, IReadOnlySet<Keyword> WouldLock, ChipRole Role);

internal static class BadgeView
{
    private const int FallbackBadgeText = 16, FallbackTooltipText = 15;
    private const float FallbackTooltipWidth = 300f;
    // ⚠는 게임 글꼴에 없을 수 있어 CJK 글꼴에 흔한 ※를 쓴다.
    private const string WarningMark = "※";
    private static readonly Color WarningFill = new(0.93f, 0.55f, 0.13f, 0.95f), WarningInk = new(0.1f, 0.1f, 0.1f, 1f);

    private sealed record Badge(RuntimeObject Item, RuntimeObject Pill, RuntimeObject Label, Advice? Advice, string? Warning, IReadOnlySet<Keyword> WouldLock, Vector2 PillSize, int TooltipText);

    private static readonly List<Badge> Badges = new();
    private static readonly List<RuntimeObject> RoleTags = new();
    private static IReadOnlyList<CardView> _cards = Array.Empty<CardView>();
    private static readonly Color RoleTagFill = new(0.04f, 0.05f, 0.07f, 0.8f), RoleTagInk = new(0.85f, 0.9f, 1f, 1f);
    private static RuntimeObject? _container;
    private static RuntimeObject? _tooltip;
    private static IntPtr _tooltipFor;
    private static readonly IReadOnlySet<Keyword> NoKeywords = new HashSet<Keyword>();

    internal static bool Showing => Badges.Count > 0 || RoleTags.Count > 0 || KeywordPanel.Alive;

    internal static void Show(RuntimeObject container, IReadOnlyList<CardView> cards, IReadOnlyList<KeywordRow>? keywords, RoleWeights? weights, bool roleTags)
    {
        // 카드 객체는 창·리롤마다 재사용되므로 이전 배지를 먼저 걷어내야 겹쳐 붙지 않는다.
        Clear();
        _container = container;
        _cards = cards;
        foreach (var card in cards)
        {
            if (card.Advice != null || card.Warning != null) Badges.Add(CreateBadge(card.Item, card.Advice, card.Warning, card.WouldLock));
            if (roleTags && card.Role.Label() is { Length: > 0 } label && CreateRoleTag(card.Item, label) is { } tag) RoleTags.Add(tag);
        }
        if (cards.Count > 0)
        {
            var title = TitleSize(cards[0].Item);
            KeywordPanel.Show(container, keywords, weights, title > 0 ? (int)Math.Round(title * 0.7f) : FallbackTooltipText, IconClass(cards[0].Item));
        }
    }

    internal static bool StillAttached() =>
        GameUi.Visible(_container) && Badges.All(badge => GameUi.Visible(badge.Item) && GameUi.Alive(badge.Pill));

    internal static void UpdateHover(RuntimeObject root)
    {
        KeywordPanel.UpdateHover(root);
        var card = _cards.FirstOrDefault(c => GameUi.IsUnderPointer(root, c.Item));
        // 패널의 칩 아이콘까지 마우스를 옮겨 이름을 볼 수 있도록, 패널 위에서는 미리보기를 유지하고 그 밖에서는 선택한 카드를 따른다.
        if (card == null && !KeywordPanel.UnderPointer(root)) card = _cards.FirstOrDefault(c => Selected(c.Item));
        if (card != null || !KeywordPanel.UnderPointer(root)) KeywordPanel.Preview(card?.WouldLock ?? NoKeywords, card?.Role.Added ?? RoleWeights.Zero);
        var hovered = Badges.FirstOrDefault(badge => GameUi.IsUnderPointer(root, badge.Item));
        if (hovered == null)
        {
            HideTooltip();
            return;
        }
        if (_tooltipFor == hovered.Item.Pointer && GameUi.Alive(_tooltip)) return;
        HideTooltip();
        _tooltip = CreateTooltip(hovered);
        _tooltipFor = hovered.Item.Pointer;
    }

    private static IntPtr IconClass(RuntimeObject item)
    {
        try
        {
            var loader = item.Field("loader_Icon");
            if (loader == null) return IntPtr.Zero;
            if (Plugin.Diagnostics.Value) Plugin.Logger.LogInfo($"[진단] 칩 아이콘 로더: {loader.TypeName}");
            return loader.ClassPointer;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"칩 아이콘 로더를 읽지 못해 기본 로더를 씁니다: {ex.Message}");
            return IntPtr.Zero;
        }
    }

    private static bool Selected(RuntimeObject item)
    {
        try { return item.Get<bool>("selected"); }
        catch { return false; }
    }

    internal static void Clear()
    {
        HideTooltip();
        try { KeywordPanel.Clear(); }
        catch (Exception ex) { Plugin.Logger.LogWarning($"키워드 패널을 정리하지 못했습니다: {ex.Message}"); }
        var failed = false;
        try
        {
            foreach (var badge in Badges)
            {
                try { GameUi.Dispose(badge.Label); } catch { failed = true; }
                try { GameUi.Dispose(badge.Pill); } catch { failed = true; }
            }
        }
        finally
        {
            foreach (var tag in RoleTags)
            {
                try { GameUi.Dispose(tag); } catch { failed = true; }
            }
            Badges.Clear();
            RoleTags.Clear();
            _cards = Array.Empty<CardView>();
            _container = null;
            if (failed) Plugin.Logger.LogWarning("일부 배지를 정리하지 못했습니다.");
        }
    }

    // 칩 이름 바로 위 가운데에 역할군과 추가 가중치를 붙인다. 카드 애니메이션을 따라가도록 카드에 직접 붙인다.
    private static RuntimeObject? CreateRoleTag(RuntimeObject item, string text)
    {
        var title = item.Field("txt_tltle");
        if (title == null) return null;
        var size = TitleSize(item);
        var label = Ui.Text(text, size > 0 ? (int)Math.Round(size * 0.6f) : FallbackTooltipText, RoleTagInk, 0f, wrap: false);
        var tag = RuntimeObject.New(GameUi.Require("FairyGUI", "GComponent"));
        try
        {
            tag.Set("touchable", false);
            var width = label.Get<float>("width");
            var height = label.Get<float>("height");
            var padding = height * 0.35f;
            tag.Call("AddChild", Ui.Graph(width + padding * 2, height, RoleTagFill, height / 2));
            label.Call("SetXY", padding, 0f);
            tag.Call("AddChild", label);
            tag.Call("SetSize", width + padding * 2, height);
            var x = title.Get<float>("x") + (title.Get<float>("width") - width - padding * 2) / 2;
            tag.Call("SetXY", x, title.Get<float>("y") - height - padding * 0.5f);
            item.Call("AddChild", tag);
            return tag;
        }
        catch
        {
            GameUi.Dispose(label);
            GameUi.Dispose(tag);
            throw;
        }
    }

    // 글자 크기를 카드 제목에 맞춰야 게임 UI 좌표계에서 읽을 수 있는 크기가 된다. 읽지 못하면 고정값으로 대신한다.
    private static int TitleSize(RuntimeObject item)
    {
        try { return item.Field("txt_tltle")?.Get("textFormat")?.Field<int>("size") ?? 0; }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"칩 카드 글자 크기를 읽지 못해 기본 크기를 씁니다: {ex.Message}");
            return 0;
        }
    }

    // 카드는 열릴 때 떨어지고 선택되면 커지는 애니메이션이 있어, 카드 기준 좌표를 한 번 계산하면 어긋난다. 아이콘에 직접 붙여 오른쪽 가운데에 고정한다.
    private static (RuntimeObject Host, Vector2 Origin)? IconPlacement(RuntimeObject item, Vector2 size)
    {
        try
        {
            var icon = item.Field("loader_Frame");
            return icon == null ? null : (icon, new Vector2(icon.Get<float>("width") + size.y * 0.15f, (icon.Get<float>("height") - size.y) / 2));
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"칩 아이콘을 읽지 못해 배지를 카드에 붙입니다: {ex.Message}");
            return null;
        }
    }

    // 아이콘을 못 쓰면 카드에 붙인다. 긴 칩 이름을 가리지 않도록 제목 줄 아래, 그것도 안 되면 카드 모서리로 대신한다.
    private static Vector2 CardOrigin(RuntimeObject item, Vector2 size, float inset)
    {
        var maxX = item.Get<float>("width") - size.x;
        return TitleOrigin(item, maxX, inset) ?? new Vector2(Fit(maxX - inset, maxX), inset);
    }

    private static Vector2? TitleOrigin(RuntimeObject item, float maxX, float inset)
    {
        try
        {
            var title = item.Field("txt_tltle");
            return title == null ? null : new Vector2(Fit(maxX - inset, maxX), title.Get<float>("y") + title.Get<float>("height") + inset / 2);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"칩 제목 위치를 읽지 못해 카드 모서리에 둡니다: {ex.Message}");
            return null;
        }
    }

    private static float Fit(float x, float max) => Math.Max(0f, Math.Min(x, max));

    private static Badge CreateBadge(RuntimeObject item, Advice? advice, string? warning, IReadOnlySet<Keyword> wouldLock)
    {
        var title = TitleSize(item);
        var textSize = title > 0 ? (int)Math.Round(title * 0.75f) : FallbackBadgeText;
        var tooltipText = title > 0 ? (int)Math.Round(title * 0.7f) : FallbackTooltipText;
        var inset = textSize * 0.4f;

        var (text, fill, ink) = advice == null ? (WarningMark + " 키워드", WarningFill, WarningInk) : Style(advice.Verdict);
        if (advice != null && warning != null) text += " " + WarningMark;
        var label = Ui.Text(text, textSize, ink, 0f, wrap: false);
        RuntimeObject? pill = null;
        try
        {
            var size = new Vector2(label.Get<float>("width") + textSize, textSize * 1.6f);
            var (host, origin) = IconPlacement(item, size) ?? (item, CardOrigin(item, size, inset));
            pill = Ui.Graph(size.x, size.y, fill, size.y / 2);
            pill.Call("SetXY", origin.x, origin.y);
            host.Call("AddChild", pill);
            label.Call("SetXY", origin.x + (size.x - label.Get<float>("width")) / 2, origin.y + (size.y - label.Get<float>("height")) / 2);
            host.Call("AddChild", label);
            return new Badge(item, pill, label, advice, warning, wouldLock, size, tooltipText);
        }
        catch
        {
            GameUi.Dispose(label);
            GameUi.Dispose(pill);
            throw;
        }
    }

    private static RuntimeObject CreateTooltip(Badge badge)
    {
        var lines = new List<string>();
        if (badge.Advice is { } advice)
        {
            lines.Add($"{Symbol(advice.Verdict)} {advice.Reason}");
            lines.AddRange(advice.Notes.Select(note => $"참고: {Symbol(note.Verdict)} {note.Why}"));
        }
        if (badge.Warning != null) lines.Add($"{WarningMark} {badge.Warning}");

        var container = _container!;
        var containerSize = new Vector2(container.Get<float>("width"), container.Get<float>("height"));
        var padding = badge.TooltipText * 0.7f;
        var itemWidth = badge.Item.Get<float>("width");
        var width = itemWidth <= 0 ? FallbackTooltipWidth : Math.Min(Math.Max(itemWidth, badge.TooltipText * 12f), containerSize.x);

        var panel = RuntimeObject.New(GameUi.Require("FairyGUI", "GComponent"));
        panel.Set("touchable", false);
        var label = Ui.Text(string.Join("\n", lines), badge.TooltipText, Color.white, width - padding * 2, wrap: true);
        var height = label.Get<float>("height") + padding * 2;
        panel.Call("AddChild", Ui.Graph(width, height, new Color(0.04f, 0.05f, 0.07f, 0.92f), padding));
        label.Call("SetXY", padding, padding);
        panel.Call("AddChild", label);
        panel.Call("SetSize", width, height);

        // 카드가 애니메이션 중일 수 있어 표시 시점의 배지 위치에서 계산한다. 배지 바로 아래, 배지 오른쪽 끝에 맞추며 창 하단 안내 문구는 카드 아래쪽에 있어 가리지 않는다.
        var global = badge.Pill.Call("LocalToGlobal", badge.PillSize)!.Value<Vector2>();
        var local = container.Call("GlobalToLocal", global)!.Value<Vector2>();
        panel.Call("SetXY", Fit(local.x - width, containerSize.x - width), Fit(local.y + padding / 2, containerSize.y - height));
        container.Call("AddChild", panel);
        return panel;
    }

    private static void HideTooltip()
    {
        try { GameUi.Dispose(_tooltip); }
        catch (Exception ex) { Plugin.Logger.LogWarning($"툴팁을 정리하지 못했습니다: {ex.Message}"); }
        finally
        {
            _tooltip = null;
            _tooltipFor = IntPtr.Zero;
        }
    }

    private static (string Text, Color Fill, Color Ink) Style(Verdict verdict) => verdict switch
    {
        Verdict.Core => ("★ 코어", new Color(1f, 0.81f, 0.2f, 0.95f), new Color(0.1f, 0.1f, 0.1f, 1f)),
        Verdict.Good => ("○ 무난", new Color(0.23f, 0.56f, 0.36f, 0.95f), Color.white),
        _ => ("× 보류", new Color(0.71f, 0.23f, 0.23f, 0.95f), Color.white),
    };

    private static string Symbol(Verdict verdict) => verdict switch
    {
        Verdict.Core => "★",
        Verdict.Good => "○",
        _ => "×",
    };
}
