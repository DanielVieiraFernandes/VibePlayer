# Transcreve uma amostra com o executável já instalado. É onde o empacotamento quebra: o VLC e o
# Whisper procuram os nativos a partir do executável, e um arquivo fora do lugar só aparece aqui.
#
# Usa o processo de transcrição do próprio app (--transcription-worker), que não precisa de janela:
# job em JSON na entrada padrão e um evento em JSON por linha na saída. A entrada precisa ficar
# aberta até o fim, porque o worker entende o fechamento como "o app morreu" e sai.
#
# Roda com pwsh nos três sistemas. Só olha o tipo dos eventos: o texto da aula não vai para o log.
#
# Uso: smoke-transcription.ps1 -Executable <caminho> -Fixture <áudio> -Model <modelo>
param(
    [Parameter(Mandatory)] [string] $Executable,
    [Parameter(Mandatory)] [string] $Fixture,
    [Parameter(Mandatory)] [string] $Model,
    [int] $TimeoutSeconds = 300
)

$ErrorActionPreference = 'Stop'

$job = @{
    VideoPath = (Resolve-Path $Fixture).Path
    ModelPath = (Resolve-Path $Model).Path
    Language  = 'en'
    Threads   = 2
    ForceCpu  = $true
} | ConvertTo-Json -Compress

$startInfo = [System.Diagnostics.ProcessStartInfo]::new($Executable)
$startInfo.ArgumentList.Add('--transcription-worker')
$startInfo.RedirectStandardInput = $true
$startInfo.RedirectStandardOutput = $true
$startInfo.UseShellExecute = $false

$process = [System.Diagnostics.Process]::Start($startInfo)
try {
    $process.StandardInput.WriteLine($job)
    $process.StandardInput.Flush()

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ($true) {
        $read = $process.StandardOutput.ReadLineAsync()
        $remaining = [int]($deadline - [DateTime]::UtcNow).TotalMilliseconds
        if ($remaining -le 0 -or -not $read.Wait($remaining)) {
            throw "A transcrição não terminou em $TimeoutSeconds s."
        }

        $line = $read.Result
        if ($null -eq $line) {
            $process.WaitForExit()
            throw "O processo de transcrição saiu com o código $($process.ExitCode), sem o evento Done."
        }

        # Bibliotecas nativas podem escrever na mesma saída: linha que não é JSON não é evento.
        try {
            $workerEvent = $line | ConvertFrom-Json
        }
        catch {
            continue
        }

        switch ($workerEvent.Kind) {
            'Done' {
                Write-Host 'Transcrição concluída com o executável instalado.'
                return
            }
            # Value é um código de TranscriptionErrors, nunca texto da aula.
            'Error' { throw "O processo de transcrição informou o erro '$($workerEvent.Value)'." }
            'NoAudio' { throw 'O processo de transcrição não encontrou áudio na amostra.' }
        }
    }
}
finally {
    if (-not $process.HasExited) {
        $process.Kill($true)
    }
}
