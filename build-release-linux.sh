#!/usr/bin/env bash
# Unified release builder — builds Windows portable + Linux AppImage from Linux.
#
# Usage:
#   ./build-release-linux.sh                        # обе платформы
#   ./build-release-linux.sh --win-only             # только Windows portable
#   ./build-release-linux.sh --linux-only           # только Linux AppImage
#   ./build-release-linux.sh --skip-npm-install     # пропустить npm ci
#   ./build-release-linux.sh --skip-dotnet-restore  # пропустить dotnet restore
#
# Переменные окружения:
#   SELF_UPDATE_BASE_URL — базовый URL для self-update (по умолч. https://void-rp.ru/launcher/self-update)

set -euo pipefail

# ── Аргументы ─────────────────────────────────────────────────────────────────
BUILD_WIN=true
BUILD_LINUX=true
SKIP_NPM=false
SKIP_DOTNET_RESTORE=false
SELF_UPDATE_BASE_URL="${SELF_UPDATE_BASE_URL:-https://void-rp.ru/launcher/self-update}"

for arg in "$@"; do
  case "$arg" in
    --win-only)            BUILD_LINUX=false ;;
    --linux-only)          BUILD_WIN=false ;;
    --skip-npm-install)    SKIP_NPM=true ;;
    --skip-dotnet-restore) SKIP_DOTNET_RESTORE=true ;;
  esac
done

# ── Утилиты ───────────────────────────────────────────────────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

step()  { printf "\n\033[36m==> %s\033[0m\n" "$*"; }
ok()    { printf "    \033[32m✓ %s\033[0m\n" "$*"; }
warn()  { printf "    \033[33m⚠ %s\033[0m\n" "$*"; }
die()   { printf "\033[31mERROR: %s\033[0m\n" "$*" >&2; exit 1; }

sha256up() { sha256sum "$1" | awk '{print toupper($1)}'; }

# ── Убиваем старые процессы ───────────────────────────────────────────────────
step "Останавливаем запущенные процессы сборки"
pkill -f "VoidRpLauncher.CoreHost" 2>/dev/null && warn "killed CoreHost" || true
pkill -f "electron.*voidrp" 2>/dev/null && warn "killed electron" || true
ok "done"

# ── Проверяем / устанавливаем dotnet 9 ───────────────────────────────────────
step "Проверяем dotnet SDK 9.x"

# Предпочитаем ~/.dotnet (туда ставит официальный install-скрипт)
for candidate in "$HOME/.dotnet/dotnet" /usr/share/dotnet/dotnet /opt/dotnet/dotnet; do
  if [ -x "$candidate" ]; then
    export PATH="$(dirname "$candidate"):$PATH"
    break
  fi
done

dotnet_has_sdk9() {
  command -v dotnet &>/dev/null && dotnet --list-sdks 2>/dev/null | grep -q '^9\.'
}

install_dotnet9() {
  warn "dotnet SDK 9.x не найден — устанавливаем..."
  # Ubuntu 24.04: пакет dotnet-sdk-9.0 доступен только через Microsoft feed.
  # Используем официальный install-скрипт — быстрее и не требует sudo для feed.
  mkdir -p "$HOME/.dotnet"
  curl -fsSL https://dot.net/v1/dotnet-install.sh \
    | bash -s -- --channel 9.0 --install-dir "$HOME/.dotnet"
  export PATH="$HOME/.dotnet:$PATH"
}

if ! dotnet_has_sdk9; then
  install_dotnet9
fi

dotnet_has_sdk9 || die "dotnet SDK 9.x не удалось установить"
dotnet --version
ok "dotnet OK (SDK 9.x)"

# ── Проверяем / устанавливаем Wine (нужен для Windows portable) ──────────────
if [[ "$BUILD_WIN" == true ]]; then
  step "Проверяем Wine (для Windows cross-build)"
  if ! command -v wine &>/dev/null && ! command -v wine64 &>/dev/null; then
    warn "Wine не найден — устанавливаем..."
    sudo apt-get install -y wine wine64
  fi
  command -v wine &>/dev/null || command -v wine64 &>/dev/null \
    || { warn "Wine не удалось установить — Windows portable будет пропущен."; BUILD_WIN=false; }
  command -v wine &>/dev/null && ok "wine $(wine --version 2>/dev/null || echo 'ok')" || true
  command -v wine64 &>/dev/null && ok "wine64 ok" || true
fi

# ── Проверяем node / npm / npx ────────────────────────────────────────────────
step "Проверяем node / npm"
command -v node &>/dev/null || die "node не найден (https://nodejs.org)"
command -v npm  &>/dev/null || die "npm не найден"
command -v npx  &>/dev/null || die "npx не найден"
ok "node $(node --version), npm $(npm --version)"

