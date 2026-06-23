import { app, BrowserWindow, ipcMain, session, shell } from 'electron'

// Required for WebGL to work correctly in webview on all GPUs
app.commandLine.appendSwitch('ignore-gpu-blacklist')
app.commandLine.appendSwitch('enable-gpu-rasterization')
import { spawn, type ChildProcessWithoutNullStreams } from 'node:child_process'
import { createHash } from 'node:crypto'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

const CORE_HOST = '127.0.0.1'
const CORE_PORT = 38765
const CORE_BASE_URL = `http://${CORE_HOST}:${CORE_PORT}`
const DEFAULT_SELF_UPDATE_MANIFEST_URL = 'https://void-rp.ru/launcher/self-update/manifest.json'

type DesktopRequestPayload = {
  method: string
  path: string
  body?: unknown
}

type CoreStatusPayload = {
  running: boolean
  pid: number | null
  baseUrl: string
  executablePath: string
  lastError: string
}

type UpdaterStatusPayload = {
  available: boolean
  downloading: boolean
  downloaded: boolean
  progressPercent: number
  message: string
}

type LauncherUpdateArtifact = {
  kind: string
  url: string
  sha256: string
}

type LauncherUpdatePlatformArtifacts = {
  launcher: LauncherUpdateArtifact
  updater: LauncherUpdateArtifact
}

type LauncherUpdateManifest = {
  version: string
  notes?: string
  artifacts?: Record<string, LauncherUpdatePlatformArtifacts>
}

type PendingShellUpdate = {
  version: string
  launcher: LauncherUpdateArtifact
  updater: LauncherUpdateArtifact
}

let mainWindow: BrowserWindow | null = null
let coreProcess: ChildProcessWithoutNullStreams | null = null
let coreStartPromise: Promise<void> | null = null
let lastCoreError = ''
let coreReadyResolve: (() => void) | null = null
const coreReadyPromise: Promise<void> = new Promise(resolve => { coreReadyResolve = resolve })
let shellUpdaterStatus: UpdaterStatusPayload = {
  available: false,
  downloading: false,
  downloaded: false,
  progressPercent: 0,
  message: 'Проверка обновлений оболочки ещё не выполнялась.'
}
let pendingShellUpdate: PendingShellUpdate | null = null
let shellUpdateInProgress = false

function logMain(message: string) {
  console.log(`[VoidRP Main] ${message}`)
}

function emitCoreStatus() {
  if (!mainWindow || mainWindow.isDestroyed()) return
  mainWindow.webContents.send('desktop:core-status', getCoreStatus())
}

function emitUpdaterStatus(partial?: Partial<UpdaterStatusPayload>) {
  shellUpdaterStatus = {
    ...shellUpdaterStatus,
    ...partial
  }

  if (!mainWindow || mainWindow.isDestroyed()) return
  mainWindow.webContents.send('desktop:updater-status', shellUpdaterStatus)
}

function getCoreStatus(): CoreStatusPayload {
  return {
    running: !!coreProcess && coreProcess.exitCode === null,
    pid: coreProcess?.pid ?? null,
    baseUrl: CORE_BASE_URL,
    executablePath: resolveCoreExecutablePath(),
    lastError: lastCoreError
  }
}

function resolveCoreRid(): string {
  if (process.platform === 'win32') return 'win-x64'
  if (process.platform === 'darwin') return process.arch === 'arm64' ? 'osx-arm64' : 'osx-x64'
  return process.arch === 'arm64' ? 'linux-arm64' : 'linux-x64'
}

function resolveCoreExeName(): string {
  return process.platform === 'win32' ? 'VoidRpLauncher.CoreHost.exe' : 'VoidRpLauncher.CoreHost'
}

function resolveRendererIndexPath(): string {
  if (app.isPackaged) {
    return path.join(app.getAppPath(), 'dist-renderer', 'index.html')
  }

  return path.join(process.cwd(), 'dist-renderer', 'index.html')
}

function resolvePreloadPath(): string {
  if (app.isPackaged) {
    return path.join(app.getAppPath(), 'dist-electron', 'preload.cjs')
  }

  return path.join(__dirname, 'preload.cjs')
}

