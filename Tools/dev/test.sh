#!/bin/bash
# 사용: Tools/dev/test.sh — EditMode 테스트 전부 (비동기로 돌리고 끝날 때까지 기다린 뒤 요약 · 실패만 보여 준다)
source "$(dirname "$0")/_env.sh"
cd "$GAME" || exit 1
rm -f Temp/pipeline_test_status.json
$U test run --mode EditMode --async_tests true >/dev/null 2>&1
for i in $(seq 1 120); do sleep 5; [ -f Temp/pipeline_test_status.json ] && grep -q '"status": "completed"' Temp/pipeline_test_status.json && break; done
"$PY" -I -c "
import json
d = json.load(open('Temp/pipeline_test_status.json', encoding='utf-8'))
print(d['summary'])
for r in d['results']:
    if r['Status'] != 'Passed': print(r['FullName'], (r['Message'] or '')[:300])
"
