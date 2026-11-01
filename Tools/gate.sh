#!/bin/bash
# 머지 게이트: main에 합치기 전에 통과해야 한다 (~/.claude/hooks/merge-gate.sh가 git merge 때 부른다).
#   1) 비밀 파일 · 비밀 값이 저장소에 없다 (공개 저장소)
#   2) EditMode 테스트 전부 통과 (Unity 에디터가 Game/을 열고 있어야 한다)
# 실패하면 코드를 고친다. 게이트를 고쳐서 통과시키지 않는다.
cd "$(dirname "$0")/.." || exit 1

bad=$(git ls-files | grep -E '\.(keystore|jks|p12)$|(signing|kakao)\.properties$|service-account.*\.json$' || true)
[ -n "$bad" ] && { echo "❌ 비밀 파일이 저장소에 있다: $bad"; exit 1; }
git grep -q -E -- '-----BEGIN (RSA |EC )?PRIVATE KEY-----' && { echo "❌ 개인 키(PEM)가 저장소에 있다"; exit 1; }
sa="$HOME/.history-heroes/google-play-service-account.json" # 서버 결제 검증용 구글 서비스 계정 (Unity Secrets에도 있음)
if [ -f "$sa" ]; then
  kid=$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["private_key_id"])' "$sa" 2>/dev/null)
  [ -n "$kid" ] && git grep -q -F -- "$kid" && { echo "❌ 구글 서비스 계정 키가 저장소에 있다"; exit 1; }
fi

# ~/.history-heroes의 비밀 값 (서명 비밀번호 · 카카오 앱 키)이 추적 파일에 들어갔는지
for f in "$HOME/.history-heroes/signing.properties" "$HOME/.history-heroes/kakao.properties"; do
  [ -f "$f" ] || continue
  while IFS='=' read -r k v; do
    case "$k" in storePass|keyPass|nativeAppKey) ;; *) continue ;; esac
    [ -n "$v" ] && git grep -q -F -- "$v" && { echo "❌ 비밀 값($k)이 저장소 파일에 있다"; exit 1; }
  done < "$f"
done
echo "✅ 비밀 검사"

unity status 2>/dev/null | grep -q ready || { echo "❌ Unity 에디터가 열려 있지 않아 테스트를 못 돌린다"; exit 1; }
out=$(bash Tools/dev/test.sh 2>&1)
echo "$out" | tail -5
echo "$out" | grep -qE "'failed': 0," && echo "$out" | grep -qE "'total': [1-9]" || { echo "❌ EditMode 테스트 실패"; exit 1; }
echo "✅ EditMode 테스트"
