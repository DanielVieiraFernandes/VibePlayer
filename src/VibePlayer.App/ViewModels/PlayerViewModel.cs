using CommunityToolkit.Mvvm.ComponentModel;

namespace VibePlayer.App.ViewModels;

/// <summary>Área do vídeo e barra de controles. Sem aula carregada, os controles ficam desabilitados.</summary>
public sealed partial class PlayerViewModel : ViewModelBase
{
    // Fixo em false até a spec do player: os controles existem para o layout, sem função ainda.
    public bool HasLesson => false;

    /// <summary>Aula escolhida na biblioteca; por enquanto só o título aparece (sem reprodução).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedLesson))]
    public partial SelectedLesson? CurrentLesson { get; set; }

    public bool HasSelectedLesson => CurrentLesson is not null;
}
