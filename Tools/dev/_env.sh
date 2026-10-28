#!/bin/bash
# 공용 설정: 저장소 위치 · unity 명령 · 파이썬 (Git Bash에서 쓴다, 어느 컴퓨터든)
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
GAME="$ROOT/Game"
if [ -n "$LOCALAPPDATA" ] && command -v cygpath >/dev/null 2>&1; then
  export PATH="$PATH:$(cygpath "$LOCALAPPDATA")/Microsoft/WindowsApps" # winget으로 깐 unity CLI
fi
export UNITY_NO_PAGER=1
U="unity command --caller plugin --skill unity-cli"
PY="${PYTHON:-$(command -v python || command -v py || echo python)}"
