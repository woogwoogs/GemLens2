using ExileCore2.PoEMemory.Components;
using ImGuiNET;

namespace GemLens2;

public sealed partial class GemLens2
{
    private sealed record PlayerSkill(string Name, GemEntry? Gem, int? Level);
    private Dictionary<string, int[]> _skillLevels = new();
    private PlayerSkill? _selectedPlayerSkill;
    private List<PlayerSkill> _mySkills = new();
    private DateTime _nextSkillRead;
    private long _skillPlayer;
    private string _skillStatus = "";

    // Exact, unambiguous display-name matches only. Internal child effects must
    // never be assigned the progression of a similarly named parent skill.
    private GemEntry? MatchSkill(string name)
    {
        var matches = _database.Gems.Where(g => string.Equals(g.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private void RefreshMySkills(bool force = false)
    {
        if (!force && DateTime.UtcNow < _nextSkillRead) return;
        _nextSkillRead = DateTime.UtcNow.AddSeconds(1);
        var found = new List<PlayerSkill>();
        int unreadable = 0;
        try
        {
            var player = GameController.Player;
            if (player == null || !player.IsValid) { _mySkills.Clear(); _skillLevels.Clear(); _skillStatus = "Waiting for your character."; return; }
            _skillPlayer = player.Address;
            var skills = player.GetComponent<Actor>()?.ActorSkills;
            if (skills == null) { _mySkills.Clear(); _skillLevels.Clear(); _skillStatus = "Waiting for your skills."; return; }
            foreach (var skill in skills)
            {
                try
                {
                    if (!skill.IsUserSkill && !skill.IsOnSkillBar) continue;
                    var effect = skill.EffectsPerLevel;
                    var granted = effect?.GrantedEffect;
                    if (granted?.IsSupport == true || granted?.ActiveSkill?.IsGem != true) continue;
                    string name = granted?.ActiveSkill?.DisplayName ?? "";
                    if (string.IsNullOrWhiteSpace(name)) name = skill.Name ?? "";
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    int? level = null;
                    try { int raw = effect?.Level ?? 0; if (raw > 0 && raw <= 100) level = raw; } catch { }
                    var match = MatchSkill(name);
                    if (match == null) continue;
                    found.Add(new PlayerSkill(match.Name, match, level));
                }
                catch { unreadable++; }
            }
            _skillLevels = found.GroupBy(s => s.Gem!.Slug).ToDictionary(g => g.Key,
                g => g.Where(s => s.Level.HasValue).Select(s => s.Level!.Value).Distinct().Order().ToArray());
            _mySkills = found.GroupBy(s => s.Gem!.Slug).Select(g => g.OrderByDescending(s => s.Level).First())
                .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
            _skillStatus = unreadable > 0 ? "Some skills could not be read. Retrying automatically." : "";
        }
        catch { _mySkills.Clear(); _skillLevels.Clear(); _skillStatus = "Skills are unavailable. Retrying automatically."; }
    }

    private void SelectPlayerSkill(PlayerSkill skill)
    {
        if (skill.Gem == null) return;
        Select(skill.Gem, skill.Level ?? 20);
        _selectedPlayerSkill = skill;
        _notice = skill.Level is int level
            ? (_skillLevels.TryGetValue(skill.Gem.Slug, out var levels) && levels.Length > 1
                ? $"Detected levels: {string.Join(", ", levels)}. Comparing {level} (highest detected)."
                : $"My skills: level {level} reported by the game skill effect.")
            : "My skills: level unavailable. Choose From manually.";
        if (skill.Level is int actual && Current is { } t && t.Levels[_from] != actual)
            _notice += $" Reference starts at the nearest available level, {t.Levels[_from]}.";
    }

    private void DrawMySkills()
    {
        RefreshMySkills();
        if (ImGui.SmallButton("Refresh")) RefreshMySkills(true);
        Colored(Muted, $"{_mySkills.Count} gem skills");
        ImGui.Separator();
        ImGui.BeginChild("my-skill-results");
        try
        {
            if (_skillStatus.Length > 0) Wrapped(_skillStatus);
            if (_mySkills.Count == 0 && _skillStatus.Length == 0)
                Wrapped("No matching gem skills detected. Browse All skills to find other entries.");
            foreach (var skill in _mySkills)
            {
                string label = skill.Name + (skill.Level is int level ? $" (Lv {level})" : " (Lv ?)");
                ImGui.BeginDisabled(skill.Gem == null);
                if (ImGui.Selectable(label + "##my" + skill.Name + skill.Level,
                    skill.Gem != null && skill.Gem == _selectedPlayerSkill?.Gem)) SelectPlayerSkill(skill);
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    Tooltip(skill.Gem == null ? skill.Name + "\nNo exact match in the bundled reference."
                        : label + (_skillLevels.TryGetValue(skill.Gem.Slug, out var levels) && levels.Length > 1
                            ? "\nDetected levels: " + string.Join(", ", levels) + "\nOpens the highest detected level. Other levels are selectable in the details."
                            : "\nLevel reported by the game skill effect. Click to compare."));
            }
        }
        finally { ImGui.EndChild(); }
    }

    private void DrawDetectedLevels()
    {
        if (_selectedPlayerSkill?.Gem is not { } gem || !_skillLevels.TryGetValue(gem.Slug, out var levels) || levels.Length < 2) return;
        Colored(Muted, "Detected levels:");
        foreach (int level in levels)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton(level + "##detected"))
            {
                ResetLevels(level);
                _scrollToLevel = true;
                _notice = $"Detected levels: {string.Join(", ", levels)}. Comparing {level}.";
                if (Current is { } t && t.Levels[_from] != level)
                    _notice += $" Reference starts at the nearest available level, {t.Levels[_from]}.";
            }
        }
    }

    private void StepComparison(int direction)
    {
        if (Current is { } t) _to = Math.Clamp(_to + direction, 0, t.Levels.Length - 1);
    }
}
