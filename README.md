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
- **Синхронизация модпака** — скачивание, обновление, проверка файлов через CmlLib
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
