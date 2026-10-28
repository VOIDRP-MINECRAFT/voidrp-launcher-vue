import { app, BrowserWindow, dialog, ipcMain, session, shell } from 'electron'

// Required for WebGL to work correctly in webview on all GPUs
app.commandLine.appendSwitch('ignore-gpu-blacklist')
app.commandLine.appendSwitch('enable-gpu-rasterization')
import { spawn, spawnSync, type ChildProcessWithoutNullStreams } from 'node:child_process'
import { createHash } from 'node:crypto'
import fs from 'node:fs'
import net from 'node:net'
import os from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)

const CORE_HOST = '127.0.0.1'
const CORE_PREFERRED_PORT = 38765
// 38765 unless it is taken (another program, or reserved by Windows for Hyper-V/WSL); then
// prepareCorePort() picks a free one and the renderer gets it through the preload.
let corePort = CORE_PREFERRED_PORT
const coreBaseUrl = () => `http://${CORE_HOST}:${corePort}`
// How long the start-up update check may hold the window back.
const UPDATE_MANIFEST_TIMEOUT_MS = 8000
// A self-update download that stops receiving data for this long fails instead of hanging.
const UPDATE_DOWNLOAD_IDLE_TIMEOUT_MS = 30000

// A second copy would start a second core and both would fight over the port. Players double-click
// again while the portable exe is still unpacking; bring the open window forward instead.
const isPrimaryInstance = app.requestSingleInstanceLock()
if (!isPrimaryInstance) {
  app.quit()
} else {
  app.on('second-instance', () => {
    if (!mainWindow || mainWindow.isDestroyed()) return
    if (mainWindow.isMinimized()) mainWindow.restore()
    mainWindow.show()
    mainWindow.focus()
  })
}
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
  exitCode: number | null
  logTail: string[]
}

type UpdaterStatusPayload = {
  available: boolean
  downloading: boolean
  downloaded: boolean
  progressPercent: number
  message: string
  notes?: string
  version?: string
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
  notes?: string
  launcher: LauncherUpdateArtifact
  updater: LauncherUpdateArtifact
}

let mainWindow: BrowserWindow | null = null
let coreProcess: ChildProcessWithoutNullStreams | null = null
let coreStartPromise: Promise<void> | null = null
// Port choice + first spawn, started at launch; everything that talks to the core waits for it.
let coreBootPromise: Promise<void> | null = null
let lastCoreError = ''
let lastCoreExitCode: number | null = null
// Last lines the core printed: shown on the splash when it fails, so a player can paste the real reason.
const coreLogTail: string[] = []
function pushCoreLog(line: string) {
  for (const l of line.split(/\r?\n/)) {
    if (!l.trim()) continue
    coreLogTail.push(l.length > 400 ? l.slice(0, 400) + '…' : l)
  }
  if (coreLogTail.length > 40) coreLogTail.splice(0, coreLogTail.length - 40)
}
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

// Packaged on Windows, console output goes nowhere: without this file a launcher that fails to
// start leaves no trace. The boot report sends its tail (see describeBootError in the renderer).
let mainLogPath: string | null = null
function resolveMainLogPath(): string {
  if (mainLogPath !== null) return mainLogPath
  try {
    const dir = path.join(app.getPath('userData'), 'logs')
    fs.mkdirSync(dir, { recursive: true })
    mainLogPath = path.join(dir, 'main.log')
    const stat = fs.statSync(mainLogPath, { throwIfNoEntry: false })
    if (stat && stat.size > 1_000_000) fs.renameSync(mainLogPath, path.join(dir, 'main.old.log'))
  } catch {
    mainLogPath = ''
  }
  return mainLogPath
}

function logMain(message: string) {
  console.log(`[VoidRP Main] ${message}`)
  const logPath = resolveMainLogPath()
  if (!logPath) return
  try {
    fs.appendFileSync(logPath, `[${new Date().toISOString()}] ${message}\n`)
  } catch {
    // a log we cannot write must not stop the launcher
  }
}

function readMainLogTail(maxLines = 80): string {
  const logPath = resolveMainLogPath()
  if (!logPath) return ''
  try {
    const stat = fs.statSync(logPath)
    const length = Math.min(stat.size, 32 * 1024)
    const buffer = Buffer.alloc(length)
    const fd = fs.openSync(logPath, 'r')
    try {
      fs.readSync(fd, buffer, 0, length, stat.size - length)
    } finally {
      fs.closeSync(fd)
    }
    return buffer.toString('utf-8').split(/\r?\n/).filter(Boolean).slice(-maxLines).join('\n')
  } catch {
    return ''
  }
}

