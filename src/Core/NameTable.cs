using System.Text.Json;

namespace ChipGuide.Core;

public sealed class NameTable
{
    private readonly Dictionary<string, HashSet<int>> _chipsByName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<int>> _chipsByBase = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<int>> _heroes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashSet<int>> _maps = new(StringComparer.Ordinal);
    private readonly Dictionary<int, Grade> _grades = new();

    public IReadOnlyDictionary<int, Grade> ChipGrades => _grades;

    public IReadOnlySet<int>? Chip(string name) => Find(_chipsByName, name) ?? Find(_chipsByBase, name);
    public IReadOnlySet<int>? Hero(string name) => Find(_heroes, name);
    public IReadOnlySet<int>? Map(string name) => Find(_maps, name);

    public static string BaseName(string name)
    {
        var separator = name.IndexOf(" - ", StringComparison.Ordinal);
        return (separator < 0 ? name : name[..separator]).Trim();
    }

    public static Grade ParseGrade(string? value) => value?.ToLowerInvariant() switch
    {
        "blue" => Grade.Blue,
        "purple" => Grade.Purple,
        "orange" => Grade.Orange,
        _ => Grade.None,
    };

    public static NameTable Parse(string json)
    {
        using var document = Json.Parse(json, "names.json");
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || Json.Int(root, "version") != 1)
            throw new RuleFormatException("names.json: version이 1인 객체여야 합니다. 템플릿을 다시 생성하세요.");

        var table = new NameTable();
        foreach (var (id, name, element) in Entries(root, "chips"))
        {
            Add(table._chipsByName, name, id);
            Add(table._chipsByBase, Json.String(element, "base")?.Trim() is { Length: > 0 } baseName ? baseName : BaseName(name), id);
            table._grades[id] = ParseGrade(Json.String(element, "grade"));
        }
        foreach (var (id, name, _) in Entries(root, "heroes")) Add(table._heroes, name, id);
        foreach (var (id, name, _) in Entries(root, "maps")) Add(table._maps, name, id);
        return table;
    }

    private static IEnumerable<(int Id, string Name, JsonElement Element)> Entries(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var list) || list.ValueKind != JsonValueKind.Array) yield break;
        foreach (var element in list.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object) continue;
            var id = Json.Int(element, "id");
            var name = Json.String(element, "name")?.Trim();
            if (id is null || string.IsNullOrEmpty(name)) continue;
            yield return (id.Value, name, element);
        }
    }

    private static void Add(Dictionary<string, HashSet<int>> index, string name, int id)
    {
        if (!index.TryGetValue(name, out var ids)) index[name] = ids = new HashSet<int>();
        ids.Add(id);
    }

    private static IReadOnlySet<int>? Find(Dictionary<string, HashSet<int>> index, string name) =>
        index.TryGetValue(name.Trim(), out var ids) ? ids : null;
}
