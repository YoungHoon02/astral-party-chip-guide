using System.Diagnostics;
using BepInEx.Logging;

namespace ChipGuide.Plugin;

// 새 릴리스 확인은 게임 밖 프로그램(src/Updater)이 한다. 게임 프로세스는 띄우기만 하고 네트워크를 쓰지 않는다.
internal static class ReleaseCheck
{
    private const string UpdaterExe = "AstralPartyChipGuideUpdater.exe";

    public static void Start(ManualLogSource log, string pluginDir)
    {
        string exe = Path.Combine(pluginDir, UpdaterExe);
        if (!File.Exists(exe))
        {
            log.LogInfo($"[업데이트] {UpdaterExe}가 없어 새 버전 확인을 건너뜁니다.");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = "--auto",
                WorkingDirectory = pluginDir,
                UseShellExecute = true,
            });
            log.LogInfo("[업데이트] 게임 밖에서 새 버전 확인을 시작했습니다.");
        }
        catch (Exception e)
        {
            log.LogWarning($"[업데이트] {UpdaterExe}를 실행하지 못했습니다: {e.Message}");
        }
    }
}
