namespace ChipGuide.Plugin.Game;

internal static class PveGate
{
    private static readonly HashSet<int> PveModes = new() { 4, 6, 9, 10, 12 };

    // 방이 해제돼도 MatchMode는 이전 값이 남으므로 TeamId를 먼저 본다.
    internal static bool Open()
    {
        var logic = GameState.Logic();
        var match = logic?.Get("match")?.Field("matchData");
        var controller = logic?.Get("room")?.Field("roomController");
        if (logic == null || match == null || controller == null) return false;
        if (match.Get<long>("TeamId") != 0 && !PveModes.Contains(match.Get("MatchMode")!.Value<int>())) return false;
        if (controller.Field("roomStateType")!.Value<int>() == 0) return false;
        var map = controller.Get("localRoom")?.Field("info")?.Get<int>("MapType") ?? 0;
        if (!PveModes.Contains(map)) return false;
        if (logic.Get("watch")?.Call("PlayerIsWatcher")?.Value<bool>() != false) return false;
        return logic.Get("replay")?.Get("Session")?.Get<bool>("CanSendC2S") == true;
    }
}
