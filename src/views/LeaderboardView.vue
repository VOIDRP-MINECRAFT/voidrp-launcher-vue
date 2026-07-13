<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useLauncherStore, BACKEND_BASE, SITE_BASE } from '../stores/launcher'

const launcher = useLauncherStore()

// Per-server feature gating (mirrors AppLayout): a feature is enabled unless the
// active server explicitly disables it.
const activeServer = computed(() =>
  launcher.serverList.find((s) => s.slug === launcher.selectedSlug) ?? null,
)
function feat(key: string) {
  const f = activeServer.value?.features
  return !f || f[key] !== false
}

function openPlayer(nick: string) { launcher.openExternal(`${SITE_BASE}/u/${nick}`) }
function openNation(slug: string) { launcher.openExternal(`${SITE_BASE}/nation/${slug}`) }
const PUBLIC_API = BACKEND_BASE

// ─── Types ───────────────────────────────────────────────────────────────────

interface ProgressionTier { tier_name: string; tier_label: string; unlocked_at: string }
interface PlayerProgressionData {
  minecraft_nickname: string; tiers: ProgressionTier[]
  current_tier_label: string | null
}
interface LeaderboardEntry { rank: number; minecraft_nickname: string; unlocked_at: string }
interface TierLeaderboard { tier_name: string; tier_label: string; entries: LeaderboardEntry[] }
interface FullLeaderboard { tiers: TierLeaderboard[] }

interface PlayerTopEntry { rank: number; minecraft_nickname: string; value: number; last_seen_at: string | null }
interface PlayerTopCategory { key: string; label: string; unit: string; entries: PlayerTopEntry[] }
interface PlayerTopResponse { categories: PlayerTopCategory[] }

interface NationRankingItem {
  slug: string; title: string; tag: string; accent_color?: string; icon_url?: string
  members_count: number; treasury_balance: number; territory_points: number
  total_playtime_minutes: number; pvp_kills: number; mob_kills: number
  prestige_score: number; score: number
}
interface NationRankingResponse { items: NationRankingItem[] }

// ─── Main tabs ────────────────────────────────────────────────────────────────

type MainTab = 'progression' | 'players' | 'nations'
const mainTab = ref<MainTab>('progression')

// Which per-server feature each tab needs. Age progression and nations are hidden
// on servers that lack them (e.g. the anarchy server); the top-players tab uses the
// generic "leaderboards" feature.
const TAB_FEATURE: Record<MainTab, string> = {
  progression: 'progression',
  players: 'leaderboards',
  nations: 'nations',
}
const TAB_LABELS: Record<MainTab, string> = {
  progression: 'Прогрессия', players: 'Игроки', nations: 'Государства',
}
const visibleTabs = computed<MainTab[]>(() =>
  (['progression', 'players', 'nations'] as MainTab[]).filter((t) => feat(TAB_FEATURE[t])),
)

const TAB_ICONS: Record<MainTab, string> = {
  progression: 'M9.813 15.904 9 18.75l-.813-2.846a4.5 4.5 0 0 0-3.09-3.09L2.25 12l2.846-.813a4.5 4.5 0 0 0 3.09-3.09L9 5.25l.813 2.846a4.5 4.5 0 0 0 3.09 3.09L15.75 12l-2.846.813a4.5 4.5 0 0 0-3.09 3.09ZM18.259 8.715 18 9.75l-.259-1.035a3.375 3.375 0 0 0-2.455-2.456L14.25 6l1.036-.259a3.375 3.375 0 0 0 2.455-2.456L18 2.25l.259 1.035a3.375 3.375 0 0 0 2.456 2.456L21.75 6l-1.035.259a3.375 3.375 0 0 0-2.456 2.456ZM16.894 20.567 16.5 21.75l-.394-1.183a2.25 2.25 0 0 0-1.423-1.423L13.5 18.75l1.183-.394a2.25 2.25 0 0 0 1.423-1.423l.394-1.183.394 1.183a2.25 2.25 0 0 0 1.423 1.423l1.183.394-1.183.394a2.25 2.25 0 0 0-1.423 1.423Z',
  players:     'M15.75 6a3.75 3.75 0 1 1-7.5 0 3.75 3.75 0 0 1 7.5 0ZM4.501 20.118a7.5 7.5 0 0 1 14.998 0A17.933 17.933 0 0 1 12 21.75c-2.676 0-5.216-.584-7.499-1.632Z',
  nations:     'M12 21v-8.25M15.75 21v-8.25M8.25 21v-8.25M3 9l9-6 9 6m-1.5 12V10.332A48.36 48.36 0 0 0 12 9.75c-2.551 0-5.056.2-7.5.582V21M3 21h18M12 6.75h.008v.008H12V6.75Z',
}

