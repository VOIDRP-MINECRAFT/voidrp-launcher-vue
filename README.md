# 🚀 VoidRP Launcher

> Десктопный лаунчер VoidRP: вход в аккаунт, выбор сервера, установка и обновление модпака, Java нужной версии,
> вход в игру по одноразовому билету и помощь, если игра упала.

![Electron](https://img.shields.io/badge/Electron-Vue%203%20%2B%20TS-47848F?logo=electron&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet&logoColor=white)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux-lightgrey)
![Version](https://img.shields.io/badge/version-4.0.40-8b5cf6)
[![Build](https://github.com/VOIDRP-MINECRAFT/voidrp-launcher-vue/actions/workflows/build.yml/badge.svg)](https://github.com/VOIDRP-MINECRAFT/voidrp-launcher-vue/actions/workflows/build.yml)
![License](https://img.shields.io/badge/license-proprietary-red)

**[📥 Скачать лаунчер](https://void-rp.ru/download-launcher)**

---

## 🗺️ Место в экосистеме

```mermaid
flowchart LR
    P(["🎮 Игрок"])
    subgraph L["voidrp-launcher-vue"]
        R["Renderer<br/>Vue 3 · TS · Pinia · Tailwind"]
        E["Electron main"]
        C["CoreHost<br/>.NET 8 · порт 38765"]
    end
    B[("minecraft-backend")]
    CDN[("void-rp.ru/launcher<br/>пак и манифесты")]
    MC["Minecraft"]
    S1["🏰 VoidRP<br/>модовый"]
    S2["🌱 Origins<br/>плагинный"]

    P --> R
    R <-- "IPC" --> E <-- "HTTP" --> C
    C -- "JWT · play-ticket · каталог серверов<br/>правила крашей · настройки игры" --> B
    C -- "манифест + файлы<br/>SHA-256, HTTP/2" --> CDN
    C -- "запуск JVM" --> MC
    MC -- "билет в play-ticket.json<br/>(auth-bridge)" --> S1
    MC -- "билет меткой в адресе<br/>(auth-plugin)" --> S2
```

---

## ✨ Возможности

| | |
|---|---|
| 🔐 **Аккаунт** | Вход аккаунтом VoidRP, хранение и ротация сессии; перед запуском — окно с обновлёнными документами, пока не приняты оферта и согласие на обработку ПДн |
| 🌍 **Мультисервер** | Каталог серверов с бэкенда; у каждого сервера свои файлы в `servers/<slug>/` и свой профиль (модовый NeoForge или чистый клиент для плагинных серверов) |
| 📦 **Модпак** | Синхронизация по манифесту: до 8 файлов параллельно, проверка SHA-256, удаление лишних модов |
| ☕ **Java** | Скачивает нужный рантайм сам |
| 🎫 **Вход без пароля** | Одноразовый play-ticket: модовому серверу — через файл, плагинному — меткой в адресе подключения |
| 🎛️ **Настройки игры на аккаунте** | Клавиши, графика и звук сохраняются на аккаунт (отдельно для каждого сервера) и не перезаписываются паком — переустановка или второй компьютер ничего не стоят |
| 📂 **Папка игры** | Можно перенести файлы игры на другой диск или в путь без кириллицы — с проверками и откатом при ошибке |
| 🩺 **Краш-советник** | Перед «Играть» проверяет память, место на диске и незакрытый краш; после краша разбирает лог по правилам (встроенным и присланным сервером), объясняет причину и предлагает кнопку-исправление, повторные краши эскалирует |
| 🔄 **Самообновление** | electron-updater + отдельный `VoidRpLauncher.Updater` |
| 🎨 **Интерфейс** | Темы, новости, нации, рейтинги, карта, список модов, предложение модов, обратная связь |

---

## 🏗️ Три процесса

```mermaid
flowchart TB
    subgraph R["1 · Renderer — src/"]
        R1["Vue 3 · TypeScript · Pinia · Tailwind v4"]
        R2["stores/launcher.ts — состояние"]
        R3["window.desktopBridge.request()"]
    end
    subgraph E["2 · Electron main — electron/main.ts"]
        E1["BrowserWindow"]
        E2["запуск CoreHost"]
        E3["IPC ↔ HTTP-прокси"]
    end
    subgraph C["3 · CoreHost — core/ (.NET 8, ASP.NET minimal API, :38765)"]
        C1["LauncherFacadeService · AuthenticatedLaunchService"]
        C2["FileSync · RuntimeBootstrap · CmlLib"]
        C3["CrashAdvisor · CrashRuleService · Preflight"]
        C4["PlayerConfigSync · GameLocation · ServerCatalog"]
    end
    R -- "IPC" --> E -- "HTTP 127.0.0.1:38765" --> C
```

Если ядро не поднялось, заставка показывает причину, даёт повторить и скопировать отчёт; до старта
лаунчер ждёт, пока ядро откроет порт.

---

## 🔄 Синхронизация файлов (CoreHost)

Лаунчер сверяет файлы сервера с манифестом пака (его генерирует бэкенд, URL лежит в `game_servers`)
и качает **до 8 файлов параллельно** (`Parallel.ForEachAsync`, HTTP/2). Кэш SHA-256 (`hash-cache.json`,
ключ «путь + размер + mtime») не пересчитывает хеши без нужды.

```mermaid
flowchart LR
    A["📄 Файл<br/>из манифеста"] --> B{"Есть<br/>локально?"}
    B -- нет --> DL["⬇️ Скачать"]
    B -- да --> C{"alwaysOverwrite?"}
    C -- да --> DL
    C -- нет --> E{"config/<br/>и не managed?"}
    E -- да --> F{"Поменяли в паке<br/>после доставки<br/>этому клиенту?"}
    F -- да --> G["⬇️ Новая версия<br/>копия игрока →<br/>config-backups"]
    F -- нет --> KEEP["✋ Оставить<br/>копию игрока"]
    E -- нет --> H{"Файл игрока?<br/>options.txt · servers.dat<br/>resourcepacks · shaderpacks"}
    H -- да --> KEEP
    H -- нет --> I{"Размер и SHA-256<br/>совпадают?"}
    I -- нет --> DL
    I -- да --> OK["✅ Актуален"]
```

- `alwaysOverwrite` — мелкие конфиги, которые пак навязывает всегда (например, layout'ы fancymenu).
- `managed` — статичные ассеты под `config/`: сверяются по хешу, поэтому обновления доходят, а неизменное не перекачивается.
- Для каждого клиента хранится хеш версии каждого `config/`-файла, которую он получил. Без такой записи
  (первая синхронизация новой версией лаунчера) побеждает локальная копия.
- Файлы, пропавшие из манифеста, и посторонние моды убираются в резервную копию, а не удаляются насовсем.

---

## 📋 Требования

| Компонент | Версия |
|---|---|
| Node.js | 22 (как в CI) |
| .NET SDK | 8.0 |
| npm | идёт с Node |

---

## 🚀 Разработка

```bash
npm ci
npm run dev                     # renderer (Vite :5177) + CoreHost + Electron
```

| Команда | Что делает |
|---|---|
| `npm run build:core:dev:linux` | CoreHost (Debug, Linux) |
| `npm run build:electron` | Electron main process |
| `npm run build:renderer` | Renderer (Vite) |
| `npm run build:linux` | Релизная сборка под Linux |

Релизные сборки — `build-release-linux.sh` и `build-release-win-portable.ps1`.

---

## 🔗 Связанные репозитории

| Репо | Связь |
|---|---|
| [minecraft-backend](https://github.com/VOIDRP-MINECRAFT/minecraft-backend) | Аккаунт, play-ticket, каталог серверов, правила крашей, настройки игры, манифесты |
| [voidrp-auth-bridge](https://github.com/VOIDRP-MINECRAFT/voidrp-auth-bridge) | Принимает билет на модовом сервере |
| [voidrp-auth-plugin](https://github.com/VOIDRP-MINECRAFT/voidrp-auth-plugin) | Принимает билет на плагинном сервере |
| [voidrp-launcher-java](https://github.com/VOIDRP-MINECRAFT/voidrp-launcher-java) | Запасной лаунчер (JavaFX) |

---

<div align="center">
<a href="https://void-rp.ru">🌐 Сайт</a> ·
<a href="https://github.com/VOIDRP-MINECRAFT">🏠 Организация</a>
</div>
