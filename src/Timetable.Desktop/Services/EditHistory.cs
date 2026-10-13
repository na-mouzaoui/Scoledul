using Timetable.Domain.Entities;

namespace Timetable.Desktop.Services;

/// <summary>
/// Pile d'annuler / rétablir pour l'édition manuelle de l'emploi du temps (FR-12),
/// conservant au moins 50 niveaux.
/// </summary>
public sealed class EditHistory
{
    private const int Limit = 100;

    private readonly List<IReadOnlyList<Lesson>> _undo = new();
    private readonly List<IReadOnlyList<Lesson>> _redo = new();

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public int Depth => _undo.Count;

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    /// <summary>Mémorise l'état courant avant une modification.</summary>
    public void Push(IReadOnlyList<Lesson> state)
    {
        _undo.Add(Clone(state));
        if (_undo.Count > Limit)
        {
            _undo.RemoveAt(0);
        }

        _redo.Clear();
    }

    /// <summary>Annule : renvoie l'état précédent, ou <c>null</c> si la pile est vide.</summary>
    public IReadOnlyList<Lesson>? Undo(IReadOnlyList<Lesson> current)
    {
        if (_undo.Count == 0)
        {
            return null;
        }

        _redo.Add(Clone(current));
        var state = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        return Clone(state);
    }

    /// <summary>Rétablit : renvoie l'état suivant, ou <c>null</c>.</summary>
    public IReadOnlyList<Lesson>? Redo(IReadOnlyList<Lesson> current)
    {
        if (_redo.Count == 0)
        {
            return null;
        }

        _undo.Add(Clone(current));
        var state = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        return Clone(state);
    }

    private static IReadOnlyList<Lesson> Clone(IReadOnlyList<Lesson> lessons)
        => lessons.Select(l => new Lesson
        {
            Id = l.Id,
            VersionId = l.VersionId,
            ClassId = l.ClassId,
            SubjectId = l.SubjectId,
            TeacherId = l.TeacherId,
            TimeSlotId = l.TimeSlotId,
            BlockKey = l.BlockKey,
            IsLocked = l.IsLocked,
        }).ToList();
}