function describeSystem(): string {
  // os.version() names the edition ("Windows 11 Pro"), os.release() the build ("10.0.26200").
  const name = typeof os.version === 'function' ? os.version() : os.type()
  return `${name} ${os.release()} ${process.arch}`
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
    baseUrl: coreBaseUrl(),
    executablePath: resolveCoreExecutablePath(),
    lastError: lastCoreError,
    exitCode: lastCoreExitCode,
    logTail: [...coreLogTail]
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
  // Abort when no data arrives for a while: a stalled connection used to leave the update
  // window at the same percent forever, and the launcher never opened.
  const controller = new AbortController()
  let idleTimer = setTimeout(() => controller.abort(), UPDATE_DOWNLOAD_IDLE_TIMEOUT_MS)
  const touch = () => {
    clearTimeout(idleTimer)
    idleTimer = setTimeout(() => controller.abort(), UPDATE_DOWNLOAD_IDLE_TIMEOUT_MS)
  }

  try {
    const response = await fetch(url, { signal: controller.signal })
    if (!response.ok || !response.body) {
      throw new Error(`Download failed (${response.status}) for ${url}`)
    }

    const chunks: Buffer[] = []
    const reader = response.body.getReader()
    while (true) {
      const { done, value } = await reader.read()
      if (done) break
      touch()
      chunks.push(Buffer.from(value))
    }
    await fs.promises.writeFile(targetPath, Buffer.concat(chunks))
  } catch (error) {
    if (controller.signal.aborted) {
      throw new Error(`Скачивание обновления остановилось: ${UPDATE_DOWNLOAD_IDLE_TIMEOUT_MS / 1000} с без данных (${url}).`)
    }
    throw error
  } finally {
    clearTimeout(idleTimer)
  }
}

