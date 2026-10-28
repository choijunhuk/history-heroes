#!/bin/bash
# 사용: Tools/dev/qa.sh eval '<C#>'   — 열린 Unity 에디터에서 C# 한 줄 실행 (결과 글만)
#       Tools/dev/qa.sh cap <이름>    — Game 화면 찍기 (540x1170) → Game/Assets/Temp/qa/g_<이름>.png (git 제외)
source "$(dirname "$0")/_env.sh"
cd "$GAME" || exit 1
case "$1" in
  eval) $U --timeout 300 eval --format json "$2" 2>&1 | "$PY" -I "$ROOT/Tools/dev/eval_result.py" ;;
  cap)
    $U eval --format json 'UnityEditorInternal.InternalEditorUtility.RepaintAllViews(); return "ok";' >/dev/null 2>&1
    sleep 1
    $U capture game_view --width 540 --height 1170 --save_path "Temp/qa/g_$2.png" 2>&1 | tail -1 | cut -c1-60
    echo "-> Game/Assets/Temp/qa/g_$2.png" ;;
  *) echo "사용: qa.sh eval '<C#>' | qa.sh cap <이름>" ;;
esac
