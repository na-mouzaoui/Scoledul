using CommunityToolkit.Mvvm.ComponentModel;
using MahApps.Metro.IconPacks;

namespace Timetable.Desktop.ViewModels;

/// <summary>Classe de base d'une étape de l'assistant (barre latérale, §11).</summary>
public abstract partial class StepViewModel : ObservableObject
{
    protected StepViewModel(string title) => Title = title;

    public string Title { get; }

    /// <summary>Icône affichée dans la barre de navigation.</summary>
    public PackIconMaterialKind Icon { get; set; } = PackIconMaterialKind.CircleOutline;

    /// <summary>Faux si l'étape empêche de passer à la suivante (FR §11).</summary>
    [ObservableProperty]
    private bool _isValid = true;

    /// <summary>Recharge le contenu depuis l'état du projet.</summary>
    public virtual void Refresh()
    {
    }

    /// <summary>Persiste les modifications de l'étape.</summary>
    public virtual Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
