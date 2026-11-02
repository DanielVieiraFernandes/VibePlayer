namespace VibePlayer.App.ViewModels;

/// <summary>Área do vídeo e barra de controles. Sem aula carregada, os controles ficam desabilitados.</summary>
public sealed class PlayerViewModel : ViewModelBase
{
    // Fixo em false até a spec do player: os controles existem para o layout, sem função ainda.
    public bool HasLesson => false;
}
