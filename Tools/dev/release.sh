#!/bin/bash
# 사용: Tools/dev/release.sh <버전> ["바뀐 점"]     예) Tools/dev/release.sh 1.0.0-beta.2 "보스 밸런스 조정"
#   열린 Unity 에디터로 출시용 APK를 만들고 GitHub 릴리스(v<버전>)에 HistoryHeroes.apk로 올린다.
#   README의 내려받기 링크(releases/latest/download/HistoryHeroes.apk)는 늘 가장 최근 릴리스를 가리킨다.
#   버전 번호(versionCode) = 커밋 수 (늘 커져야 폰에서 업데이트가 된다).
#   서명 키: ~/.history-heroes/signing.properties (없으면 디버그 키 — 다른 컴퓨터 APK와 업데이트 호환 안 됨)
#   APK만 만들고 올리지 않으려면: Tools/dev/release.sh <버전> --no-upload
#   구글 플레이용 AAB (Play Console에 직접 올림):  Tools/dev/release.sh <버전> --aab
set -e
source "$(dirname "$0")/_env.sh"
VERSION="$1"
NOTES="$2"
[ -z "$VERSION" ] && { echo "사용: release.sh <버전> [\"바뀐 점\" | --no-upload | --aab]"; exit 1; }
AAB=false; [ "$NOTES" = "--aab" ] && AAB=true
unity status 2>/dev/null | grep -q ready || { echo "Unity 에디터를 먼저 열 것 (Game/ 프로젝트)"; exit 1; }

cd "$ROOT"
[ -n "$(git status --porcelain -- Game/Assets/Scripts Game/Assets/Editor Game/ProjectSettings)" ] && echo "⚠ 커밋 안 된 코드 · 설정이 있다 (APK에는 들어가지만 태그 커밋에는 없다)"
CODE=$(git rev-list --count HEAD)
RESULT="$GAME/Builds/Android/last-build.txt"
APK="$GAME/Builds/Android/HistoryHeroes.apk"
rm -f "$RESULT"

cd "$GAME"
echo "빌드 시작: $VERSION ($CODE) — 10~30분 걸린다"
# eval 안에서 바로 빌드한다 (맥은 에디터가 뒤에 있으면 delayCall이 돌지 않는다). 연결이 먼저 끊겨도 빌드는 계속되니 결과 파일을 기다린다
$U --timeout 3600 eval "return HistoryHeroes.EditorTools.AndroidRelease.Build(\"$VERSION\", $CODE, $AAB);" >/dev/null 2>&1 || true # Play 중이면 Build가 "실패"를 남긴다
until [ -f "$RESULT" ]; do sleep 15; done
cat "$RESULT"; echo
grep -q "^성공" "$RESULT" || exit 1
$AAB && { echo "Play Console → 테스트 → 새 버전 만들기 → $GAME/Builds/Android/HistoryHeroes.aab 올리기"; exit 0; }

# 구글 플레이 16KB 메모리 페이지 정렬 확인 (안드로이드 15+ 요구)
ZIPALIGN=$(ls -d /Applications/Unity/Hub/Editor/*/PlaybackEngines/AndroidPlayer/SDK/build-tools/*/zipalign "/c/Program Files/Unity/Hub/Editor/"*/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/build-tools/*/zipalign.exe 2>/dev/null | tail -1)
[ -n "$ZIPALIGN" ] && { "$ZIPALIGN" -c -P 16 4 "$APK" >/dev/null 2>&1 && echo "16KB 정렬 ✅" || echo "⚠ 16KB 정렬 아님 (구글 플레이 거절 가능)"; }

[ "$NOTES" = "--no-upload" ] && { echo "올리지 않음: $APK"; exit 0; }
cd "$ROOT"
git push origin HEAD
gh release create "v$VERSION" "$APK" --target "$(git rev-parse HEAD)" --title "History Heroes $VERSION" --notes "$(cat <<EOF
${NOTES:-테스트판}

## 📱 설치
1. 아래 **HistoryHeroes.apk**를 폰에서 받는다.
2. 받은 파일을 열고, "출처를 알 수 없는 앱" 설치를 허용한다.
3. 이미 깔려 있으면 덮어 설치(업데이트)된다. 저장도 그대로.

- 안드로이드 7.1 이상 · 64비트 폰
- 구글 플레이 판이 나오면 서명이 달라서, 이 APK에서 플레이 판으로 갈 때는 지우고 다시 깔아야 한다.

버전 $VERSION (빌드 $CODE) · 커밋 $(git rev-parse --short HEAD)
EOF
)"
echo "릴리스: https://github.com/choijunhuk/history-heroes/releases/tag/v$VERSION"