# ── Версия из package.json ────────────────────────────────────────────────────
VERSION=$(node -p "require('./package.json').version")
[[ -n "$VERSION" ]] || die "Не удалось прочитать version из package.json"
echo "    Version: $VERSION"

# ── Пути ──────────────────────────────────────────────────────────────────────
BUILD_WIN_CORE="$SCRIPT_DIR/build/core/win-x64"
BUILD_LINUX_CORE="$SCRIPT_DIR/build/core/linux-x64"
DIST_RELEASE="$SCRIPT_DIR/dist-release"
DIST_RENDERER="$SCRIPT_DIR/dist-renderer"
DIST_ELECTRON="$SCRIPT_DIR/dist-electron"
DIST_SELF_UPDATE="$SCRIPT_DIR/dist-self-update"
DIST_DEPLOY="$SCRIPT_DIR/dist-deploy/launcher"

CORE_PROJ="$SCRIPT_DIR/core/VoidRpLauncher.CoreHost/VoidRpLauncher.CoreHost.csproj"
[[ -f "$CORE_PROJ" ]] || die "CoreHost csproj не найден: $CORE_PROJ"

UPDATER_PROJ="$SCRIPT_DIR/VoidRpLauncher.Updater/VoidRpLauncher.Updater.csproj"
[[ -f "$UPDATER_PROJ" ]] || die "Updater csproj не найден: $UPDATER_PROJ"

BUILD_WIN_UPDATER="$SCRIPT_DIR/build/updater/win-x64"
BUILD_LINUX_UPDATER="$SCRIPT_DIR/build/updater/linux-x64"

# ── Чистим ────────────────────────────────────────────────────────────────────
step "Чистим предыдущий билд"
rm -rf "$DIST_RELEASE" "$DIST_RENDERER" "$DIST_ELECTRON" \
       "$DIST_SELF_UPDATE" "$DIST_DEPLOY" \
       "$BUILD_WIN_CORE" "$BUILD_LINUX_CORE" \
       "$BUILD_WIN_UPDATER" "$BUILD_LINUX_UPDATER"
mkdir -p "$DIST_SELF_UPDATE/win-x64" \
         "$DIST_SELF_UPDATE/linux-x64" \
         "$DIST_DEPLOY/self-update/win-x64" \
         "$DIST_DEPLOY/self-update/linux-x64"
ok "done"

# ── npm ci ────────────────────────────────────────────────────────────────────
if [[ "$SKIP_NPM" == false ]]; then
  if [[ ! -d "$SCRIPT_DIR/node_modules" ]]; then
    step "Устанавливаем node_modules (npm ci)"
    npm ci
  else
    step "node_modules уже есть — пропускаем npm ci"
  fi
fi

# ── dotnet restore ────────────────────────────────────────────────────────────
if [[ "$SKIP_DOTNET_RESTORE" == false ]]; then
  step "dotnet restore"
  dotnet restore "$CORE_PROJ"
  dotnet restore "$UPDATER_PROJ"
fi

# ── Publish CoreHost ──────────────────────────────────────────────────────────
PUBLISH_FLAGS=(
  -c Release
  --self-contained true
  "/p:Version=$VERSION"
  /p:PublishSingleFile=false
  /p:DebugType=None
  /p:DebugSymbols=false
)

if [[ "$BUILD_WIN" == true ]]; then
  step "Публикуем CoreHost → win-x64"
  dotnet publish "$CORE_PROJ" -r win-x64 -o "$BUILD_WIN_CORE" "${PUBLISH_FLAGS[@]}"
  ok "win-x64 CoreHost готов"
fi

if [[ "$BUILD_LINUX" == true ]]; then
  step "Публикуем CoreHost → linux-x64"
  dotnet publish "$CORE_PROJ" -r linux-x64 -o "$BUILD_LINUX_CORE" "${PUBLISH_FLAGS[@]}"
  chmod +x "$BUILD_LINUX_CORE/VoidRpLauncher.CoreHost" 2>/dev/null || true
  ok "linux-x64 CoreHost готов"
fi

# ── Publish Updater ───────────────────────────────────────────────────────────
UPDATER_PUBLISH_FLAGS=(
  -c Release
  --self-contained true
  "/p:Version=$VERSION"
  /p:PublishSingleFile=true
  /p:IncludeNativeLibrariesForSelfExtract=true
  /p:DebugType=None
  /p:DebugSymbols=false
)

if [[ "$BUILD_WIN" == true ]]; then
  step "Публикуем Updater → win-x64"
  dotnet publish "$UPDATER_PROJ" -r win-x64 -o "$BUILD_WIN_UPDATER" "${UPDATER_PUBLISH_FLAGS[@]}"
  ok "win-x64 Updater готов"
fi

