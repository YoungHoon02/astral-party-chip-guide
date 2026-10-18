using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ChipGuide.Core;

public enum RuleUpdateAction { Keep, Install, Replace }

public sealed record RuleUpdatePlan(RuleUpdateAction Action, bool Backup, string Content);

// 플러그인에 든 기본 규칙의 지문을 rules.json 첫 줄에 남긴다. 지문이 같으면 이용자가 고친 내용을 두고,
// 다르면 모드가 업데이트된 것이라 새 기본 규칙으로 바꾼다. 고친 흔적이 있을 때만 백업한다.
public static class RuleUpdate
{
    public const string BackupSuffix = ".bak";

    private static readonly Regex Header = new(@"^// 칩 가이드 v\S+ 기본 규칙 \[(?<id>[0-9a-f]{12})\][^\n]*\n?");

    public static RuleUpdatePlan Plan(string? installed, string bundled, string version)
    {
        var id = Fingerprint(bundled);
        var content = $"// 칩 가이드 v{version} 기본 규칙 [{id}] — 모드를 업데이트하면 새 기본 규칙으로 자동으로 바뀝니다. 직접 고친 내용은 rules.json{BackupSuffix}에 남습니다.\n"
            + bundled.TrimStart('﻿');
        if (installed is null) return new(RuleUpdateAction.Install, false, content);

        installed = installed.TrimStart('﻿');
        var header = Header.Match(installed);
        if (header.Success && header.Groups["id"].Value == id) return new(RuleUpdateAction.Keep, false, installed);
        var edited = header.Success
            ? Fingerprint(installed[header.Length..]) != header.Groups["id"].Value
            : Fingerprint(installed) != id;
        return new(RuleUpdateAction.Replace, edited, content);
    }

    // 줄바꿈(git autocrlf, 편집기)만 다른 파일은 같은 규칙으로 본다.
    public static string Fingerprint(string rules)
    {
        var normalized = rules.TrimStart('﻿').Replace("\r\n", "\n");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash, 0, 6).ToLowerInvariant();
    }
}
