#!/usr/bin/env python3
"""
Windows için Unity MCP Stdio <-> Streamable HTTP yedek köprü (bridge) script'i.

Kullanım amacı:
- Antigravity'nin doğrudan Streamable HTTP bağlantısında "invalid trailing data" veya
  "Mcp-Session-Id" başlık (header) hatası verdiği durumlarda, Antigravity ile stdio (JSON lines)
  üzerinden konuşup istekleri Unity MCP yerel HTTP sunucusuna (varsayılan: http://127.0.0.1:8080/mcp)
  iletir.
- Dönen Mcp-Session-Id başlığını saklayıp sonraki tüm isteklere otomatik ekler.
- SSE (data: ...) veya fazladan trailing data verisini temizleyerek geçerli tek satır JSON döndürür.

Ortam değişkenleri:
- UNITY_MCP_HTTP_URL: Hedef HTTP MCP adresi (varsayılan: http://127.0.0.1:8080/mcp)
- UNITY_MCP_TIMEOUT: İstek zaman aşımı saniye (varsayılan: 30)
"""

import json
import os
import sys
import urllib.error
import urllib.request
from typing import Optional


DEFAULT_URL = "http://127.0.0.1:8080/mcp"
TIMEOUT_SECONDS = float(os.environ.get("UNITY_MCP_TIMEOUT", "30"))
TARGET_URL = os.environ.get("UNITY_MCP_HTTP_URL", DEFAULT_URL).strip() or DEFAULT_URL

_session_id: Optional[str] = None


def log_err(msg: str) -> None:
    sys.stderr.write(f"[unity_mcp_bridge] {msg}\n")
    sys.stderr.flush()


def emit_json(obj: object) -> None:
    line = json.dumps(obj, ensure_ascii=False, separators=(",", ":"))
    sys.stdout.write(line + "\n")
    sys.stdout.flush()


def parse_and_emit_body(raw_body: str, content_type: str, req_id: object) -> None:
    stripped = raw_body.strip()
    if not stripped:
        return

    # Streamable HTTP SSE yanıtlarını (text/event-stream) ayrıştır
    if "text/event-stream" in content_type.lower() or stripped.startswith("event:") or stripped.startswith("data:"):
        data_buffer = []
        for line in raw_body.splitlines():
            line_s = line.strip()
            if not line_s:
                if data_buffer:
                    payload = "\n".join(data_buffer).strip()
                    data_buffer.clear()
                    if payload and payload != "[DONE]":
                        try:
                            emit_json(json.loads(payload))
                        except json.JSONDecodeError as exc:
                            log_err(f"SSE JSON ayrıştırma hatası: {exc}")
                continue
            if line_s.startswith("data:"):
                data_buffer.append(line_s[5:].lstrip())
        if data_buffer:
            payload = "\n".join(data_buffer).strip()
            if payload and payload != "[DONE]":
                try:
                    emit_json(json.loads(payload))
                except json.JSONDecodeError as exc:
                    log_err(f"SSE son JSON ayrıştırma hatası: {exc}")
        return

    # Düz JSON yanıtlarında olası trailing data kalıntılarını JSONDecoder.raw_decode ile temizle
    decoder = json.JSONDecoder()
    idx = 0
    length = len(stripped)
    while idx < length:
        while idx < length and stripped[idx].isspace():
            idx += 1
        if idx >= length:
            break
        try:
            obj, next_idx = decoder.raw_decode(stripped, idx)
            emit_json(obj)
            idx = next_idx
        except json.JSONDecodeError as exc:
            log_err(f"Yanıt trailing/JSON hatası (offset={idx}): {exc}")
            if req_id is not None:
                emit_json({
                    "jsonrpc": "2.0",
                    "id": req_id,
                    "error": {"code": -32001, "message": f"Gecersiz JSON yaniti: {exc}"}
                })
            break


def forward_request(line: str) -> None:
    global _session_id

    try:
        req_obj = json.loads(line)
    except json.JSONDecodeError as exc:
        log_err(f"Gelen stdin satiri JSON degil: {exc}")
        return

    req_id = req_obj.get("id") if isinstance(req_obj, dict) else None
    payload_bytes = json.dumps(req_obj, ensure_ascii=False).encode("utf-8")

    headers = {
        "Content-Type": "application/json",
        "Accept": "application/json, text/event-stream",
    }
    if _session_id:
        headers["Mcp-Session-Id"] = _session_id

    request = urllib.request.Request(TARGET_URL, data=payload_bytes, headers=headers, method="POST")

    try:
        with urllib.request.urlopen(request, timeout=TIMEOUT_SECONDS) as response:
            new_sid = response.headers.get("Mcp-Session-Id") or response.headers.get("mcp-session-id")
            if new_sid:
                _session_id = new_sid.strip()
            ctype = response.headers.get("Content-Type", "")
            body = response.read().decode("utf-8", errors="replace")
            if response.status != 202 and body.strip():
                parse_and_emit_body(body, ctype, req_id)
    except urllib.error.HTTPError as err:
        err_body = err.read().decode("utf-8", errors="replace")
        log_err(f"HTTP {err.code} hatasi ({TARGET_URL}): {err_body}")
        if err.code in (400, 404) and _session_id:
            # Oturum zaman asimina ugradiysa temizle
            _session_id = None
        if req_id is not None:
            emit_json({
                "jsonrpc": "2.0",
                "id": req_id,
                "error": {"code": -32000, "message": f"Unity MCP HTTP {err.code}: {err_body[:300]}"}
            })
    except Exception as exc:
        log_err(f"Baglanti hatasi ({TARGET_URL}): {exc}")
        if req_id is not None:
            emit_json({
                "jsonrpc": "2.0",
                "id": req_id,
                "error": {"code": -32000, "message": f"Unity MCP HTTP sunucusuna erisilemiyor ({TARGET_URL}): {exc}"}
            })


def main() -> int:
    # Windows üzerinde UTF-8 stdio zorlaması
    if hasattr(sys.stdin, "reconfigure"):
        sys.stdin.reconfigure(encoding="utf-8", errors="replace")
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    log_err(f"Kopru baslatildi -> {TARGET_URL}")
    for raw_line in sys.stdin:
        line = raw_line.strip()
        if not line:
            continue
        forward_request(line)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
