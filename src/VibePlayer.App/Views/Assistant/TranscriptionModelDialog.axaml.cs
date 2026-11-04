using Avalonia.Controls;
using Avalonia.Interactivity;
using VibePlayer.Core.Transcription;

namespace VibePlayer.App.Views.Assistant;

/// <summary>Devolve o modelo escolhido, ou <c>null</c> ao cancelar ou fechar.</summary>
public partial class TranscriptionModelDialog : Window
{
    public TranscriptionModelDialog()
    {
        InitializeComponent();
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e) =>
        Close(AccurateOption.IsChecked == true ? WhisperModels.TurboQuantized : WhisperModels.SmallQuantized);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
}
