using Timetable.Domain.Entities;

namespace Timetable.Desktop.Services;

/// <summary>
/// Jeu de données d'exemple : 3 classes, 8 matières, 2 enseignants par matière et un programme
/// hebdomadaire complet, pour pouvoir générer un emploi du temps immédiatement.
/// </summary>
public sealed class DemoDataService
{
    private static readonly (string Name, string Color, int BlockSize)[] Subjects =
    {
        ("Mathématiques", "#4F81BD", 1),
        ("Français", "#C0504D", 1),
        ("Anglais", "#9BBB59", 1),
        ("Histoire-Géographie", "#8064A2", 1),
        ("Sciences", "#4BACC6", 1),
        ("Sport", "#F79646", 2),
        ("Arts plastiques", "#F756D0", 1),
        ("Musique", "#7F6000", 1),
    };

    private static readonly Dictionary<string, int> WeeklySessions = new()
    {
        ["Mathématiques"] = 4,
        ["Français"] = 4,
        ["Anglais"] = 3,
        ["Histoire-Géographie"] = 3,
        ["Sciences"] = 2,
        ["Sport"] = 2,            // bloc de 2 (taille de bloc de la matière Sport)
        ["Arts plastiques"] = 1,
        ["Musique"] = 1,
    };

    private static readonly (string Subject, string LastName, string FirstName)[] Teachers =
    {
        ("Mathématiques", "Dupont", "Jean"),
        ("Mathématiques", "Martin", "Claire"),
        ("Français", "Bernard", "Sophie"),
        ("Français", "Petit", "Lucas"),
        ("Anglais", "Durand", "Emma"),
        ("Anglais", "Moreau", "Thomas"),
        ("Histoire-Géographie", "Lefebvre", "Paul"),
        ("Histoire-Géographie", "Rousseau", "Julie"),
        ("Sciences", "Fournier", "Hugo"),
        ("Sciences", "Girard", "Léa"),
        ("Sport", "Bonnet", "Marc"),
        ("Sport", "Lambert", "Chloé"),
        ("Arts plastiques", "Mercier", "Anna"),
        ("Arts plastiques", "Faure", "Louis"),
        ("Musique", "Gauthier", "Eva"),
        ("Musique", "Roy", "Nathan"),
    };

    private static readonly string[] ClassNames = { "6A", "6B", "6C" };

    private readonly IDialogService _dialogs;

    public DemoDataService(IDialogService dialogs) => _dialogs = dialogs;

    /// <summary>
    /// Ajoute le jeu d'exemple au projet courant (les éléments déjà présents sont réutilisés,
    /// jamais dupliqués). Le programme est (re)défini pour toutes les classes.
    /// </summary>
    public async Task LoadIntoCurrentAsync(AppState state, CancellationToken cancellationToken = default)
    {
        if (state.Repository is null)
        {
            _dialogs.ShowWarning("Ouvrez ou créez d'abord un projet.");
            return;
        }

        var repository = state.Repository;

        // 1) Matières.
        var snapshot = await repository.LoadAsync(cancellationToken);
        foreach (var (name, color, blockSize) in Subjects)
        {
            if (snapshot.Subjects.All(s => s.Name != name))
            {
                await repository.SaveSubjectAsync(
                    new Subject { Name = name, ColorHex = color, BlockSize = blockSize },
                    cancellationToken);
            }
        }

        // 2) Classes.
        snapshot = await repository.LoadAsync(cancellationToken);
        var position = snapshot.Classes.Count == 0 ? 0 : snapshot.Classes.Max(c => c.Position) + 1;
        foreach (var className in ClassNames)
        {
            if (snapshot.Classes.All(c => c.Name != className))
            {
                await repository.SaveClassAsync(
                    new SchoolClass { Name = className, Position = position++ },
                    cancellationToken);
            }
        }

        // 3) Enseignants (2 par matière).
        snapshot = await repository.LoadAsync(cancellationToken);
        var subjectIdByName = snapshot.Subjects.ToDictionary(s => s.Name, s => s.Id);
        foreach (var (subjectName, lastName, firstName) in Teachers)
        {
            if (!subjectIdByName.TryGetValue(subjectName, out var subjectId))
            {
                continue;
            }

            var exists = snapshot.Teachers.Any(t =>
                t.SubjectId == subjectId &&
                string.Equals(t.LastName, lastName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(t.FirstName, firstName, StringComparison.OrdinalIgnoreCase));

            if (!exists)
            {
                await repository.SaveTeacherAsync(
                    new Teacher { LastName = lastName, FirstName = firstName, SubjectId = subjectId },
                    cancellationToken);
            }
        }

        // 4) Programme : nombre de séances par matière, pour toutes les classes.
        snapshot = await repository.LoadAsync(cancellationToken);
        foreach (var schoolClass in snapshot.Classes)
        {
            var requirements = snapshot.Subjects
                .Select(subject => new ClassSubjectRequirement
                {
                    ClassId = schoolClass.Id,
                    SubjectId = subject.Id,
                    WeeklySessions = WeeklySessions.GetValueOrDefault(subject.Name, 0),
                })
                .ToList();

            await repository.SaveRequirementsAsync(schoolClass.Id, requirements, cancellationToken);
        }

        await state.ReloadAsync(cancellationToken);

        _dialogs.ShowInformation(
            $"Jeu de démonstration chargé :\n" +
            $"• {snapshot.Classes.Count} classe(s)\n" +
            $"• {snapshot.Subjects.Count} matière(s)\n" +
            $"• {snapshot.Teachers.Count} enseignant(s)\n" +
            $"• programme hebdomadaire défini pour chaque classe\n\n" +
            "Vous pouvez passer à l'étape « Génération ».",
            "Jeu de démonstration");
    }
}
