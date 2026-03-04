#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$SCRIPT_DIR"

INSTANCES_FILE="${1:-instances.json}"
WHATIF="${WHATIF:-false}"

resolve_from_root() {
    local base_dir="$1"
    local path_value="$2"

    if [ -z "$path_value" ]; then
        echo ""
        return
    fi

    if [[ "$path_value" = /* ]]; then
        echo "$path_value"
    else
        echo "$base_dir/$path_value"
    fi
}

is_enabled() {
    local val="$1"
    val="$(echo "$val" | tr '[:upper:]' '[:lower:]' | xargs)"
    case "$val" in
        true|1|yes|y|on|enabled) return 0 ;;
        *) return 1 ;;
    esac
}

resolve_anchor_filename() {
    local val="$1"
    if [ -z "$val" ]; then
        echo "anchors"
        return
    fi
    local leaf
    leaf="$(basename "$val")"
    if [ -z "$leaf" ]; then
        echo "anchors"
    else
        echo "$leaf"
    fi
}

# Locate instances file
INSTANCES_PATH="$(resolve_from_root "$REPO_ROOT" "$INSTANCES_FILE")"
if [ ! -f "$INSTANCES_PATH" ]; then
    LEGACY_PATH="$(resolve_from_root "$REPO_ROOT" "scripts/instances.json")"
    if [ "$INSTANCES_FILE" = "instances.json" ] && [ -f "$LEGACY_PATH" ]; then
        echo "WARNING: instances.json not found at repo root. Falling back to legacy path: $LEGACY_PATH"
        INSTANCES_PATH="$LEGACY_PATH"
    else
        echo "ERROR: Instances file not found: $INSTANCES_PATH" >&2
        exit 1
    fi
fi

# Parse runtime config
RUNTIME_MODE="$(jq -r '.runtime.mode // empty' "$INSTANCES_PATH")"
RUNTIME_TARGET_RAW="$(jq -r '.runtime.target // empty' "$INSTANCES_PATH")"
RUNTIME_WORKDIR_RAW="$(jq -r '.runtime.workingDirectory // empty' "$INSTANCES_PATH")"

if [ -z "$RUNTIME_MODE" ] || [ -z "$RUNTIME_TARGET_RAW" ]; then
    echo "ERROR: Invalid instances.json: runtime.mode and runtime.target are required." >&2
    exit 1
fi

RUNTIME_TARGET="$(resolve_from_root "$REPO_ROOT" "$RUNTIME_TARGET_RAW")"
RUNTIME_WORKDIR="$(resolve_from_root "$REPO_ROOT" "$RUNTIME_WORKDIR_RAW")"
if [ -z "$RUNTIME_WORKDIR" ]; then
    RUNTIME_WORKDIR="$REPO_ROOT"
fi

RUNTIME_MODE="$(echo "$RUNTIME_MODE" | tr '[:upper:]' '[:lower:]')"

if [ "$RUNTIME_MODE" = "exe" ]; then
    if [ ! -f "$RUNTIME_TARGET" ]; then
        echo "ERROR: Runtime exe not found: $RUNTIME_TARGET" >&2
        exit 1
    fi
    chmod +x "$RUNTIME_TARGET"
elif [ "$RUNTIME_MODE" = "dotnet" ]; then
    if ! command -v dotnet &>/dev/null; then
        echo "ERROR: dotnet is not available in PATH." >&2
        exit 1
    fi
    if [ ! -f "$RUNTIME_TARGET" ]; then
        echo "ERROR: Runtime dll not found: $RUNTIME_TARGET" >&2
        exit 1
    fi
else
    echo "ERROR: Unsupported runtime.mode '$RUNTIME_MODE'. Use 'dotnet' or 'exe'." >&2
    exit 1
fi

# Parse instances
INSTANCE_COUNT="$(jq '.instances | length' "$INSTANCES_PATH")"
if [ "$INSTANCE_COUNT" -eq 0 ]; then
    echo "No instances were defined in $INSTANCES_PATH"
    exit 0
fi

ENABLED_COUNT=0
DISABLED_NAMES=""

for i in $(seq 0 $((INSTANCE_COUNT - 1))); do
    ENABLED_RAW="$(jq -r ".instances[$i].enabled // \"false\"" "$INSTANCES_PATH")"
    NAME="$(jq -r ".instances[$i].name // \"unnamed\"" "$INSTANCES_PATH")"

    if ! is_enabled "$ENABLED_RAW"; then
        if [ -n "$DISABLED_NAMES" ]; then
            DISABLED_NAMES="$DISABLED_NAMES, "
        fi
        DISABLED_NAMES="$DISABLED_NAMES$NAME"
        continue
    fi

    ENABLED_COUNT=$((ENABLED_COUNT + 1))

    INST_WORKDIR_RAW="$(jq -r ".instances[$i].workingDirectory // empty" "$INSTANCES_PATH")"
    INST_WORKDIR="$(resolve_from_root "$REPO_ROOT" "$INST_WORKDIR_RAW")"
    if [ -z "$INST_WORKDIR" ]; then
        INST_WORKDIR="$RUNTIME_WORKDIR"
    fi

    mkdir -p "$INST_WORKDIR"

    CONFIG="$(jq -r ".instances[$i].config // empty" "$INSTANCES_PATH")"
    TWITCH="$(jq -r ".instances[$i].twitch // empty" "$INSTANCES_PATH")"
    SERVER="$(jq -r ".instances[$i].server // empty" "$INSTANCES_PATH")"
    EXTRA="$(jq -r ".instances[$i].extra // empty" "$INSTANCES_PATH")"
    GITHUB="$(jq -r ".instances[$i].github // empty" "$INSTANCES_PATH")"

    CONFIG_PATH="$(resolve_from_root "$INST_WORKDIR" "${CONFIG:-config.json}")"
    TWITCH_PATH="$(resolve_from_root "$INST_WORKDIR" "${TWITCH:-twitch.json}")"
    SERVER_PATH="$(resolve_from_root "$INST_WORKDIR" "${SERVER:-server.json}")"
    EXTRA_PATH="$(resolve_from_root "$INST_WORKDIR" "${EXTRA:-extraconfig.json}")"
    GITHUB_PATH="$(resolve_from_root "$INST_WORKDIR" "${GITHUB:-github.json}")"

    # Anchor handling
    ANCHOR_TEMPLATE_RAW="$(jq -r ".instances[$i].anchorTemplate // empty" "$INSTANCES_PATH")"
    ANCHOR_FILENAME_RAW="$(jq -r ".instances[$i].anchorFilename // empty" "$INSTANCES_PATH")"
    ANCHOR_FILENAME="$(resolve_anchor_filename "$ANCHOR_FILENAME_RAW")"

    if [ -n "$ANCHOR_TEMPLATE_RAW" ]; then
        ANCHOR_TEMPLATE="$(resolve_from_root "$REPO_ROOT" "$ANCHOR_TEMPLATE_RAW")"
        ANCHOR_DEST="$INST_WORKDIR/$ANCHOR_FILENAME"
        OVERWRITE_RAW="$(jq -r ".instances[$i].overwriteAnchors // \"false\"" "$INSTANCES_PATH")"

        if [ -f "$ANCHOR_TEMPLATE" ]; then
            if [ ! -f "$ANCHOR_DEST" ] || is_enabled "$OVERWRITE_RAW"; then
                cp "$ANCHOR_TEMPLATE" "$ANCHOR_DEST"
                echo "[$NAME] imported anchors template -> $ANCHOR_DEST"
            else
                echo "[$NAME] $ANCHOR_FILENAME already exists; skipping template import"
            fi
        else
            echo "WARNING: [$NAME] anchors template not found: $ANCHOR_TEMPLATE"
        fi
    fi

    # Build args
    ARGS=()
    if [ "$RUNTIME_MODE" = "dotnet" ]; then
        CMD="dotnet"
        ARGS+=("$RUNTIME_TARGET")
    else
        CMD="$RUNTIME_TARGET"
    fi

    ARGS+=("$CONFIG_PATH" "$TWITCH_PATH" "$SERVER_PATH" "$EXTRA_PATH" "$GITHUB_PATH")

    echo "[$NAME] wd=$INST_WORKDIR"
    echo "[$NAME] $CMD ${ARGS[*]}"

    if [ "$WHATIF" != "true" ]; then
        cd "$INST_WORKDIR"
        "$CMD" "${ARGS[@]}" &
        echo "[$NAME] started pid=$!"
        cd "$REPO_ROOT"
    fi
done

if [ "$ENABLED_COUNT" -eq 0 ]; then
    echo "No enabled instances found in $INSTANCES_PATH"
    exit 0
fi

echo "Started $ENABLED_COUNT instance(s)"
if [ -n "$DISABLED_NAMES" ]; then
    echo "Skipped disabled instance(s): $DISABLED_NAMES"
fi

# Wait for all background processes
wait
