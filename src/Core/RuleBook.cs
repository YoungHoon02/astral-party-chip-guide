using System.Text.Json;

namespace ChipGuide.Core;

public sealed record RuleLoad(RuleSet Rules, IReadOnlyList<string> Errors);

public static class RuleBook
{
    public static RuleLoad Load(string json, NameTable names)
    {
        using var document = Json.Parse(json, "rules.json");
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || Json.Int(root, "version") != 1)
            throw new RuleFormatException("rules.json: version이 1인 객체여야 합니다");

        var builder = new Builder(names);
        if (root.TryGetProperty("roles", out var roles)) builder.ReadRoles(roles);
        if (root.TryGetProperty("characters", out var characters)) builder.ReadCharacters(characters);
        if (root.TryGetProperty("rules", out var rules)) builder.ReadRules(rules);
        return new RuleLoad(new RuleSet(builder.Rules, builder.Roles), builder.Errors);
    }

    private sealed class Builder
    {
        private static readonly (string Key, Verdict Verdict)[] Lists =
            { ("core", Verdict.Core), ("good", Verdict.Good), ("avoid", Verdict.Avoid) };

        private readonly NameTable _names;
        public readonly List<ChipRule> Rules = new();
        public readonly Dictionary<int, Role> Roles = new();
        public readonly List<string> Errors = new();

        public Builder(NameTable names) => _names = names;

        public void ReadRoles(JsonElement roles)
        {
            if (!Expect(roles, JsonValueKind.Object, "roles")) return;
            foreach (var scope in roles.EnumerateObject())
            {
                var path = $"roles.{scope.Name}";
                if (RoleScope(scope.Name) is not { } target)
                {
                    Errors.Add($"{path}: common, dealer, support 중 하나여야 합니다");
                    continue;
                }
                if (!Expect(scope.Value, JsonValueKind.Object, path)) continue;
                ReadLists(scope.Value, path, target.Label, target.Specificity, new Condition(Roles: target.Roles), null);
            }
        }

        public void ReadCharacters(JsonElement characters)
        {
            if (!Expect(characters, JsonValueKind.Object, "characters")) return;
            foreach (var character in characters.EnumerateObject())
            {
                var path = $"characters.{character.Name}";
                var heroes = _names.Hero(character.Name);
                if (heroes is null)
                {
                    Errors.Add($"{path}: '{character.Name}' 캐릭터를 찾을 수 없습니다");
                    continue;
                }
                var body = character.Value;
                if (!Expect(body, JsonValueKind.Object, path)) continue;
                if (body.TryGetProperty("role", out var roleElement))
                {
                    if (ParseRole(roleElement) is { } role)
                        foreach (var id in heroes) Roles[id] = role;
                    else
                        Errors.Add($"{path}.role: dealer, support, flex 중 하나여야 합니다");
                }
                var whyMap = body.TryGetProperty("why", out var why) ? ReadWhyMap(why, $"{path}.why") : null;
                ReadLists(body, path, character.Name, 2, new Condition(Character: heroes), whyMap);
            }
        }

        public void ReadRules(JsonElement rules)
        {
            if (!Expect(rules, JsonValueKind.Array, "rules")) return;
            var index = 0;
            foreach (var rule in rules.EnumerateArray())
            {
                var path = $"rules[{index++}]";
                if (!Expect(rule, JsonValueKind.Object, path)) continue;
                if (ReadCondition(rule, path) is not { } condition) continue;
                if (!rule.TryGetProperty("verdict", out var verdictElement) || ParseVerdict(verdictElement) is not { } verdict)
                {
                    Errors.Add($"{path}.verdict: core, good, avoid 중 하나여야 합니다");
                    continue;
                }
                var why = Json.String(rule, "why");
                if (string.IsNullOrWhiteSpace(why))
                {
                    Errors.Add($"{path}.why: 조건부 규칙에는 이유 문구가 필요합니다");
                    continue;
                }
                if (!rule.TryGetProperty("chip", out var chipElement))
                {
                    Errors.Add($"{path}.chip: 칩이 없습니다");
                    continue;
                }
                if (ReadChipRef(chipElement, $"{path}.chip") is not { } chip) continue;
                var minGrade = chip.MinGrade;
                if (rule.TryGetProperty("minGrade", out var gradeElement))
                {
                    minGrade = ParseGrade(gradeElement);
                    if (minGrade is null)
                    {
                        Errors.Add($"{path}.minGrade: blue, purple, orange 중 하나여야 합니다");
                        continue;
                    }
                }
                Rules.Add(new ChipRule(chip.Ids, verdict, TextSafety.Clean(why), 2 + condition.Kinds, condition.Value, minGrade));
            }
        }

        private void ReadLists(JsonElement scope, string path, string label, int specificity, Condition condition,
            IReadOnlyDictionary<int, string>? whyMap)
        {
            foreach (var (key, verdict) in Lists)
            {
                if (!scope.TryGetProperty(key, out var list)) continue;
                if (!Expect(list, JsonValueKind.Array, $"{path}.{key}")) continue;
                var index = 0;
                foreach (var item in list.EnumerateArray())
                {
                    if (ReadChipRef(item, $"{path}.{key}[{index++}]") is not { } chip) continue;
                    var why = chip.Why ?? FirstWhy(whyMap, chip.Ids) ?? $"{label} {VerdictLabel(verdict)}";
                    Rules.Add(new ChipRule(chip.Ids, verdict, TextSafety.Clean(why), specificity, condition, chip.MinGrade));
                }
            }
        }

        private (IReadOnlySet<int> Ids, Grade? MinGrade, string? Why)? ReadChipRef(JsonElement item, string path)
        {
            string? name;
            Grade? minGrade = null;
            string? why = null;
            if (item.ValueKind == JsonValueKind.String)
            {
                name = item.GetString();
            }
            else if (item.ValueKind == JsonValueKind.Object)
            {
                name = Json.String(item, "chip");
                why = Json.String(item, "why");
                if (string.IsNullOrWhiteSpace(why)) why = null;
                if (item.TryGetProperty("minGrade", out var gradeElement))
                {
                    minGrade = ParseGrade(gradeElement);
                    if (minGrade is null)
                    {
                        Errors.Add($"{path}.minGrade: blue, purple, orange 중 하나여야 합니다");
                        return null;
                    }
                }
            }
            else
            {
                Errors.Add($"{path}: 칩 이름 문자열이나 {{ \"chip\": \"이름\" }} 객체여야 합니다");
                return null;
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                Errors.Add($"{path}: 칩 이름이 비어 있습니다");
                return null;
            }
            if (_names.Chip(name) is not { } ids)
            {
                Errors.Add($"{path}: '{name}' 칩을 찾을 수 없습니다");
                return null;
            }
            return (ids, minGrade, why);
        }

        private (Condition Value, int Kinds)? ReadCondition(JsonElement rule, string path)
        {
            if (!rule.TryGetProperty("when", out var whenElement) || whenElement.ValueKind != JsonValueKind.Object)
            {
                Errors.Add($"{path}.when: 조건 객체가 필요합니다");
                return null;
            }
            IReadOnlySet<int>? owned = null, ally = null, map = null, character = null;
            var kinds = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in whenElement.EnumerateObject())
            {
                var conditionPath = $"{path}.when.{property.Name}";
                if (!seen.Add(property.Name))
                {
                    Errors.Add($"{conditionPath}: 중복된 조건입니다");
                    return null;
                }
                Func<string, IReadOnlySet<int>?> resolve;
                if (property.Name == "owned") resolve = _names.Chip;
                else if (property.Name is "ally" or "character") resolve = _names.Hero;
                else if (property.Name == "map") resolve = _names.Map;
                else
                {
                    Errors.Add($"{conditionPath}: owned, ally, map, character 중 하나여야 합니다");
                    return null;
                }
                if (ReadNames(property.Value, conditionPath, resolve) is not { } ids) return null;
                switch (property.Name)
                {
                    case "owned": owned = ids; break;
                    case "ally": ally = ids; break;
                    case "character": character = ids; break;
                    default: map = ids; break;
                }
                kinds++;
            }
            if (kinds == 0)
            {
                Errors.Add($"{path}.when: 조건이 비어 있습니다");
                return null;
            }
            return (new Condition(owned, ally, map, character), kinds);
        }

        private IReadOnlySet<int>? ReadNames(JsonElement list, string path, Func<string, IReadOnlySet<int>?> resolve)
        {
            if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() == 0)
            {
                Errors.Add($"{path}: 이름이 하나 이상 든 배열이어야 합니다");
                return null;
            }
            var result = new HashSet<int>();
            foreach (var item in list.EnumerateArray())
            {
                var name = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
                if (string.IsNullOrWhiteSpace(name) || resolve(name) is not { } ids)
                {
                    Errors.Add($"{path}: '{name}'을(를) 찾을 수 없습니다");
                    return null;
                }
                result.UnionWith(ids);
            }
            return result;
        }

        private Dictionary<int, string> ReadWhyMap(JsonElement why, string path)
        {
            var result = new Dictionary<int, string>();
            if (!Expect(why, JsonValueKind.Object, path)) return result;
            foreach (var property in why.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.String || _names.Chip(property.Name) is not { } ids)
                {
                    Errors.Add($"{path}.{property.Name}: 칩 이름과 문구 문자열이어야 합니다");
                    continue;
                }
                var text = property.Value.GetString()!;
                if (string.IsNullOrWhiteSpace(text))
                {
                    Errors.Add($"{path}.{property.Name}: 이유 문구가 비어 있습니다");
                    continue;
                }
                foreach (var id in ids) result[id] = text;
            }
            return result;
        }

        private bool Expect(JsonElement element, JsonValueKind kind, string path)
        {
            if (element.ValueKind == kind) return true;
            Errors.Add($"{path}: {(kind == JsonValueKind.Object ? "객체여야 합니다" : "배열이어야 합니다")}");
            return false;
        }

        private static string? FirstWhy(IReadOnlyDictionary<int, string>? whyMap, IReadOnlySet<int> ids)
        {
            if (whyMap is null) return null;
            foreach (var id in ids)
                if (whyMap.TryGetValue(id, out var why)) return why;
            return null;
        }

        private static (int Specificity, IReadOnlySet<Role>? Roles, string Label)? RoleScope(string name) => name switch
        {
            "common" => (0, null, "공용"),
            "dealer" => (1, new HashSet<Role> { Role.Dealer, Role.Flex }, "딜러 공용"),
            "support" => (1, new HashSet<Role> { Role.Support, Role.Flex }, "서포터 공용"),
            _ => null,
        };

        private static string VerdictLabel(Verdict verdict) => verdict switch
        {
            Verdict.Core => "코어",
            Verdict.Good => "무난",
            _ => "보류",
        };

        private static Role? ParseRole(JsonElement element) =>
            (element.ValueKind == JsonValueKind.String ? element.GetString()?.ToLowerInvariant() : null) switch
            {
                "dealer" => Role.Dealer,
                "support" => Role.Support,
                "flex" => Role.Flex,
                _ => null,
            };

        private static Verdict? ParseVerdict(JsonElement element) =>
            (element.ValueKind == JsonValueKind.String ? element.GetString()?.ToLowerInvariant() : null) switch
            {
                "core" => Verdict.Core,
                "good" => Verdict.Good,
                "avoid" => Verdict.Avoid,
                _ => null,
            };

        private static Grade? ParseGrade(JsonElement element)
        {
            var grade = NameTable.ParseGrade(element.ValueKind == JsonValueKind.String ? element.GetString() : null);
            return grade == Grade.None ? null : grade;
        }
    }
}
