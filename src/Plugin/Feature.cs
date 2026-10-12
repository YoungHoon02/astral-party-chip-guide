namespace ChipGuide.Plugin;

internal sealed class Feature
{
    private readonly string _name;
    private readonly Action? _cleanup;

    internal Feature(string name, Action? cleanup = null)
    {
        _name = name;
        _cleanup = cleanup;
    }

    internal bool Blocked { get; private set; }

    internal void Run(Action action)
    {
        if (Blocked) return;
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Blocked = true;
            Plugin.Logger.LogError($"[{_name}] 오류가 발생해 이번 실행 동안 이 기능을 끕니다. 게임 업데이트로 호환이 끊겼을 수 있습니다.\n{ex}");
            try { _cleanup?.Invoke(); }
            catch (Exception cleanupError) { Plugin.Logger.LogWarning($"[{_name}] 정리 중 오류: {cleanupError.Message}"); }
        }
    }
}
