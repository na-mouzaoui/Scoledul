namespace Timetable.Domain.Scheduling;

/// <summary>Codes des contraintes souples (§8.4, SC-01 à SC-06).</summary>
public enum SoftConstraintCode
{
    /// <summary>SC-01 — Limiter les trous dans la journée d'une classe (poids par défaut : élevé).</summary>
    Holes = 1,

    /// <summary>SC-02 — Éviter plus de 2 séances de la même matière le même jour.</summary>
    MaxTwoSameSubjectPerDay = 2,

    /// <summary>SC-03 — Répartir les séances d'une matière sur différents jours.</summary>
    SpreadSubjectOverDays = 3,

    /// <summary>SC-04 — Au plus un bloc de sport par jour et par classe.</summary>
    OneBlockPerDay = 4,

    /// <summary>SC-05 — Équilibrer la charge journalière des enseignants et limiter leurs trous.</summary>
    TeacherBalance = 5,

    /// <summary>SC-06 — Éviter le sport juste avant ou après une pause.</summary>
    NoSportAroundBreak = 6,
}

/// <summary>Poids ajustables des contraintes souples (FR-10 / écran « Préférences de génération »).</summary>
public sealed class GenerationWeights
{
    public const int MinWeight = 0;
    public const int MaxWeight = 10;

    private readonly int[] _weights = new int[7];

    public GenerationWeights()
    {
        this[SoftConstraintCode.Holes] = 10;
        this[SoftConstraintCode.MaxTwoSameSubjectPerDay] = 6;
        this[SoftConstraintCode.SpreadSubjectOverDays] = 6;
        this[SoftConstraintCode.OneBlockPerDay] = 6;
        this[SoftConstraintCode.TeacherBalance] = 3;
        this[SoftConstraintCode.NoSportAroundBreak] = 3;
    }

    public static GenerationWeights Default => new();

    public int this[SoftConstraintCode code]
    {
        get => _weights[(int)code];
        set => _weights[(int)code] = Math.Clamp(value, MinWeight, MaxWeight);
    }

    public GenerationWeights Clone()
    {
        var clone = new GenerationWeights();
        foreach (var code in Enum.GetValues<SoftConstraintCode>())
        {
            clone[code] = this[code];
        }

        return clone;
    }

    public IEnumerable<(SoftConstraintCode Code, int Weight)> Enumerate()
    {
        foreach (var code in Enum.GetValues<SoftConstraintCode>())
        {
            yield return (code, this[code]);
        }
    }
}
