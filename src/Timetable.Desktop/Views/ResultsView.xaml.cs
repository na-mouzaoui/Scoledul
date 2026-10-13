using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Timetable.Desktop.ViewModels;

namespace Timetable.Desktop.Views;

/// <summary>
/// Vue des résultats : gère le glisser-déposer des séances (FR-12) et le verrouillage
/// par double-clic. Toute la logique métier est déléguée au <see cref="ResultsStepViewModel"/>.
/// </summary>
public partial class ResultsView : UserControl
{
    private ScheduleCellViewModel? _dragSource;

    public ResultsView() => InitializeComponent();

    private void Cell_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not FrameworkElement element)
        {
            return;
        }

        if (element.DataContext is ScheduleCellViewModel { HasLesson: true } cell)
        {
            _dragSource = cell;
            DragDrop.DoDragDrop(element, cell, DragDropEffects.Move);
        }
    }

    private void Cell_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = _dragSource is null ? DragDropEffects.None : DragDropEffects.Move;
        e.Handled = true;
    }

    private async void Cell_Drop(object sender, DragEventArgs e)
    {
        if (_dragSource is null || sender is not FrameworkElement element ||
            element.DataContext is not ScheduleCellViewModel target)
        {
            return;
        }

        if (DataContext is ResultsStepViewModel viewModel)
        {
            await viewModel.TryMoveAsync(_dragSource, target);
        }

        _dragSource = null;
        e.Handled = true;
    }

    private async void Cell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2 || sender is not FrameworkElement element ||
            element.DataContext is not ScheduleCellViewModel cell)
        {
            return;
        }

        if (DataContext is ResultsStepViewModel viewModel)
        {
            await viewModel.ToggleLockCommand.ExecuteAsync(cell);
        }

        e.Handled = true;
    }
}
