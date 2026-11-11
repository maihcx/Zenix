#!/usr/bin/env bash
# synthesize-repro.sh — Synthesize minimal failing repro test harness for GSS-Bridge bug triage.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
. "$SCRIPT_DIR/common-actor-resolution.sh"

SLUG=""
SUSPECT_NODE=""
SYMPTOM=""
OUTPUT_JSON=0

while [ $# -gt 0 ]; do
    case "$1" in
        --slug)
            SLUG="$2"
            shift 2
            ;;
        --suspect-node)
            SUSPECT_NODE="$2"
            shift 2
            ;;
        --symptom)
            SYMPTOM="$2"
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

if [ -z "$SLUG" ]; then
    echo "Error: --slug is required" >&2
    exit 2
fi

REPO_ROOT="$(get_bridge_repo_root)"
TESTS_DIR="$REPO_ROOT/tests"
REPRO_DIR="$TESTS_DIR/repro"
mkdir -p "$REPRO_DIR"

EXT="sh"
FRAMEWORK="bash"

if [ -f "$REPO_ROOT/pyproject.toml" ] || [ -f "$REPO_ROOT/setup.py" ]; then
    EXT="py"
    FRAMEWORK="pytest"
elif [ -f "$REPO_ROOT/package.json" ]; then
    EXT="js"
    FRAMEWORK="jest"
fi

TEST_FILE="test_${SLUG}__repro.$EXT"
TEST_PATH="$REPRO_DIR/$TEST_FILE"
REL_PATH="tests/repro/$TEST_FILE"

if [ "$EXT" = "py" ]; then
    cat <<EOF > "$TEST_PATH"
# Auto-generated Superpowers TDD reproduction test for bug: $SLUG
# Suspect Component: ${SUSPECT_NODE:-Unknown}
# Symptom: $SYMPTOM

import unittest

class TestBug_${SLUG}(unittest.TestCase):
    def test_reproduce_symptom(self):
        # RED: This test reproduces the reported defect and must fail initially.
        symptom_verified = False
        self.assertTrue(symptom_verified, "Reproduction failed: $SYMPTOM")

if __name__ == '__main__':
    unittest.main()
EOF
elif [ "$EXT" = "js" ]; then
    cat <<EOF > "$TEST_PATH"
// Auto-generated Superpowers TDD reproduction test for bug: $SLUG
// Suspect Component: ${SUSPECT_NODE:-Unknown}
// Symptom: $SYMPTOM

describe('Bug Repro: $SLUG', () => {
    test('reproduce reported symptom', () => {
        // RED: This test reproduces the reported defect and must fail initially.
        const symptomVerified = false;
        expect(symptomVerified).toBe(true);
    });
});
EOF
else
    cat <<EOF > "$TEST_PATH"
#!/usr/bin/env bash
# Auto-generated Superpowers TDD reproduction test for bug: $SLUG
# Suspect Component: ${SUSPECT_NODE:-Unknown}
# Symptom: $SYMPTOM

set -euo pipefail

echo "[repro] Running reproduction test for $SLUG..."
SYMPTOM_VERIFIED=0

if [ "\$SYMPTOM_VERIFIED" -ne 1 ]; then
    echo "RED Phase Assertion Failed: $SYMPTOM" >&2
    exit 1
fi

echo "GREEN: Bug is resolved."
exit 0
EOF
    chmod +x "$TEST_PATH"
fi

if [ "$OUTPUT_JSON" -eq 1 ]; then
    printf '{"status":"synthesized","slug":"%s","suspect_node":"%s","framework":"%s","repro_test_file":"%s","absolute_path":"%s"}\n' \
        "$SLUG" "$SUSPECT_NODE" "$FRAMEWORK" "$REL_PATH" "$TEST_PATH"
else
    echo "================================================================="
    echo "GSS-BRIDGE REPRODUCTION TEST HARNESS SYNTHESIZER"
    echo "================================================================="
    echo "Bug Slug:        $SLUG"
    echo "Framework:       $FRAMEWORK"
    echo "Repro Test File: $REL_PATH"
    echo "TDD State:       RED (Failing test ready for execution)"
    echo "================================================================="
fi

exit 0
