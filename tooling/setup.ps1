param([ValidateSet('core','dotnet','maui')][string[]]$Modules = @('core','dotnet','maui'))
$ErrorActionPreference = 'Stop'
# Inspect before changing anything. No optional module is installed here.
Get-Command python,dotnet,java,adb,headroom,netcoredbg-mcp -ErrorAction SilentlyContinue | Select-Object Name,Source
if (Get-Command dotnet -ErrorAction SilentlyContinue) { & dotnet --info; & dotnet workload list }
foreach ($module in ($Modules | Select-Object -Unique)) {
    switch ($module) {
        core { & "$PSScriptRoot/modules/core.ps1" }
        dotnet { & "$PSScriptRoot/modules/sdk.ps1"; & "$PSScriptRoot/modules/debugger.ps1" }
        maui { & "$PSScriptRoot/modules/maui.ps1"; & "$PSScriptRoot/modules/rider-android.ps1" }
    }
}
Write-Output 'Installation finished. Run configure.ps1 and the documented functional verification before declaring success.'