function resolveCoreExecutablePath(): string {
  const rid = resolveCoreRid()
  const exeName = resolveCoreExeName()

  const candidates = app.isPackaged
    ? [
        path.join(process.resourcesPath, 'core', rid, exeName)
      ]
    : [
        path.join(process.cwd(), 'build', 'core', rid, exeName),
        path.join(process.cwd(), 'core', 'VoidRpLauncher.CoreHost', 'bin', 'Debug', `net8.0`, rid, exeName),
        path.join(process.cwd(), 'core', 'VoidRpLauncher.CoreHost', 'bin', 'Release', `net8.0`, rid, exeName)
      ]

  for (const candidate of candidates) {
    if (fs.existsSync(candidate)) return candidate
  }

  return candidates[0]
}

function resolveWindowIcon(): string | undefined {
  try {
    const iconFile = process.platform === 'win32' ? 'icon.ico' : 'icon.png'
    if (app.isPackaged) {
      return path.join(process.resourcesPath, 'icons', iconFile)
    }
    // Dev: __dirname = dist-electron/, go up to project root
    return path.join(__dirname, '..', 'build-resources', iconFile)
  } catch {
    return undefined
  }
}

function resolvePortableLauncherTargetPath(): string {
  const portableExecutable = process.env.PORTABLE_EXECUTABLE_FILE
  if (portableExecutable && fs.existsSync(portableExecutable)) {
    return portableExecutable
  }

  return process.execPath
}

function resolveAppSettingsPath(): string | null {
  const rid = resolveCoreRid()
  const candidates = app.isPackaged
    ? [
        path.join(process.resourcesPath, 'core', rid, 'appsettings.json')
      ]
    : [
        path.join(process.cwd(), 'core', 'VoidRpLauncher.CoreHost', 'appsettings.json')
      ]

  for (const candidate of candidates) {
    if (fs.existsSync(candidate)) return candidate
  }

  return null
}

function resolveSelfUpdateManifestUrl(): string {
  const appSettingsPath = resolveAppSettingsPath()
  if (!appSettingsPath) return DEFAULT_SELF_UPDATE_MANIFEST_URL

  try {
    const parsed = JSON.parse(fs.readFileSync(appSettingsPath, 'utf-8'))
    const url = parsed?.Launcher?.SelfUpdateManifestUrl
    if (typeof url === 'string' && url.trim()) {
      return url.trim()
    }
  } catch (error) {
    logMain(`Failed to read self-update url from appsettings: ${String(error)}`)
  }

  return DEFAULT_SELF_UPDATE_MANIFEST_URL
}

function detectUpdateRid(): string {
  if (process.platform === 'win32' && process.arch === 'x64') return 'win-x64'
  if (process.platform === 'darwin' && process.arch === 'arm64') return 'osx-arm64'
  if (process.platform === 'darwin' && process.arch === 'x64') return 'osx-x64'
  if (process.platform === 'linux' && process.arch === 'x64') return 'linux-x64'
  if (process.platform === 'linux' && process.arch === 'arm64') return 'linux-arm64'
  throw new Error(`Unsupported platform for self-update: ${process.platform}/${process.arch}`)
}

function compareVersions(left: string, right: string): number {
  const normalize = (value: string) =>
    value
      .split('-')[0]
      .split('.')
      .map((part) => Number.parseInt(part, 10) || 0)

  const a = normalize(left)
  const b = normalize(right)
  const max = Math.max(a.length, b.length)

  for (let index = 0; index < max; index += 1) {
    const av = a[index] ?? 0
    const bv = b[index] ?? 0
    if (av > bv) return 1
    if (av < bv) return -1
  }

  return 0
}

function ensureDirectory(dirPath: string) {
  fs.mkdirSync(dirPath, { recursive: true })
}

function computeSha256(filePath: string): string {
  const hash = createHash('sha256')
  hash.update(fs.readFileSync(filePath))
  return hash.digest('hex').toUpperCase()
}

async function downloadToFile(url: string, targetPath: string) {
  const response = await fetch(url)
  if (!response.ok) {
    throw new Error(`Download failed (${response.status}) for ${url}`)
  }

  const buffer = Buffer.from(await response.arrayBuffer())
  await fs.promises.writeFile(targetPath, buffer)
}

async function fetchUpdateManifest(manifestUrl: string): Promise<LauncherUpdateManifest> {
  const separator = manifestUrl.includes('?') ? '&' : '?'
  const response = await fetch(`${manifestUrl}${separator}t=${Date.now()}`)
  if (!response.ok) {
    throw new Error(`Self-update manifest request failed with status ${response.status}.`)
  }

  return response.json() as Promise<LauncherUpdateManifest>
}

