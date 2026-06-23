<script setup lang="ts">
import { computed } from 'vue'
import { useLauncherStore } from '../stores/launcher'

const launcher = useLauncherStore()

const hasNation = computed(() => Boolean(launcher.nation.title))
const nationImage = computed(() => launcher.nation.bannerUrl || launcher.nation.bannerPreviewUrl || '')
const nationIcon = computed(() => launcher.nation.iconUrl || launcher.nation.iconPreviewUrl || '')

function fmt(value: number | string | null | undefined) {
  const n = Number(value ?? 0)
  return new Intl.NumberFormat('ru-RU').format(Number.isFinite(n) ? n : 0)
}
function fmtH(minutes: number | null | undefined) {
  return fmt(Math.floor((minutes ?? 0) / 60)) + ' ч'
}
function fmtDate(value: string | null | undefined) {
  if (!value) return '—'
  const d = new Date(value)
  return Number.isNaN(d.getTime()) ? '—' : new Intl.DateTimeFormat('ru-RU', { dateStyle: 'medium', timeStyle: 'short' }).format(d)
}
</script>

<template>
  <div class="space-y-3">
    <template v-if="hasNation">

      <!-- ── Nation banner ──────────────────────────────────────── -->
      <section class="relative overflow-hidden rounded-[22px] border border-white/10">
        <div
          class="relative min-h-[180px] p-5"
          :style="nationImage
            ? { backgroundImage: `linear-gradient(160deg, rgba(4,6,15,.25) 0%, rgba(4,6,15,.82) 60%), url(${nationImage})`, backgroundSize: 'cover', backgroundPosition: 'center' }
            : {}"
        >
          <!-- Gradient overlay when no image -->
          <div v-if="!nationImage" class="absolute inset-0 bg-gradient-to-br from-violet-950/40 via-indigo-950/20 to-transparent"></div>
          <!-- Subtle glow orb -->
          <div class="pointer-events-none absolute -right-12 -top-12 h-48 w-48 rounded-full bg-violet-600/10 blur-[60px]"></div>

          <div class="relative flex h-full flex-col justify-between gap-4">
            <!-- Top row: icon + name -->
            <div class="flex items-start gap-3">
              <div class="flex h-14 w-14 shrink-0 items-center justify-center overflow-hidden rounded-[16px] border border-white/15 bg-white/8 text-base font-bold shadow-lg">
                <img v-if="nationIcon" :src="nationIcon" alt="icon" class="h-full w-full object-cover" />
                <span v-else class="text-white/70">{{ (launcher.nation.tag || 'ST').slice(0, 2) }}</span>
              </div>
              <div class="min-w-0 flex-1">
                <p class="text-[10px] uppercase tracking-[0.24em] text-white/50">Государство</p>
                <h2 class="mt-1 truncate text-2xl font-bold leading-tight">{{ launcher.nation.title }}</h2>
                <div class="mt-1 flex flex-wrap items-center gap-2">
                  <span v-if="launcher.nation.tag" class="rounded-full border border-white/15 bg-white/8 px-2 py-0.5 text-[10px] font-semibold text-white/70">
                    [{{ launcher.nation.tag }}]
                  </span>
                  <span class="text-[11px] text-white/55">{{ launcher.nation.role || 'Участник' }}</span>
                  <span v-if="launcher.nation.allianceTitle" class="text-[11px] text-violet-300/70">
                    · Альянс: {{ launcher.nation.allianceTitle }}{{ launcher.nation.allianceTag ? ` [${launcher.nation.allianceTag}]` : '' }}
                  </span>
                </div>
              </div>
              <!-- Treasury badge -->
              <div class="shrink-0 rounded-[14px] border border-white/10 bg-black/25 px-4 py-2.5 text-right backdrop-blur-md">
                <p class="text-[10px] uppercase tracking-[0.16em] text-white/45">Казна</p>
                <p class="mt-0.5 text-xl font-bold tabular-nums">{{ fmt(launcher.nationStats.treasuryBalance) }}</p>
              </div>
            </div>
          </div>
        </div>
      </section>

      <!-- ── Nation stat cards ──────────────────────────────────── -->
      <div class="grid grid-cols-4 gap-2.5">
        <div
          v-for="stat in [
            { label: 'Территория', value: fmt(launcher.nationStats.territoryPoints), dot: 'bg-sky-400', accent: 'from-sky-400/35 to-sky-600/0' },
            { label: 'Престиж',    value: fmt(launcher.nationStats.prestigeScore),    dot: 'bg-amber-400', accent: 'from-amber-400/35 to-amber-600/0' },
            { label: 'Онлайн',     value: fmtH(launcher.nationStats.totalPlaytimeMinutes), dot: 'bg-emerald-400', accent: 'from-emerald-400/35 to-emerald-600/0' },
            { label: 'События',    value: fmt(launcher.nationStats.eventsCompleted),  dot: 'bg-violet-400', accent: 'from-violet-400/35 to-violet-600/0' },
          ]"
          :key="stat.label"
          class="relative overflow-hidden rounded-[18px] border border-white/8 bg-white/[0.03] p-4"
        >
          <div class="absolute inset-x-0 top-0 h-[2px] rounded-t-[18px] bg-gradient-to-r" :class="stat.accent"></div>
          <div class="flex items-start justify-between gap-2">
            <p class="text-[10px] font-medium uppercase tracking-[0.18em] text-white/35">{{ stat.label }}</p>
            <span class="mt-0.5 h-1.5 w-1.5 shrink-0 rounded-full" :class="stat.dot"></span>
          </div>
          <p class="mt-2.5 truncate text-xl font-bold leading-none text-white/90">{{ stat.value }}</p>
        </div>
      </div>

      <!-- ── Personal + Activity ────────────────────────────────── -->
      <div class="grid grid-cols-12 gap-3">

        <!-- Personal stats -->
        <div class="col-span-7 rounded-[20px] border border-white/10 bg-white/[0.035] p-4">
          <div class="flex items-center justify-between gap-3">
            <div>
              <p class="text-[10px] uppercase tracking-[0.22em] text-violet-300/70">Личная статистика</p>
              <h3 class="mt-1 text-base font-semibold">Твой вклад</h3>
            </div>
            <div class="text-right">
              <p class="text-[10px] uppercase tracking-[0.16em] text-white/35">Баланс</p>
              <p class="mt-0.5 text-lg font-bold tabular-nums">{{ fmt(launcher.walletBalance) }}</p>
            </div>
          </div>

          <div class="mt-4 grid grid-cols-4 gap-2">
            <div
              v-for="s in [
                { label: 'Онлайн',  value: fmtH(launcher.playerStats.totalPlaytimeMinutes) },
                { label: 'PvP',     value: fmt(launcher.playerStats.pvpKills) },
                { label: 'Мобы',    value: fmt(launcher.playerStats.mobKills) },
                { label: 'Смерти',  value: fmt(launcher.playerStats.deaths) },
              ]"
              :key="s.label"
              class="rounded-[14px] border border-white/8 bg-white/[0.03] p-3 text-center"
            >
              <p class="text-base font-bold leading-none">{{ s.value }}</p>
              <p class="mt-1.5 text-[10px] uppercase tracking-[0.14em] text-white/35">{{ s.label }}</p>
            </div>
          </div>

          <div class="mt-2.5 grid grid-cols-2 gap-2">
            <div class="rounded-[14px] border border-white/8 bg-white/[0.03] p-3">
              <p class="text-sm font-bold">{{ fmt(launcher.playerStats.blocksPlaced) }}</p>
              <p class="mt-1 text-[10px] uppercase tracking-[0.14em] text-white/35">Поставлено блоков</p>
            </div>
            <div class="rounded-[14px] border border-white/8 bg-white/[0.03] p-3">
              <p class="text-sm font-bold">{{ fmt(launcher.playerStats.blocksBroken) }}</p>
              <p class="mt-1 text-[10px] uppercase tracking-[0.14em] text-white/35">Сломано блоков</p>
            </div>
          </div>

          <div class="mt-2.5 grid grid-cols-2 gap-2">
            <div class="rounded-[14px] border border-white/8 bg-white/[0.03] p-3">
              <p class="text-sm font-bold">{{ fmt(launcher.playerStats.completedQuests) }}</p>
              <p class="mt-1 text-[10px] uppercase tracking-[0.14em] text-white/35">Квесты</p>
            </div>
            <div class="rounded-[14px] border border-white/8 bg-white/[0.03] p-3">
              <p class="text-sm font-bold">{{ fmt(launcher.playerStats.pvpKills) }}</p>
              <p class="mt-1 text-[10px] uppercase tracking-[0.14em] text-white/35">Убийств игроков</p>
            </div>
          </div>
        </div>

        <!-- Recent activity -->
        <div class="col-span-5 rounded-[20px] border border-white/10 bg-white/[0.035] p-4">
          <p class="text-[10px] uppercase tracking-[0.22em] text-violet-300/70">Активность</p>
          <h3 class="mt-1 text-base font-semibold">Последние события</h3>

          <div v-if="!launcher.recentActivity.length" class="mt-4 rounded-[14px] border border-white/8 bg-white/[0.03] p-4 text-center">
            <p class="text-xs text-white/35">Активность ещё не загружена</p>
          </div>

          <div v-else class="mt-3 space-y-2">
            <div
              v-for="item in launcher.recentActivity"
              :key="`${item.eventType}_${item.createdAt}`"
              class="relative overflow-hidden rounded-[14px] border border-white/8 bg-white/[0.03] p-3 pl-4"
            >
              <div class="absolute inset-y-0 left-0 w-[3px] rounded-l-[14px] bg-violet-400/50"></div>
              <p class="text-[12px] font-medium leading-5 text-white/85">{{ item.message || item.eventType }}</p>
              <p class="mt-0.5 text-[10px] text-white/35">{{ fmtDate(item.createdAt) }}</p>
            </div>
          </div>
        </div>

      </div>
    </template>

    <!-- ── No nation ──────────────────────────────────────────── -->
    <section v-else class="relative overflow-hidden rounded-[22px] border border-white/10 bg-white/[0.035] p-6">
      <div class="pointer-events-none absolute -right-8 -top-8 h-40 w-40 rounded-full bg-violet-600/8 blur-[50px]"></div>
      <p class="text-[10px] uppercase tracking-[0.22em] text-violet-300/70">Государство</p>
      <h2 class="mt-2 text-2xl font-bold">Ты пока не в государстве</h2>
      <p class="mt-2 max-w-[520px] text-sm leading-6 text-white/50">
        Когда вступишь в государство, здесь появятся казна, территория, престиж, твоя роль и лента последних событий.
      </p>
    </section>

  </div>
</template>
