# 🚀 VoidRP Launcher

> Десктопный лаунчер VoidRP — устанавливает модпак, авторизует игрока и запускает Minecraft.

![Electron](https://img.shields.io/badge/Electron-latest-47848F?logo=electron&logoColor=white)
![Vue](https://img.shields.io/badge/Vue-3+TS-42b883?logo=vuedotjs&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux-lightgrey)
![License](https://img.shields.io/badge/license-proprietary-red)

---

## 🗺️ Место в экосистеме

```
  Игрок запускает лаунчер
        │
┌───────────────────────────────────┐
│  voidrp-launcher-vue              │
│  Renderer (Vue 3) ←IPC→ Electron  │
│       └── HTTP:38765 → CoreHost   │
└───────────┬───────────────────────┘
            │ play-ticket auth (HTTPS)
            ▼
  minecraft-backend → Minecraft Server
```

---

## ✨ Возможности

- **Авторизация** — вход через аккаунт VoidRP, хранение сессии
- **Play-ticket flow** — безопасное получение тикета для входа на сервер без передачи пароля в игру
- **Мульти-сервер** — несколько игровых серверов, файлы каждого изолированы в `servers/<slug>/`
- **Синхронизация модпака** — параллельное скачивание с проверкой по SHA-256 (см. ниже)
- **Запуск Minecraft** — bootstrap JVM с нужными параметрами памяти и аргументами
- **Самообновление** — electron-updater + отдельный VoidRpLauncher.Updater (C#)
- **Настройки** — выбор JVM, объём RAM, игровой директории

---

## 🏗️ Три процесса

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Renderer  (Vue 3 + TypeScript + Pinia + Tailwind v4)     │
│    src/stores/launcher.ts — состояние лаунчера              │
│    window.desktopBridge.request() → IPC                     │
├─────────────────────────────────────────────────────────────┤
│ 2. Electron main  (electron/main.ts)                        │
│    BrowserWindow · запуск CoreHost · IPC ↔ HTTP proxy       │
├─────────────────────────────────────────────────────────────┤
│ 3. CoreHost  (.NET 8, ASP.NET minimal API, порт 38765)      │
│    LauncherFacadeService · AuthenticatedLaunchService       │
│    RuntimeBootstrapService · CmlLib (файлы Minecraft)       │
└─────────────────────────────────────────────────────────────┘
```

---

## 🔄 Синхронизация файлов (CoreHost)

`FileSyncService` сверяет локальные файлы с манифестом пака и докачивает недостающие/изменённые.

- **Параллельно** — до 8 файлов одновременно (`Parallel.ForEachAsync`, HTTP/2), чтобы не упираться в задержку на каждый мелкий jar. Кэш SHA-256 (`hash-cache.json`, ключ путь+размер+mtime) не пересчитывает хеши без нужды.
- **Правила скачивания файла** (`NeedsDownload`):
  - нет локально → качать;
  - флаг `alwaysOverwrite` в манифесте → качать всегда (мелкие изменяемые конфиги, напр. layout'ы fancymenu);
  - флаг `managed` → сверка по хешу даже под `config/` (статичные ассеты: качаются один раз, обновляются только при смене хеша, не перекачиваются зря);
  - обычный файл под player-writable путём (`options.txt`, `config/`, `resourcepacks/`, `shaderpacks/`…) и уже есть → пропуск (правки игрока сохраняются);
  - иначе — сверка размера и SHA-256.
- **Очистка** — файлы, пропавшие из манифеста, и посторонние моды в `mods/` удаляются.

Манифест генерируется на бэкенде (`scripts/generate_launcher_manifest.py`), URL берётся из `game_servers`.

---

## 📋 Требования

| Компонент | Версия |
|---|---|
| Node.js | 18+ |
| .NET SDK | 8.0+ |
| npm | 9+ |

---

## 🚀 Быстрый старт (разработка)

```bash
cd voidrp_launcher_vue
npm install

# Собрать CoreHost (.NET) и запустить всё
npm run dev
```

### Отдельные команды

```bash
npm run build:core:dev:linux   # только CoreHost (Linux)
npm run build:electron         # только Electron main process
npm run build:linux            # продакшн-сборка AppImage/deb
```

---

## 🔗 Связанные репозитории

| Репо | Связь |
|---|---|
| [minecraft-backend](https://github.com/VOIDRP-MINECRAFT/minecraft-backend) | Auth API, play-ticket endpoint |
| [voidrp-launcher-java](https://github.com/VOIDRP-MINECRAFT/voidrp-launcher-java) | Альтернативный лаунчер (JavaFX) |
| [voidrp-site](https://github.com/VOIDRP-MINECRAFT/voidrp-site) | Сайт — открывается из лаунчера |

---

<div align="center">
<a href="https://void-rp.ru">🌐 Сайт</a> ·
<a href="https://github.com/VOIDRP-MINECRAFT">🏠 Организация</a>
</div>
