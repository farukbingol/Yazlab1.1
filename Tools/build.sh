#!/usr/bin/env bash
# Yazlab build betiği: Linux ve/veya Windows build'ini Unity batchmode ile alır.
#   Kullanım : ./Tools/build.sh [linux|windows|all]      (varsayılan: all)
#   Çıktı    : Builds/Linux/Yazlab.x86_64, Builds/Windows/Yazlab.exe
#   Loglar   : Logs/build_linux.log, Logs/build_windows.log
# Önce Unity Editor'ü kapat: aynı proje açıkken batchmode çalışmaz (proje kilidi).
# Unity farklı yerdeyse: UNITY_PATH=/yol/Unity ./Tools/build.sh
set -euo pipefail

PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
VERSION="6000.0.42f1"
TARGET="${1:-all}"
LOG_DIR="$PROJECT/Logs"
mkdir -p "$LOG_DIR"

# Unity'yi bul: UNITY_PATH > Unity Hub flatpak > Hub'ın varsayılan kurulum yeri.
EDITOR_BIN="$HOME/Unity/Hub/Editor/$VERSION/Editor/Unity"
if [[ -n "${UNITY_PATH:-}" ]]; then
    RUN=("$UNITY_PATH")
elif command -v flatpak >/dev/null && flatpak info com.unity.UnityHub >/dev/null 2>&1; then
    RUN=(flatpak run "--command=$EDITOR_BIN" com.unity.UnityHub)
elif [[ -x "$EDITOR_BIN" ]]; then
    RUN=("$EDITOR_BIN")
else
    echo "✗ Unity $VERSION bulunamadı. UNITY_PATH=/yol/Unity ile belirt." >&2
    exit 1
fi

if pgrep -if -- "-projectpath $PROJECT" >/dev/null; then
    echo "✗ Unity Editor bu projeyle açık görünüyor. Kapatıp tekrar çalıştır." >&2
    exit 1
fi

build() {
    local name="$1" method="$2" log="$LOG_DIR/build_${1,,}.log"
    echo "▶ $name build alınıyor... (log: ${log#$PROJECT/})"
    local start=$SECONDS
    "${RUN[@]}" -batchmode -quit -projectPath "$PROJECT" \
        -executeMethod "Yazlab.EditorTools.BuildTools.$method" -logFile "$log" || true

    if grep -a -q "\[Build\] $name: Succeeded" "$log"; then
        echo "✓ $(grep -a "\[Build\] $name:" "$log" | tail -1)  ($((SECONDS - start)) sn)"
    else
        echo "✗ $name build başarısız. Son satırlar:" >&2
        grep -a -E "\[Build\]|error CS|Error building|BuildFailedException" "$log" | tail -15 >&2 || tail -20 "$log" >&2
        return 1
    fi
}

status=0
case "$TARGET" in
    linux)   build Linux BuildLinux || status=1 ;;
    windows) build Windows BuildWindows || status=1 ;;
    all)     build Linux BuildLinux || status=1
             build Windows BuildWindows || status=1 ;;
    *)       echo "Kullanım: $0 [linux|windows|all]" >&2; exit 2 ;;
esac

exit $status
