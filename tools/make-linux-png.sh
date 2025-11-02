#!/usr/bin/env bash
set -euo pipefail

SRC_PNG="${1:-}"
if [[ -z "${SRC_PNG}" ]]; then
  echo "Usage: $0 /absolute/path/to/source.png"
  exit 1
fi
if [[ ! -f "${SRC_PNG}" ]]; then
  echo "Error: Source PNG not found: ${SRC_PNG}"
  exit 1
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
OUTPUT_PNG="${REPO_ROOT}/src/AGI.Kapster.Desktop/logo.png"

# Produce a 512x512 PNG for Linux desktop/pixmaps usage
sips -z 512 512 "$SRC_PNG" --out "$OUTPUT_PNG" >/dev/null 2>&1

if [[ -f "$OUTPUT_PNG" ]]; then
  echo "[OK] Generated: $OUTPUT_PNG"
else
  echo "[ERROR] Failed to generate 512px PNG"
  exit 1
fi


