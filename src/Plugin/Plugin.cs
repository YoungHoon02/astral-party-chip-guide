using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ChipGuide.Plugin;

[BepInPlugin(Guid, Name, Version)]
public sealed class Plugin : BasePlugin
{
    public const string Guid = "kr.younghoon02.astralparty.chipguide";
    public const string Name = "AstralPartyChipGuide";
    public const string Version = "0.4.0";

    internal static ManualLogSource Logger { get; private set; } = null!;
    internal static string Folder { get; private set; } = "";
    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<KeyCode> ToggleKey { get; private set; } = null!;
    internal static ConfigEntry<bool> KeywordWarning { get; private set; } = null!;
    internal static ConfigEntry<bool> KeywordPanel { get; private set; } = null!;
    internal static ConfigEntry<bool> ShowRoleWeights { get; private set; } = null!;
    internal static ConfigEntry<bool> CoreStrip { get; private set; } = null!;
    internal static ConfigEntry<bool> Diagnostics { get; private set; } = null!;

    public override void Load()
    {
        Logger = Log;
        Folder = Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!;
        Enabled = Config.Bind("일반", "활성화", true, "칩 선택 창에 판정 배지를 표시합니다.");
        ToggleKey = Config.Bind("일반", "표시 전환 키", KeyCode.F10, "칩 선택 창의 모드 표시(배지·역할군 태그·키워드 패널·코어 칩 줄·판정 이유)를 한 번에 숨기거나 다시 보여 줍니다.");
        KeywordWarning = Config.Bind("일반", "키워드 고정 경고", true, "키워드 칩을 한 종류만 가졌을 때, 다른 키워드 칩을 고르면 나머지 두 키워드가 더 나오지 않는다고 알려 줍니다.");
        KeywordPanel = Config.Bind("일반", "키워드 패널", true, "칩 선택 창 제목 띠 위에 키워드 4종의 보유·막힘 상태를 한 줄로 보여 줍니다.");
        ShowRoleWeights = Config.Bind("일반", "역할군 가중치", true, "칩 아이콘 왼쪽 위에 칩의 역할군과 추가 가중치를, 키워드 패널에 내 캐릭터의 현재 역할군 가중치를 보여 줍니다.");
        CoreStrip = Config.Bind("일반", "코어 칩 줄", true, "확인 버튼 아래에 내 캐릭터의 코어 칩 아이콘을 보여 줍니다. 가진 칩은 컬러, 아직 없는 칩은 회색입니다.");
        Diagnostics = Config.Bind("진단", "상세 로그", false, "칩 선택 창이 열릴 때 읽은 값을 로그에 남깁니다.");
        ConfigEntry<bool> checkRelease = Config.Bind("업데이트", "새 버전 확인", true,
            "게임을 켤 때 같은 폴더의 AstralPartyChipGuideUpdater.exe를 띄워 새 버전이 있는지 확인합니다. " +
            "새 버전이 있을 때만 게임 밖에 알림 창이 뜹니다. 확인 프로그램은 GitHub에 요청 하나만 보내며 " +
            "계정·게임 정보는 싣지 않고, 내려받거나 설치하지는 않습니다. 게임 프로세스는 네트워크를 쓰지 않습니다.");
        if (checkRelease.Value) ReleaseCheck.Start(Logger, Folder);
        Guide.Init();
        new Harmony(Guid).CreateClassProcessor(typeof(FrameHook)).Patch();
        Logger.LogInfo($"{Name} {Version} 로드됨");
    }
}

[HarmonyPatch(typeof(EventSystem), "Update")]
internal static class FrameHook
{
    private static int _frame = -1;

    [HarmonyPostfix]
    private static void Postfix()
    {
        if (_frame == Time.frameCount) return;
        _frame = Time.frameCount;
        Guide.Tick();
    }
}
