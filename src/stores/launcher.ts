import { computed, reactive, ref, watch } from 'vue'
import { defineStore } from 'pinia'
import { DEFAULT_THEME_ID, loadThemeId, saveThemeId } from '../theme/themes'

type ToastTone = 'success' | 'warning' | 'error' | 'info'

interface LauncherProgress {
  visible: boolean
  title: string
  details: string
  percent: number
}

interface LauncherLinks {
  registerUrl: string
  forgotPasswordUrl: string
  verifyEmailUrl: string
}

interface LauncherAccountSecurity {
  activeRefreshSessions: number
  mustUseLauncher: boolean
  legacyHashPresent: boolean
  legacyReady: boolean
}

interface LauncherNation {
  id: string
  slug: string
  title: string
  tag: string
  accentColor: string
  role: string
  iconUrl: string
  iconPreviewUrl: string
  bannerUrl: string
  bannerPreviewUrl: string
  backgroundUrl: string
  backgroundPreviewUrl: string
  allianceTitle: string
  allianceTag: string
}

interface LauncherNationStats {
  treasuryBalance: number
  territoryPoints: number
  totalPlaytimeMinutes: number
  pvpKills: number
  mobKills: number
  bossKills: number
  deaths: number
  blocksPlaced: number
  blocksBroken: number
  eventsCompleted: number
  prestigeScore: number
}

interface LauncherPlayerStats {
  minecraftNickname: string
  totalPlaytimeMinutes: number
  pvpKills: number
  mobKills: number
  deaths: number
  blocksPlaced: number
  blocksBroken: number
  currentBalance: number
  completedQuests: number
  source: string
  lastSeenAt: string | null
  lastSyncedAt: string | null
}

interface LauncherDashboard {
  nation: LauncherNation
  nationStats: LauncherNationStats
  playerStats: LauncherPlayerStats
  recentActivity: Array<{ eventType: string; message: string; createdAt: string | null }>
  walletBalance: number
}

interface BattlePassProfile {
  minecraft_uuid: string
  season: string | null
  level: number
  xp: number
  has_premium: boolean
  premium_expires_at: string | null
}

export interface CrashAction {
  index: number
  // fix_files | reset_config | reset_all_configs | repair  → executed by CoreHost
  // open_settings | relaunch | copy_report | show_crash    → handled in the renderer
  type: string
  label: string
  paths: string[]
}

export interface LauncherCrashInfo {
  id: string
  exitCode: number
  exitCodeHex: string
  title: string
  cause: string
  solution: string
  recognized: boolean
  detectedAt: string
  ruleKey?: string | null
  repeatCount: number
  actions: CrashAction[]
}

export interface PreflightWarning {
  id: string
  severity: 'critical' | 'warning' | 'info'
  title: string
  message: string
  actions: CrashAction[]
}

export interface GameLocationInfo {
  currentRoot: string
  defaultRoot: string
  isCustom: boolean
  gameDirectory: string
  sizeBytes: number | null
  freeBytes: number | null
}

export const SERVER_SIDE_CRASH_ACTIONS = new Set(['fix_files', 'reset_config', 'reset_all_configs', 'repair'])

const PREFLIGHT_MUTED_KEY = 'voidrp_preflight_muted_v1'

function loadMutedPreflight(): Set<string> {
  try {
    const raw = localStorage.getItem(PREFLIGHT_MUTED_KEY)
    return new Set(Array.isArray(JSON.parse(raw || '[]')) ? JSON.parse(raw || '[]') : [])
  } catch {
    return new Set()
  }
}

interface LauncherState {
  initialized: boolean
  isBusy: boolean
  isAuthenticated: boolean
  statusText: string
  launcherVersionText: string
  accountPrimaryText: string
  accountSecondaryText: string
  accountIsAdmin: boolean
  emailVerifiedText: string
  currentMemoryMb: number
  currentMemoryText: string
  logsDirectory: string
  dataDirectory: string
  gameDirectory: string
  diagnosticsText: string
  progress: LauncherProgress
  links: LauncherLinks
  security: LauncherAccountSecurity
  dashboard: LauncherDashboard
  lastCrash: LauncherCrashInfo | null
}

interface OperationResponse {
  ok: boolean
  message: string
  pendingElectronExit?: boolean
  state: LauncherState
}

interface SkinState {
  hasSkin: boolean
  modelVariant: string
  skinUrl: string
  headPreviewUrl: string
  bodyPreviewUrl: string
  sha256: string
  width: number
  height: number
  updatedAt: string | null
}

interface SkinOpResponse {
  ok: boolean
  message: string
  skin: SkinState
}

interface ModInfo {
  path: string
  displayName: string
  description: string
  optional: boolean
  required: boolean
  enabled: boolean
}

interface ModListResponse {
  mods: ModInfo[]
}

interface ModToggleResponse {
  ok: boolean
  message: string
  mods: ModInfo[]
}

interface ToastItem {
  id: string
  tone: ToastTone
  title: string
  message: string
}

export const API_BASE = 'http://127.0.0.1:38765'
export const BACKEND_BASE = 'https://api.void-rp.ru/api/v1'
export const SITE_BASE = 'https://void-rp.ru'

