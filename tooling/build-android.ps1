param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug', [switch]$DiagnosticTestHooks)
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
. "$PSScriptRoot/enter-env.ps1" -Modules dotnet,maui

# Windows AAPT2 cannot reliably read resource paths containing non-ASCII text.
# An ephemeral DOS drive alias keeps source and build output in the checkout.
$mappedDrive = $null
$buildRoot = $repositoryRoot
try {
    if ($IsWindows -and $repositoryRoot -match '[^\x00-\x7F]') {
        $used = @(Get-PSDrive -PSProvider FileSystem | Select-Object -ExpandProperty Name)
        foreach ($code in 90..70) {
            $letter = [string][char]$code
            if ($letter -in $used -or (Test-Path "${letter}:\")) { continue }
            & subst "${letter}:" $repositoryRoot
            if ($LASTEXITCODE -ne 0) { throw "Could not create the temporary Android build drive." }
            $mappedDrive = "${letter}:"
            $buildRoot = "$mappedDrive\"
            break
        }
        if (!$mappedDrive) { throw 'No free drive letter for the Android build. Use an ASCII-only checkout path.' }
    }

    & dotnet build (Join-Path $buildRoot 'src/DoingIt/DoingIt.csproj') -c $Configuration "-p:DiagnosticTestHooks=$($DiagnosticTestHooks.IsPresent.ToString().ToLowerInvariant())" --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw "Android build failed with exit code $LASTEXITCODE." }
    Write-Host "APK: $(Join-Path $repositoryRoot "src/DoingIt/bin/$Configuration/net10.0-android/com.doingit.app-Signed.apk")"
}
finally {
    if ($mappedDrive) { & subst $mappedDrive /d }
}