async function resolvePendingShellUpdate(): Promise<PendingShellUpdate | null> {
  const manifestUrl = resolveSelfUpdateManifestUrl()
  const manifest = await fetchUpdateManifest(manifestUrl)
  const rid = detectUpdateRid()
  const platformArtifacts = manifest.artifacts?.[rid]

  if (!platformArtifacts?.launcher?.url || !platformArtifacts?.updater?.url) {
    logMain(`No self-update artifacts for ${rid}.`)
    return null
  }

  if (compareVersions(manifest.version, app.getVersion()) <= 0) {
    return null
  }

  return {
    version: manifest.version,
    launcher: platformArtifacts.launcher,
    updater: platformArtifacts.updater
  }
}

async function installShellUpdate(update: PendingShellUpdate) {
  if (shellUpdateInProgress) {
    return
  }

  shellUpdateInProgress = true

  try {
    const workDir = path.join(os.tmpdir(), 'VoidRpLauncher', 'self-update')
    ensureDirectory(workDir)

    const launcherTarget = resolvePortableLauncherTargetPath()
    const exeSuffix = process.platform === 'win32' ? '.exe' : ''
    const launcherDownloadPath = path.join(workDir, `VoidRpLauncher.update${exeSuffix}`)
    const updaterDownloadPath = path.join(workDir, `VoidRpLauncher.Updater.update${exeSuffix}`)

    emitUpdaterStatus({
      available: true,
      downloading: true,
      downloaded: false,
      progressPercent: 10,
      message: `Найдена новая версия ${update.version}. Скачиваем обновление лаунчера...`
    })

    await downloadToFile(update.launcher.url, launcherDownloadPath)

    if (update.launcher.sha256?.trim()) {
      const actualSha = computeSha256(launcherDownloadPath)
      if (actualSha !== update.launcher.sha256.trim().toUpperCase()) {
        throw new Error(`SHA-256 launcher update mismatch. Expected ${update.launcher.sha256}, got ${actualSha}.`)
      }
    }

    emitUpdaterStatus({
      progressPercent: 55,
      message: 'Скачиваем модуль обновления...'
    })

    await downloadToFile(update.updater.url, updaterDownloadPath)

    if (update.updater.sha256?.trim()) {
      const actualSha = computeSha256(updaterDownloadPath)
      if (actualSha !== update.updater.sha256.trim().toUpperCase()) {
        throw new Error(`SHA-256 updater mismatch. Expected ${update.updater.sha256}, got ${actualSha}.`)
      }
    }

    emitUpdaterStatus({
      progressPercent: 85,
      message: 'Подготавливаем установку обновления...'
    })

    const args = [
      '--pid', String(process.pid),
      '--source', launcherDownloadPath,
      '--target', launcherTarget,
      '--source-kind', 'single-file',
      '--restart', 'start-file'
    ]

    if (process.platform !== 'win32') {
      try { fs.chmodSync(updaterDownloadPath, 0o755) } catch { /* ignore */ }
    }

    const child = spawn(updaterDownloadPath, args, {
      detached: true,
      stdio: 'ignore',
      ...(process.platform === 'win32' ? { windowsHide: true } : {})
    })

    child.unref()

    emitUpdaterStatus({
      downloading: false,
      downloaded: true,
      progressPercent: 100,
      message: `Обновление ${update.version} скачано. Перезапускаем лаунчер...`
    })

    setTimeout(() => {
      app.exit(0)
    }, 180)
  } catch (error) {
    shellUpdateInProgress = false
    pendingShellUpdate = null
    emitUpdaterStatus({
      available: false,
      downloading: false,
      downloaded: false,
      progressPercent: 0,
      message: error instanceof Error ? error.message : 'Не удалось установить обновление оболочки.'
    })
    throw error
  }
}

async function runStartupAutoUpdate(): Promise<boolean> {
  try {
    const resolved = await resolvePendingShellUpdate()
    if (!resolved) {
      pendingShellUpdate = null
      shellUpdateInProgress = false
      emitUpdaterStatus({
        available: false,
        downloading: false,
        downloaded: false,
        progressPercent: 0,
        message: 'У вас уже установлена актуальная версия лаунчера.'
      })
      return false
    }

    pendingShellUpdate = resolved
    await installShellUpdate(resolved)
    return true
  } catch (error) {
    logMain(`Startup update check failed: ${error instanceof Error ? error.message : String(error)}`)
    emitUpdaterStatus({
      available: false,
      downloading: false,
      downloaded: false,
      progressPercent: 0,
      message: error instanceof Error ? `Не удалось проверить обновления: ${error.message}` : 'Не удалось проверить обновления.'
    })
    return false
  }
}

