"""minimal_yaml.py -- tiny block-style YAML subset loader (Python3 stdlib only).

Repo governance tooling is not allowed to depend on third-party packages
(no PyYAML, no jq, no node, no ruby). This module implements just enough of
YAML to read the hand-authored, consistently 2-space-indented docs/*.yaml
and .agent/docs/*.yaml governance surfaces used by the *.sh check scripts:

  - nested block mappings and block sequences (including sequences of maps)
  - flow sequences of scalars: [a, b, c]
  - single/double quoted scalar strings
  - folded (`>`, `>-`) and literal (`|`, `|-`) block scalars
  - `#` comments (outside quoted strings)
  - int / float / bool / null scalars

It does not implement anchors/aliases, flow mappings, multi-document
streams, or tag directives -- none of those appear in this repository's
governance YAML, and adding support for them would be speculative.
"""
from __future__ import annotations

import re

_BOOL_TRUE = {"true", "True", "TRUE"}
_BOOL_FALSE = {"false", "False", "FALSE"}
_NULLS = {"null", "Null", "NULL", "~"}
_BLOCK_MARKERS = {">", ">-", ">+", "|", "|-", "|+"}
_INT_RE = re.compile(r"^-?\d+$")
_FLOAT_RE = re.compile(r"^-?\d+\.\d+$")
_KEY_RE = re.compile(r"^[A-Za-z0-9_.\-]+$")


def _strip_comment(line: str) -> str:
    in_s = in_d = False
    for i, ch in enumerate(line):
        if ch == "'" and not in_d:
            in_s = not in_s
        elif ch == '"' and not in_s:
            in_d = not in_d
        elif ch == "#" and not in_s and not in_d:
            if i == 0 or line[i - 1] in (" ", "\t"):
                return line[:i]
    return line


def _find_top_level_colon(content: str):
    in_s = in_d = False
    depth = 0
    for i, ch in enumerate(content):
        if ch == "'" and not in_d:
            in_s = not in_s
        elif ch == '"' and not in_s:
            in_d = not in_d
        elif ch in "[{" and not in_s and not in_d:
            depth += 1
        elif ch in "]}" and not in_s and not in_d:
            depth -= 1
        elif ch == ":" and depth == 0 and not in_s and not in_d:
            if i + 1 == len(content) or content[i + 1] in (" ", "\t"):
                return i
    return None


def _split_flow_items(inner: str):
    items = []
    depth = 0
    in_s = in_d = False
    cur = []
    for ch in inner:
        if ch == "'" and not in_d:
            in_s = not in_s
        elif ch == '"' and not in_s:
            in_d = not in_d
        elif ch in "[{" and not in_s and not in_d:
            depth += 1
        elif ch in "]}" and not in_s and not in_d:
            depth -= 1
        if ch == "," and depth == 0 and not in_s and not in_d:
            items.append("".join(cur))
            cur = []
            continue
        cur.append(ch)
    if cur or items:
        items.append("".join(cur))
    return [it.strip() for it in items if it.strip() != ""]


def _unquote(s: str) -> str:
    if len(s) >= 2 and s[0] == '"' and s[-1] == '"':
        return s[1:-1].replace('\\"', '"').replace("\\\\", "\\")
    if len(s) >= 2 and s[0] == "'" and s[-1] == "'":
        return s[1:-1].replace("''", "'")
    return s


def parse_scalar(s: str):
    s = s.strip()
    if s == "":
        return None
    if (s[0] == '"' and s[-1] == '"' and len(s) >= 2) or (s[0] == "'" and s[-1] == "'" and len(s) >= 2):
        return _unquote(s)
    if s in _NULLS:
        return None
    if s in _BOOL_TRUE:
        return True
    if s in _BOOL_FALSE:
        return False
    if _INT_RE.match(s):
        return int(s)
    if _FLOAT_RE.match(s):
        return float(s)
    if s.startswith("[") and s.endswith("]"):
        inner = s[1:-1].strip()
        if inner == "":
            return []
        return [parse_scalar(it) for it in _split_flow_items(inner)]
    return _unquote(s)


