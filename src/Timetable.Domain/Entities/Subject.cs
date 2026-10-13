namespace Timetable.Domain.Entities;

/// <summary>Matière enseignée (table <c>subject</c>).</summary>
public class Subject
{
    public const int MinBlockSize = 1;
    public const int MaxBlockSize = 4;

    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ColorHex { get; set; } = "#4F81BD";

    /// <summary>Taille de bloc : 1 = séances isolées ; 2 = sport (deux séances consécutives).</summary>
    public int BlockSize { get; set; } = 1;

    /// <summary>Vrai si la matière exige des blocs de plusieurs séances consécutives.</summary>
    public bool RequiresBlocks => BlockSize > 1;
}
