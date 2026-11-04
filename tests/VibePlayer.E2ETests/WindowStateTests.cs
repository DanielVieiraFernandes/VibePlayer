using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VibePlayer.App;
using VibePlayer.App.State;
using VibePlayer.App.ViewModels;
using VibePlayer.App.Views;
using VibePlayer.Core.Settings;

namespace VibePlayer.E2ETests;

public sealed class WindowStateTests : IDisposable
{
    private readonly TemporaryAppData _data = new();

    public void Dispose() => _data.Dispose();

    [AvaloniaFact]
    public void Reabertura_RestauraPainelEJanela()
    {
        using (var services = _data.BuildServices())
        {
            var window = Open(services);
            var vm = window.ViewModel();
            vm.ShowTabCommand.Execute(SidePanelTab.Chat);
            vm.ResizeSidePanel(60);
            vm.ToggleSidePanelCommand.Execute(null);
            window.Width = 1100;
            window.Height = 700;
            WindowTestExtensions.Settle();
            window.Close();
        }

        using (var services = _data.BuildServices())
        {
            var window = Open(services);
            var vm = window.ViewModel();

            vm.IsSidePanelOpen.ShouldBeFalse();
            vm.SelectedTab.ShouldBe(SidePanelTab.Chat);
            vm.SidePanelWidth.ShouldBe(MainWindowViewModel.DefaultSidePanelWidth + 60);
            window.ClientSize.Width.ShouldBe(1100);
            window.ClientSize.Height.ShouldBe(700);
            window.WindowState.ShouldBe(WindowState.Normal);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Reabertura_RestauraJanelaMaximizadaSemPerderOTamanhoNormal()
    {
        using (var services = _data.BuildServices())
        {
            var window = Open(services);
            window.Width = 1000;
            window.Height = 650;
            WindowTestExtensions.Settle();
            window.WindowState = WindowState.Maximized;
            WindowTestExtensions.Settle();
            window.Close();
        }

        using (var services = _data.BuildServices())
        {
            var settings = services.GetRequiredService<ISettingsStore>();
            var window = Open(services);

            window.WindowState.ShouldBe(WindowState.Maximized);
            settings.GetAsync(UiSettings.WindowSize).GetAwaiter().GetResult().ShouldBe(new WindowSize(1000, 650));
            window.Close();
        }
    }

    [AvaloniaFact]
    public void MudancasRapidas_GravamUmaVez()
    {
        CountingSettingsStore? counter = null;
        using var services = _data.BuildServices(overrides => UseFastTracker(overrides, store => counter = store));
        var window = Open(services);
        var vm = window.ViewModel();

        // Arraste da divisória: dezenas de mudanças seguidas.
        for (var i = 0; i < 30; i++)
        {
            vm.ResizeSidePanel(1);
        }

        WaitForPendingSave();
        counter!.WidthWrites.ShouldBe(1);
        services.GetRequiredService<ISettingsStore>().GetAsync(UiSettings.SidePanelWidth).GetAwaiter().GetResult()
            .ShouldBe(MainWindowViewModel.DefaultSidePanelWidth + 30);
        window.Close();
    }

    [AvaloniaFact]
    public void FecharLogoAposMudar_NaoPerdeAMudanca()
    {
        using (var services = _data.BuildServices())
        {
            var window = Open(services);
            window.ViewModel().ShowTabCommand.Execute(SidePanelTab.KeyPoints);

            // Fecha antes dos 500 ms do atraso de gravação.
            window.Close();
        }

        using (var services = _data.BuildServices())
        {
            var window = Open(services);
            window.ViewModel().SelectedTab.ShouldBe(SidePanelTab.KeyPoints);
            window.Close();
        }
    }

    private static MainWindow Open(ServiceProvider services)
    {
        AppStartup.Prepare(services).Succeeded.ShouldBeTrue();
        var window = new VibePlayerApp(services).CreateMainWindow();
        window.Show();
        WindowTestExtensions.Settle();
        return window;
    }

    private static void UseFastTracker(IServiceCollection services, Action<CountingSettingsStore> created)
    {
        services.AddSingleton<CountingSettingsStore>(sp =>
        {
            var store = new CountingSettingsStore(ActivatorUtilities.CreateInstance<VibePlayer.Infrastructure.Settings.SqliteSettingsStore>(sp));
            created(store);
            return store;
        });
        services.AddSingleton<ISettingsStore>(sp => sp.GetRequiredService<CountingSettingsStore>());
        services.AddSingleton(sp => new WindowStateTracker(
            sp.GetRequiredService<ISettingsStore>(),
            sp.GetRequiredService<ILogger<WindowStateTracker>>())
        {
            SaveDelay = TimeSpan.FromMilliseconds(100),
        });
    }

    /// <summary>Deixa o DispatcherTimer do atraso disparar e a gravação terminar.</summary>
    private static void WaitForPendingSave()
    {
        // RunJobs não dispara timers; um frame do dispatcher roda o loop de verdade até o prazo.
        var frame = new DispatcherFrame();
        DispatcherTimer.RunOnce(() => frame.Continue = false, TimeSpan.FromMilliseconds(600));
        Dispatcher.UIThread.PushFrame(frame);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Conta as gravações da largura do painel para verificar o agrupamento.</summary>
    private sealed class CountingSettingsStore(ISettingsStore inner) : ISettingsStore
    {
        private int _widthWrites;

        public int WidthWrites => Volatile.Read(ref _widthWrites);

        public Task<T> GetAsync<T>(SettingKey<T> key, CancellationToken cancellationToken = default) =>
            inner.GetAsync(key, cancellationToken);

        public Task SetAsync<T>(SettingKey<T> key, T value, CancellationToken cancellationToken = default)
        {
            if (key.Name == UiSettings.SidePanelWidth.Name)
            {
                Interlocked.Increment(ref _widthWrites);
            }

            return inner.SetAsync(key, value, cancellationToken);
        }
    }
}
