using System.Text.Encodings.Web;
using System.Text.Json;
using ChipGuide.Core;
using ChipGuide.Plugin.Runtime;

namespace ChipGuide.Plugin.Game;

internal sealed record ChipIcon(string Name, string Icon);

internal static class GameTables
{
    // UIStringType 정적 필드를 읽지 못할 때 쓰는 값이다. 선언 순서로 계산했고,
    // 실제 게임의 카드 문자열 값 9(Card)와 일치해 순서 기반 값이 맞음을 확인했다.
    private static readonly Dictionary<string, int> FallbackStringTypes = new() { ["Character"] = 10, ["Map"] = 31, ["Relic"] = 46 };
    private static readonly Dictionary<string, int> StringTypes = new();
    private static readonly HashSet<string> FailedFields = new();
    private static readonly Dictionary<int, string> ChipNames = new();
    private static readonly Dictionary<int, ChipRole> ChipRoles = new();
    private static Dictionary<int, RuntimeObject>? _heroWeights;
    private static Dictionary<Keyword, List<(ChipIcon Chip, List<int> Maps)>>? _keywordChips;

    internal static (Grade Grade, Keyword Keyword) ChipInfo(int relicId)
    {
        var config = RuntimeObject.StaticCall(GameUi.Require("UI", "UIHelper"), "GetRelicInfoConfigure", relicId);
        if (config == null) return (Grade.None, Keyword.None);
        var grade = config.Get<int>("RelicQualityType");
        var keyword = config.Get<int>("KeyWordType");
        return (grade is >= 1 and <= 3 ? (Grade)grade : Grade.None, keyword is >= 1 and <= 4 ? (Keyword)keyword : Keyword.None);
    }

    internal static ChipRole ChipRoleOf(int relicId)
    {
        if (ChipRoles.TryGetValue(relicId, out var role)) return role;
        var config = RuntimeObject.StaticCall(GameUi.Require("UI", "UIHelper"), "GetRelicInfoConfigure", relicId);
        if (config == null) return ChipRole.None;
        var roles = ReadInts(config.Get("TagType")).Select(RoleWeights.FromTag).OfType<RoleKind>().ToHashSet();
        var added = new RoleWeights(config.Get<int>("SupportPt"), config.Get<int>("TankPt"), config.Get<int>("AttackPt"), config.Get<int>("CardPt"));
        return ChipRoles[relicId] = new ChipRole(roles, added);
    }

    internal static RoleWeights? HeroWeights(int heroId)
    {
        _heroWeights ??= CharacterWeights(GameUi.Require("", "StaticConfigure"));
        if (_heroWeights.Count == 0) _heroWeights = null;
        if (_heroWeights == null || !_heroWeights.TryGetValue(heroId, out var weight)) return null;
        return new RoleWeights(weight.Get<int>("SupportPt"), weight.Get<int>("TankPt"), weight.Get<int>("AttackPt"), weight.Get<int>("CardPt"));
    }

    internal static string ChipName(int relicId)
    {
        if (ChipNames.TryGetValue(relicId, out var name)) return name;
        var config = RuntimeObject.StaticCall(GameUi.Require("UI", "UIHelper"), "GetRelicInfoConfigure", relicId);
        name = config == null ? "" : NameTable.BaseName(Local(config.Get<int>("NameID"), "Relic"));
        return ChipNames[relicId] = name;
    }

    // 등급만 다른 칩은 하나로 합친다. 설정 테이블은 실행 중 바뀌지 않아 한 번만 읽는다.
    internal static IReadOnlyList<ChipIcon> KeywordChips(Keyword keyword, int mapId)
    {
        _keywordChips ??= LoadKeywordChips();
        if (_keywordChips == null || !_keywordChips.TryGetValue(keyword, out var chips)) return Array.Empty<ChipIcon>();
        return chips.Where(c => c.Maps.Count == 0 || c.Maps.Contains(mapId)).Select(c => c.Chip).ToList();
    }

