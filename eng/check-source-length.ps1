[CmdletBinding()]
param()

$warningLimit = 300
$hardLimit = 500
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$exceptionPath = Join-Path $PSScriptRoot 'source-length-exceptions.txt'
$exceptions = @{}

if (Test-Path -LiteralPath $exceptionPath) {
    foreach ($line in Get-Content -LiteralPath $exceptionPath) {
        $trimmed = $line.Trim()
        if ($trimmed.Length -eq 0 -or $trimmed.StartsWith('#')) {
            continue
        }

        $parts = $trimmed.Split('|', 2)
        if ($parts.Count -ne 2 -or [string]::IsNullOrWhiteSpace($parts[1])) {
            Write-Error "Invalid source-length exception '$trimmed'. Use PATH|JUSTIFICATION."
            exit 2
        }
        $exceptions[$parts[0].Replace('\', '/')] = $parts[1].Trim()
    }
}

$violations = 0
$sourceRoots = @('src', 'tests') | ForEach-Object { Join-Path $repositoryRoot $_ }
$files = Get-ChildItem -LiteralPath $sourceRoots -Recurse -File -Filter '*.cs' |
    Where-Object { $_.FullName -notmatch '[\\/](Generated|bin|obj)[\\/]' }

foreach ($file in $files) {
    $relativePath = [IO.Path]::GetRelativePath($repositoryRoot, $file.FullName).Replace('\', '/')
    $lineCount = (Get-Content -LiteralPath $file.FullName).Count
    if ($lineCount -gt $hardLimit) {
        if ($exceptions.ContainsKey($relativePath)) {
            Write-Warning "$relativePath has $lineCount lines; hard-limit exception: $($exceptions[$relativePath])"
        }
        else {
            Write-Error "$relativePath has $lineCount lines and exceeds the $hardLimit-line hard limit. Refactor it or add a justified exception."
            $violations++
        }
    }
    elseif ($lineCount -gt $warningLimit) {
        Write-Warning "$relativePath has $lineCount lines; consider refactoring before it reaches the $hardLimit-line hard limit."
    }
}

if ($violations -gt 0) {
    exit 1
}

Write-Host "Source length check passed for $($files.Count) handwritten C# files."
