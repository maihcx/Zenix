$ErrorActionPreference = "Stop"

function Get-BridgeRepoRoot {
    if (Get-Command git -ErrorAction SilentlyContinue) {
        $previousErrorAction = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        try {
            $root = (& git rev-parse --show-toplevel 2>$null)
            if ($LASTEXITCODE -eq 0 -and $root) {
                return $root.Trim()
            }
        }
        finally {
            $ErrorActionPreference = $previousErrorAction
        }
    }

    return (Get-Location).Path
}

function Resolve-BridgeActor {
    param(
        [string]$Argument = "",
        [string]$RepoRoot = ""
    )

    $valid = @("codex", "claude", "antigravity", "gemini", "unknown")

    if (-not [string]::IsNullOrWhiteSpace($Argument)) {
        $arg = $Argument.Trim().ToLowerInvariant()
        if ($valid -contains $arg) { return $arg }
        return "unknown"
    }

    if (-not [string]::IsNullOrWhiteSpace($env:GSS_BRIDGE_ACTOR)) {
        $envActor = $env:GSS_BRIDGE_ACTOR.Trim().ToLowerInvariant()
        if ($valid -contains $envActor) { return $envActor }
    }

    if (-not [string]::IsNullOrWhiteSpace($env:SPECKIT_BRIDGE_ACTOR)) {
        $envActor = $env:SPECKIT_BRIDGE_ACTOR.Trim().ToLowerInvariant()
        if ($valid -contains $envActor) { return $envActor }
    }

    return "unknown"
}
function Ensure-GraphifyIndex {
    param(
        [string]$RepoRoot = "",
        [switch]$Silent
    )

    if (-not $RepoRoot) { $RepoRoot = Get-BridgeRepoRoot }
    $graphPath = Join-Path $RepoRoot "graphify-out\graph.json"

    if (Test-Path -LiteralPath $graphPath -PathType Leaf) {
        return $graphPath
    }

    # Graphify index missing: execute initialization command instead of skipping
    $initialized = $false
    if (Get-Command "graphify" -ErrorAction SilentlyContinue) {
        if (-not $Silent) {
            Write-Output "[graphify] Notice: graphify-out/graph.json not found. Executing graphify to initialize knowledge graph..."
        }
        $prevDir = Get-Location
        try {
            Set-Location $RepoRoot
            & graphify . | Out-Null
            $initialized = $true
        } catch {
            Write-Verbose "Graphify initialization failed: $($_.Exception.Message)"
        } finally {
            Set-Location $prevDir
        }
    }

    if (-not $initialized -and -not (Test-Path -LiteralPath $graphPath -PathType Leaf)) {
        $pyCmd = $null
        foreach ($cmd in @("python", "python3", "py")) {
            if (Get-Command $cmd -ErrorAction SilentlyContinue) {
                & $cmd -c "import graphify" 2>$null
                if ($LASTEXITCODE -eq 0) {
                    $pyCmd = $cmd
                    break
                }
            }
        }
        if ($pyCmd) {
            if (-not $Silent) {
                Write-Output "[graphify] Notice: graphify-out/graph.json not found. Executing graphify to initialize knowledge graph..."
            }
            $prevDir = Get-Location
            try {
                Set-Location $RepoRoot
                & $pyCmd -m graphify . | Out-Null
            } catch {
                Write-Verbose "Graphify initialization failed: $($_.Exception.Message)"
            } finally {
                Set-Location $prevDir
            }
        }
    }

    return $graphPath
}
