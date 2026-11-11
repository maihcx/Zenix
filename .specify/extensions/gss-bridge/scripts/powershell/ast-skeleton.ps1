# ast-skeleton.ps1 — Extract AST structural skeletons to reduce prompt tokens for subagents.
param(
    [Parameter(Mandatory = $true)]
    [string]$Path,
    [switch]$Json
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
. (Join-Path $ScriptDir "common-actor-resolution.ps1")

$repoRoot = Get-BridgeRepoRoot
$targetPath = if ([System.IO.Path]::IsPathRooted($Path)) { $Path } else { Join-Path $repoRoot $Path }

if (-not (Test-Path -LiteralPath $targetPath)) {
    Write-Error "File not found: '$targetPath'"
    exit 2
}

$pythonCmd = $null
foreach ($cmd in @("python", "python3", "py")) {
    if (Get-Command $cmd -ErrorAction SilentlyContinue) {
        $pythonCmd = $cmd
        break
    }
}

if (-not $pythonCmd) {
    Write-Error "Python 3 is required for AST skeleton extraction."
    exit 2
}

$pyExtractor = @'
import sys, os, ast, json

path = sys.argv[1]
with open(path, "r", encoding="utf-8-sig", errors="replace") as f:
    source = f.read()

ext = os.path.splitext(path)[1].lower()

def extract_py_skeleton(code):
    try:
        tree = ast.parse(code)
    except Exception as e:
        return f"# [Parse error: {e}]\n" + "\n".join([line for line in code.splitlines() if line.startswith("def ") or line.startswith("class ")])

    lines = []
    doc = ast.get_docstring(tree)
    if doc:
        lines.append(f'"""{doc}"""\n')

    for node in tree.body:
        if isinstance(node, (ast.Import, ast.ImportFrom)):
            lines.append(ast.get_source_segment(code, node))
        elif isinstance(node, ast.ClassDef):
            decorator_lines = [f"@{ast.unparse(d)}" for d in node.decorator_list]
            bases = ", ".join([ast.unparse(b) for b in node.bases])
            base_str = f"({bases})" if bases else ""
            lines.extend(decorator_lines)
            lines.append(f"class {node.name}{base_str}:")
            cdoc = ast.get_docstring(node)
            if cdoc:
                lines.append(f'    """{cdoc}"""')
            has_methods = False
            for item in node.body:
                if isinstance(item, (ast.FunctionDef, ast.AsyncFunctionDef)):
                    has_methods = True
                    fn_deco = [f"    @{ast.unparse(d)}" for d in item.decorator_list]
                    lines.extend(fn_deco)
                    prefix = "async def" if isinstance(item, ast.AsyncFunctionDef) else "def"
                    ret = f" -> {ast.unparse(item.returns)}" if item.returns else ""
                    args = ast.unparse(item.args)
                    lines.append(f"    {prefix} {item.name}({args}){ret}:")
                    fdoc = ast.get_docstring(item)
                    if fdoc:
                        lines.append(f'        """{fdoc}"""')
                    lines.append("        ...")
            if not has_methods:
                lines.append("    ...")
            lines.append("")
        elif isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef)):
            decorator_lines = [f"@{ast.unparse(d)}" for d in node.decorator_list]
            lines.extend(decorator_lines)
            prefix = "async def" if isinstance(node, ast.AsyncFunctionDef) else "def"
            ret = f" -> {ast.unparse(node.returns)}" if node.returns else ""
            args = ast.unparse(node.args)
            lines.append(f"{prefix} {node.name}({args}){ret}:")
            fdoc = ast.get_docstring(node)
            if fdoc:
                lines.append(f'    """{fdoc}"""')
            lines.append("    ...")
            lines.append("")
    return "\n".join(lines).strip()

def extract_heuristic_skeleton(code):
    lines = []
    for line in code.splitlines():
        trimmed = line.strip()
        if trimmed.startswith(("export ", "function ", "class ", "interface ", "type ", "def ", "pub fn ", "public class ", "function")):
            lines.append(line.rstrip("{ ;") + " { ... }")
        elif trimmed.startswith(("import ", "from ", "require(", "#include", "using ")):
            lines.append(line)
    return "\n".join(lines).strip()

if ext == ".py":
    skeleton = extract_py_skeleton(source)
else:
    skeleton = extract_heuristic_skeleton(source)

orig_chars = len(source)
orig_lines = len(source.splitlines())
skel_chars = len(skeleton)
skel_lines = len(skeleton.splitlines())

savings_pct = round((1.0 - (skel_chars / max(orig_chars, 1))) * 100, 1)

res = {
    "file": path,
    "original_lines": orig_lines,
    "skeleton_lines": skel_lines,
    "original_chars": orig_chars,
    "skeleton_chars": skel_chars,
    "savings_pct": savings_pct,
    "skeleton": skeleton
}
print(json.dumps(res))
'@

$tempPy = [System.IO.Path]::GetTempFileName() + ".py"
[System.IO.File]::WriteAllText($tempPy, $pyExtractor, [System.Text.Encoding]::UTF8)

try {
    $raw = & $pythonCmd $tempPy $targetPath | Out-String
    if ($LASTEXITCODE -ne 0) {
        Write-Error "AST skeleton extraction failed."
        exit 1
    }

    if ($Json) {
        $raw.Trim()
    } else {
        $obj = $raw | ConvertFrom-Json
        Write-Host "=================================================================" -ForegroundColor Cyan
        Write-Host "GSS-BRIDGE AST SKELETON CONTEXT PRUNER" -ForegroundColor Cyan
        Write-Host "================================================================="
        Write-Host "File:           $($obj.file)"
        Write-Host "Original Size:  $($obj.original_lines) lines ($($obj.original_chars) chars)"
        Write-Host "Skeleton Size:  $($obj.skeleton_lines) lines ($($obj.skeleton_chars) chars)"
        Write-Host "Token Savings:  $($obj.savings_pct)%" -ForegroundColor Green
        Write-Host "-----------------------------------------------------------------"
        Write-Host $obj.skeleton
        Write-Host "================================================================="
    }
} finally {
    Remove-Item -Force -LiteralPath $tempPy -ErrorAction SilentlyContinue
}
