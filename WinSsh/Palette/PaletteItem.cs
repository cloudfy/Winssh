namespace WinSsh.Palette;

public enum PaletteItemKind
{
    Profile,
    Action
}

public sealed class PaletteItem
{
    public PaletteItemKind Kind { get; set; }
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public string? IconGlyph { get; set; } // Segoe MDL2 Assets glyph (optional)
    public object? Data { get; set; }      // HostProfile or action string

    public override string ToString() => Title;
}