if [[ "$BUILD_LINUX" == true ]]; then
  step "Публикуем Updater → linux-x64"
  dotnet publish "$UPDATER_PROJ" -r linux-x64 -o "$BUILD_LINUX_UPDATER" "${UPDATER_PUBLISH_FLAGS[@]}"
  chmod +x "$BUILD_LINUX_UPDATER/VoidRpLauncher.Updater" 2>/dev/null || true
  ok "linux-x64 Updater готов"
fi

# ── Electron + Renderer ───────────────────────────────────────────────────────
step "Компилируем Electron main/preload"
npm run build:electron

step "Собираем Vue renderer"
npm run build:renderer

# ── electron-builder ──────────────────────────────────────────────────────────
export VOIDRP_LAUNCHER_VERSION="$VERSION"

if [[ "$BUILD_WIN" == true ]]; then
  step "Пакуем Windows portable .exe"
  npx electron-builder --win portable --publish never
  ok "Windows portable собран"
fi

if [[ "$BUILD_LINUX" == true ]]; then
  step "Пакуем Linux AppImage"
  npx electron-builder --linux AppImage --publish never
  ok "Linux AppImage собран"
fi

# ── Находим артефакты ─────────────────────────────────────────────────────────
step "Собираем артефакты"

WIN_ARTIFACT=""
LINUX_ARTIFACT=""

if [[ "$BUILD_WIN" == true ]]; then
  WIN_ARTIFACT=$(find "$DIST_RELEASE" -maxdepth 1 -name "*portable.exe" 2>/dev/null \
    | sort -V | tail -n1)
  [[ -n "$WIN_ARTIFACT" ]] || die "Windows portable .exe не найден в $DIST_RELEASE"
  ok "Windows: $(basename "$WIN_ARTIFACT")"
fi

if [[ "$BUILD_LINUX" == true ]]; then
  LINUX_ARTIFACT=$(find "$DIST_RELEASE" -maxdepth 1 -name "*.AppImage" 2>/dev/null \
    | sort -V | tail -n1)
  [[ -n "$LINUX_ARTIFACT" ]] || die "Linux AppImage не найден в $DIST_RELEASE"
  ok "Linux: $(basename "$LINUX_ARTIFACT")"
fi

# ── Копируем в self-update / deploy ───────────────────────────────────────────
WIN_SHA=""
WIN_UPDATER_SHA=""
LINUX_SHA=""
LINUX_UPDATER_SHA=""

if [[ "$BUILD_WIN" == true ]]; then
  cp "$WIN_ARTIFACT"                           "$DIST_SELF_UPDATE/win-x64/VoidRpLauncher.exe"
  cp "$WIN_ARTIFACT"                           "$DIST_DEPLOY/self-update/win-x64/VoidRpLauncher.exe"
  cp "$WIN_ARTIFACT"                           "$DIST_DEPLOY/VoidRpLauncher.exe"
  WIN_SHA=$(sha256up "$DIST_SELF_UPDATE/win-x64/VoidRpLauncher.exe")

  cp "$BUILD_WIN_UPDATER/VoidRpLauncher.Updater.exe" \
                                               "$DIST_SELF_UPDATE/win-x64/VoidRpLauncher.Updater.exe"
  cp "$BUILD_WIN_UPDATER/VoidRpLauncher.Updater.exe" \
                                               "$DIST_DEPLOY/self-update/win-x64/VoidRpLauncher.Updater.exe"
  WIN_UPDATER_SHA=$(sha256up "$DIST_SELF_UPDATE/win-x64/VoidRpLauncher.Updater.exe")
fi

if [[ "$BUILD_LINUX" == true ]]; then
  cp "$LINUX_ARTIFACT"                         "$DIST_SELF_UPDATE/linux-x64/VoidRpLauncher"
  cp "$LINUX_ARTIFACT"                         "$DIST_DEPLOY/self-update/linux-x64/VoidRpLauncher"
  cp "$LINUX_ARTIFACT"                         "$DIST_DEPLOY/VoidRpLauncher"
  chmod +x "$DIST_SELF_UPDATE/linux-x64/VoidRpLauncher" \
            "$DIST_DEPLOY/self-update/linux-x64/VoidRpLauncher" \
            "$DIST_DEPLOY/VoidRpLauncher"
  LINUX_SHA=$(sha256up "$DIST_SELF_UPDATE/linux-x64/VoidRpLauncher")

  cp "$BUILD_LINUX_UPDATER/VoidRpLauncher.Updater" \
                                               "$DIST_SELF_UPDATE/linux-x64/VoidRpLauncher.Updater"
  cp "$BUILD_LINUX_UPDATER/VoidRpLauncher.Updater" \
                                               "$DIST_DEPLOY/self-update/linux-x64/VoidRpLauncher.Updater"
  chmod +x "$DIST_SELF_UPDATE/linux-x64/VoidRpLauncher.Updater" \
            "$DIST_DEPLOY/self-update/linux-x64/VoidRpLauncher.Updater"
  LINUX_UPDATER_SHA=$(sha256up "$DIST_SELF_UPDATE/linux-x64/VoidRpLauncher.Updater")
