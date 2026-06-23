# VoidRP Launcher

Десктопный лаунчер для VoidRP — устанавливает и запускает Minecraft с нужным модпаком, авторизует игрока через backend.

## Стек

- **Electron** (main process) · TypeScript
- **Vue 3 + TypeScript + Pinia + Tailwind CSS v4** (renderer)
- **.NET 8** минимальный ASP.NET API — CoreHost sidecar (порт 38765)
- **CmlLib** — установка и управление файлами Minecraft
- **electron-updater** — самообновление

## Архитектура (три процесса)

```
Renderer (Vue 3)
    ↕ window.desktopBridge.request() (IPC)
Electron main (electron/main.ts)
    ↕ HTTP localhost:38765
CoreHost (.NET 8, VoidRpLauncher.CoreHost)
    ↕ HTTPS
Backend API (void-rp.ru)
```

- **Renderer** — интерфейс лаунчера, Pinia store (`src/stores/launcher.ts`)
- **Electron main** — создаёт BrowserWindow, запускает CoreHost как дочерний процесс, проксирует IPC → HTTP
- **CoreHost** — вся тяжёлая логика: скачивание файлов Minecraft (CmlLib), play-ticket flow, управление сессией

## Быстрый старт (Linux)

```bash
cd voidrp_launcher_vue
npm install

# Сборка CoreHost (.NET 8 должен быть установлен)
npm run build:core:dev:linux

# Запуск в dev режиме
npm run dev
```

## Основные команды

```bash
npm run dev                    # dev режим (renderer :5177 + Electron)
npm run build:core:dev:linux   # сборка CoreHost для Linux (dev конфиг)
npm run build:electron         # сборка main process
npm run build:linux            # полная сборка AppImage / deb
```

## Auth flow (play-ticket)

1. Игрок вводит логин/пароль → CoreHost аутентифицирует через `/api/v1/auth/login`
2. CoreHost запрашивает play-ticket у backend: `POST /api/v1/auth/play-ticket`
3. Minecraft запускается с аргументами сессии, play-ticket передаётся в JVM args
4. Auth Bridge мод на сервере проверяет тикет через backend при входе игрока

## Самообновление

Манифест: `https://void-rp.ru/launcher/self-update/manifest.json`
Обновление применяется через отдельный `VoidRpLauncher.Updater` (C# консольное приложение).
