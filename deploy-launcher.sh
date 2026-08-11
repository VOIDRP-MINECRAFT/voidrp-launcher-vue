#!/usr/bin/env bash
# Build the launcher (both platforms) via build-release-linux.sh, then publish
# the artifacts to the web deploy dir with the manifest written LAST. Driven by
# the admin "Лаунчер" tab (core/launcher_ops.py) but safe to run by hand too.
#
# Env (set by the backend; sensible defaults for manual runs):
#   LAUNCHER_DEPLOY_DIR   target dir (default /var/www/void-rp/launcher/self-update)
#   LAUNCHER_STATUS_FILE  json status file to update on finish (optional)
#
# Stage banners use "==> ..." so the admin UI can show the current step live.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

DEPLOY_DIR="${LAUNCHER_DEPLOY_DIR:-/var/www/void-rp/launcher/self-update}"
STATUS_FILE="${LAUNCHER_STATUS_FILE:-}"
HISTORY_FILE="${LAUNCHER_HISTORY_FILE:-}"
RELEASE_NOTES="${LAUNCHER_RELEASE_NOTES:-}"
SRC="$SCRIPT_DIR/dist-deploy/launcher"

step() { printf "\n\033[36m==> %s\033[0m\n" "$*"; }

# Update the JSON status file (preserving backend-written fields) and append a
# history record on finish.
write_status() {
  local state="$1" error="${2:-}"
  [[ -z "$STATUS_FILE" ]] && return 0
  python3 - "$STATUS_FILE" "$state" "$error" "${HISTORY_FILE:-}" <<'PY' || true
import json, sys, datetime, os
path, state, error, hist = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
try:
    data = json.load(open(path, encoding="utf-8"))
except Exception:
    data = {}
now = datetime.datetime.now(datetime.timezone.utc)
data["state"] = state
data["finished_at"] = now.isoformat()
if error:
    data["error"] = error
tmp = path + ".tmp"
json.dump(data, open(tmp, "w", encoding="utf-8"), ensure_ascii=False)
os.replace(tmp, path)
# Append one history line per finished run.
if hist:
    dur = None
    try:
        s = datetime.datetime.fromisoformat(data.get("started_at"))
        dur = round((now - s).total_seconds())
    except Exception:
        pass
    rec = {
        "version": data.get("version"),
        "actor": data.get("actor"),
        "state": state,
        "started_at": data.get("started_at"),
        "finished_at": now.isoformat(),
        "duration_sec": dur,
        "error": error or None,
    }
    with open(hist, "a", encoding="utf-8") as f:
        f.write(json.dumps(rec, ensure_ascii=False) + "\n")
PY
}

on_error() {
  local code=$?
  step "ОШИБКА сборки/деплоя (код $code)"
  write_status "failed" "Сборка/деплой завершились с ошибкой (код $code) — см. лог"
  exit "$code"
}
trap on_error ERR

# ── 1. Build both platforms ───────────────────────────────────────────────────
step "Запуск сборки релиза (build-release-linux.sh)"
./build-release-linux.sh --skip-npm-install

[[ -f "$SRC/self-update/manifest.json" ]] || { echo "manifest не собран"; exit 1; }

# ── 1b. Apply admin release notes to the manifest (shown to players) ──────────
if [[ -n "$RELEASE_NOTES" ]]; then
  step "Пишем release notes в манифест"
  RELEASE_NOTES="$RELEASE_NOTES" python3 - "$SRC/self-update/manifest.json" <<'PY' || true
import json, sys, os
path = sys.argv[1]
notes = os.environ.get("RELEASE_NOTES", "").strip()
m = json.load(open(path, encoding="utf-8"))
if notes:
    m["notes"] = notes
json.dump(m, open(path, "w", encoding="utf-8"), ensure_ascii=False, indent=2)
PY
fi

# ── 2. Publish artifacts (manifest LAST) ──────────────────────────────────────
sha256up() { sha256sum "$1" | awk '{print toupper($1)}'; }
# atomic copy: write .tmp then rename (same filesystem)
acp() { cp -f "$1" "$2.tmp" && mv -f "$2.tmp" "$2"; }