async function fetchUpdateManifest(manifestUrl: string): Promise<LauncherUpdateManifest> {
  const separator = manifestUrl.includes('?') ? '&' : '?'
  let response: Response
  try {
    // The window waits for this check; a slow or blocked server must not keep it hidden.
    response = await fetch(`${manifestUrl}${separator}t=${Date.now()}`, {
      signal: AbortSignal.timeout(UPDATE_MANIFEST_TIMEOUT_MS)
    })
  } catch (error) {
    if (error instanceof Error && (error.name === 'TimeoutError' || error.name === 'AbortError')) {
      throw new Error(`Сервер обновлений не ответил за ${UPDATE_MANIFEST_TIMEOUT_MS / 1000} с.`)
    }
    throw error
  }
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
    notes: manifest.notes,
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
      message: `Найдена новая версия ${update.version}. Скачиваем обновление лаунчера...`,
      notes: update.notes
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

    // app.exit() below skips before-quit, so the core would outlive the launcher, keep the port
    // and leave the updated launcher talking to the old core.
    await stopCoreProcess()

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
    // Seed the status (incl. release notes) so the premium update window shows
    // "what's new" the instant it finishes loading, then open it and download.
    emitUpdaterStatus({
      available: true,
      downloading: true,
      downloaded: false,
      progressPercent: 5,
      message: `Найдена новая версия ${resolved.version}. Готовим обновление…`,
      notes: resolved.notes,
      version: resolved.version
    })
    createWindow(true)
    await installShellUpdate(resolved)
    return true
  } catch (error) {
    logMain(`Startup update check failed: ${error instanceof Error ? error.message : String(error)}`)
    // The update window may already be open; the main window takes its place, not a second one.
    if (mainWindow && !mainWindow.isDestroyed()) {
      mainWindow.destroy()
      mainWindow = null
    }
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

function createWindow(updateMode = false) {
  const iconPath = resolveWindowIcon()
  mainWindow = new BrowserWindow({
    width: updateMode ? 620 : 1280,
    height: updateMode ? 720 : 860,
    minWidth: updateMode ? 620 : 1180,
    minHeight: updateMode ? 720 : 760,
    resizable: !updateMode,
    maximizable: !updateMode,
    center: true,
    frame: !updateMode,
    autoHideMenuBar: true,
    backgroundColor: '#050608',
    ...(iconPath ? { icon: iconPath } : {}),
    webPreferences: {
      preload: resolvePreloadPath(),
      // The core's address (the port may not be 38765), read by the preload.
      additionalArguments: [`--voidrp-core-url=${coreBaseUrl()}`],
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false,
      webviewTag: true
    }
  })

  // Update-mode window renders a dedicated premium updater screen (boot=update)
  // instead of booting the full app — see App.vue / UpdateScreen.vue.
  const query = updateMode ? { search: 'boot=update' } : undefined
  if (app.isPackaged) {
    void mainWindow.loadFile(resolveRendererIndexPath(), query)
  } else {
    void mainWindow.loadURL(`http://127.0.0.1:5177/${updateMode ? '?boot=update' : ''}`)
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
    // On Windows this is almost always an antivirus quarantining the unsigned core exe.
    lastCoreError = `CoreHost exe not found: ${executablePath}`
    throw new Error(lastCoreError)
  }

  lastCoreError = ''
  lastCoreExitCode = null
  logMain(`Starting core: ${executablePath}`)

  if (process.platform !== 'win32') {
    try { fs.chmodSync(executablePath, 0o755) } catch { /* ignore */ }
  }

  coreProcess = spawn(executablePath, [], {
    cwd: path.dirname(executablePath),
    env: {
      ...process.env,
      ASPNETCORE_URLS: coreBaseUrl(),
      // The core watches this pid and stops when the launcher is gone (crash, Task Manager).
      VOIDRP_PARENT_PID: String(process.pid),
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
    pushCoreLog(text)
    if (coreReadyResolve && text.includes('Now listening on:')) {
      coreReadyResolve()
      coreReadyResolve = null
    }
  })

  coreProcess.stderr.on('data', (chunk) => {
    const text = chunk.toString().trim()
    if (!text) return
    lastCoreError = text
    pushCoreLog(text)
    logMain(`Core stderr: ${text}`)
    emitCoreStatus()
  })

  coreProcess.on('exit', (code, signal) => {
    logMain(`Core exited. code=${code ?? 'null'} signal=${signal ?? 'null'}`)

    lastCoreExitCode = code
    if (code !== 0) {
      lastCoreError = `CoreHost exited with code ${code ?? 'null'}${signal ? ` (${signal})` : ''}`
      pushCoreLog(lastCoreError)
    }

    coreProcess = null
    emitCoreStatus()
  })

  coreProcess.on('error', (error) => {
    lastCoreError = `Core spawn error: ${error.message}`
    pushCoreLog(lastCoreError)
    logMain(`Core spawn error: ${error.message}`)
    emitCoreStatus()
  })

  emitCoreStatus()
}

async function readCoreHealth(port: number): Promise<{ pid?: number } | null> {
  try {
    const response = await fetch(`http://${CORE_HOST}:${port}/health`, { method: 'GET', signal: AbortSignal.timeout(1500) })
    if (!response.ok) return null
    return (await response.json().catch(() => ({}))) as { pid?: number }
  } catch {
    return null
  }
}

async function pingCore(): Promise<boolean> {
  const health = await readCoreHealth(corePort)
  if (!health) return false
  // Cores since 4.0.56 say their pid: only our own child counts, not a stale one on the port.
  if (typeof health.pid === 'number' && health.pid !== coreProcess?.pid) return false
  return true
}

function isPortFree(port: number): Promise<boolean> {
  return new Promise((resolve) => {
    const server = net.createServer()
    server.once('error', () => resolve(false))
    server.once('listening', () => server.close(() => resolve(true)))
    server.listen(port, CORE_HOST)
  })
}

function findFreePort(): Promise<number> {
  return new Promise((resolve, reject) => {
    const server = net.createServer()
    server.once('error', reject)
    server.listen(0, CORE_HOST, () => {
      const address = server.address()
      server.close(() => (address && typeof address === 'object' ? resolve(address.port) : reject(new Error('No free port'))))
    })
  })
}

function killStaleCore(pid: number | undefined) {
  try {
    if (typeof pid === 'number') {
      process.kill(pid)
    } else if (process.platform === 'win32') {
      // Cores before 4.0.56 do not report their pid. The single-instance lock means no other
      // launcher of ours is open, so any core still running is a leftover.
      spawnSync('taskkill', ['/F', '/IM', resolveCoreExeName()], { windowsHide: true, timeout: 5000 })
    } else {
      spawnSync('pkill', ['-f', resolveCoreExeName()], { timeout: 5000 })
    }
  } catch (error) {
    logMain(`Could not stop the stale core: ${error instanceof Error ? error.message : String(error)}`)
  }
}

async function prepareCorePort(): Promise<void> {
  if (await isPortFree(CORE_PREFERRED_PORT)) {
    corePort = CORE_PREFERRED_PORT
    return
  }

  // Something holds our port. A core answering there was left by an earlier run (an update
  // exit, a crash): the new core could not bind and the window would talk to the old one.
  const stale = await readCoreHealth(CORE_PREFERRED_PORT)
  if (stale) {
    logMain(`Port ${CORE_PREFERRED_PORT} is held by a stale core (pid ${stale.pid ?? 'unknown'}); stopping it.`)
    killStaleCore(stale.pid)
    for (let attempt = 0; attempt < 20; attempt += 1) {
      if (await isPortFree(CORE_PREFERRED_PORT)) {
        corePort = CORE_PREFERRED_PORT
        return
      }
      await new Promise((r) => setTimeout(r, 150))
    }
  }

  corePort = await findFreePort()
  logMain(`Port ${CORE_PREFERRED_PORT} is busy (${stale ? 'the stale core did not stop' : 'another program, or reserved by Windows'}); the core uses ${corePort}.`)
}

async function stopCoreProcess(timeoutMs = 3000): Promise<void> {
  const child = coreProcess
  if (!child || child.exitCode !== null) return
  const exited = new Promise<void>((resolve) => child.once('exit', () => resolve()))
  try {
    child.kill()
  } catch {
    return
  }
  await Promise.race([exited, new Promise((r) => setTimeout(r, timeoutMs))])
}

async function waitForCoreReady(timeoutMs = 90000): Promise<void> {
  const startedAt = Date.now()

  // Race stdout signal against polling fallback
  const timeout = new Promise<never>((_, reject) =>
    setTimeout(() => reject(new Error(lastCoreError || 'CoreHost did not become ready in time.')), timeoutMs))

  const pollUntilReady = async () => {
    let delay = 50
    while (true) {
      if (await pingCore()) return
      if (!coreProcess) throw new Error(lastCoreError || 'CoreHost exited before it became ready.')
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

  if (coreBootPromise) {
    await coreBootPromise
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

  const response = await fetch(`${coreBaseUrl()}${normalizedPath}`, {
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

  // For the boot report a player's launcher sends when it cannot start.
  ipcMain.handle('desktop:get-boot-info', async () => ({
    version: app.getVersion(),
    os: describeSystem(),
    mainLogTail: readMainLogTail()
  }))

  ipcMain.handle('desktop:open-external', async (_event, url?: string) => {
    if (!url) return false
    await shell.openExternal(url)
    return true
  })

  // Folder picker for «Папка с файлами игры». Returns '' when the player cancels.
  ipcMain.handle('desktop:select-directory', async (event, defaultPath?: string) => {
    const owner = BrowserWindow.fromWebContents(event.sender) ?? undefined
    const options: Electron.OpenDialogOptions = {
      title: 'Папка для файлов игры',
      defaultPath: defaultPath || undefined,
      properties: ['openDirectory', 'createDirectory', 'promptToCreate'],
    }
    const result = owner ? await dialog.showOpenDialog(owner, options) : await dialog.showOpenDialog(options)
    return result.canceled || result.filePaths.length === 0 ? '' : result.filePaths[0]
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
  if (!isPrimaryInstance) return
  logMain(`Launcher ${app.getVersion()} starting on ${describeSystem()}.`)
  registerIpcHandlers()

  // Allow all permission requests from webviews (needed for WebGL/GPU in map view)
  session.defaultSession.setPermissionRequestHandler((_wc, _permission, callback) => {
    callback(true)
  })

  // Boot CoreHost immediately so it warms up while the update manifest is being fetched.
  // The port is settled first: the window gets the core's address when it is created.
  coreBootPromise = (async () => {
    try {
      await prepareCorePort()
      startCoreProcess()
    } catch (error) {
      // Will retry inside ensureCoreRunning if the exe is missing or fails
      logMain(`Core start failed: ${error instanceof Error ? error.message : String(error)}`)
    }
  })()

  const autoUpdateStarted = await runStartupAutoUpdate()
  if (autoUpdateStarted) {
    return
  }

  await coreBootPromise
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
