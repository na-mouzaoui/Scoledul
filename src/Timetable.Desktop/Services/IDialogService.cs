namespace Timetable.Desktop.Services;

/// <summary>Abstraction des boîtes de dialogue et sélecteurs de fichiers (testable).</summary>
public interface IDialogService
{
    void ShowInformation(string message, string title = "Information");

    void ShowWarning(string message, string title = "Attention");

    void ShowError(string message, string title = "Erreur");

    bool Confirm(string message, string title = "Confirmation");

    string? OpenFile(string filter, string? defaultDirectory = null);

    string? SaveFile(string filter, string suggestedName, string? defaultDirectory = null);

    string? PickFolder(string? defaultDirectory = null);
}
