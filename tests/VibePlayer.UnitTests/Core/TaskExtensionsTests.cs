using Microsoft.Extensions.Logging;
using NSubstitute;
using VibePlayer.Core.Threading;

namespace VibePlayer.UnitTests.Core;

public sealed class TaskExtensionsTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();

    public TaskExtensionsTests()
    {
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
    }

    [Fact]
    public async Task FireAndForget_TaskFalha_RegistraErroSemRelancar()
    {
        var failure = new InvalidOperationException("falha simulada");
        var source = new TaskCompletionSource();

        // Não pode lançar aqui: quem chama FireAndForget não espera exceção.
        Should.NotThrow(() => source.Task.FireAndForget(_logger, "Teste"));
        source.SetException(failure);

        await WaitUntilLoggedAsync();

        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            failure,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task FireAndForget_TaskCancelada_RegistraDebugENaoErro()
    {
        var source = new TaskCompletionSource();

        source.Task.FireAndForget(_logger, "Teste");
        source.SetCanceled(TestContext.Current.CancellationToken);

        await WaitUntilLoggedAsync();

        _logger.Received(1).Log(
            LogLevel.Debug,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
        _logger.DidNotReceive().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task FireAndForget_TaskConcluiComSucesso_NaoRegistraNada()
    {
        Task.CompletedTask.FireAndForget(_logger, "Teste");

        await Task.Delay(50, TestContext.Current.CancellationToken);

        _logger.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(ILogger.Log)).ShouldBeEmpty();
    }

    // A observação da task roda em continuação assíncrona; espera o registro com prazo
    // curto em vez de um Delay fixo, para o teste não ficar lento nem intermitente.
    private async Task WaitUntilLoggedAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (_logger.ReceivedCalls().Any(c => c.GetMethodInfo().Name == nameof(ILogger.Log)))
            {
                return;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }
}