fi

# ── Генерируем manifest.json через отдельный JS-файл ─────────────────────────
step "Генерируем manifest.json"
MANIFEST_PATH="$DIST_SELF_UPDATE/manifest.json"
MANIFEST_TMP=$(mktemp /tmp/gen-manifest-XXXXXX.mjs)
# shellcheck disable=SC2064
trap "rm -f '$MANIFEST_TMP'" EXIT

cat > "$MANIFEST_TMP" << 'GENEOF'
import fs from 'fs';

const [
  manifestPath, version, notes,
  doBuildWin, doBuildLinux, baseUrl,
  winSha, winUpdaterSha,
  linuxSha, linuxUpdaterSha
] = process.argv.slice(2);

let artifacts = {};

if (fs.existsSync(manifestPath)) {
  try { artifacts = JSON.parse(fs.readFileSync(manifestPath, 'utf8')).artifacts ?? {}; } catch {}
}

if (doBuildWin === 'true' && winSha) {
  artifacts['win-x64'] = {
    launcher: { kind: 'single-file', url: `${baseUrl}/win-x64/VoidRpLauncher.exe`,         sha256: winSha },
    updater:  { kind: 'single-file', url: `${baseUrl}/win-x64/VoidRpLauncher.Updater.exe`, sha256: winUpdaterSha }
  };
}

if (doBuildLinux === 'true' && linuxSha) {
  artifacts['linux-x64'] = {
    launcher: { kind: 'single-file', url: `${baseUrl}/linux-x64/VoidRpLauncher`,         sha256: linuxSha },
    updater:  { kind: 'single-file', url: `${baseUrl}/linux-x64/VoidRpLauncher.Updater`, sha256: linuxUpdaterSha }
  };
}

fs.writeFileSync(manifestPath, JSON.stringify({ version, notes, artifacts }, null, 2) + '\n');
console.log('manifest.json written:', manifestPath);
GENEOF

BUILD_DATE=$(date -u "+%Y-%m-%d %H:%M:%S UTC")

node "$MANIFEST_TMP" \
  "$MANIFEST_PATH" \
  "$VERSION" \
  "Built $BUILD_DATE" \
  "$BUILD_WIN" \
  "$BUILD_LINUX" \
  "$SELF_UPDATE_BASE_URL" \
  "${WIN_SHA:-}" \
  "${WIN_UPDATER_SHA:-}" \
  "${LINUX_SHA:-}" \
  "${LINUX_UPDATER_SHA:-}"

cp "$MANIFEST_PATH" "$DIST_DEPLOY/self-update/manifest.json"
ok "manifest.json готов"

# ── Итог ──────────────────────────────────────────────────────────────────────
step "Готово!"
echo ""
[[ "$BUILD_WIN"   == true ]] && echo "  Windows portable:        $WIN_ARTIFACT"
[[ "$BUILD_WIN"   == true ]] && echo "  Windows launcher SHA256: $WIN_SHA"
[[ "$BUILD_WIN"   == true ]] && echo "  Windows updater SHA256:  $WIN_UPDATER_SHA"
[[ "$BUILD_LINUX" == true ]] && echo "  Linux AppImage:          $LINUX_ARTIFACT"
[[ "$BUILD_LINUX" == true ]] && echo "  Linux launcher SHA256:   $LINUX_SHA"
[[ "$BUILD_LINUX" == true ]] && echo "  Linux updater SHA256:    $LINUX_UPDATER_SHA"
echo "  Manifest:              $MANIFEST_PATH"
echo "  Deploy root:           $DIST_DEPLOY"
echo ""
echo "  Структура для заливки на сервер (dist-deploy/launcher/):"
echo "  launcher/"
[[ "$BUILD_WIN"   == true ]] && echo "    VoidRpLauncher.exe"
[[ "$BUILD_LINUX" == true ]] && echo "    VoidRpLauncher       ← AppImage"
echo "    self-update/"
echo "      manifest.json"
[[ "$BUILD_WIN"   == true ]] && echo "      win-x64/VoidRpLauncher.exe"
[[ "$BUILD_WIN"   == true ]] && echo "      win-x64/VoidRpLauncher.Updater.exe"
[[ "$BUILD_LINUX" == true ]] && echo "      linux-x64/VoidRpLauncher"
[[ "$BUILD_LINUX" == true ]] && echo "      linux-x64/VoidRpLauncher.Updater"
