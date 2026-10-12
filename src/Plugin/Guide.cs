using ChipGuide.Core;
using ChipGuide.Plugin.Game;
using ChipGuide.Plugin.Runtime;
using ChipGuide.Plugin.View;
using UnityEngine;

namespace ChipGuide.Plugin;

internal static class Guide
{
    private const float ScanInterval = 0.2f, FileCheckInterval = 2f, NamesRetryInterval = 3f;

    private static readonly Feature Template = new("템플릿 생성");
    private static readonly Feature Badges = new("칩 가이드", BadgeView.Clear);
    private static RuleFiles _files = null!;
    private static RuleSource _rules = null!;
    private static float _nextScan, _nextFileCheck, _nextNamesTry;
    private static bool _namesSynced;
    private static readonly IReadOnlySet<Keyword> NoKeywords = new HashSet<Keyword>();
    private static string _signature = "";
    private static bool _pve;

    internal static void Init()
    {
        _files = new RuleFiles(Plugin.Folder);
        _rules = new RuleSource(_files.ReadNames, _files.ReadRules, message => Plugin.Logger.LogInfo($"[규칙] {message}"));
        Badges.Run(() =>
        {
            _files.Changed();
            _rules.Reload();
        });
    }

    internal static void Tick()
    {
        Template.Run(TickTemplate);
        Badges.Run(TickBadges);
    }

    // 게임 업데이트·언어 변경을 따라가도록 세션마다 게임 데이터가 준비되면 한 번 자동으로 맞춘다.
    private static void TickTemplate()
    {
        if (Input.GetKeyDown(Plugin.TemplateKey.Value))
        {
            if (ExportNames() is { } chips) Plugin.Logger.LogInfo($"names.json을 만들었습니다: 칩 {chips}개");
            else Plugin.Logger.LogInfo("게임 데이터가 아직 준비되지 않았습니다. 로비에서 다시 눌러 주세요.");
            return;
        }
        var now = Time.unscaledTime;
        if (_namesSynced || now < _nextNamesTry) return;
        _nextNamesTry = now + NamesRetryInterval;
        if (ExportNames() is { } count)
        {
            _namesSynced = true;
            Plugin.Logger.LogInfo($"게임 데이터로 names.json을 확인했습니다: 칩 {count}개");
        }
    }

    private static int? ExportNames()
    {
        // 핫업데이트 어셈블리가 로드되기 전에는 설정 테이블 클래스가 없다.
        if (RuntimeObject.FindClass("", "StaticConfigure") == IntPtr.Zero) return null;
        try
        {
            return GameTables.ExportNames(Path.Combine(Plugin.Folder, "names.json"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Plugin.Logger.LogWarning($"names.json을 쓰지 못했습니다({ex.Message}). 잠시 뒤 다시 시도합니다.");
            return null;
        }
    }

    private static void TickBadges()
    {
        var now = Time.unscaledTime;
        if (now >= _nextFileCheck)
        {
            _nextFileCheck = now + FileCheckInterval;
            if (_files.Changed()) _rules.Reload();
        }

        var root = GameUi.Root();
        if (!Plugin.Enabled.Value || root == null)
        {
            _pve = false;
            Hide();
            return;
        }

        var scan = now >= _nextScan;
        if (scan)
        {
            _nextScan = now + ScanInterval;
            _pve = PveGate.Open();
        }
        if (!_pve)
        {
            Hide();
            return;
        }
        if (BadgeView.Showing)
        {
            if (!BadgeView.StillAttached()) Hide();
            else BadgeView.UpdateHover(root);
        }
        if (scan) Scan(root);
    }

    private static void Scan(RuntimeObject root)
    {
        var screen = ChipScreen.Read(root);
        if (screen == null)
        {
            Hide();
            return;
        }
        var signature = $"{_rules.Version}|{string.Join(",", screen.Cards.Select(c => $"{c.Item.Pointer:X}:{c.RelicId}"))}";
        if (signature == _signature) return;
        var situation = GameState.Read();
        if (situation == null)
        {
            Hide();
            return;
        }

        var infos = screen.Cards.Select(card => GameTables.ChipInfo(card.RelicId)).ToList();
        var options = screen.Cards.Select((card, i) => new ChipOption(card.RelicId, infos[i].Grade)).ToList();
        var advice = ChipAdvisor.Advise(_rules.Current, situation, options);
        var ownedKeywords = situation.OwnedRelics.Select(id => GameTables.ChipInfo(id).Keyword).ToList();
        var owned = KeywordLock.Owned(ownedKeywords);
        var warn = Plugin.KeywordWarning.Value;
        var core = warn && owned.Count == KeywordLock.Limit - 1
            ? ChipAdvisor.CoreChips(_rules.Current, situation).Select(id => (GameTables.ChipName(id), GameTables.ChipInfo(id).Keyword)).ToList()
            : null;
        var warnings = infos.Select(info => warn ? KeywordLock.Warning(owned, info.Keyword, core) : null).ToList();
        var panel = Plugin.KeywordPanel.Value
            ? KeywordLock.Keywords.Select(k => new KeywordRow(k, KeywordLock.State(owned, k), ownedKeywords.Count(o => o == k))).ToList()
            : null;
        BadgeView.Show(screen.Container,
            screen.Cards.Select((card, i) => (card.Item, advice[i], warnings[i], warn ? KeywordLock.WouldLock(owned, infos[i].Keyword) : NoKeywords)).ToList(),
            panel);
        _signature = signature;

        if (Plugin.Diagnostics.Value)
            Plugin.Logger.LogInfo($"[진단] 캐릭터={situation.HeroId} 보유={string.Join(",", situation.OwnedRelics)} 아군={string.Join(",", situation.AllyHeroes)} 맵={situation.MapId} "
                + $"보유 키워드={string.Join(",", owned)} "
                + $"판정={string.Join(" / ", options.Select((o, i) => $"{o.RelicId}({o.Grade},{infos[i].Keyword})={advice[i]?.Verdict.ToString() ?? "없음"}{(warnings[i] != null ? "+키워드경고" : "")}"))}");
    }

    private static void Hide()
    {
        if (BadgeView.Showing) BadgeView.Clear();
        _signature = "";
    }
}
