using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace GemLens2;

public sealed class GemLens2Settings : ISettings
{
    public ToggleNode Enable { get; set; } = new(true);
    public HotkeyNodeV2 OpenKey { get; set; } = new(Keys.F7);
    public ToggleNode SelectHoveredGem { get; set; } = new(true);
    public RangeNode<int> Scale { get; set; } = new(100, 75, 150);
    public string LastGem { get; set; } = "shield_wall";
    public List<string> Favorites { get; set; } = new();
}
