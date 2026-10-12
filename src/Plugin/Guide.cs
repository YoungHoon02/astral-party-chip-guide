using ChipGuide.Core;
using ChipGuide.Plugin.Game;
using ChipGuide.Plugin.Runtime;
using ChipGuide.Plugin.View;
using UnityEngine;

namespace ChipGuide.Plugin;

internal static class Guide
{
    private const float ScanInterval = 0.2f, FileCheckInterval = 2f;

    private static readonly Feature Template = new("템플릿 생성");
    private static readonly Feature Badges = new("칩 가이드", BadgeView.Clear);
    private static RuleFiles _files = null!;
    private static RuleSource _rules = null!;
    private static float _nextScan, _nextFileCheck;
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

    private static void TickTemplate()
    {
        if (!Input.GetKeyDown(Plugin.TemplateKey.Value)) return;
        var path = Path.Combine(Plugin.Folder, "names.json");
        int? count;
        try
        {
            count = GameTables.ExportNames(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Plugin.Logger.LogWarning($"names.json을 쓰지 못했습니다({ex.Message}). 잠시 후 다시 눌러 주세요.");
            return;
        }
        Plugin.Logger.LogInfo(count is { } chips
            ? $"names.json을 만들었습니다: 칩 {chips}개 ({path})"
            : "게임 데이터가 아직 준비되지 않았습니다. 로비에서 다시 눌러 주세요.");
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
        var owned = KeywordLock.Owned(situation.OwnedRelics.Select(id => GameTables.ChipInfo(id).Keyword));
        var warn = Plugin.KeywordWarning.Value;
        var core = warn && owned.Count == KeywordLock.Limit - 1
            ? ChipAdvisor.CoreChips(_rules.Current, situation).Select(id => (GameTables.ChipName(id), GameTables.ChipInfo(id).Keyword)).ToList()
            : null;
        var warnings = infos.Select(info => warn ? KeywordLock.Warning(owned, info.Keyword, core) : null).ToList();
        BadgeView.Show(screen.Container, screen.Cards.Select((card, i) => (card.Item, advice[i], warnings[i])).ToList());
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
