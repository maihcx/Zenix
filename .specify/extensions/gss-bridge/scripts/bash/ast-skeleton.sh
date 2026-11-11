#!/usr/bin/env bash
# ast-skeleton.sh — Extract AST structural skeletons to reduce prompt tokens for subagents.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/common-actor-resolution.sh"

TARGET_FILE=""
OUTPUT_JSON=0

while [ $# -gt 0 ]; do
    case "$1" in
        --path)
            TARGET_FILE="$2"
            shift 2
            ;;
        --json)
            OUTPUT_JSON=1
            shift
            ;;
        *)
            shift
            ;;
    esac
done

if [ -z "$TARGET_FILE" ]; then
    echo "Error: --path is required" >&2
    exit 2
fi

REPO_ROOT="$(get_bridge_repo_root)"
if [[ "$TARGET_FILE" = /* ]] || [[ "$TARGET_FILE" =~ ^[A-Za-z]: ]]; then
    FULL_PATH="$TARGET_FILE"
else
    FULL_PATH="$REPO_ROOT/$TARGET_FILE"
fi

if [ ! -f "$FULL_PATH" ]; then
    echo "Error: File not found: '$FULL_PATH'" >&2
    exit 2
fi

PYTHON_CMD=""
for cmd in python3 python py; do
    if command -v "$cmd" >/dev/null 2>&1 && "$cmd" -c "import sys" >/dev/null 2>&1; then
        PYTHON_CMD="$cmd"
        break
    fi
done

if [ -z "$PYTHON_CMD" ]; then
    echo "Error: Python 3 runtime is required for ast-skeleton.sh" >&2
    exit 2
fi

RAW_RESULT="$($PYTHON_CMD - "$FULL_PATH" <<'PYEOF'
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
PYEOF
)"

if [ "$OUTPUT_JSON" -eq 1 ]; then
    echo "$RAW_RESULT"
else
    echo "================================================================="
    echo "GSS-BRIDGE AST SKELETON CONTEXT PRUNER"
    echo "================================================================="
    echo "$RAW_RESULT" | $PYTHON_CMD -c '
import sys, json
d = json.load(sys.stdin)
print(f"File:           {d.get(\"file\")}")
print(f"Original Size:  {d.get(\"original_lines\")} lines ({d.get(\"original_chars\")} chars)")
print(f"Skeleton Size:  {d.get(\"skeleton_lines\")} lines ({d.get(\"skeleton_chars\")} chars)")
print(f"Token Savings:  {d.get(\"savings_pct\")}%")
print("-----------------------------------------------------------------")
print(d.get("skeleton", ""))
'
    echo "================================================================="
fi
exit 0
