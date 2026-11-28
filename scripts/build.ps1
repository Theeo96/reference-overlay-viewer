param([string]$Dotnet = 'dotnet', [switch]$Publish)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    if ($Dotnet -eq 'dotnet' -and (Test-Path '.local\dotnet\dotnet.exe')) { $Dotnet = (Resolve-Path '.local\dotnet\dotnet.exe').Path }
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    $env:DOTNET_NOLOGO = '1'
    $env:DOTNET_CLI_HOME = Join-Path $projectRoot '.local\cli'
    $env:NUGET_PACKAGES = Join-Path $projectRoot '.local\nuget'
    $env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
    & $Dotnet build src/ReferenceOverlayViewer/ReferenceOverlayViewer.csproj -c Release "-p:RestoreConfigFile=$projectRoot\NuGet.Config"
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    $report = Join-Path $projectRoot 'artifacts\self-test.json'
    New-Item -ItemType Directory -Force artifacts | Out-Null
    $exe = Join-Path $projectRoot 'src\ReferenceOverlayViewer\bin\Release\net10.0-windows\ReferenceOverlayViewer.exe'
    $env:DOTNET_ROOT = Split-Path (Get-Command $Dotnet).Source -Parent
    $testProcess = Start-Process -FilePath $exe -ArgumentList @('--self-test', ('"' + $report + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($testProcess.ExitCode -ne 0) { throw "Self-test failed: $report" }
    $result = Get-Content $report -Raw | ConvertFrom-Json
    if (@($result.results | Where-Object { -not $_.passed }).Count -gt 0) { throw 'Self-test assertions failed' }
    Write-Output ("Passed {0} regression checks" -f $result.results.Count)
    if ($Publish) {
        & $Dotnet publish src/ReferenceOverlayViewer/ReferenceOverlayViewer.csproj -c Release -r win-x64 --self-contained true -o artifacts/win-x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false "-p:RestoreConfigFile=$projectRoot\NuGet.Config"
        if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
        $publishedReport = Join-Path $projectRoot 'artifacts\published-self-test.json'
        $buildDotnetRoot = $env:DOTNET_ROOT
        try {
            # Deliberately use a nonexistent runtime directory for the self-contained smoke check.
            $env:DOTNET_ROOT = Join-Path $projectRoot '.local\no-external-runtime'
            $env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
            $published = Start-Process -FilePath (Join-Path $projectRoot 'artifacts\win-x64\ReferenceOverlayViewer.exe') -ArgumentList @('--self-test', ('"' + $publishedReport + '"')) -WindowStyle Hidden -Wait -PassThru
        } finally { $env:DOTNET_ROOT = $buildDotnetRoot; Remove-Item Env:DOTNET_ROOT_X64 -ErrorAction SilentlyContinue }
        if ($published.ExitCode -ne 0) { throw 'Published executable self-test failed' }
        $packagedResult = Get-Content $publishedReport -Raw | ConvertFrom-Json
        if (@($packagedResult.results | Where-Object { -not $_.passed }).Count -gt 0) { throw 'Published self-test assertions failed' }
        Copy-Item README.md artifacts/win-x64/README.md
        Compress-Archive -Path artifacts/win-x64/* -DestinationPath artifacts/ReferenceOverlayViewer-win-x64.zip -Force
        Copy-Item -LiteralPath artifacts/win-x64/ReferenceOverlayViewer.exe -Destination ReferenceOverlayViewer.exe
        Get-FileHash ReferenceOverlayViewer.exe -Algorithm SHA256 | Format-List
    }
} finally { Pop-Location }
