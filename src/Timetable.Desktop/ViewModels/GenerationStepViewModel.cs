using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Timetable.Application.Abstractions;
using Timetable.Application.Models;
using Timetable.Application.Services;
using Timetable.Desktop.Resources;
using Timetable.Desktop.Services;
using Timetable.Domain.Entities;
using Timetable.Domain.Scheduling;

namespace Timetable.Desktop.ViewModels;

/// <summary>Étape « Génération » : pré-validation, préférences, lancement en tâche de fond et journal (FR-09, FR-10).</summary>
public sealed partial class GenerationStepViewModel : StepViewModel
{
    private readonly ProjectWorkflowService _workflow;
    private readonly GenerationService _generation;
    private readonly PreCheckService _preCheck;
    private readonly IDialogService _dialogs;

    private bool _loading;

    public GenerationStepViewModel(
        ProjectWorkflowService workflow,
        GenerationService generation,
        PreCheckService preCheck,
        IDialogService dialogs)
        : base(Strings.NavGeneration)
    {
        _workflow = workflow;
        _generation = generation;
        _preCheck = preCheck;
        _dialogs = dialogs;

        foreach (var (code, label) in WeightLabels)
        {
            Weights.Add(new WeightViewModel(code, label, GenerationWeights.Default[code], OnWeightChanged));
        }
    }

    private static readonly (SoftConstraintCode Code, string Label)[] WeightLabels =
    {
        (SoftConstraintCode.Holes, "Limiter les trous dans la journée des classes"),
        (SoftConstraintCode.MaxTwoSameSubjectPerDay, "Éviter plus de 2 séances d'une matière le même jour"),
        (SoftConstraintCode.SpreadSubjectOverDays, "Répartir les matières sur plusieurs jours"),
        (SoftConstraintCode.OneBlockPerDay, "Au plus un bloc de sport par jour et par classe"),
        (SoftConstraintCode.TeacherBalance, "Équilibrer la charge des enseignants"),
        (SoftConstraintCode.NoSportAroundBreak, "Éviter le sport avant/après une pause"),
    };

    public ObservableCollection<PreCheckIssue> Issues { get; } = new();

    public ObservableCollection<WeightViewModel> Weights { get; } = new();

    public ObservableCollection<string> Log { get; } = new();

    [ObservableProperty]
    private bool _hasBlockingIssues;

    [ObservableProperty]
    private string? _reportSummary;

    [ObservableProperty]
    private int _maxTimeSeconds = 60;

    [ObservableProperty]
    private int _randomSeed;

    [ObservableProperty]
    private bool _oneClassPerTeacherWeek;

    [ObservableProperty]
    private bool _avoidSportAroundBreak = true;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _progressText = string.Empty;

    [ObservableProperty]
    private double _elapsedSeconds;

    [ObservableProperty]
    private string? _resultSummary;

    /// <summary>Le bouton « Générer » n'est actif que si aucune erreur bloquante n'est détectée.</summary>
    public bool CanGenerate => !IsRunning && !HasBlockingIssues;

    partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(CanGenerate));

    partial void OnHasBlockingIssuesChanged(bool value) => OnPropertyChanged(nameof(CanGenerate));

    public event EventHandler? GenerationCompleted;

    public override void Refresh()
    {
        _loading = true;

        var snapshot = _workflow.State.Snapshot;
        foreach (var weight in Weights)
        {
            weight.Weight = snapshot.Weights[weight.Code];
        }

        OneClassPerTeacherWeek = snapshot.OneClassPerTeacherWeek;
        _loading = false;

        UpdatePreCheck(snapshot);
    }

    private void OnWeightChanged(WeightViewModel row)
    {
        if (_loading)
        {
            return;
        }

        _workflow.QueueSave(async ct =>
        {
            if (_workflow.State.Repository is not null)
            {
                await _workflow.State.Repository.SavePreferencesAsync(CollectWeights(), ct);
                await _workflow.State.ReloadAsync(ct);
            }
        });
    }

    private GenerationWeights CollectWeights()
    {
        var weights = new GenerationWeights();
        foreach (var weight in Weights)
        {
            weights[weight.Code] = weight.Weight;
        }

        return weights;
    }

    private void UpdatePreCheck(ProjectSnapshot snapshot)
    {
        var report = _preCheck.Validate(snapshot);
        Issues.Clear();
        foreach (var issue in report.Issues)
        {
            Issues.Add(issue);
        }

        HasBlockingIssues = report.HasBlockingIssues;
        ReportSummary = report.Issues.Count == 0
            ? "Aucun problème détecté. La génération peut démarrer."
            : $"{report.Errors.Count()} bloquant(s), {report.Warnings.Count()} avertissement(s), " +
              $"{report.Information.Count()} information(s).";
        IsValid = !report.HasBlockingIssues;
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task Generate(CancellationToken cancellationToken)
    {
        var repository = _workflow.State.Repository;
        if (repository is null)
        {
            _dialogs.ShowWarning("Ouvrez ou créez d'abord un projet.");
            return;
        }

        await _workflow.FlushAsync(cancellationToken);
        await repository.SavePreferencesAsync(CollectWeights(), cancellationToken);
        await _workflow.State.ReloadAsync(cancellationToken);

        var snapshot = _workflow.State.Snapshot;
        UpdatePreCheck(snapshot);
        if (HasBlockingIssues)
        {
            AddLog("Génération refusée : la pré-validation signale des problèmes bloquants.");
            return;
        }

        IsRunning = true;
        Log.Clear();
        ResultSummary = null;
        AddLog($"Démarrage de la génération (durée maximale {MaxTimeSeconds} s, graine {RandomSeed})…");

        var options = new GenerationOptions
        {
            MaxTimeSeconds = Math.Clamp(MaxTimeSeconds, 10, 600),
            RandomSeed = RandomSeed,
            OneClassPerTeacherWeek = OneClassPerTeacherWeek,
            AvoidSportAroundBreak = AvoidSportAroundBreak,
        };

        var progress = new Progress<SolverProgress>(p =>
        {
            ElapsedSeconds = p.ElapsedSeconds;
            ProgressText = p.Message;
            Progress = Math.Min(99, p.ElapsedSeconds / Math.Max(1, options.MaxTimeSeconds) * 100);
        });

        try
        {
            var result = await _generation.GenerateAsync(snapshot, options, progress, cancellationToken);
            await HandleResultAsync(result, options, repository, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            AddLog("Génération annulée par l'utilisateur.");
        }
        catch (Exception ex)
        {
            AddLog($"Erreur pendant la génération : {ex.Message}");
            _dialogs.ShowError($"La génération a échoué :\n{ex.Message}");
        }
        finally
        {
            IsRunning = false;
            Progress = 100;
        }
    }

    private async Task HandleResultAsync(
        SolverResult result,
        GenerationOptions options,
        ITimetableRepository repository,
        CancellationToken cancellationToken)
    {
        ElapsedSeconds = result.ElapsedSeconds;

        if (result.HasSolution && result.Lessons.Count > 0)
        {
            var versions = _workflow.State.Snapshot.Versions;
            var version = new TimetableVersion
            {
                Name = $"Version {versions.Count + 1}",
                CreatedAtUtc = DateTime.UtcNow,
                IsSelected = true,
                SolverSeed = options.RandomSeed,
                Score = result.Score,
            };

            var saved = await repository.SaveVersionAsync(version, cancellationToken);
            var lessons = GenerationService.ToLessons(saved.Id, result);
            await repository.SaveLessonsAsync(saved.Id, lessons, cancellationToken);
            await repository.SetVersionSelectedAsync(saved.Id, cancellationToken);

            AddLog($"Solution {(result.Status == SolverStatus.Optimal ? "optimale" : "trouvée")} en " +
                   $"{result.ElapsedSeconds:0.0} s — score {result.Score:0}.");
            AddLog($"{lessons.Count} séances enregistrées dans « {saved.Name} ».");
            ResultSummary = $"Solution trouvée : {lessons.Count} séances (score {result.Score:0}).";

            await _workflow.State.ReloadAsync(cancellationToken);
            GenerationCompleted?.Invoke(this, EventArgs.Empty);
        }
        else if (result.HasSolution)
        {
            AddLog("Le solveur n'a produit aucune séance : le programme est vide.");
            AddLog("Renseignez le nombre de séances par matière dans l'étape « Programme », puis relancez.");
            ResultSummary = "Aucune séance à placer : le programme est vide.";
        }
        else
        {
            AddLog(result.Message ?? "Aucune solution trouvée.");
            foreach (var diagnostic in result.Diagnostics)
            {
                AddLog($"• [{diagnostic.Code}] {diagnostic.Message}");
            }

            ResultSummary = result.Status switch
            {
                SolverStatus.Infeasible => "Problème infaisable : voir le diagnostic ci-dessus.",
                SolverStatus.Timeout => "Délai dépassé sans solution complète.",
                SolverStatus.Cancelled => "Génération annulée.",
                _ => "Aucune solution trouvée.",
            };
        }
    }

    private void AddLog(string message) => Log.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
}

/// <summary>Curseur de pondération d'une contrainte souple (FR-10).</summary>
public sealed partial class WeightViewModel : ObservableObject
{
    private readonly Action<WeightViewModel> _onChanged;

    public WeightViewModel(SoftConstraintCode code, string label, int weight, Action<WeightViewModel> onChanged)
    {
        Code = code;
        Label = label;
        _weight = weight;
        _onChanged = onChanged;
    }

    public SoftConstraintCode Code { get; }

    public string Label { get; }

    [ObservableProperty]
    private int _weight;

    partial void OnWeightChanged(int value) => _onChanged(this);
}
