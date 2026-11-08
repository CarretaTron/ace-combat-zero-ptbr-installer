param([Parameter(Mandatory=$true)][string]$Original, [Parameter(Mandatory=$true)][string]$Rebuilt)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Hash-Bytes([byte[]]$Bytes) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { ([BitConverter]::ToString($algorithm.ComputeHash($Bytes))).Replace('-','').ToLowerInvariant() }
    finally { $algorithm.Dispose() }
}
function Describe-Assembly([string]$Path) {
    $assembly = [Reflection.Assembly]::LoadFile((Resolve-Path -LiteralPath $Path).Path)
    $methods = [ordered]@{}
    $flags = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance,DeclaredOnly'
    foreach ($type in @($assembly.GetTypes() | Sort-Object MetadataToken)) {
        $members = @($type.GetMethods($flags)) + @($type.GetConstructors($flags))
        foreach ($method in @($members | Sort-Object MetadataToken)) {
            $body = $method.GetMethodBody()
            $key = $type.FullName + '::' + $method.ToString()
            if ($null -eq $body) { $methods[$key] = $null; continue }
            $locals = @($body.LocalVariables | ForEach-Object { $_.LocalIndex.ToString() + ':' + $_.LocalType.FullName + ':' + $_.IsPinned.ToString() })
            $clauses = @($body.ExceptionHandlingClauses | ForEach-Object {
                $detail = $_.Flags.ToString() + ':' + $_.TryOffset + ':' + $_.TryLength + ':' + $_.HandlerOffset + ':' + $_.HandlerLength
                if ($_.Flags -eq [Reflection.ExceptionHandlingClauseOptions]::Clause) { $detail += ':' + $_.CatchType.FullName }
                if ($_.Flags -eq [Reflection.ExceptionHandlingClauseOptions]::Filter) { $detail += ':' + $_.FilterOffset }
                $detail
            })
            $methods[$key] = [ordered]@{ token = $method.MetadataToken; il_sha256 = (Hash-Bytes $body.GetILAsByteArray());
                init_locals = $body.InitLocals; max_stack = $body.MaxStackSize; locals = $locals; exceptions = $clauses }
        }
    }
    $resources = [ordered]@{}
    foreach ($name in @($assembly.GetManifestResourceNames() | Sort-Object)) {
        $stream = $assembly.GetManifestResourceStream($name)
        $algorithm = [Security.Cryptography.SHA256]::Create()
        try { $resources[$name] = [ordered]@{ length = $stream.Length;
            sha256 = ([BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace('-','').ToLowerInvariant() } }
        finally { $stream.Dispose(); $algorithm.Dispose() }
    }
    return [ordered]@{ methods = $methods; resources = $resources }
}
$left = Describe-Assembly $Original
$right = Describe-Assembly $Rebuilt
$methodsMatch = ($left.methods | ConvertTo-Json -Depth 8 -Compress) -ceq ($right.methods | ConvertTo-Json -Depth 8 -Compress)
$resourcesMatch = ($left.resources | ConvertTo-Json -Depth 8 -Compress) -ceq ($right.resources | ConvertTo-Json -Depth 8 -Compress)
$result = [ordered]@{ ok = ($methodsMatch -and $resourcesMatch); methods_checked = $left.methods.Count;
    managed_method_bodies_match = $methodsMatch; managed_resources_checked = $left.resources.Count; managed_resources_match = $resourcesMatch;
    entry_points_invoked = $false; complete_pe_byte_identity_checked = $false }
$result | ConvertTo-Json
if (-not $result.ok) { throw 'Compiled managed code/resource comparison failed.' }
