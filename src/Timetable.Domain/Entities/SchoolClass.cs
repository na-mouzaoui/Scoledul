namespace Timetable.Domain.Entities;

/// <summary>Classe de l'établissement (table <c>school_class</c>).</summary>
public class SchoolClass
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Position { get; set; }

    public static SchoolClass WithDefaultName(int position) => new()
    {
        Name = $"Classe {position + 1}",
        Position = position,
    };
}