// ─── Progression ─────────────────────────────────────────────────────────────

const TIER_ICONS: Record<string, string> = {
  create_age: '⚙️', mekanism_age: '🔩', ae2_age: '💾',
  reactor_age: '⚛️', draconic_age: '🐉', quantum_age: '🌌', endgame: '♾️',
}
const loadingLeaderboard = ref(true)
const loadingMine = ref(true)
const leaderboard = ref<FullLeaderboard | null>(null)
const myProgression = ref<PlayerProgressionData | null>(null)
const activeTier = ref('')
const progressionError = ref('')

const myNick = computed(() => launcher.playerStats.minecraftNickname)

const unlockedKeys = computed<Set<string>>(() => {
  if (!myProgression.value) return new Set()
  return new Set(myProgression.value.tiers.map(t => t.tier_name))
})

const enrichedTiers = computed(() => {
  if (!leaderboard.value) return []
  const tierMap: Record<string, ProgressionTier> = {}
  if (myProgression.value) for (const t of myProgression.value.tiers) tierMap[t.tier_name] = t
  return leaderboard.value.tiers.map(tier => ({
    ...tier,
    icon: TIER_ICONS[tier.tier_name] ?? '🏆',
    unlocked: unlockedKeys.value.has(tier.tier_name),
    unlocked_at: tierMap[tier.tier_name]?.unlocked_at ?? null,
    total: tier.entries.length,
    myRank: tier.entries.find(e => e.minecraft_nickname.toLowerCase() === myNick.value?.toLowerCase())?.rank ?? null,
  }))
})

const activeTierData = computed<TierLeaderboard | null>(() => {
  if (!leaderboard.value || !activeTier.value) return null
  return leaderboard.value.tiers.find(t => t.tier_name === activeTier.value) ?? null
})

// ─── Player top ──────────────────────────────────────────────────────────────

const loadingPlayers = ref(false)
const playersData = ref<PlayerTopResponse | null>(null)
const activePlayerCat = ref('')
const playersError = ref('')

// Categories that only make sense with the matching feature — hide "richest"
// (balance) without an economy, quests without the quests system. Also hide any
// category that has no real data yet (all zero), like on the anarchy server.
const CATEGORY_FEATURE: Record<string, string> = {
  balance: 'economy',
  completed_quests: 'quests',
}
const visiblePlayerCategories = computed<PlayerTopCategory[]>(() => {
  if (!playersData.value) return []
  return playersData.value.categories.filter((c) => {
    const req = CATEGORY_FEATURE[c.key]
    if (req && !feat(req)) return false
    if (!c.entries.length) return false
    return c.entries.some((e) => Number(e.value) > 0)
  })
})

const activePlayerCategory = computed<PlayerTopCategory | null>(() => {
  if (!activePlayerCat.value) return null
  return visiblePlayerCategories.value.find(c => c.key === activePlayerCat.value) ?? null
})

function fmtPlayerValue(entry: PlayerTopEntry, cat: PlayerTopCategory): string {
  if (cat.key === 'kd') return entry.value.toFixed(2)
  const n = new Intl.NumberFormat('ru-RU').format(Math.floor(entry.value))
  return cat.unit ? `${n} ${cat.unit}` : n
}

// ─── Nations ─────────────────────────────────────────────────────────────────

const loadingNations = ref(false)
const nationsData = ref<NationRankingResponse | null>(null)
const activeNationMetric = ref('score')
const nationsError = ref('')

type NationKey = keyof NationRankingItem

const NATION_METRICS: { key: NationKey; label: string; fmt: (n: NationRankingItem) => string }[] = [
  { key: 'score',                  label: 'Общий',      fmt: n => String(Math.floor(n.score)) },
  { key: 'treasury_balance',       label: 'Казна',      fmt: n => new Intl.NumberFormat('ru-RU').format(Math.floor(n.treasury_balance)) },
  { key: 'territory_points',       label: 'Территория', fmt: n => new Intl.NumberFormat('ru-RU').format(n.territory_points) },
  { key: 'prestige_score',         label: 'Престиж',    fmt: n => new Intl.NumberFormat('ru-RU').format(n.prestige_score) },
  { key: 'pvp_kills',              label: 'PvP',        fmt: n => new Intl.NumberFormat('ru-RU').format(n.pvp_kills) },
  { key: 'total_playtime_minutes', label: 'Онлайн',     fmt: n => Math.floor(n.total_playtime_minutes / 60) + ' ч' },
]

