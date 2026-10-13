using ChipGuide.Core;
using ChipGuide.Plugin.Runtime;
using UnityEngine;

namespace ChipGuide.Plugin.View;

internal sealed record CardView(RuntimeObject Item, string Name, Advice? Advice, string? Warning, IReadOnlySet<Keyword> WouldLock, ChipRole Role, Keyword Keyword, KeywordState KeywordState);

internal static class BadgeView
{
    private const int FallbackBadgeText = 13, FallbackTooltipText = 15, FallbackPanelText = 13;
    private const float FallbackTooltipWidth = 300f;
    // ⚠는 게임 글꼴에 없을 수 있어 CJK 글꼴에 흔한 ※를 쓴다.
    private const string WarningMark = "※";
    // 아이콘 위로 많이 올리면 칩 이름 글자에 겹쳐서, 배지 높이의 1/3만 아이콘 위로 내민다.
    private const float BadgeTop = -0.35f;
    private const int SelectedStatus = 1;
    private static readonly Color WarningFill = new(0.93f, 0.55f, 0.13f, 0.95f), WarningInk = new(0.1f, 0.1f, 0.1f, 1f);

    private sealed record Badge(RuntimeObject Item, RuntimeObject Pill, RuntimeObject Label, Vector2 PillSize);

    private sealed record Tooltip(RuntimeObject Panel, IntPtr For, Vector2 Size);

    private static readonly List<Badge> Badges = new();
    private static readonly List<RuntimeObject> RoleTags = new();
    private static IReadOnlyList<CardView> _cards = Array.Empty<CardView>();
    private static readonly Color RoleTagFill = new(0.04f, 0.05f, 0.07f, 0.8f), RoleTagInk = new(0.85f, 0.9f, 1f, 1f);
    private static RuntimeObject? _container;
    private static RoleWeights? _weights;
    private static Tooltip? _tooltip;
    private static readonly IReadOnlySet<Keyword> NoKeywords = new HashSet<Keyword>();

    internal static bool Showing => Badges.Count > 0 || RoleTags.Count > 0 || KeywordPanel.Alive || CoreStrip.Alive;

    internal static void Show(RuntimeObject container, IReadOnlyList<CardView> cards, IReadOnlyList<KeywordRow>? keywords, IReadOnlyList<CoreChip>? core, RoleWeights? weights, bool roleTags)
    {
        // 카드 객체는 창·리롤마다 재사용되므로 이전 배지를 먼저 걷어내야 겹쳐 붙지 않는다.
        Clear();
        _container = container;
        _cards = cards;
        _weights = weights;
        foreach (var card in cards)
        {
            var badge = card.Advice != null || card.Warning != null ? CreateBadge(card.Item, card.Advice, card.Warning) : null;
            if (badge != null) Badges.Add(badge);
            if (roleTags && card.Role.Label() is { Length: > 0 } label && CreateRoleTag(card.Item, label, badge?.PillSize.y) is { } tag) RoleTags.Add(tag);
        }
        if (cards.Count > 0)
        {
            var title = TitleSize(cards[0].Item);
            // 패널은 화면 맨 위 라운드 진행 표시와 겹치므로 툴팁보다 작은 글자를 쓴다.
            var panelText = title > 0 ? (int)Math.Round(title * 0.6f) : FallbackPanelText;
            var iconClass = IconClass(cards[0].Item);
            KeywordPanel.Show(container, keywords, weights, panelText, iconClass);
            CoreStrip.Show(container, core, panelText, iconClass);
        }
    }

    internal static bool StillAttached() =>
        GameUi.Visible(_container) && Badges.All(badge => GameUi.Visible(badge.Item) && GameUi.Alive(badge.Pill));