function defaultProgress(): LauncherProgress {
  return { visible: false, title: '', details: '', percent: 0 }
}
function defaultLinks(): LauncherLinks {
  return { registerUrl: '', forgotPasswordUrl: '', verifyEmailUrl: '' }
}
function defaultSecurity(): LauncherAccountSecurity {
  return { activeRefreshSessions: 0, mustUseLauncher: false, legacyHashPresent: false, legacyReady: false }
}
function defaultNation(): LauncherNation {
  return {
    id: '', slug: '', title: '', tag: '', accentColor: '', role: '',
    iconUrl: '', iconPreviewUrl: '', bannerUrl: '', bannerPreviewUrl: '',
    backgroundUrl: '', backgroundPreviewUrl: '', allianceTitle: '', allianceTag: '',
  }
}
function defaultNationStats(): LauncherNationStats {
  return {
    treasuryBalance: 0, territoryPoints: 0, totalPlaytimeMinutes: 0, pvpKills: 0, mobKills: 0,
    bossKills: 0, deaths: 0, blocksPlaced: 0, blocksBroken: 0, eventsCompleted: 0, prestigeScore: 0,
  }
}
function defaultPlayerStats(): LauncherPlayerStats {
  return {
    minecraftNickname: '', totalPlaytimeMinutes: 0, pvpKills: 0, mobKills: 0, deaths: 0,
    blocksPlaced: 0, blocksBroken: 0, currentBalance: 0, completedQuests: 0, source: '', lastSeenAt: null, lastSyncedAt: null,
  }
}
function defaultDashboard(): LauncherDashboard {
  return { nation: defaultNation(), nationStats: defaultNationStats(), playerStats: defaultPlayerStats(), recentActivity: [], walletBalance: 0 }
}
function defaultState(): LauncherState {
  return {
    initialized: false,
    isBusy: false,
    isAuthenticated: false,
    statusText: 'Инициализация лаунчера...',
    launcherVersionText: '0.0.0',
    accountPrimaryText: 'Гость',
    accountSecondaryText: 'Войдите, чтобы запустить игру',
    accountIsAdmin: false,
    emailVerifiedText: 'Требуется вход',
    currentMemoryMb: 4096,
    currentMemoryText: '4.0 GB',
    logsDirectory: '',
    dataDirectory: '',
    gameDirectory: '',
    diagnosticsText: '',
    progress: defaultProgress(),
    links: defaultLinks(),
    security: defaultSecurity(),
    dashboard: defaultDashboard(),
    lastCrash: null,
  }
}
function defaultSkin(): SkinState {
  return {
    hasSkin: false, modelVariant: 'classic', skinUrl: '', headPreviewUrl: '',
    bodyPreviewUrl: '', sha256: '', width: 0, height: 0, updatedAt: null,
  }
}
function toastId() {
  return `${Date.now()}_${Math.random().toString(36).slice(2, 8)}`
}

// Извлекает читаемое сообщение об ошибке, скрывая технические детали от пользователя.
export type BootError = { message: string; hint: string; report: string }

type CoreStatus = { running?: boolean; pid?: number | null; executablePath?: string; lastError?: string; exitCode?: number | null; logTail?: string[] }

// Turn a failed start into something a player can act on and paste to support.
async function describeBootError(error: unknown): Promise<BootError> {
  const message = sanitizeError(error)
  let core: CoreStatus = {}
  try {
    core = ((window as any)?.desktop?.getCoreStatus ? await (window as any).desktop.getCoreStatus() : {}) as CoreStatus
  } catch { /* status is best-effort */ }
  const text = `${message}\n${core.lastError ?? ''}\n${(core.logTail ?? []).join('\n')}`
  let hint = 'Перезапусти лаунчер. Если не помогло — нажми «Скопировать отчёт» и отправь его в поддержку.'
  if (/not found|ENOENT/i.test(text)) {
    hint = 'Файл ядра лаунчера пропал — обычно его удаляет антивирус. Открой «Безопасность Windows» → «Журнал защиты», восстанови VoidRpLauncher.CoreHost.exe и добавь папку лаунчера в исключения.'
  } else if (/EACCES|EPERM|UNKNOWN|blocked|заблок/i.test(text)) {
    hint = 'Windows не дала запустить ядро лаунчера. Проверь «Безопасность Windows» → «Журнал защиты» и «Управление приложениями» (Smart App Control), разреши VoidRpLauncher.CoreHost.exe.'
  } else if (/38765|address already in use|access permissions|10013|10048/i.test(text)) {
    hint = 'Порт 38765 занят или зарезервирован Windows (Hyper-V/WSL). Закрой другие копии лаунчера; если не помогло — перезагрузи ПК.'
  }
  const report = [
    `Ошибка: ${message}`,
    core.lastError ? `Ядро: ${core.lastError}` : '',
    `Запущено: ${core.running ? 'да' : 'нет'}, pid ${core.pid ?? '-'}, код выхода ${core.exitCode ?? '-'}`,
    core.executablePath ? `Путь: ${core.executablePath}` : '',
    `Система: ${navigator.userAgent}`,
    core.logTail?.length ? `\nЛог ядра:\n${core.logTail.join('\n')}` : '',
  ].filter(Boolean).join('\n')
  return { message, hint, report }
}

