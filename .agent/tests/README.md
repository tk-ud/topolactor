# `.agent/tests` — 実行チェックと出力方針

## 役割

`check-*.sh` は SSOT / 構造 / ランタイム意味の**実行可能ゲート**です。  
成功時はログを短く、失敗時だけ詳細を出します（CI とローカルで同じ見え方）。

## 出力方針（必須）

| 結果 | 方針 | 例 |
|------|------|-----|
| **成功** | 1 行（多レーン gate はレーンご視点でも 1 行） | `PASS runtime-semantics lanes=3` |
| **失敗** | `FAIL` 行（label / exit / command）+ **キャプチャした stdout/stderr 全文** | `FAIL runtime_semantics lane=backend_runtime exit=1 command=dotnet test ...` のあと dotnet/deno の詳細 |

### 原則

1. **ok log は短く** — `dotnet test` / `deno test` の成功ログを gate 本体に流さない。
2. **error は詳細に** — 失敗時はランナー出力を省略せず stderr に replay する。
3. **missing tool は pass にしない** — `ERROR: required tool not found` で即 fail。
4. **gate ラベルは安定** — `PASS` / `FAIL` 行の prefix は機械 grep 可能な固定語彙にする。

### 実装（正本）

共有ヘルパー: [`.agent/scripts/lib/noise_control.sh`](../scripts/lib/noise_control.sh)

| 関数 | 用途 |
|------|------|
| `noise_run LABEL cmd...` | 単一コマンド。成功時 `PASS LABEL`（`NOISE_QUIET_SUCCESS=1` で抑制可） |
| `noise_run_lane LABEL cmd...` | 多レーン内の 1 レーン。成功時は無出力 |
| `noise_run_bash LABEL 'cmd string'` | bash -c 経由（unified gate 向け） |

```bash
source .agent/scripts/lib/noise_control.sh

# 単一レーン
noise_run "backend_runtime_tests" dotnet test ... --nologo --verbosity minimal

# 多レーン（参考: check-runtime-semantics.sh, check-unified-test-gate.sh）
run_lane() {
  if ! noise_run_lane "$1" "${@:2}"; then FAILURES=$((FAILURES + 1)); fi
}
# ... run_lane 呼び出し ...
[ "$FAILURES" -eq 0 ] && { echo "PASS my-gate lanes=N"; exit 0; }
echo "FAIL my-gate failures=$FAILURES" >&2
exit 1
```

### ランナー引数

| ランナー | gate 経由時の推奨 |
|----------|-------------------|
| `dotnet test` | `--nologo --verbosity minimal` |
| `deno test` | 追加フラグ不要（出力は `noise_run*` がキャプチャ） |
| 素の bash 検査 | `noise_run` / `noise_run_bash` でラップ |

### 手動デバッグ vs gate

- **gate / CI**: 必ず `bash .agent/tests/check-*.sh` 経由（上記方針）。
- **開発中の単体調査**: ランナーを直接実行してよい（例: `dotnet test --filter ... -v normal`）。gate スクリプト自体は verbose 化しない。

### 参考実装

| パターン | ファイル |
|----------|----------|
| 多レーン + 末尾 1 行 summary | `check-runtime-semantics.sh`, `check-unified-test-gate.sh` |
| 多レーン + FAILURES 集約 | `check-default-entity-search.sh` |
| 単一レーン | `check-backend-tests.sh` |
| 親 gate が子 stdout を 1 行化 | `check-local-ci.sh` の `run_check` |

新規 `check-*.sh` を追加するときは、上記いずれかのパターンに合わせ、`noise_control.sh` を source してください。素の `dotnet test` / `deno test` を gate 本体に直書きしないでください。