    // 마우스가 지나갈 때마다 정보가 바뀌면 읽기 어려워, 판정 이유와 가중치 미리보기는 클릭으로 선택한 카드만 따른다.
    internal static void Update(RuntimeObject root)
    {
        KeywordPanel.UpdateHover(root);
        var card = _cards.FirstOrDefault(c => Selected(c.Item));
        CoreStrip.Update(root, card?.Name);
        KeywordPanel.Preview(card?.Keyword ?? Keyword.None, card?.WouldLock ?? NoKeywords, card?.Role.Added ?? RoleWeights.Zero);
        if (card == null)
        {
            HideTooltip();
            return;
        }
        if (_tooltip?.For != card.Item.Pointer || !GameUi.Alive(_tooltip.Panel))
        {
            HideTooltip();
            _tooltip = CreateTooltip(card);
        }
        PlaceTooltip(card.Item, _tooltip);
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

    // 게임은 GButton의 selected를 쓰지 않고, 카드를 누르면 status 컨트롤러를 1로 바꿔 선택을 표시한다.
    private static bool Selected(RuntimeObject item)
    {
        try { return item.Field("status")?.Get<int>("selectedIndex") == SelectedStatus; }
        catch { return false; }
    }

    internal static void Clear()
    {
        HideTooltip();
        try { KeywordPanel.Clear(); }
        catch (Exception ex) { Plugin.Logger.LogWarning($"키워드 패널을 정리하지 못했습니다: {ex.Message}"); }
        try { CoreStrip.Clear(); }
        catch (Exception ex) { Plugin.Logger.LogWarning($"코어 칩 줄을 정리하지 못했습니다: {ex.Message}"); }
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

    // 판정 배지와 마주 보게 아이콘 왼쪽 위에 역할군과 추가 가중치를 붙인다. 카드 애니메이션을 따라가도록 아이콘에 직접 붙인다.
    // 칩 이름 위에 두면 제목 칸이 글자보다 위로 길어 창 상단 띠의 칩 획득 확률과 겹친다.
    private static RuntimeObject? CreateRoleTag(RuntimeObject item, string text, float? badgeHeight)
    {
        var icon = item.Field("loader_Frame");
        var title = item.Field("txt_tltle");
        if (icon == null && title == null) return null;
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
            if (icon != null)
            {
                var middle = BadgeTop * (badgeHeight ?? height) + (badgeHeight ?? height) / 2;
                tag.Call("SetXY", Math.Max(-(width + padding * 2) / 2, -icon.Get<float>("x")), middle - height / 2);
                icon.Call("AddChild", tag);
                return tag;
            }
            var x = title!.Get<float>("x") + (title.Get<float>("width") - width - padding * 2) / 2;
            tag.Call("SetXY", x, Ui.TextTop(title) - height - padding * 0.5f);
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

    // 카드는 열릴 때 떨어지고 선택되면 커지는 애니메이션이 있어, 카드 기준 좌표를 한 번 계산하면 어긋난다. 아이콘에 직접 붙여 오른쪽 위에 고정한다.
    // 아이콘 오른쪽 가운데는 카드 칸 밖이라 선택 테두리를 넘고 옆 카드와 가까워서, 카드 칸 안쪽으로 당긴다.
    private static (RuntimeObject Host, Vector2 Origin)? IconPlacement(RuntimeObject item, Vector2 size)
    {
        try
        {
            var icon = item.Field("loader_Frame");
            if (icon == null) return null;
            var x = Math.Min(icon.Get<float>("width") - size.x / 2, item.Get<float>("width") - icon.Get<float>("x") - size.x);
            return (icon, new Vector2(x, BadgeTop * size.y));
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

    private static Badge CreateBadge(RuntimeObject item, Advice? advice, string? warning)
    {
        var title = TitleSize(item);
        var textSize = title > 0 ? (int)Math.Round(title * 0.6f) : FallbackBadgeText;
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
            return new Badge(item, pill, label, size);
        }
        catch
        {
            GameUi.Dispose(label);
            GameUi.Dispose(pill);
            throw;
        }
    }

    private static Tooltip CreateTooltip(CardView card)
    {
        var title = TitleSize(card.Item);
        var textSize = title > 0 ? (int)Math.Round(title * 0.7f) : FallbackTooltipText;
        var container = _container!;
        var padding = textSize * 0.7f;
        var itemWidth = card.Item.Get<float>("width");
        var width = itemWidth <= 0 ? FallbackTooltipWidth : Math.Min(Math.Max(itemWidth, textSize * 12f), container.Get<float>("width"));

        var panel = RuntimeObject.New(GameUi.Require("FairyGUI", "GComponent"));
        panel.Set("touchable", false);
        var label = Ui.Text(string.Join("\n", TooltipLines(card)), textSize, Color.white, width - padding * 2, wrap: true);
        var height = label.Get<float>("height") + padding * 2;
        panel.Call("AddChild", Ui.Graph(width, height, new Color(0.04f, 0.05f, 0.07f, 0.92f), padding));
        label.Call("SetXY", padding, padding);
        panel.Call("AddChild", label);
        panel.Call("SetSize", width, height);
        container.Call("AddChild", panel);
        return new Tooltip(panel, card.Item.Pointer, new Vector2(width, height));
    }

    private static List<string> TooltipLines(CardView card)
    {
        var lines = new List<string>();
        if (card.Advice is { } advice)
        {
            // 이긴 판정은 배지에 이미 보이므로 기호를 붙이지 않는다. 참고 줄은 판정이 배지와 다를 수 있어 기호를 남긴다.
            lines.Add(advice.Reason);
            lines.AddRange(advice.Notes.Select(note => $"참고: {Symbol(note.Verdict)} {note.Why}"));
        }
        if (card.Warning != null) lines.Add($"{WarningMark} {card.Warning}");
        if (card.Advice != null) return lines;

        // 판정 규칙이 없는 칩도 선택하면 무언가 떠야 고장으로 보이지 않는다. 고를지 판단할 재료로 역할군과 키워드 영향을 보여 준다.
        lines.Insert(0, "판정 규칙 없음");
        if (_weights is { } weights && card.Role.Added != RoleWeights.Zero)
            lines.Add("역할군 " + string.Join(" · ", RoleWeights.Kinds
                .Where(kind => card.Role.Added[kind] > 0)
                .Select(kind => $"{RoleWeights.Name(kind)} {weights[kind]}→{weights[kind] + card.Role.Added[kind]}")));
        if (card.Warning == null) lines.Add(KeywordLine(card));
        return lines;
    }

    private static string KeywordLine(CardView card)
    {
        if (card.Keyword == Keyword.None) return "키워드 없음";
        var name = KeywordLock.Name(card.Keyword);
        if (card.KeywordState == KeywordState.Owned) return $"키워드 {name}: 이미 보유";
        return card.WouldLock.Count == 0
            ? $"키워드 {name}: 막히는 것 없음"
            : $"키워드 {name}: 고르면 {string.Join("·", card.WouldLock.OrderBy(k => k).Select(KeywordLock.Name))} 막힘";
    }

    // 선택 중에는 계속 떠 있으므로 아이콘을 가리지 않게 칩 설명 글자 바로 아래 가운데에 둔다.
    // 카드 칸은 설명보다 훨씬 아래 확인 버튼 근처까지 내려가서 칸 아래쪽에 두면 버튼을 가린다. 선택한 카드는 커지는 애니메이션이 있어 매 프레임 위치를 다시 맞춘다.
    private static void PlaceTooltip(RuntimeObject item, Tooltip tooltip)
    {
        var container = _container!;
        var global = item.Call("LocalToGlobal", new Vector2(item.Get<float>("width") / 2, DescBottom(item)))!.Value<Vector2>();
        var local = container.Call("GlobalToLocal", global)!.Value<Vector2>();
        // 상자는 클릭을 받지 않아 버튼은 눌리지만, 확인 버튼이 가려지지 않게 그 위에서 멈추고 넘치면 칩 설명 쪽으로 올린다.
        var bottom = container.Get<float>("height");
        if (container.Field("btn_Select") is { } button) bottom = Math.Min(bottom, button.Get<float>("y") - tooltip.Size.y * 0.05f);
        tooltip.Panel.Call("SetXY",
            Fit(local.x - tooltip.Size.x / 2, container.Get<float>("width") - tooltip.Size.x),
            Fit(local.y + tooltip.Size.y * 0.1f, bottom - tooltip.Size.y));
    }

    private static float DescBottom(RuntimeObject item)
    {
        var desc = item.Field("txt_Desc");
        return desc == null
            ? item.Get<float>("height")
            : Ui.TextTop(desc) + Math.Min(desc.Get<float>("textHeight"), desc.Get<float>("height"));
    }

    private static void HideTooltip()
    {
        try { GameUi.Dispose(_tooltip?.Panel); }
        catch (Exception ex) { Plugin.Logger.LogWarning($"툴팁을 정리하지 못했습니다: {ex.Message}"); }
        finally { _tooltip = null; }
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