// The core is spawned by the main process at the same time the window opens. On a cold start
// (fresh Windows, slow disk, antivirus scanning the .NET files) it can take many seconds to
// open its port, so wait for /health instead of failing on the first refused connection.
async function waitForCore(timeoutMs = 90_000): Promise<void> {
  const desktop = (window as any)?.desktop
  const startedAt = Date.now()
  let lastError = ''
  while (Date.now() - startedAt < timeoutMs) {
    try {
      const res = await fetch(`${API_BASE}/health`, { cache: 'no-store' })
      if (res.ok) return
    } catch (e) {
      lastError = String((e as any)?.message ?? e)
    }
    // A core that has already exited will not come up by waiting: report its own error.
    if (desktop?.getCoreStatus && Date.now() - startedAt > 3000) {
      try {
        const st = await desktop.getCoreStatus() as { running?: boolean; lastError?: string }
        if (st && st.running === false && st.lastError) throw new Error(st.lastError)
      } catch (e) {
        if (e instanceof Error && e.message) throw e
      }
    }
    await new Promise((r) => setTimeout(r, 400))
  }
  throw new Error(`Ядро не ответило за ${Math.round(timeoutMs / 1000)} с${lastError ? ` (${lastError})` : ''}`)
}

function sanitizeError(err: unknown): string {
  const raw = String((err as any)?.message ?? err ?? '').trim()
  if (!raw) return 'Неизвестная ошибка.'

  // Если в сообщении — JSON (остаток от проброса тела ответа) — достаём detail/message
  if (raw.startsWith('{') || raw.startsWith('[')) {
    try {
      const j = JSON.parse(raw)
      const msg = j.detail ?? j.message ?? j.Message ?? j.error
      if (typeof msg === 'string' && msg.trim()) return msg.trim()
    } catch { /* не JSON — идём дальше */ }
    return 'Внутренняя ошибка. Попробуйте ещё раз.'
  }

  // Известные технические паттерны → дружелюбный текст
  if (/fetch|Failed to fetch|NetworkError|ERR_CONNECTION|ECONNREFUSED/i.test(raw))
    return 'Нет соединения с сервером. Проверьте интернет.'
  if (/not authenticated|not authorized|Unauthorized|401|403/i.test(raw))
    return 'Сессия истекла. Войдите снова.'
  if (/HTTP 5\d\d|status 5\d\d|500|502|503/i.test(raw))
    return 'Ошибка сервера. Попробуйте позже.'
  if (/Empty API response|Пустой ответ/i.test(raw))
    return 'Нет ответа от ядра лаунчера.'
  if (/CoreHost|core exe|38765/i.test(raw))
    return 'Ядро лаунчера недоступно. Перезапустите лаунчер.'
  if (/ at \w|Exception:|StackTrace|System\.|Microsoft\./i.test(raw))
    return 'Внутренняя ошибка. Попробуйте ещё раз.'

  return raw
}

// Нормализует response.message: если CoreHost пробросил JSON-тело бэкенда, извлекаем текст.
function normalizeMessage(msg: string): string {
  if (!msg) return msg
  const trimmed = msg.trim()
  if (trimmed.startsWith('{') || trimmed.startsWith('[')) {
    try {
      const j = JSON.parse(trimmed)
      const extracted = j.detail ?? j.message ?? j.Message ?? j.error
      if (typeof extracted === 'string' && extracted.trim()) return extracted.trim()
    } catch { /* ignore */ }
  }
  return trimmed
}

async function readJson<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, { cache: 'no-store', ...(init || {}) })
  if (!response.ok) {
    const detail = await response.text()
    throw new Error(detail || `HTTP ${response.status}`)
  }
  return (await response.json()) as T
}

export interface ConsentStatus {
  missing: string[]
  distribution_answered: boolean
  distribution: { profile: boolean; map: boolean; purchases: boolean }
}

export interface ConsentUpdate {
  accept_offer?: boolean
  accept_personal_data?: boolean
  distribution?: { profile: boolean; map: boolean; purchases: boolean }
}

interface ServerStatus {
  online: boolean
  playersOnline: number
  playersMax: number
  version: string
}

export interface GameServer {
  slug: string
  name: string
  description?: string | null
  iconUrl?: string | null
  bannerUrl?: string | null
  host: string
  port: number
  mcVersion: string
  loader: string
  maxPlayers: number
  whitelistMode: string
  maintenance: boolean
  isDefault: boolean
  staffOnly?: boolean
  mapUrl?: string | null
  accentColor?: string | null
  features?: Record<string, boolean> | null
  status?: { online: boolean; playersOnline: number; playersMax: number; version?: string | null } | null
}

