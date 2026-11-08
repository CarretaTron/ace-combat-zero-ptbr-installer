param([Parameter(Mandatory=$true)][string]$Installer)
$ErrorActionPreference = 'Stop'
$null = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $Installer).Path)
$ac0TestRoot = Join-Path $PSScriptRoot ('diretorios_' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $ac0TestRoot
$ac0Results = [Collections.Generic.List[object]]::new()

function Snapshot([string]$Directory) {
    $state = [ordered]@{}
    if (Test-Path -LiteralPath $Directory) {
        foreach ($item in @(Get-ChildItem -LiteralPath $Directory -Recurse -Force | Sort-Object FullName)) {
            $key = $item.FullName.Substring($Directory.Length)
            $state[$key] = if ($item.PSIsContainer) { 'directory' } else { (Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash }
        }
    }
    return ($state | ConvertTo-Json -Compress)
}

function Reject-WithoutMutation([string]$Name, [string]$Selection) {
    $before = Snapshot $ac0TestRoot
    $rejected = $false
    $reason = ''
    try { $null = [Ac0PtBrInstaller.InstallerEngine]::Install($Selection, $null) }
    catch { $rejected = $true; $reason = $_.Exception.Message }
    if (-not $rejected) { throw ('Diretório inválido aceito: ' + $Name) }
    if ((Snapshot $ac0TestRoot) -ne $before) { throw ('Arquivos alterados na recusa: ' + $Name) }
    $ac0Results.Add([ordered]@{ name = $Name; rejected = $true; files_unchanged = $true; reason = $reason })
}

Reject-WithoutMutation 'campo_vazio' ''
Reject-WithoutMutation 'inexistente' (Join-Path $ac0TestRoot 'nao_existe')
$ac0Empty = Join-Path $ac0TestRoot 'pasta_vazia'
$null = New-Item -ItemType Directory -Path $ac0Empty
Reject-WithoutMutation 'pasta_vazia' $ac0Empty
foreach ($missing in @('acz.exe', 'a.arc', 'd.arc')) {
    $directory = Join-Path $ac0TestRoot ('sem_' + $missing)
    $null = New-Item -ItemType Directory -Path $directory
    foreach ($name in @('acz.exe', 'a.arc', 'd.arc')) {
        if ($name -ne $missing) { [IO.File]::WriteAllText((Join-Path $directory $name), 'isolated test fixture') }
    }
    Reject-WithoutMutation ('falta_' + $missing) $directory
}
$ac0Parent = Join-Path $ac0TestRoot 'Steam Version com espaços'
$ac0Game = Join-Path $ac0Parent 'ACZero'
$null = New-Item -ItemType Directory -Path $ac0Game -Force
foreach ($name in @('acz.exe', 'a.arc', 'd.arc')) {
    [IO.File]::WriteAllText((Join-Path $ac0Game $name), [Guid]::NewGuid().ToString())
}
$before = Snapshot $ac0TestRoot
if ([Ac0PtBrInstaller.InstallerEngine]::ResolveGameDirectory($ac0Parent) -ne $ac0Game) { throw 'O filho ACZero não foi localizado.' }
if ([Ac0PtBrInstaller.InstallerEngine]::ResolveGameDirectory($ac0Game) -ne $ac0Game) { throw 'A pasta com os três arquivos não foi reconhecida.' }
if ((Snapshot $ac0TestRoot) -ne $before) { throw 'Resolver o caminho alterou arquivos.' }
$ac0Results.Add([ordered]@{ name = 'resolver_pai_e_ACZero_sem_trava_de_exe'; passed = $true; files_unchanged = $true })
Reject-WithoutMutation 'pacotes_nao_compativeis' $ac0Game
$report = [ordered]@{ status = 'passed'; total = $ac0Results.Count; fixture = $ac0TestRoot; results = $ac0Results.ToArray() }
$reportPath = Join-Path $PSScriptRoot 'resultado_diretorios_v010.json'
[IO.File]::WriteAllText($reportPath, ($report | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
Write-Output ('Aprovados: ' + $ac0Results.Count + '; nenhuma alteração nas recusas.')
