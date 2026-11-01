#!/bin/bash
# 사용: Tools/dev/ucompile.sh — 새로고침 → 다시 컴파일 → 오류 보기. Play 중이면 먼저 멈출 것 (안 그러면 컴파일이 끝나지 않는다)
source "$(dirname "$0")/_env.sh"
cd "$GAME" || exit 1
$U console clear >/dev/null 2>&1
$U eval 'UnityEditor.AssetDatabase.Refresh(); return "ok";' >/dev/null 2>&1
$U script recompile >/dev/null 2>&1
for i in $(seq 1 40); do sleep 3; s=$($U script recompile_status 2>&1 | tail -1); case "$s" in *completed*|*up_to_date*) break;; esac; done
$U console status --format json 2>&1 | grep -o '"compilationFailed": [a-z]*'
$U console get --tail 30 --level error --format json 2>&1 | grep '"message"' | head -15
