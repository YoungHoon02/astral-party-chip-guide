using ChipGuide.Core;
using ChipGuide.Plugin.Runtime;

namespace ChipGuide.Plugin.Game;

internal static class GameState
{
    private const int MonsterType = 2;

    // 핫업데이트 어셈블리가 로드되기 전(타이틀·업데이트 화면)에는 클래스가 없으므로 예외 대신 null을 돌려준다.
    internal static RuntimeObject? Logic()
    {
        var klass = RuntimeObject.FindClass("GameLogic", "GameLogicManager");
        return klass == IntPtr.Zero ? null : RuntimeObject.StaticField(klass, "_inst");
    }

    internal static Situation? Read()
    {
        var logic = Logic();
        var battle = logic?.Get("battle");
        var room = logic?.Get("room")?.Get("curRoomInfo");
        var self = battle?.Call("GetSelfPlayerData");
        if (battle == null || room == null || self == null) return null;

        var selfPlayer = self.Field("player");
        var selfHero = selfPlayer == null ? null : HeroId(selfPlayer);
        if (selfPlayer == null || selfHero == null) return null;

        // 계정 ID는 나와 아군을 구분하는 데만 쓰고 어디에도 남기지 않는다.
        var selfId = selfPlayer.Get<long>("Id");
        var allies = new HashSet<int>();
        foreach (var data in Players(battle))
        {
            var player = data.Field("player");
            if (player == null || data.Get<int>("characterType") == MonsterType) continue;
            if (player.Get<long>("Id") != selfId && HeroId(player) is { } hero) allies.Add(hero);
        }
        return new Situation(selfHero.Value, Ints(self.Call("GetRelicIds")), allies, room.Get<int>("MapId"));
    }

    internal static string DescribePlayers()
    {
        var battle = Logic()?.Get("battle");
        if (battle == null) return "battle 없음";
        return string.Join(", ", Players(battle).Select(data => $"type={data.Get<int>("characterType")}"));
    }

    private static IEnumerable<RuntimeObject> Players(RuntimeObject battle)
    {
        var list = battle.Field("PlayerDatas");
        if (list == null) yield break;
        var count = list.Get<int>("Count");
        for (var i = 0; i < count; i++)
            if (list.Call("get_Item", i) is { } data) yield return data;
    }

    private static int? HeroId(RuntimeObject player) => player.Get("Hero")?.Get<int>("HeroId");

    private static HashSet<int> Ints(RuntimeObject? list)
    {
        var result = new HashSet<int>();
        if (list == null) return result;
        var count = list.Get<int>("Count");
        for (var i = 0; i < count; i++)
            if (list.Call("get_Item", i) is { } item) result.Add(item.Value<int>());
        return result;
    }
}
