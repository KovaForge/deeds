$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$apiDir = Join-Path $root "api"
$appDir = Join-Path $root "app"

if (-not $env:DB) {
    Write-Warning "DB environment variable is not set. The Functions API needs a Postgres connection string to start."
}

$func = Get-Command func -ErrorAction SilentlyContinue
if (-not $func) {
    Write-Error "Azure Functions Core Tools ('func') is required to run the API locally." -ErrorAction Stop
}

$apiProcess = $null
try {
    $apiProcess = Start-Process -FilePath $func.Source -ArgumentList "start" -WorkingDirectory $apiDir -PassThru -NoNewWindow
    Push-Location $appDir
    dotnet watch run
}
finally {
    if ($apiProcess -and -not $apiProcess.HasExited) {
        $apiProcess.Kill()
    }
    Pop-Location -ErrorAction SilentlyContinue
}