function createWindow() {
  const iconPath = resolveWindowIcon()
  mainWindow = new BrowserWindow({
    width: 1280,
    height: 860,
    minWidth: 1180,
    minHeight: 760,
    autoHideMenuBar: true,
    backgroundColor: '#050608',
    ...(iconPath ? { icon: iconPath } : {}),
    webPreferences: {
      preload: resolvePreloadPath(),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false,
      webviewTag: true
    }
  })

  if (app.isPackaged) {
    void mainWindow.loadFile(resolveRendererIndexPath())
  } else {
    void mainWindow.loadURL('http://127.0.0.1:5177')
  }

  mainWindow.webContents.on('did-finish-load', () => {
    emitUpdaterStatus()
    emitCoreStatus()
  })

  mainWindow.on('closed', () => {
    mainWindow = null
  })
}

function startCoreProcess() {
  const executablePath = resolveCoreExecutablePath()

  if (!fs.existsSync(executablePath)) {
    throw new Error(`CoreHost exe not found: ${executablePath}`)
  }

  lastCoreError = ''
  logMain(`Starting core: ${executablePath}`)

  if (process.platform !== 'win32') {
    try { fs.chmodSync(executablePath, 0o755) } catch { /* ignore */ }
  }

  coreProcess = spawn(executablePath, [], {
    cwd: path.dirname(executablePath),
    env: {
      ...process.env,
      ASPNETCORE_URLS: CORE_BASE_URL,
      DOTNET_ENVIRONMENT: 'Production',
      ASPNETCORE_ENVIRONMENT: 'Production',
      VOIDRP_LAUNCHER_VERSION: app.getVersion()
    },
    windowsHide: true,
    stdio: 'pipe'
  })

  coreProcess.stdout.on('data', (chunk) => {
    const text = chunk.toString().trim()
    if (!text) return
    logMain(`Core stdout: ${text}`)
    if (coreReadyResolve && text.includes('Now listening on:')) {
      coreReadyResolve()
      coreReadyResolve = null
    }
  })

  coreProcess.stderr.on('data', (chunk) => {
    const text = chunk.toString().trim()
    if (!text) return
    lastCoreError = text
    logMain(`Core stderr: ${text}`)
    emitCoreStatus()
  })

  coreProcess.on('exit', (code, signal) => {
    logMain(`Core exited. code=${code ?? 'null'} signal=${signal ?? 'null'}`)

    if (code !== 0) {
      lastCoreError = `CoreHost exited with code ${code ?? 'null'}`
    }

    coreProcess = null
    emitCoreStatus()
  })

  coreProcess.on('error', (error) => {
    lastCoreError = error.message
    logMain(`Core spawn error: ${error.message}`)
    emitCoreStatus()
  })

  emitCoreStatus()
}

async function pingCore(): Promise<boolean> {
  try {
    const response = await fetch(`${CORE_BASE_URL}/health`, { method: 'GET' })
    return response.ok
  } catch {
    return false
  }
}

async function waitForCoreReady(timeoutMs = 20000): Promise<void> {
  const startedAt = Date.now()

  // Race stdout signal against polling fallback
  const timeout = new Promise<never>((_, reject) =>
    setTimeout(() => reject(new Error('CoreHost did not become ready in time.')), timeoutMs))

  const pollUntilReady = async () => {
    let delay = 50
    while (true) {
      if (await pingCore()) return
      await new Promise(r => setTimeout(r, delay))
      if (delay < 250) delay = Math.min(delay * 2, 250)
      if (Date.now() - startedAt >= timeoutMs) throw new Error('CoreHost did not become ready in time.')
    }
  }

  await Promise.race([coreReadyPromise, pollUntilReady(), timeout])
  emitCoreStatus()
}

async function ensureCoreRunning(): Promise<void> {
  if (shellUpdateInProgress) {
    throw new Error('Идёт обновление оболочки. Дождитесь завершения.')
  }

  if (await pingCore()) {
    return
  }

  if (coreStartPromise) {
    return coreStartPromise
  }

  coreStartPromise = (async () => {
    if (!coreProcess || coreProcess.exitCode !== null) {
      startCoreProcess()
    }

    await waitForCoreReady()
  })().finally(() => {
    coreStartPromise = null
  })

  return coreStartPromise
}

