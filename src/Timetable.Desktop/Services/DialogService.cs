using System.Windows;
using Microsoft.Win32;

namespace Timetable.Desktop.Services;

/// <summary>Implémentation WPF des dialogues (NFR-07 : messages compréhensibles et orientés action).</summary>
public sealed class DialogService : IDialogService
{
    public void ShowInformation(string message, string title = "Information")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowWarning(string message, string title = "Attention")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void ShowError(string message, string title = "Erreur")
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public bool Confirm(string message, string title = "Confirmation")
        => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public string? OpenFile(string filter, string? defaultDirectory = null)
    {
        var dialog = new OpenFileDialog { Filter = filter, CheckFileExists = true };
        if (!string.IsNullOrEmpty(defaultDirectory))
        {
            dialog.InitialDirectory = defaultDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SaveFile(string filter, string suggestedName, string? defaultDirectory = null)
    {
        var dialog = new SaveFileDialog { Filter = filter, FileName = suggestedName, AddExtension = true };
        if (!string.IsNullOrEmpty(defaultDirectory))
        {
            dialog.InitialDirectory = defaultDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder(string? defaultDirectory = null)
    {
        var dialog = new OpenFolderDialog();
        if (!string.IsNullOrEmpty(defaultDirectory))
        {
            dialog.InitialDirectory = defaultDirectory;
        }

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