# Snapshot the CURRENT live release into prev/ before overwriting (for rollback).
if [[ -f "$DEPLOY_DIR/manifest.json" ]]; then
  step "Архивируем текущий релиз в prev/ (для отката)"
  PREV="$DEPLOY_DIR/prev"
  rm -rf "$PREV"; mkdir -p "$PREV/win-x64" "$PREV/linux-x64"
  cp -f "$DEPLOY_DIR/manifest.json" "$PREV/manifest.json" 2>/dev/null || true
  cp -f "$DEPLOY_DIR"/win-x64/*   "$PREV/win-x64/"   2>/dev/null || true
  cp -f "$DEPLOY_DIR"/linux-x64/* "$PREV/linux-x64/" 2>/dev/null || true
  cp -f "$DEPLOY_DIR/VoidRpLauncher.exe" "$PREV/VoidRpLauncher.exe" 2>/dev/null || true
  cp -f "$DEPLOY_DIR/VoidRpLauncher"     "$PREV/VoidRpLauncher"     2>/dev/null || true
fi

step "Публикуем бинарники в $DEPLOY_DIR"
mkdir -p "$DEPLOY_DIR/win-x64" "$DEPLOY_DIR/linux-x64"

acp "$SRC/self-update/win-x64/VoidRpLauncher.exe"         "$DEPLOY_DIR/win-x64/VoidRpLauncher.exe"
acp "$SRC/self-update/win-x64/VoidRpLauncher.Updater.exe" "$DEPLOY_DIR/win-x64/VoidRpLauncher.Updater.exe"
acp "$SRC/self-update/linux-x64/VoidRpLauncher"           "$DEPLOY_DIR/linux-x64/VoidRpLauncher"
acp "$SRC/self-update/linux-x64/VoidRpLauncher.Updater"   "$DEPLOY_DIR/linux-x64/VoidRpLauncher.Updater"
chmod +x "$DEPLOY_DIR/linux-x64/VoidRpLauncher" "$DEPLOY_DIR/linux-x64/VoidRpLauncher.Updater"

# Public top-level downloads (site "download launcher" button).
[[ -f "$SRC/VoidRpLauncher.exe" ]] && acp "$SRC/VoidRpLauncher.exe" "$DEPLOY_DIR/VoidRpLauncher.exe"
[[ -f "$SRC/VoidRpLauncher" ]]     && { acp "$SRC/VoidRpLauncher" "$DEPLOY_DIR/VoidRpLauncher"; chmod +x "$DEPLOY_DIR/VoidRpLauncher"; }

# Backup old manifest, then publish the new one LAST.
[[ -f "$DEPLOY_DIR/manifest.json" ]] && cp -f "$DEPLOY_DIR/manifest.json" "$DEPLOY_DIR/manifest.prev.json"
acp "$SRC/self-update/manifest.json" "$DEPLOY_DIR/manifest.json"

# ── 3. Verify deployed hashes == manifest ─────────────────────────────────────
step "Проверяем целостность задеплоенных файлов"
verify_one() {
  local rel="$1"
  local want got
  want=$(python3 -c "import json,sys;m=json.load(open('$DEPLOY_DIR/manifest.json'));print(m['artifacts']['$2']['$3']['sha256'].upper())")
  got=$(sha256up "$DEPLOY_DIR/$rel")
  if [[ "$want" != "$got" ]]; then echo "SHA MISMATCH: $rel"; exit 1; fi
}
verify_one "win-x64/VoidRpLauncher.exe"         "win-x64"   "launcher"
verify_one "win-x64/VoidRpLauncher.Updater.exe" "win-x64"   "updater"
verify_one "linux-x64/VoidRpLauncher"           "linux-x64" "launcher"
verify_one "linux-x64/VoidRpLauncher.Updater"   "linux-x64" "updater"

step "Готово — релиз опубликован"
write_status "success"
