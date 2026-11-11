# synthesize-repro.ps1 — Synthesize minimal failing repro test harness for GSS-Bridge bug triage.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Slug,
    [string]$SuspectNode = "",
    [string]$Symptom = "",
    [switch]$Json
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptDir 'common-actor-resolution.ps1')

$RepoRoot = Get-BridgeRepoRoot
$testsDir = Join-Path $RepoRoot "tests"
$reproDir = Join-Path $testsDir "repro"
if (-not (Test-Path -LiteralPath $reproDir)) {
    New-Item -ItemType Directory -Force -Path $reproDir | Out-Null
}

$hasPy = (Test-Path -LiteralPath (Join-Path $RepoRoot "pyproject.toml")) -or (Test-Path -LiteralPath (Join-Path $RepoRoot "setup.py"))
$hasJs = (Test-Path -LiteralPath (Join-Path $RepoRoot "package.json"))

$ext = "ps1"
$framework = "powershell"

if ($hasPy) {
    $ext = "py"
    $framework = "pytest"
} elseif ($hasJs) {
    $ext = "js"
    $framework = "jest"
} else {
    $ext = "ps1"
    $framework = "pester"
}

$testFileName = "test_${Slug}__repro.$ext"
$testFilePath = Join-Path $reproDir $testFileName
$relPath = "tests/repro/$testFileName"

$content = ""
if ($ext -eq "py") {
    $content = @"
# Auto-generated Superpowers TDD reproduction test for bug: $Slug
# Suspect Component: $(if ($SuspectNode) { $SuspectNode } else { "Unknown" })
# Symptom: $Symptom

import unittest

class TestBug_${Slug}(unittest.TestCase):
    def test_reproduce_symptom(self):
        # RED: This test reproduces the reported defect and must fail initially.
        symptom_verified = False
        self.assertTrue(symptom_verified, "Reproduction failed: $Symptom")

if __name__ == '__main__':
    unittest.main()
"@
} elseif ($ext -eq "js") {
    $content = @"
// Auto-generated Superpowers TDD reproduction test for bug: $Slug
// Suspect Component: $(if ($SuspectNode) { $SuspectNode } else { "Unknown" })
// Symptom: $Symptom

describe('Bug Repro: $Slug', () => {
    test('reproduce reported symptom', () => {
        // RED: This test reproduces the reported defect and must fail initially.
        const symptomVerified = false;
        expect(symptomVerified).toBe(true);
    });
});
"@
} else {
    $content = @"
# Auto-generated Superpowers TDD reproduction test for bug: $Slug
# Suspect Component: $(if ($SuspectNode) { $SuspectNode } else { "Unknown" })
# Symptom: $Symptom

`$ErrorActionPreference = "Stop"

Write-Output "[repro] Running reproduction test for $Slug..."
`$symptomVerified = `$false

if (-not `$symptomVerified) {
    Write-Error "RED Phase Assertion Failed: $Symptom"
    exit 1
}

Write-Output "GREEN: Bug is resolved."
exit 0
"@
}

[System.IO.File]::WriteAllText($testFilePath, $content, [System.Text.Encoding]::UTF8)

$result = [ordered]@{
    status = "synthesized"
    slug = $Slug
    suspect_node = $SuspectNode
    framework = $framework
    repro_test_file = $relPath
    absolute_path = $testFilePath
}

if ($Json) {
    $result | ConvertTo-Json -Compress
} else {
    Write-Output "================================================================="
    Write-Output "GSS-BRIDGE REPRODUCTION TEST HARNESS SYNTHESIZER"
    Write-Output "================================================================="
    Write-Output "Bug Slug:        $Slug"
    Write-Output "Framework:       $framework"
    Write-Output "Repro Test File: $relPath"
    Write-Output "TDD State:       RED (Failing test ready for execution)"
    Write-Output "================================================================="
}
exit 0
