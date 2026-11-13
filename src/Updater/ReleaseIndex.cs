using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace ChipGuide.Updater;

// distribution 브랜치의 release-index.json. 형식과 게시 순서는 docs/ARCHITECTURE.md "릴리스 알림".
internal static class ReleaseIndex
{
    public const string Repository = "YoungHoon02/astral-party-chip-guide";
    public const string ReleasesPage = "https://github.com/" + Repository + "/releases/latest";
    // api.github.com은 미인증 IP당 시간당 60회라 IP를 나눠 쓰는 환경에서 막힌다. raw는 CDN이다.
    public const string Url = "https://raw.githubusercontent.com/" + Repository + "/distribution/release-index.json";
    public const int MaxBytes = 64 * 1024;
    private const int SupportedSchema = 1;

    [DataContract]
    private sealed class Index
    {
        [DataMember(Name = "schemaVersion")] public int SchemaVersion { get; set; }
        [DataMember(Name = "version")] public string? Version { get; set; }
        [DataMember(Name = "downloadUrl")] public string? DownloadUrl { get; set; }
        [DataMember(Name = "sha256")] public string? Sha256 { get; set; }
        [DataMember(Name = "size")] public long Size { get; set; }
    }

    // 더 새 버전이면 그 버전 문자열, 아니면 null. 인덱스가 어긋나면 예외 — 엉뚱한 알림보다 침묵이 낫다.
    // 베타 설치본은 같은 번호의 정식판보다 앞이라 번호가 같아도 알린다. 인덱스에는 사전 릴리스가 오르지 않는다.
    public static string? Newer(byte[] json, Version current, bool prerelease = false)
    {
        Index index;
        try
        {
            using var stream = new MemoryStream(json);
            index = (Index)new DataContractJsonSerializer(typeof(Index)).ReadObject(stream)!;
        }
        catch (SerializationException e)
        {
            throw new InvalidDataException("bad index json", e);
        }

        if (index.SchemaVersion != SupportedSchema) throw new InvalidDataException("unknown index schema");
        string version = index.Version ?? "";
        if (!System.Version.TryParse(version, out Version? latest)) throw new InvalidDataException("bad version");

        // 인덱스가 오염돼도 엉뚱한 주소를 안내하지 못하게, 버전으로 주소를 다시 만들어 대조한다.
        string expected = $"https://github.com/{Repository}/releases/download/v{version}/AstralPartyChipGuide-v{version}.zip";
        if (index.DownloadUrl != expected) throw new InvalidDataException("unexpected download url");
        if (!IsSha256(index.Sha256) || index.Size <= 0) throw new InvalidDataException("bad package digest");

        int order = Normalize(latest!).CompareTo(Normalize(current));
        return order > 0 || (order == 0 && prerelease) ? version : null;
    }

    // 어셈블리 버전에는 베타 꼬리표가 남지 않아 0.6.0-beta.1이 0.6.0.0이 된다.
    // 빌드가 제품 버전에 넣는 원래 문자열(0.6.0-beta.1+커밋)에서 꼬리표를 찾고, 번호가 어셈블리 버전과 다르면 믿지 않는다.
    public static string? Prerelease(string? productVersion, Version current)
    {
        if (string.IsNullOrEmpty(productVersion)) return null;
        int plus = productVersion!.IndexOf('+');
        string text = plus < 0 ? productVersion : productVersion.Substring(0, plus);
        int dash = text.IndexOf('-');
        return dash > 0 && text.Substring(0, dash) == Normalize(current).ToString() ? text : null;
    }

    // 어셈블리 버전은 0.1.0.0이고 인덱스는 0.1.0라, 네 번째 자리를 버리고 비교한다.
    public static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0));

    private static bool IsSha256(string? s)
    {
        if (s is null || s.Length != 64) return false;
        foreach (char c in s)
            if (!Uri.IsHexDigit(c)) return false;
        return true;
    }
}
