namespace Timetable.Domain.Entities;

/// <summary>Poids d'une contrainte souple (table <c>generation_preference</c>, §8.4).</summary>
public class GenerationPreference
{
    public string Code { get; set; } = string.Empty;

    /// <summary>Poids de 0 (désactivée) à 10 (prioritaire).</summary>
    public int Weight { get; set; }
}
