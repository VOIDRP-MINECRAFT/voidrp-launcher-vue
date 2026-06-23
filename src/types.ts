export type ToastTone = 'info' | 'success' | 'warning' | 'error'

export interface ToastItem {
  id: string
  title: string
  message: string
  tone: ToastTone
}

export interface LauncherProgressDto {
  visible: boolean
  title: string
  details: string
  percent: number
}

export interface LauncherLinksDto {
  registerUrl: string
  forgotPasswordUrl: string
  verifyEmailUrl: string
}

export interface LauncherDashboardNationDto {
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

export interface LauncherDashboardNationStatsDto {
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

export interface LauncherDashboardPlayerStatsDto {
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

export interface LauncherDashboardActivityDto {
  eventType: string
  message: string
  createdAt: string | null
}

export interface LauncherDashboardDto {
  nation: LauncherDashboardNationDto
  nationStats: LauncherDashboardNationStatsDto
  playerStats: LauncherDashboardPlayerStatsDto
  recentActivity: LauncherDashboardActivityDto[]
  walletBalance: number
}

export interface LauncherStateDto {
  initialized: boolean
  isBusy: boolean
  isAuthenticated: boolean
  statusText: string
  launcherVersionText: string
  accountPrimaryText: string
  accountSecondaryText: string
  emailVerifiedText: string
  currentMemoryMb: number
  currentMemoryText: string
  logsDirectory: string
  dataDirectory: string
  gameDirectory: string
  diagnosticsText: string
  progress: LauncherProgressDto
  links: LauncherLinksDto
  dashboard: LauncherDashboardDto
}

export interface OperationResponseDto {
  ok: boolean
  message: string
  pendingElectronExit: boolean
  state: LauncherStateDto
}

export interface PlayerSkinDto {
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

export interface PlayerSkinOperationDto {
  ok: boolean
  message: string
  skin: PlayerSkinDto
}

export interface LoginCommandDto {
  login: string
  password: string
}

export interface SettingsUpdateDto {
  maxRamMb: number
}
