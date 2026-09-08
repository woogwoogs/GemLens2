using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace GemLens2;

public sealed class GemDatabase
{
    public int Schema { get; set; }
    public string ImportedUtc { get; set; } = "";
    public string Source { get; set; } = "";
    public List<GemEntry> Gems { get; set; } = new();

    public static GemDatabase Load(string? directory = null)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GemLens2.Data.gems.json")
            ?? (directory != null && File.Exists(Path.Combine(directory,"Data","gems.json"))
                ? File.OpenRead(Path.Combine(directory,"Data","gems.json")) : null)
            ?? throw new InvalidDataException("Bundled gem database is missing. Reinstall the complete plugin.");
        var db = JsonSerializer.Deserialize<GemDatabase>(stream) ?? throw new InvalidDataException("Gem database is empty.");
        if (db.Schema != 1 || db.Gems.Count == 0) throw new InvalidDataException("Unsupported gem database.");
        foreach (var gem in db.Gems)
        foreach (var table in gem.Tables)
        {
            if (table.Levels.Length == 0 || !table.Levels.SequenceEqual(table.Levels.Distinct().Order()))
                throw new InvalidDataException($"Invalid progression: {gem.Name}");
            foreach (var column in table.Columns)
                if (column.Cells.Length != table.Levels.Length || column.Min.Length != table.Levels.Length || column.Max.Length != table.Levels.Length)
                    throw new InvalidDataException($"Invalid column: {gem.Name}");
        }
        return db;
    }
}

public sealed class GemEntry
{
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Metadata { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Category { get; set; } = "";
    public string Weapon { get; set; } = "";
    public string Patch { get; set; } = "";
    public string Source { get; set; } = "";
    public string[] Quality { get; set; } = Array.Empty<string>();
    public List<Progression> Tables { get; set; } = new();
}

public sealed class Progression
{
    public int[] Levels { get; set; } = Array.Empty<int>();
    public List<StatColumn> Columns { get; set; } = new();
    public int Nearest(int level) => Enumerable.Range(0, Levels.Length).MinBy(i => Math.Abs(Levels[i] - level));
}

public sealed class StatColumn
{
    public string Name { get; set; } = "";
    public string[] Cells { get; set; } = Array.Empty<string>();
    public double?[] Min { get; set; } = Array.Empty<double?>();
    public double?[] Max { get; set; } = Array.Empty<double?>();
    public bool Numeric => Min.Any(v => v.HasValue);
    public bool HasRange => Min.Where((v,i) => v.HasValue && v != Max[i]).Any();
    public double? Value(int row, int mode) => mode switch
    {
        1 => Min[row], 2 => Max[row], _ => (Min[row] + Max[row]) / 2
    };
}

public static class Comparison
{
    public static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    public static string Delta(double? a, double? b)
    {
        if (!a.HasValue || !b.HasValue) return "—";
        double diff = b.Value - a.Value;
        string absolute = (diff > 0 ? "+" : "") + Number(diff);
        return a.Value == 0 ? absolute : absolute + " (" + (diff / Math.Abs(a.Value) > 0 ? "+" : "") + Number(diff / Math.Abs(a.Value) * 100) + "%)";
    }
}