const sortedNations = computed(() => {
  if (!nationsData.value) return []
  const key = activeNationMetric.value as NationKey
  return [...nationsData.value.items]
    .sort((a, b) => Number(b[key]) - Number(a[key]))
    .map((item, i) => ({ ...item, rank: i + 1 }))
})

const activeNationMetricDef = computed(() =>
  NATION_METRICS.find(m => m.key === activeNationMetric.value) ?? NATION_METRICS[0]
)

// ─── Loaders ─────────────────────────────────────────────────────────────────

async function loadLeaderboard() {
  loadingLeaderboard.value = true
  try {
    const resp = await launcher.backendFetch(`/progression/leaderboard`)
    if (!resp.ok) throw new Error(`HTTP ${resp.status}`)
    const data: FullLeaderboard = await resp.json()
    leaderboard.value = data
    if (data.tiers.length && !activeTier.value) activeTier.value = data.tiers[0].tier_name
  } catch { progressionError.value = 'Не удалось загрузить рейтинг прогрессии.' }
  finally { loadingLeaderboard.value = false }
}

async function loadMyProgression() {
  const nick = myNick.value
  if (!nick) { loadingMine.value = false; return }
  loadingMine.value = true
  try {
    const resp = await launcher.backendFetch(`/progression/player/${encodeURIComponent(nick)}`)
    if (resp.status === 404) { myProgression.value = null; return }
    if (!resp.ok) throw new Error(`HTTP ${resp.status}`)
    myProgression.value = await resp.json()
  } catch { /* silence */ }
  finally { loadingMine.value = false }
}

async function loadPlayers() {
  if (playersData.value) return
  loadingPlayers.value = true
  try {
    const resp = await launcher.backendFetch(`/players/top`)
    if (!resp.ok) throw new Error(`HTTP ${resp.status}`)
    const data: PlayerTopResponse = await resp.json()
    playersData.value = data
    if (!activePlayerCat.value || !visiblePlayerCategories.value.some(c => c.key === activePlayerCat.value)) {
      activePlayerCat.value = visiblePlayerCategories.value[0]?.key ?? ''
    }
  } catch { playersError.value = 'Не удалось загрузить топ игроков.' }
  finally { loadingPlayers.value = false }
}

async function loadNations() {
  if (nationsData.value) return
  loadingNations.value = true
  try {
    const resp = await launcher.backendFetch(`/nation-stats/rankings`)
    if (!resp.ok) throw new Error(`HTTP ${resp.status}`)
    nationsData.value = await resp.json()
  } catch { nationsError.value = 'Не удалось загрузить рейтинг государств.' }
  finally { loadingNations.value = false }
}

function switchTab(tab: MainTab) {
  mainTab.value = tab
  if (tab === 'players') loadPlayers()
  if (tab === 'nations') loadNations()
}

function headUrl(nick: string) {
  return `${PUBLIC_API}/public/player-head/${encodeURIComponent(nick)}`
}
function onHeadError(e: Event, nick: string) {
  const img = e.currentTarget as HTMLImageElement
  img.onerror = null
  img.src = `https://mc-heads.net/avatar/${encodeURIComponent(nick)}/24`
}
function fmtDate(iso: string | null) {
  if (!iso) return '—'
  return new Intl.DateTimeFormat('ru-RU', { dateStyle: 'short', timeStyle: 'short' }).format(new Date(iso))
}
function fmtDateShort(iso: string | null) {
  if (!iso) return '—'
  return new Intl.DateTimeFormat('ru-RU', { dateStyle: 'short' }).format(new Date(iso))
}

function rankClass(rank: number) {
  if (rank === 1) return 'text-yellow-400'
  if (rank === 2) return 'text-slate-300'
  if (rank === 3) return 'text-amber-600'
  return 'text-white/25'
}
function rankRowClass(rank: number) {
  if (rank === 1) return 'bg-yellow-400/[0.06]'
  if (rank === 2) return 'bg-slate-300/[0.04]'
  if (rank === 3) return 'bg-amber-600/[0.04]'
  return ''
}
function rankLabel(rank: number) {
  if (rank === 1) return '🥇'
  if (rank === 2) return '🥈'
  if (rank === 3) return '🥉'
  return String(rank)
}

