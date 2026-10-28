"""unity command eval --format json 결과에서 result 문자열만 꺼내 출력 (+ 파일로 저장)"""
import io
import json
import sys

raw = sys.stdin.buffer.read().decode("utf-8", errors="replace")
start = raw.find("{")
out = raw
if start >= 0:
    try:
        data = json.loads(raw[start:])

        def find(o):
            if isinstance(o, dict):
                if isinstance(o.get("result"), str):
                    return o["result"]
                for v in o.values():
                    r = find(v)
                    if r is not None:
                        return r
            if isinstance(o, list):
                for v in o:
                    r = find(v)
                    if r is not None:
                        return r
            return None

        r = find(data)
        if r is not None:
            out = r
    except json.JSONDecodeError:
        pass

if len(sys.argv) > 1:
    io.open(sys.argv[1], "w", encoding="utf-8").write(out)
sys.stdout.buffer.write(out.encode("utf-8"))
sys.stdout.buffer.write(b"\n")
