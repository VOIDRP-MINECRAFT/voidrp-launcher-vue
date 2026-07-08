import { computed, reactive, ref } from 'vue'
import { defineStore } from 'pinia'

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
  const serverStatus = ref<ServerStatus | null>(null)
  const serverList = ref<GameServer[]>([])
  const selectedSlug = ref<string | null>(null)
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
      try {
        initProgress.value = 10
        state.statusText = 'Подключение к ядру...'
        const response = await readJson<OperationResponse>('/api/bootstrap')
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
        initProgress.value = 0
        pushToast('error', 'Ядро лаунчера недоступно', sanitizeError(error) || 'Не удалось связаться с локальным ядром.')
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
    fetchServerStatus()
    serverStatusTimer = setInterval(fetchServerStatus, 60_000)
  }

  function stopServerStatusPolling() {
    if (serverStatusTimer != null) {
      clearInterval(serverStatusTimer)
      serverStatusTimer = null
    }
  }

  function dispose() {
    stopPolling()
    stopServerStatusPolling()
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
    initialized: computed(() => state.initialized),
    isBusy: computed(() => state.isBusy),
    isAuthenticated: computed(() => state.isAuthenticated),
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