async function proxyCoreRequest<T = unknown>(payload: DesktopRequestPayload): Promise<T> {
  await ensureCoreRunning()

  const method = (payload.method || 'GET').toUpperCase()
  const normalizedPath = payload.path.startsWith('/') ? payload.path : `/${payload.path}`

  const response = await fetch(`${CORE_BASE_URL}${normalizedPath}`, {
    method,
    headers: {
      'Content-Type': 'application/json'
    },
    body: method === 'GET' || method === 'HEAD'
      ? undefined
      : payload.body === undefined
        ? undefined
        : JSON.stringify(payload.body)
  })

  const rawText = await response.text()

  if (!response.ok) {
    throw new Error(rawText || `Core request failed with status ${response.status}`)
  }

  if (!rawText) {
    return null as T
  }

  try {
    return JSON.parse(rawText) as T
  } catch {
    return rawText as T
  }
}

function registerIpcHandlers() {
  ipcMain.handle('desktop:request', async (_event, payload: DesktopRequestPayload) => {
    return proxyCoreRequest(payload)
  })

  ipcMain.handle('desktop:get-core-status', async () => {
    try {
      await ensureCoreRunning()
    } catch {
      // still return status
    }

    return getCoreStatus()
  })

  ipcMain.handle('desktop:open-external', async (_event, url?: string) => {
    if (!url) return false
    await shell.openExternal(url)
    return true
  })

  ipcMain.handle('desktop:open-path', async (_event, targetPath?: string) => {
    if (!targetPath) return ''
    return shell.openPath(targetPath)
  })

  ipcMain.handle('desktop:check-shell-updates', async () => {
    try {
      const resolved = await resolvePendingShellUpdate()
      if (!resolved) {
        pendingShellUpdate = null
        emitUpdaterStatus({
          available: false,
          downloading: false,
          downloaded: false,
          progressPercent: 0,
          message: 'У вас уже установлена актуальная версия лаунчера.'
        })
        return {
          ok: true,
          message: 'У вас уже установлена актуальная версия лаунчера.'
        }
      }

      pendingShellUpdate = resolved
      emitUpdaterStatus({
        available: true,
        downloading: false,
        downloaded: false,
        progressPercent: 0,
        message: `Найдена новая версия ${resolved.version}.`
      })
      return {
        ok: true,
        message: `Найдена новая версия ${resolved.version}.`
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Не удалось проверить обновления оболочки.'
      emitUpdaterStatus({
        available: false,
        downloading: false,
        downloaded: false,
        progressPercent: 0,
        message
      })
      return {
        ok: false,
        message
      }
    }
  })

  ipcMain.handle('desktop:download-install-shell-update', async () => {
    try {
      if (!pendingShellUpdate) {
        const resolved = await resolvePendingShellUpdate()
        if (!resolved) {
          emitUpdaterStatus({
            available: false,
            downloading: false,
            downloaded: false,
            progressPercent: 0,
            message: 'Новых обновлений оболочки не найдено.'
          })
          return {
            ok: true,
            message: 'Новых обновлений оболочки не найдено.'
          }
        }

        pendingShellUpdate = resolved
      }

      void installShellUpdate(pendingShellUpdate)
      return {
        ok: true,
        message: `Запущена установка обновления ${pendingShellUpdate.version}.`
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Не удалось установить обновление оболочки.'
      emitUpdaterStatus({
        available: false,
        downloading: false,
        downloaded: false,
        progressPercent: 0,
        message
      })
      return {
        ok: false,
        message
      }
    }
  })
}

app.whenReady().then(async () => {
  registerIpcHandlers()

  // Allow all permission requests from webviews (needed for WebGL/GPU in map view)
  session.defaultSession.setPermissionRequestHandler((_wc, _permission, callback) => {
    callback(true)
  })

  // Boot CoreHost immediately so it warms up while the update manifest is being fetched
  try {
    startCoreProcess()
  } catch {
    // Will retry inside ensureCoreRunning if the exe is missing or fails
  }

  const autoUpdateStarted = await runStartupAutoUpdate()
  if (autoUpdateStarted) {
    return
  }

  createWindow()

  try {
    await ensureCoreRunning()
  } catch (error) {
    lastCoreError = error instanceof Error ? error.message : String(error)
    emitCoreStatus()
  }

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow()
    }
  })
})

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit()
  }
})

app.on('before-quit', () => {
  if (coreProcess && coreProcess.exitCode === null) {
    try {
      coreProcess.kill()
    } catch {
      // ignore
    }
  }
})