def _try_split_kv(content: str):
    idx = _find_top_level_colon(content)
    if idx is None:
        return None, None, False
    key_raw = content[:idx].strip()
    val = content[idx + 1:].strip()
    key = _unquote(key_raw)
    if not (_KEY_RE.match(key_raw) or (key_raw.startswith('"') or key_raw.startswith("'"))):
        return None, None, False
    return key, val, True


class _Cursor:
    """Holds (indent, content, raw_line) records with blanks/comments removed."""

    def __init__(self, text: str):
        entries = []
        for raw in text.split("\n"):
            if raw.strip() == "":
                continue
            stripped = _strip_comment(raw)
            if stripped.strip() == "":
                continue
            content = stripped.strip()
            if content in ("---", "..."):
                continue
            indent = len(stripped) - len(stripped.lstrip(" "))
            entries.append((indent, content, raw))
        self.entries = entries
        self.pos = 0

    def peek(self):
        return self.entries[self.pos] if self.pos < len(self.entries) else None

    def advance(self):
        self.pos += 1


def _parse_block(cur: _Cursor, indent: int):
    item = cur.peek()
    if item is None or item[0] != indent:
        return None
    if item[1] == "-" or item[1].startswith("- "):
        return _parse_seq(cur, indent)
    return _parse_map(cur, indent)


def _consume_block_scalar(cur: _Cursor, marker: str, key_indent: int):
    folded = marker[0] == ">"
    piece_lines = []
    base_indent = None
    while True:
        nxt = cur.peek()
        if nxt is None or nxt[0] <= key_indent:
            break
        if base_indent is None:
            base_indent = nxt[0]
        raw = nxt[2]
        piece_lines.append(raw[base_indent:] if len(raw) >= base_indent else nxt[1])
        cur.advance()
    if folded:
        text = " ".join(l.strip() for l in piece_lines if l.strip() != "")
    else:
        text = "\n".join(piece_lines)
    return text


def _resolve_value(cur: _Cursor, val: str, key_indent: int):
    if val in _BLOCK_MARKERS:
        return _consume_block_scalar(cur, val, key_indent)
    if val == "":
        nxt = cur.peek()
        if nxt is not None and nxt[0] > key_indent:
            return _parse_block(cur, nxt[0])
        return None
    return parse_scalar(val)


def _parse_map(cur: _Cursor, indent: int):
    result = {}
    while True:
        item = cur.peek()
        if item is None or item[0] != indent or item[1] == "-" or item[1].startswith("- "):
            break
        key, val, is_kv = _try_split_kv(item[1])
        if not is_kv:
            break
        cur.advance()
        result[key] = _resolve_value(cur, val, indent)
    return result


def _parse_seq(cur: _Cursor, indent: int):
    result = []
    while True:
        item = cur.peek()
        if item is None or item[0] != indent:
            break
        content = item[1]
        if content == "-":
            rest = ""
        elif content.startswith("- "):
            rest = content[2:]
        else:
            break
        cur.advance()
        if rest == "":
            nxt = cur.peek()
            if nxt is not None and nxt[0] > indent:
                result.append(_parse_block(cur, nxt[0]))
            else:
                result.append(None)
            continue
        key, val, is_kv = _try_split_kv(rest)
        if not is_kv:
            result.append(parse_scalar(rest))
            continue
        item_indent = indent + 2
        d = {key: _resolve_value(cur, val, item_indent)}
        while True:
            nxt = cur.peek()
            if nxt is None or nxt[0] != item_indent or nxt[1] == "-" or nxt[1].startswith("- "):
                break
            k2, v2, kv2 = _try_split_kv(nxt[1])
            if not kv2:
                break
            cur.advance()
            d[k2] = _resolve_value(cur, v2, item_indent)
        result.append(d)
    return result


def load(text: str):
    cur = _Cursor(text)
    first = cur.peek()
    if first is None:
        return {}
    return _parse_block(cur, first[0])


def load_file(path: str):
    with open(path, "r", encoding="utf-8") as f:
        return load(f.read())


def dig(obj, *keys):
    cur = obj
    for k in keys:
        if not isinstance(cur, dict) or k not in cur:
            return None
        cur = cur[k]
    return cur


def arr(value):
    if value is None:
        return []
    if isinstance(value, list):
        return value
    return [value]
