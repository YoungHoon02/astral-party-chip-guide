using ChipGuide.Plugin.Runtime;

namespace ChipGuide.Plugin.Game;

internal sealed record ChipCard(RuntimeObject Item, int RelicId);

internal sealed record ChipScreenState(RuntimeObject Container, IReadOnlyList<ChipCard> Cards);

internal static class ChipScreen
{
    internal static ChipScreenState? Read(RuntimeObject root)
    {
        var window = GameUi.FindWindow(root, "RelicWindow");
        var ui = window?.Get("contentPane");
        if (window == null || ui == null || ui.TypeName != "UIRelicWindow" || !GameUi.Visible(ui)) return null;
        var offered = window.Field("vailRelics");
        var list = ui.Field("list_Relic");
        if (offered == null || list == null) return null;

        var offeredCount = offered.Get<int>("Count");
        var children = list.Get<int>("numChildren");
        var cards = new List<ChipCard>(children);
        for (var i = 0; i < children; i++)
        {
            var item = list.Call("GetChildAt", i);
            if (item == null || item.TypeName != "UIRelic_Button_Item" || !GameUi.Visible(item)) continue;
            var index = list.Call("ChildIndexToItemIndex", i)!.Value<int>();
            if (index < 0 || index >= offeredCount) continue;
            cards.Add(new ChipCard(item, offered.Call("get_Item", index)!.Value<int>()));
        }
        return cards.Count == 0 ? null : new ChipScreenState(ui, cards);
    }
}
