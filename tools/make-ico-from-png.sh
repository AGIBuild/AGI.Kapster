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
TMP_DIR="$(mktemp -d)"
OUTPUT_ICO="${REPO_ROOT}/src/AGI.Kapster.Desktop/logo.ico"

sizes=(16 24 32 48 64 128 256)
for s in "${sizes[@]}"; do
  sips -z "$s" "$s" "$SRC_PNG" --out "${TMP_DIR}/${s}.png" >/dev/null 2>&1
done

if command -v magick >/dev/null 2>&1; then
  magick "${TMP_DIR}/16.png" "${TMP_DIR}/24.png" "${TMP_DIR}/32.png" "${TMP_DIR}/48.png" "${TMP_DIR}/64.png" "${TMP_DIR}/128.png" "${TMP_DIR}/256.png" "$OUTPUT_ICO"
elif command -v convert >/dev/null 2>&1; then
  convert "${TMP_DIR}/16.png" "${TMP_DIR}/24.png" "${TMP_DIR}/32.png" "${TMP_DIR}/48.png" "${TMP_DIR}/64.png" "${TMP_DIR}/128.png" "${TMP_DIR}/256.png" "$OUTPUT_ICO"
else
  echo "Error: ImageMagick not found (need 'magick' or 'convert'). Please install ImageMagick."
  exit 1
fi

rm -rf "$TMP_DIR"

if [[ -f "$OUTPUT_ICO" ]]; then
  echo "[OK] Generated: $OUTPUT_ICO"
else
  echo "[ERROR] Failed to generate .ico"
  exit 1
fi