export const useLauncherStore = defineStore('launcher', () => {
  const state = reactive<LauncherState>(defaultState())
  const skin = reactive<SkinState>(defaultSkin())
  const toasts = reactive<ToastItem[]>([])
  const mods = reactive<{ list: ModInfo[]; loading: boolean }>({ list: [], loading: false })
  const bpProfile = ref<BattlePassProfile | null>(null)
  const initProgress = ref(0)
  // Set when the local core could not be reached at startup: the splash shows it instead of spinning forever.
  const bootError = ref<BootError | null>(null)
  const serverStatus = ref<ServerStatus | null>(null)
  // Selected visual theme (client-only preference, persisted to localStorage).
  const themeId = ref<string>(loadThemeId() || DEFAULT_THEME_ID)
  const serverList = ref<GameServer[]>([])
  const selectedSlug = ref<string | null>(null)
  const dismissedCrashId = ref<string | null>(null)
  let pollHandle: number | null = null
  let bootstrapPromise: Promise<void> | null = null
  let serverStatusTimer: ReturnType<typeof setInterval> | null = null

  function pushToast(tone: ToastTone, title: string, message: string) {
    const safe = String(message || '').trim()
    if (!safe) return
    const id = toastId()
    toasts.push({ id, tone, title, message: safe })
    window.setTimeout(() => dismissToast(id), tone === 'error' ? 6500 : 4200)
  }

  function dismissToast(id: string) {
    const index = toasts.findIndex((item) => item.id === id)
    if (index >= 0) toasts.splice(index, 1)
  }

  // The crash lives in CoreHost state and is re-sent on every poll; remember the
  // dismissed id so the modal stays closed until the next (differently-id'd) crash.
  function dismissCrash() {
    if (state.lastCrash) dismissedCrashId.value = state.lastCrash.id
  }

  function applyState(next: Partial<LauncherState> | null | undefined) {
    if (!next) return
    state.initialized = Boolean(next.initialized)
    state.isBusy = Boolean(next.isBusy)
    state.isAuthenticated = Boolean(next.isAuthenticated)
    state.statusText = String(next.statusText ?? state.statusText)
    state.launcherVersionText = String(next.launcherVersionText ?? state.launcherVersionText)
    state.accountPrimaryText = String(next.accountPrimaryText ?? state.accountPrimaryText)
    state.accountSecondaryText = String(next.accountSecondaryText ?? state.accountSecondaryText)
    state.accountIsAdmin = Boolean(next.accountIsAdmin ?? state.accountIsAdmin)
    state.emailVerifiedText = String(next.emailVerifiedText ?? state.emailVerifiedText)
    state.currentMemoryMb = Number(next.currentMemoryMb ?? state.currentMemoryMb)
    state.currentMemoryText = String(next.currentMemoryText ?? state.currentMemoryText)
    state.logsDirectory = String(next.logsDirectory ?? '')
    state.dataDirectory = String(next.dataDirectory ?? '')
    state.gameDirectory = String(next.gameDirectory ?? '')
    state.diagnosticsText = String(next.diagnosticsText ?? '')
    state.progress = { ...defaultProgress(), ...(next.progress ?? {}) }
    state.links = { ...defaultLinks(), ...(next.links ?? {}) }
    state.security = { ...defaultSecurity(), ...(next.security ?? {}) }
    state.dashboard = {
      nation: { ...defaultNation(), ...((next.dashboard?.nation as any) ?? {}) },
      nationStats: { ...defaultNationStats(), ...((next.dashboard?.nationStats as any) ?? {}) },
      playerStats: { ...defaultPlayerStats(), ...((next.dashboard?.playerStats as any) ?? {}) },
      recentActivity: Array.isArray(next.dashboard?.recentActivity) ? (next.dashboard?.recentActivity as any) : [],
      walletBalance: Number(next.dashboard?.walletBalance ?? 0),
    }
    state.lastCrash = (next.lastCrash as any) ?? null
  }

  function applySkin(next: Partial<SkinState> | null | undefined) {
    const value = { ...defaultSkin(), ...(next ?? {}) }
    skin.hasSkin = Boolean(value.hasSkin)
    skin.modelVariant = String(value.modelVariant || 'classic')
    skin.skinUrl = String(value.skinUrl || '')
    skin.headPreviewUrl = String(value.headPreviewUrl || '')
    skin.bodyPreviewUrl = String(value.bodyPreviewUrl || '')
    skin.sha256 = String(value.sha256 || '')
    skin.width = Number(value.width || 0)
    skin.height = Number(value.height || 0)
    skin.updatedAt = value.updatedAt ? String(value.updatedAt) : null
  }

  // Direct-to-backend fetch that scopes to the selected server via X-Server-Slug.
  function backendFetch(path: string, init: RequestInit = {}) {
    const headers: Record<string, string> = { ...(init.headers as Record<string, string> || {}) }
    if (selectedSlug.value) headers['X-Server-Slug'] = selectedSlug.value
    return fetch(`${BACKEND_BASE}${path}`, { cache: 'no-store', ...init, headers })
  }

  async function fetchBattlePass(nickname: string) {
    try {
      const resp = await backendFetch(`/battlepass/profile-by-nick/${encodeURIComponent(nickname)}`)
      if (resp.ok) {
        bpProfile.value = await resp.json() as BattlePassProfile
      } else {
        bpProfile.value = null
      }
    } catch {
      bpProfile.value = null
    }
  }

  async function pollStateOnce() {
    try {
      const next = await readJson<LauncherState>('/api/state')
      applyState(next)
    } catch (error) {
      // keep old state, do not spam user
    }
  }

  function startPolling() {
    stopPolling()
    pollHandle = window.setInterval(() => {
      void pollStateOnce()
    }, 1200)
  }

  function stopPolling() {
    if (pollHandle != null) {
      window.clearInterval(pollHandle)
      pollHandle = null
    }
  }

  async function initializeApp() {
    if (bootstrapPromise) return bootstrapPromise
    bootstrapPromise = (async () => {
      // /api/bootstrap blocks while CoreHost downloads the Java runtime + pack.
      // Poll /api/state during it so the splash shows live progress (current
      // file + %) instead of a frozen "Подключение к ядру..." label.
      let bootstrapPoll: number | null = window.setInterval(() => { void pollStateOnce() }, 500)
      const stopBootstrapPoll = () => {
        if (bootstrapPoll != null) { window.clearInterval(bootstrapPoll); bootstrapPoll = null }
      }
      try {
        bootError.value = null
        initProgress.value = 10
        state.statusText = 'Подключение к ядру...'
        await waitForCore()
        const response = await readJson<OperationResponse>('/api/bootstrap')
        stopBootstrapPoll()
        initProgress.value = 55
        state.statusText = 'Загрузка профиля...'
        applyState(response.state)
        initProgress.value = 65
        // Узнаём выбранный сервер ДО первых scoped-запросов (battle pass,
        // статус сервера) — иначе они уходят без X-Server-Slug и бэкенд
        // отвечает данными дефолтного сервера.
        state.statusText = 'Загрузка серверов...'
        await fetchServers()
        initProgress.value = 75
        if (response.state?.isAuthenticated) {
          state.statusText = 'Загрузка скина...'
          try {
            applySkin(await readJson<SkinState>('/api/skin'))
          } catch {
            applySkin(defaultSkin())
          }
          const nick = response.state?.accountPrimaryText
          if (nick && nick !== 'Гость') void fetchBattlePass(nick)
        } else {
          applySkin(defaultSkin())
          bpProfile.value = null
        }
        initProgress.value = 95
        state.statusText = 'Готово'
        startPolling()
        startServerStatusPolling()
        initProgress.value = 100
      } catch (error: unknown) {
        stopBootstrapPoll()
        initProgress.value = 0
        bootError.value = await describeBootError(error)
      }
    })()
    try {
      await bootstrapPromise
    } finally {
      bootstrapPromise = null
    }
  }

  async function fetchServerStatus() {
    try {
      const res = await backendFetch('/server/status')
      if (!res.ok) throw new Error()
      const data = await res.json()
      serverStatus.value = {
        online: Boolean(data.online),
        playersOnline: Number(data.players_online ?? 0),
        playersMax: Number(data.players_max ?? 0),
        version: '',
      }
    } catch {
      serverStatus.value = { online: false, playersOnline: 0, playersMax: 0, version: '' }
    }
  }

  async function fetchServers() {
    try {
      const data = await readJson<{ servers: GameServer[]; selectedSlug: string | null }>('/api/servers')
      serverList.value = Array.isArray(data.servers) ? data.servers : []
      selectedSlug.value = data.selectedSlug ?? selectedSlug.value
      // Default the selection to the default/first server when none is chosen.
      if (!selectedSlug.value && serverList.value.length > 0) {
        const def = serverList.value.find((s) => s.isDefault) ?? serverList.value[0]
        selectedSlug.value = def.slug
      }
    } catch {
      // CoreHost or backend unavailable — keep whatever we had.
    }
    return serverList.value
  }

  async function selectServer(slug: string) {
    try {
      const data = await readJson<{ selectedSlug: string | null; state?: LauncherState }>('/api/servers/select', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ slug }),
      })
      selectedSlug.value = data.selectedSlug ?? slug
      // CoreHost returns dashboard re-scoped to the new server.
      if (data.state) applyState(data.state)
    } catch {
      selectedSlug.value = slug
    }
    // Refresh view-local data (battle pass) for the new server.
    bpProfile.value = null
    const nick = state.accountPrimaryText
    if (nick && nick !== 'Гость') void fetchBattlePass(nick)
    return selectedSlug.value
  }

  function startServerStatusPolling() {
    // The play screen reads live status from activeServer.status, which comes from
    // the per-server catalogue (serverList / fetchServers), NOT the legacy single
    // -server serverStatus ref. So poll fetchServers to keep online/players/
    // maintenance fresh — otherwise the status stays frozen at whatever it was on
    // bootstrap (server up but launcher shows offline & blocks Play, or vice
    // versa). CoreHost re-fetches the backend each call and the backend caches
    // mcstatus ~30s, so a 30s poll is fresh without hammering anything.
    void fetchServers()
    serverStatusTimer = setInterval(() => { void fetchServers() }, 30_000)
    // Refresh immediately when the user tabs back (e.g. after starting/stopping
    // the server outside the launcher) so they don't wait out the poll interval.
    window.addEventListener('focus', onWindowFocus)
  }

  function onWindowFocus() {
    void fetchServers()
  }

  function stopServerStatusPolling() {
    if (serverStatusTimer != null) {
      clearInterval(serverStatusTimer)
      serverStatusTimer = null
    }
    window.removeEventListener('focus', onWindowFocus)
  }

  function dispose() {
    stopPolling()
    stopServerStatusPolling()
  }

  function setTheme(id: string) {
    themeId.value = id
    saveThemeId(id)
  }

  async function login(login: string, password: string) {
    try {
      const response = await readJson<OperationResponse>('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ login, password }),
      })
      applyState(response.state)
      pushToast(response.ok ? 'success' : 'error', response.ok ? 'Вход выполнен' : 'Ошибка входа', normalizeMessage(response.message) || '')
      if (response.ok) {
        // Каталог зависит от аккаунта: серверы «только для админов» видны
        // лишь админам и модераторам с правом servers.hidden.view.
        void fetchServers()
        try {
          applySkin(await readJson<SkinState>('/api/skin'))
        } catch {
          applySkin(defaultSkin())
        }
        const nick = response.state?.accountPrimaryText
        if (nick && nick !== 'Гость') void fetchBattlePass(nick)
      }
      return response
    } catch (error: unknown) {
      pushToast('error', 'Ошибка входа', sanitizeError(error) || 'Не удалось войти.')
      return null
    }
  }

  async function logout() {
    try {
      const response = await readJson<OperationResponse>('/api/auth/logout', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}' })
      applyState(response.state)
      applySkin(defaultSkin())
      // Убираем из каталога серверы, которые были видны только под аккаунтом.
      void fetchServers()
      pushToast('success', 'Сессия завершена', normalizeMessage(response.message) || 'Вы вышли из аккаунта.')
      return response
    } catch (error: unknown) {
      pushToast('error', 'Ошибка выхода', sanitizeError(error) || 'Не удалось выйти.')
      return null
    }
  }

  async function revokeOtherSessions() {
    try {
      const response = await readJson<OperationResponse>('/api/auth/revoke-other-sessions', { method: 'POST' })
      applyState(response.state)
      pushToast(response.ok ? 'success' : 'error', response.ok ? 'Сессии очищены' : 'Не удалось завершить другие сессии', normalizeMessage(response.message) || '')
      return response
    } catch (error: unknown) {
      pushToast('error', 'Ошибка безопасности', sanitizeError(error) || 'Не удалось завершить другие сессии.')
      return null
    }
  }

  async function play() {
    try {
      const response = await readJson<OperationResponse>('/api/actions/play', { method: 'POST' })
      applyState(response.state)
      if (!response.ok) {
        pushToast('error', 'Запуск не выполнен', normalizeMessage(response.message) || 'Не удалось запустить Minecraft.')
      }
      return response
    } catch (error: unknown) {
      pushToast('error', 'Ошибка запуска', sanitizeError(error) || 'Не удалось запустить Minecraft.')
      return null
    }
  }

  // ── Consents ────────────────────────────────────────────────────────────
  // Accounts that haven't accepted the current offer and personal data consent see a blocking
  // dialog right after login; the backend also refuses a play ticket until then. `consents`
  // stays null while unknown (not logged in, or the backend unreachable) — never blocks then.
  const consents = ref<ConsentStatus | null>(null)

  async function loadConsents() {
    if (!state.isAuthenticated) {
      consents.value = null
      return null
    }
    try {
      consents.value = await readJson<ConsentStatus>('/api/consents')
    } catch {
      // Offline or backend down: don't lock the player out of the launcher over it.
    }
    return consents.value
  }

  async function submitConsents(payload: ConsentUpdate) {
    const response = await fetch(`${API_BASE}/api/consents`, {
      method: 'POST',
      cache: 'no-store',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    })
    const data = await response.json().catch(() => ({})) as ConsentStatus & { error?: string }
    if (!response.ok) throw new Error(data.error || `HTTP ${response.status}`)
    consents.value = data
    return data
  }

  watch(() => state.isAuthenticated, (value) => {
    if (value) void loadConsents()
    else consents.value = null
  })

  // ── Pre-launch check ────────────────────────────────────────────────────
  // "Play" first asks CoreHost for warnings (memory, disk, unresolved crash). If any are
  // left after the player's mutes, a modal shows them; the player can fix or launch anyway.
  const preflightWarnings = ref<PreflightWarning[]>([])
  const preflightOpen = ref(false)

  async function requestPlay() {
    if (state.isBusy) return null
    // Re-check before launching: the dialog opens instead of a server-side refusal.
    const current = await loadConsents()
    if (current?.missing?.length) return null
    let warnings: PreflightWarning[] = []
    try {
      const result = await readJson<{ warnings: PreflightWarning[] }>('/api/preflight')
      const muted = loadMutedPreflight()
      // Critical warnings can't be muted: they predict a crash, not an inconvenience.
      warnings = (result.warnings || []).filter((w) => w.severity === 'critical' || !muted.has(w.id))
    } catch {
      // a failed check must never block playing
    }
    if (warnings.length > 0) {
      preflightWarnings.value = warnings
      preflightOpen.value = true
      return null
    }
    return play()
  }

  function closePreflight() {
    preflightOpen.value = false
  }

  async function playDespitePreflight(muteShown: boolean) {
    if (muteShown) {
      const muted = loadMutedPreflight()
      preflightWarnings.value.filter((w) => w.severity !== 'critical').forEach((w) => muted.add(w.id))
      try { localStorage.setItem(PREFLIGHT_MUTED_KEY, JSON.stringify([...muted])) } catch { /* storage unavailable */ }
    }
    preflightOpen.value = false
    return play()
  }

  // ── Crash advice actions ────────────────────────────────────────────────
  async function runCrashAction(crashId: string, actionIndex: number) {
    try {
      const response = await readJson<OperationResponse>('/api/crash/action', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ crashId, actionIndex }),
      })
      applyState(response.state)
      pushToast(response.ok ? 'success' : 'error', response.ok ? 'Готово' : 'Не получилось', normalizeMessage(response.message) || '')
      return response
    } catch (error: unknown) {
      pushToast('error', 'Не получилось', sanitizeError(error) || 'Не удалось выполнить исправление.')
      return null
    }
  }

  async function restoreCrash() {
    try {
      const response = await readJson<OperationResponse>('/api/crash/restore', { method: 'POST' })
      applyState(response.state)
      if (!response.ok) pushToast('info', 'Подсказка недоступна', normalizeMessage(response.message) || '')
      return response
    } catch {
      return null
    }
  }

  async function repair() {
    try {
      const response = await readJson<OperationResponse>('/api/actions/repair', { method: 'POST' })
      applyState(response.state)
      pushToast(response.ok ? 'success' : 'error', response.ok ? 'Клиент восстановлен' : 'Ремонт не выполнен', normalizeMessage(response.message) || '')
      return response
    } catch (error: unknown) {
      pushToast('error', 'Ошибка ремонта', sanitizeError(error) || 'Не удалось починить клиент.')
      return null
    }
  }

  async function clearDiagnostics() {
    try {
      const response = await readJson<OperationResponse>('/api/diagnostics/clear', { method: 'POST' })
      applyState(response.state)
      return response
    } catch (error: unknown) {
      pushToast('error', 'Ошибка диагностики', sanitizeError(error) || 'Не удалось очистить диагностику.')
      return null
    }
  }

  async function uploadSkin(file: File, modelVariant: string) {
    const form = new FormData()
    form.append('file', file)
    form.append('model_variant', modelVariant)
    try {
      const response = await readJson<SkinOpResponse>('/api/skin', { method: 'POST', body: form })
      applySkin(response.skin)
      pushToast(response.ok ? 'success' : 'error', response.ok ? 'Скин сохранён' : 'Скин не сохранён', normalizeMessage(response.message) || '')
      return response
    } catch (error: unknown) {
      pushToast('error', 'Ошибка скина', sanitizeError(error) || 'Не удалось загрузить скин.')
      return null
    }
  }

  async function refreshSkin() {
    try {
      const response = await readJson<SkinState>('/api/skin')
      applySkin(response)
      return response
    } catch (error: unknown) {
      pushToast('error', 'Ошибка скина', sanitizeError(error) || 'Не удалось загрузить данные скина.')
      return null
    }
  }

  async function deleteSkin() {
    try {
      const response = await readJson<SkinOpResponse>('/api/skin', { method: 'DELETE' })
      applySkin(response.skin)
      pushToast(response.ok ? 'success' : 'error', response.ok ? 'Скин удалён' : 'Не удалось удалить скин', normalizeMessage(response.message) || '')
      return response
    } catch (error: unknown) {
      pushToast('error', 'Ошибка скина', sanitizeError(error) || 'Не удалось удалить скин.')
      return null
    }
  }

  function openExternal(url: string) {
    const target = String(url || '').trim()
    if (!target) return
    const desktop = (window as any)?.desktop
    if (desktop?.openExternal) {
      void desktop.openExternal(target)
      return
    }
    window.open(target, '_blank', 'noopener,noreferrer')
  }

  async function saveMemory(maxRamMb: number) {
    try {
      const response = await readJson<OperationResponse>('/api/settings', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ maxRamMb }),
      })
      applyState(response.state)
      pushToast(response.ok ? 'success' : 'error', response.ok ? 'Настройки сохранены' : 'Ошибка сохранения', normalizeMessage(response.message) || '')
      return response
    } catch (error: unknown) {
      pushToast('error', 'Ошибка настроек', sanitizeError(error) || 'Не удалось сохранить настройки.')
      return null
    }
  }

  async function resetMemory() {
    try {
      const response = await readJson<OperationResponse>('/api/settings/reset', { method: 'POST' })
      applyState(response.state)
      pushToast(response.ok ? 'success' : 'error', response.ok ? 'Настройки сброшены' : 'Ошибка сброса', normalizeMessage(response.message) || '')
      return response
    } catch (error: unknown) {
      pushToast('error', 'Ошибка настроек', sanitizeError(error) || 'Не удалось сбросить настройки.')
      return null
    }
  }

  async function getMods() {
    mods.loading = true
    try {
      const response = await readJson<ModListResponse>('/api/mods')
      mods.list.splice(0, mods.list.length, ...response.mods)
    } catch (error: unknown) {
      pushToast('error', 'Список модов недоступен', sanitizeError(error) || 'Не удалось загрузить список модов.')
    } finally {
      mods.loading = false
    }
  }

  async function toggleMod(path: string, enabled: boolean) {
    try {
      const response = await readJson<ModToggleResponse>('/api/mods/toggle', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ path, enabled }),
      })
      if (response.ok) {
        mods.list.splice(0, mods.list.length, ...response.mods)
        pushToast('success', enabled ? 'Мод включён' : 'Мод отключён', normalizeMessage(response.message) || '')
      } else {
        pushToast('error', 'Не удалось изменить настройку', normalizeMessage(response.message) || '')
      }
      return response
    } catch (error: unknown) {
      pushToast('error', 'Ошибка', sanitizeError(error) || 'Не удалось изменить настройку мода.')
      return null
    }
  }

  async function checkShellUpdates() {
    const desktop = (window as any)?.desktop
    if (desktop?.checkForShellUpdates) {
      return desktop.checkForShellUpdates() as Promise<{ ok: boolean; message: string }>
    }
    pushToast('warning', 'Нет доступа', 'Функция доступна только в десктоп-приложении.')
    return null
  }

  async function installShellUpdate() {
    const desktop = (window as any)?.desktop
    if (desktop?.downloadAndInstallShellUpdate) {
      return desktop.downloadAndInstallShellUpdate() as Promise<{ ok: boolean; message: string }>
    }
    pushToast('warning', 'Нет доступа', 'Функция доступна только в десктоп-приложении.')
    return null
  }

  // ── Game files location ─────────────────────────────────────────────────
  async function getGameLocation(includeSize = false) {
    try {
      return await readJson<GameLocationInfo>(`/api/settings/game-location${includeSize ? '?size=true' : ''}`)
    } catch {
      return null
    }
  }

  function selectDirectory(defaultPath: string) {
    const desktop = (window as any)?.desktop
    return desktop?.selectDirectory ? (desktop.selectDirectory(defaultPath) as Promise<string>) : Promise.resolve('')
  }

  async function changeGameLocation(path: string | null, moveFiles: boolean) {
    try {
      const response = await readJson<OperationResponse>('/api/settings/game-location', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ path, moveFiles }),
      })
      applyState(response.state)
      pushToast(response.ok ? 'success' : 'error', response.ok ? 'Папка игры изменена' : 'Не удалось сменить папку', normalizeMessage(response.message) || '')
      return response
    } catch (error: unknown) {
      pushToast('error', 'Не удалось сменить папку', sanitizeError(error) || 'Ошибка переноса файлов.')
      return null
    }
  }

  function openPath(targetPath: string) {
    const target = String(targetPath || '').trim()
    if (!target) return Promise.resolve('')
    const desktop = (window as any)?.desktop
    if (desktop?.openPath) {
      return desktop.openPath(target) as Promise<string>
    }
    return Promise.resolve('')
  }

  const accountNickname = computed(() => state.accountPrimaryText || 'Гость')
  const accountMeta = computed(() => {
    const raw = String(state.accountSecondaryText || '')
    const [login, email] = raw.split(' • ')
    return { login: login || '', email: email || '' }
  })

  return {
    bootError,
    initialized: computed(() => state.initialized),
    isBusy: computed(() => state.isBusy),
    isAuthenticated: computed(() => state.isAuthenticated),
    consents,
    loadConsents,
    submitConsents,
    statusText: computed(() => state.statusText),
    launcherVersionText: computed(() => state.launcherVersionText),
    accountPrimaryText: computed(() => state.accountPrimaryText),
    accountIsAdmin: computed(() => state.accountIsAdmin),
    accountSecondaryText: computed(() => state.accountSecondaryText),
    emailVerifiedText: computed(() => state.emailVerifiedText),
    currentMemoryMb: computed(() => state.currentMemoryMb),
    currentMemoryText: computed(() => state.currentMemoryText),
    logsDirectory: computed(() => state.logsDirectory),
    dataDirectory: computed(() => state.dataDirectory),
    gameDirectory: computed(() => state.gameDirectory),
    diagnosticsText: computed(() => state.diagnosticsText),
    progress: computed(() => state.progress),
    links: computed(() => state.links),
    security: computed(() => state.security),
    dashboard: computed(() => state.dashboard),
    activeCrash: computed(() =>
      state.lastCrash && state.lastCrash.id !== dismissedCrashId.value ? state.lastCrash : null),
    dismissCrash,
    nation: computed(() => state.dashboard.nation),
    nationStats: computed(() => state.dashboard.nationStats),
    playerStats: computed(() => state.dashboard.playerStats),
    recentActivity: computed(() => state.dashboard.recentActivity),
    walletBalance: computed(() => state.dashboard.walletBalance),
    skin: computed(() => skin),
    toasts: computed(() => toasts),
    shouldShowProgress: computed(() => state.progress.visible),
    initProgress,
    accountNickname,
    accountMeta,
    initializeApp,
    dispose,
    login,
    logout,
    revokeOtherSessions,
    play,
    requestPlay,
    preflightWarnings,
    preflightOpen,
    closePreflight,
    playDespitePreflight,
    runCrashAction,
    restoreCrash,
    getGameLocation,
    selectDirectory,
    changeGameLocation,
    repair,
    clearDiagnostics,
    uploadSkin,
    refreshSkin,
    deleteSkin,
    dismissToast,
    openExternal,
    saveMemory,
    resetMemory,
    checkShellUpdates,
    installShellUpdate,
    openPath,
    themeId: computed(() => themeId.value),
    setTheme,
    mods: computed(() => mods),
    getMods,
    toggleMod,
    bpProfile,
    serverStatus,
    fetchServerStatus,
    fetchBattlePass,
    serverList,
    selectedSlug,
    fetchServers,
    selectServer,
    backendFetch,
  }
})