    private static Dictionary<Keyword, List<(ChipIcon Chip, List<int> Maps)>>? LoadKeywordChips()
    {
        var relics = Infos(GameUi.Require("", "StaticConfigure"), "get_Relic");
        if (relics == null) return null;
        var result = new Dictionary<Keyword, List<(ChipIcon, List<int>)>>();
        var seen = new HashSet<string>();
        foreach (var chip in Items(relics))
        {
            var keyword = chip.Get<int>("KeyWordType");
            if (keyword is < 1 or > 4) continue;
            var name = NameTable.BaseName(Local(chip.Get<int>("NameID"), "Relic"));
            var icon = chip.Get("Icon")?.String() ?? "";
            if (name.Length == 0 || icon.Length == 0 || !seen.Add(name)) continue;
            if (!result.TryGetValue((Keyword)keyword, out var list)) result[(Keyword)keyword] = list = new();
            list.Add((new ChipIcon(name, icon), Ints(chip, "MapLimit", "chip")));
        }
        return result.Count == 0 ? null : result;
    }

    internal static int? ExportNames(string path)
    {
        FailedFields.Clear();
        var configure = GameUi.Require("", "StaticConfigure");
        var relics = Infos(configure, "get_Relic");
        var heroes = Infos(configure, "get_Character");
        var maps = Infos(configure, "get_Map");
        if (relics == null || heroes == null || maps == null) return null;
        var heroWeights = Safe("CharacterRelics", () => CharacterWeights(configure), new Dictionary<int, RuntimeObject>());

        using var buffer = new MemoryStream();
        var chipCount = 0;
        var heroCount = 0;
        var mapCount = 0;
        using (var json = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            json.WriteStartObject();
            json.WriteNumber("version", 1);

            json.WriteStartArray("chips");
            foreach (var chip in Items(relics))
            {
                var name = Local(chip.Get<int>("NameID"), "Relic");
                if (name.Length == 0) continue;
                json.WriteStartObject();
                json.WriteNumber("id", chip.Get<int>("Id"));
                json.WriteString("name", name);
                json.WriteString("base", NameTable.BaseName(name));
                json.WriteString("grade", GradeKey(chip.Get<int>("RelicQualityType")));
                json.WriteString("desc", Safe("chip.DescID", () => Local(chip.Get<int>("DescID"), "Relic"), ""));
                json.WriteNumber("keyword", Number(chip, "KeyWordType", "chip"));
                WriteInts(json, "tags", Ints(chip, "TagType", "chip"));
                WriteInts(json, "mapLimit", Ints(chip, "MapLimit", "chip"));
                json.WriteNumber("attackPt", Number(chip, "AttackPt", "chip"));
                json.WriteNumber("cardPt", Number(chip, "CardPt", "chip"));
                json.WriteNumber("supportPt", Number(chip, "SupportPt", "chip"));
                json.WriteNumber("tankPt", Number(chip, "TankPt", "chip"));
                json.WriteNumber("relicRecBaseScore", Number(chip, "RelicRecBaseScore", "chip"));
                json.WriteNumber("relicRecGrowth", Number(chip, "RelicRecGrowth", "chip"));
                json.WriteEndObject();
                chipCount++;
            }
            json.WriteEndArray();

            json.WriteStartArray("heroes");
            foreach (var hero in Items(heroes))
            {
                var id = hero.Get<int>("Id");
                var name = Local(hero.Get<int>("NameID"), "Character");
                if (name.Length == 0) continue;
                json.WriteStartObject();
                json.WriteNumber("id", id);
                json.WriteString("name", name);
                json.WriteNumber("tag", Number(hero, "TagType", "hero"));
                if (Safe("hero.weights", () => Weights(heroWeights, id), null) is { } weights)
                {
                    json.WriteStartObject("weights");
                    json.WriteNumber("support", weights.Support);
                    json.WriteNumber("tank", weights.Tank);
                    json.WriteNumber("card", weights.Card);
                    json.WriteNumber("attack", weights.Attack);
                    json.WriteEndObject();
                }
                json.WriteEndObject();
                heroCount++;
            }
            json.WriteEndArray();

            json.WriteStartArray("maps");
            foreach (var map in Items(maps))
            {
                var name = Local(map.Get<int>("MapName"), "Map");
                if (name.Length == 0) continue;
                json.WriteStartObject();
                json.WriteNumber("id", map.Get<int>("Id"));
                json.WriteString("name", name);
                json.WriteString("desc", Safe("map.MapDescription", () => Local(map.Get<int>("MapDescription"), "Map"), ""));
                WriteInts(json, "specialRelics", Ints(map, "MapSpecialRelic", "map"));
                json.WriteEndObject();
                mapCount++;
            }
            json.WriteEndArray();

            json.WriteEndObject();
        }

        if (chipCount == 0 || heroCount == 0 || mapCount == 0) return null;

        // 내용이 같으면 쓰지 않아야 파일 시각이 바뀌지 않고 규칙을 괜히 다시 읽지 않는다.
        var bytes = buffer.ToArray();
        if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) return chipCount;
        // 규칙 다시 읽기가 반쯤 쓰인 파일을 읽지 않도록 임시 파일을 쓴 뒤 바꿔 넣는다.
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, path, overwrite: true);
        if (FailedFields.Count > 0)
            Plugin.Logger.LogWarning($"참고용 필드를 읽지 못해 기본값을 썼습니다: {string.Join(", ", FailedFields)}");
        return chipCount;
    }

    private static RuntimeObject? Infos(IntPtr configure, string getter) =>
        RuntimeObject.StaticCall(configure, getter)?.Get("Infos");

    private static IEnumerable<RuntimeObject> Items(RuntimeObject list)
    {
        var count = list.Get<int>("Count");
        for (var i = 0; i < count; i++)
            if (list.Call("get_Item", i) is { } item) yield return item;
    }

    // 참고용 필드는 게임 데이터 이름을 실행 중에 처음 확인하므로, 하나가 틀려도 파일 생성을 막지 않는다.
    private static T Safe<T>(string field, Func<T> read, T fallback)
    {
        if (FailedFields.Contains(field)) return fallback;
        try
        {
            return read();
        }
        catch (Exception)
        {
            FailedFields.Add(field);
            return fallback;
        }
    }

    private static int Number(RuntimeObject item, string field, string owner) =>
        Safe($"{owner}.{field}", () => item.Get<int>(field), 0);

    private static List<int> Ints(RuntimeObject item, string field, string owner) =>
        Safe($"{owner}.{field}", () => ReadInts(item.Get(field)), new List<int>());

    private static List<int> ReadInts(RuntimeObject? list)
    {
        var values = new List<int>();
        if (list != null)
            foreach (var item in Items(list)) values.Add(item.Value<int>());
        return values;
    }

    private static void WriteInts(Utf8JsonWriter json, string name, List<int> values)
    {
        json.WriteStartArray(name);
        foreach (var value in values) json.WriteNumberValue(value);
        json.WriteEndArray();
    }

    private static Dictionary<int, RuntimeObject> CharacterWeights(IntPtr configure)
    {
        var table = new Dictionary<int, RuntimeObject>();
        if (RuntimeObject.StaticCall(configure, "get_Relic")?.Get("CharacterRelics") is { } characterRelics)
            foreach (var weight in Items(characterRelics)) table[weight.Get<int>("Id")] = weight;
        return table;
    }

    private static (int Support, int Tank, int Card, int Attack)? Weights(Dictionary<int, RuntimeObject> table, int heroId)
    {
        if (!table.TryGetValue(heroId, out var weight)) return null;
        return (weight.Get<int>("SupportPt"), weight.Get<int>("TankPt"), weight.Get<int>("CardPt"), weight.Get<int>("AttackPt"));
    }

    private static string Local(int id, string type)
    {
        if (id <= 0) return "";
        return RuntimeObject.StaticCall(GameUi.Require("UI", "UIHelper"), "GetLocal", id, StringType(type))?.String().Trim() ?? "";
    }

    private static int StringType(string name)
    {
        if (StringTypes.TryGetValue(name, out var value)) return value;
        try
        {
            value = RuntimeObject.StaticField(GameUi.Require("UI", "UIStringType"), name)!.Value<int>();
        }
        catch (Exception ex)
        {
            value = FallbackStringTypes[name];
            Plugin.Logger.LogWarning($"UIStringType.{name}을 읽지 못해 {value}를 씁니다: {ex.Message}");
        }
        return StringTypes[name] = value;
    }

    private static string GradeKey(int value) => value switch
    {
        1 => "blue",
        2 => "purple",
        3 => "orange",
        _ => "none",
    };
}