onMounted(() => {
  loadLeaderboard()
  loadMyProgression()
})

// Keep the active tab valid for the current server: if the feature behind it is
// disabled, fall back to the first visible tab (and load its data).
watch(visibleTabs, (tabs) => {
  if (tabs.length && !tabs.includes(mainTab.value)) {
    switchTab(tabs[0])
  }
}, { immediate: true })

// Re-scope all data when the player switches servers.
watch(() => launcher.selectedSlug, () => {
  leaderboard.value = null
  myProgression.value = null
  playersData.value = null
  nationsData.value = null
  activeTier.value = ''
  activePlayerCat.value = ''
  loadLeaderboard()
  loadMyProgression()
  if (mainTab.value === 'players') loadPlayers()
  if (mainTab.value === 'nations') loadNations()
})
</script>

<template>
  <div class="space-y-3">

    <!-- ── Main tab bar ─────────────────────────────────────────── -->
    <div v-if="visibleTabs.length > 1" class="flex gap-1 rounded-[18px] border border-white/10 bg-white/[0.03] p-1">
      <button
        v-for="tab in visibleTabs"
        :key="tab"
        type="button"
        class="flex flex-1 items-center justify-center gap-2 rounded-[13px] py-2 text-xs font-semibold transition"
        :class="mainTab === tab ? 'bg-violet-500/20 text-violet-200' : 'text-white/40 hover:text-white/70'"
        @click="switchTab(tab)"
      >
        <svg class="h-3.5 w-3.5 shrink-0" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor">
          <path stroke-linecap="round" stroke-linejoin="round" :d="TAB_ICONS[tab]" />
        </svg>
        {{ TAB_LABELS[tab] }}
      </button>
    </div>

    <!-- ─── PROGRESSION ───────────────────────────────────────── -->
    <template v-if="mainTab === 'progression'">

      <!-- My progression -->
      <section class="rounded-[20px] border border-white/10 bg-white/[0.035] p-4">
        <p class="text-[10px] uppercase tracking-[0.22em] text-white/40">Моя прогрессия</p>
        <div v-if="loadingMine" class="mt-3 flex gap-1.5">
          <div v-for="i in 7" :key="i" class="h-8 flex-1 animate-pulse rounded-xl bg-white/6"></div>
        </div>
        <div v-else-if="myProgression" class="mt-3">
          <div class="mb-2 flex flex-wrap items-center gap-2">
            <span class="text-sm font-semibold text-white/90">{{ myProgression.minecraft_nickname }}</span>
            <span
              v-if="myProgression.current_tier_label"
              class="rounded-full bg-violet-500/20 px-2 py-0.5 text-[10px] font-semibold text-violet-300"
            >{{ myProgression.current_tier_label }}</span>
          </div>
          <div class="flex flex-wrap gap-1.5">
            <div
              v-for="tier in enrichedTiers"
              :key="tier.tier_name"
              class="flex items-center gap-1.5 rounded-xl px-2.5 py-1.5 text-xs transition"
              :class="tier.unlocked ? 'bg-violet-500/15 text-white/90' : 'bg-white/5 text-white/30'"
              :title="tier.unlocked ? fmtDateShort(tier.unlocked_at) : 'Не открыто'"
            >
              <span class="text-sm leading-none">{{ tier.icon }}</span>
              <span class="font-medium">{{ tier.tier_label }}</span>
              <span v-if="tier.myRank && tier.myRank <= 3" class="text-[10px] opacity-70">
                {{ rankLabel(tier.myRank) }}
              </span>
            </div>
          </div>
        </div>
        <p v-else class="mt-3 text-xs text-white/40">Прогрессия появится после первого захода в игру</p>
      </section>

      <!-- Tier leaderboard -->
      <section class="overflow-hidden rounded-[20px] border border-white/10 bg-white/[0.035]">
        <div class="border-b border-white/8 px-4 py-3">
          <p class="text-[10px] uppercase tracking-[0.22em] text-white/40">Рейтинг эпох</p>
          <h2 class="mt-0.5 text-base font-semibold">Первооткрыватели</h2>
        </div>

        <div v-if="loadingLeaderboard" class="space-y-2 p-4">
          <div class="h-8 animate-pulse rounded-xl bg-white/6"></div>
          <div v-for="i in 5" :key="i" class="h-10 animate-pulse rounded-xl bg-white/4"></div>
        </div>

        <template v-else-if="leaderboard">
          <!-- Tier selector -->
          <div class="flex flex-wrap gap-1.5 px-4 py-3">
            <button
              v-for="tier in enrichedTiers"
              :key="tier.tier_name"
              type="button"
              class="flex items-center gap-1 rounded-full px-2.5 py-1 text-[11px] font-medium transition"
              :class="activeTier === tier.tier_name
                ? 'bg-violet-500/25 text-violet-200'
                : 'bg-white/6 text-white/50 hover:bg-white/10 hover:text-white/80'"
              @click="activeTier = tier.tier_name"
            >
              <span class="text-sm leading-none">{{ tier.icon }}</span>
              <span>{{ tier.tier_label }}</span>
              <span class="ml-0.5 opacity-50">{{ tier.total }}</span>
            </button>
          </div>

          <!-- Entries -->
          <div v-if="activeTierData" class="border-t border-white/6">
            <div v-if="activeTierData.entries.length === 0" class="px-4 py-8 text-center text-xs text-white/30">
              Пока никто не открыл эту эпоху
            </div>
            <div v-else class="divide-y divide-white/[0.05]">
              <div
                v-for="entry in activeTierData.entries"
                :key="entry.minecraft_nickname"
                class="flex cursor-pointer items-center gap-3 px-4 py-2.5 transition hover:bg-white/[0.04]"
                :class="[
                  entry.minecraft_nickname.toLowerCase() === myNick?.toLowerCase()
                    ? 'bg-violet-500/10' : rankRowClass(entry.rank)
                ]"
                @click="openPlayer(entry.minecraft_nickname)"
              >
                <span class="w-6 shrink-0 text-center text-sm font-bold" :class="rankClass(entry.rank)">
                  {{ rankLabel(entry.rank) }}
                </span>
                <img :src="headUrl(entry.minecraft_nickname)" :alt="entry.minecraft_nickname" class="h-6 w-6 shrink-0 rounded-md" loading="lazy" @error="e => onHeadError(e, entry.minecraft_nickname)" />
                <p class="min-w-0 flex-1 truncate text-sm font-medium text-white/90">
                  {{ entry.minecraft_nickname }}
                  <span v-if="entry.minecraft_nickname.toLowerCase() === myNick?.toLowerCase()" class="ml-1.5 text-[10px] font-normal text-violet-300/70">· ты</span>
                </p>
                <p class="shrink-0 text-[11px] text-white/35">{{ fmtDate(entry.unlocked_at) }}</p>
              </div>
            </div>
          </div>
        </template>

        <div v-else class="px-4 py-8 text-center text-xs text-white/30">{{ progressionError || 'Нет данных' }}</div>
      </section>
    </template>

    <!-- ─── PLAYERS ─────────────────────────────────────────────── -->
    <template v-else-if="mainTab === 'players'">
      <section class="overflow-hidden rounded-[20px] border border-white/10 bg-white/[0.035]">
        <div class="border-b border-white/8 px-4 py-3">
          <p class="text-[10px] uppercase tracking-[0.22em] text-white/40">Топ игроков</p>
          <h2 class="mt-0.5 text-base font-semibold">Лучшие сезона</h2>
        </div>

        <div v-if="loadingPlayers" class="space-y-2 p-4">
          <div class="h-8 animate-pulse rounded-xl bg-white/6"></div>
          <div v-for="i in 8" :key="i" class="h-10 animate-pulse rounded-xl bg-white/4"></div>
        </div>

        <template v-else-if="playersData">
          <div class="flex flex-wrap gap-1.5 px-4 py-3">
            <button
              v-for="cat in visiblePlayerCategories"
              :key="cat.key"
              type="button"
              class="rounded-full px-2.5 py-1 text-[11px] font-medium transition"
              :class="activePlayerCat === cat.key
                ? 'bg-violet-500/25 text-violet-200'
                : 'bg-white/6 text-white/50 hover:bg-white/10 hover:text-white/80'"
              @click="activePlayerCat = cat.key"
            >{{ cat.label }}</button>
          </div>

          <div v-if="activePlayerCategory" class="divide-y divide-white/[0.05] border-t border-white/6">
            <div
              v-for="entry in activePlayerCategory.entries"
              :key="entry.minecraft_nickname"
              class="flex cursor-pointer items-center gap-3 px-4 py-2.5 transition hover:bg-white/[0.04]"
              :class="[
                entry.minecraft_nickname.toLowerCase() === myNick?.toLowerCase()
                  ? 'bg-violet-500/10' : rankRowClass(entry.rank)
              ]"
              @click="openPlayer(entry.minecraft_nickname)"
            >
              <span class="w-6 shrink-0 text-center text-sm font-bold" :class="rankClass(entry.rank)">
                {{ rankLabel(entry.rank) }}
              </span>
              <img :src="headUrl(entry.minecraft_nickname)" :alt="entry.minecraft_nickname" class="h-6 w-6 shrink-0 rounded-md" loading="lazy" @error="e => onHeadError(e, entry.minecraft_nickname)" />
              <p class="min-w-0 flex-1 truncate text-sm font-medium text-white/90">
                {{ entry.minecraft_nickname }}
                <span v-if="entry.minecraft_nickname.toLowerCase() === myNick?.toLowerCase()" class="ml-1.5 text-[10px] font-normal text-violet-300/70">· ты</span>
              </p>
              <p class="shrink-0 text-sm font-semibold text-white/80">{{ fmtPlayerValue(entry, activePlayerCategory) }}</p>
            </div>
          </div>
        </template>

        <div v-else class="px-4 py-8 text-center text-xs text-white/30">{{ playersError || 'Нет данных' }}</div>
      </section>
    </template>

    <!-- ─── NATIONS ──────────────────────────────────────────────── -->
    <template v-else-if="mainTab === 'nations'">
      <section class="overflow-hidden rounded-[20px] border border-white/10 bg-white/[0.035]">
        <div class="border-b border-white/8 px-4 py-3">
          <p class="text-[10px] uppercase tracking-[0.22em] text-white/40">Рейтинг государств</p>
          <h2 class="mt-0.5 text-base font-semibold">Силы сезона</h2>
        </div>

        <div v-if="loadingNations" class="space-y-2 p-4">
          <div class="h-8 animate-pulse rounded-xl bg-white/6"></div>
          <div v-for="i in 6" :key="i" class="h-12 animate-pulse rounded-xl bg-white/4"></div>
        </div>

        <template v-else-if="nationsData">
          <div class="flex flex-wrap gap-1.5 px-4 py-3">
            <button
              v-for="m in NATION_METRICS"
              :key="m.key"
              type="button"
              class="rounded-full px-2.5 py-1 text-[11px] font-medium transition"
              :class="activeNationMetric === m.key
                ? 'bg-violet-500/25 text-violet-200'
                : 'bg-white/6 text-white/50 hover:bg-white/10 hover:text-white/80'"
              @click="activeNationMetric = m.key"
            >{{ m.label }}</button>
          </div>

          <div class="divide-y divide-white/[0.05] border-t border-white/6">
            <div
              v-for="nation in sortedNations"
              :key="nation.slug"
              class="flex cursor-pointer items-center gap-3 px-4 py-2.5 transition hover:bg-white/[0.04]"
              :class="rankRowClass(nation.rank)"
              @click="openNation(nation.slug)"
            >
              <span class="w-6 shrink-0 text-center text-sm font-bold" :class="rankClass(nation.rank)">
                {{ rankLabel(nation.rank) }}
              </span>
              <div
                v-if="nation.icon_url"
                class="h-7 w-7 shrink-0 overflow-hidden rounded-[10px] border border-white/10"
              >
                <img :src="nation.icon_url" :alt="nation.title" class="h-full w-full object-cover" />
              </div>
              <div
                v-else
                class="flex h-7 w-7 shrink-0 items-center justify-center rounded-[10px] border border-white/10 bg-white/5 text-[10px] font-bold text-white/55"
              >{{ (nation.tag || '??').slice(0, 2) }}</div>
              <div class="min-w-0 flex-1">
                <p class="truncate text-sm font-semibold text-white/90">{{ nation.title }}</p>
                <p class="text-[10px] text-white/35">{{ nation.tag }} · {{ nation.members_count }} чел.</p>
              </div>
              <p class="shrink-0 text-sm font-bold text-white/80">{{ activeNationMetricDef.fmt(nation) }}</p>
            </div>
            <div v-if="sortedNations.length === 0" class="px-4 py-8 text-center text-xs text-white/30">
              Нет данных по государствам
            </div>
          </div>
        </template>

        <div v-else class="px-4 py-8 text-center text-xs text-white/30">{{ nationsError || 'Нет данных' }}</div>
      </section>
    </template>

  </div>
</template>
