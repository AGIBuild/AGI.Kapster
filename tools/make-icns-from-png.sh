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
ICONSET_DIR="${REPO_ROOT}/branding/icons/Kapster.iconset"
OUTPUT_ICNS="${REPO_ROOT}/src/AGI.Kapster.Desktop/logo.icns"

mkdir -p "${ICONSET_DIR}"

make_size() {
  local size="$1"; local target="$2";
  sips -z "${size}" "${size}" "${SRC_PNG}" --out "${ICONSET_DIR}/${target}" >/dev/null 2>&1
}

# Base + @2x assets
make_size 16  icon_16x16.png
make_size 32  icon_16x16@2x.png
make_size 32  icon_32x32.png
make_size 64  icon_32x32@2x.png
make_size 128 icon_128x128.png
make_size 256 icon_128x128@2x.png
make_size 256 icon_256x256.png
make_size 512 icon_256x256@2x.png
make_size 512 icon_512x512.png
make_size 1024 icon_512x512@2x.png

# Build .icns
if ! command -v iconutil >/dev/null 2>&1; then
  echo "Error: 'iconutil' not found. Please run on macOS with Xcode command line tools installed."
  exit 1
fi

iconutil -c icns "${ICONSET_DIR}" -o "${OUTPUT_ICNS}"

if [[ -f "${OUTPUT_ICNS}" ]]; then
  echo "[OK] Generated: ${OUTPUT_ICNS}"
else
  echo "[ERROR] Failed to generate .icns"
  exit 1
fi
