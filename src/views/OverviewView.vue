<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import { useLauncherStore } from '../stores/launcher'

const launcher = useLauncherStore()

function fmt(value: number | string | null | undefined) {
  const n = Number(value ?? 0)
  return new Intl.NumberFormat('ru-RU').format(Number.isFinite(n) ? n : 0)
}

// Per-server feature gating (mirrors AppLayout): a feature is enabled unless the
// active server explicitly disables it.
const activeServer = computed(() =>
  launcher.serverList.find((s) => s.slug === launcher.selectedSlug) ?? null,
)
function feat(key: string) {
  const f = activeServer.value?.features
  return !f || f[key] !== false
}

const hasNation = computed(() => Boolean(launcher.nation.title))

// `feature: undefined` = always shown (combat/time stats make sense on every
// server); economy/nation/quest cards are hidden where the feature is off.
const allStats = computed(() => [
  { feature: 'economy', label: 'Баланс', value: fmt(launcher.walletBalance), sub: 'монет', accent: 'from-amber-400/40 to-amber-600/0', dot: 'bg-amber-400' },
  {
    feature: 'nations',
    label: 'Государство',
    value: hasNation.value ? (launcher.nation.tag ? `[${launcher.nation.tag}]` : launcher.nation.title) : '—',
    sub: hasNation.value ? launcher.nation.role || 'участник' : 'не состоит',
    accent: 'from-violet-400/40 to-violet-600/0', dot: 'bg-violet-400',
  },
  { feature: 'nations', label: 'Казна', value: fmt(launcher.nationStats.treasuryBalance), sub: 'монет', accent: 'from-indigo-400/40 to-indigo-600/0', dot: 'bg-indigo-400' },
  { feature: 'nations', label: 'Территория', value: fmt(launcher.nationStats.territoryPoints), sub: 'очков', accent: 'from-sky-400/40 to-sky-600/0', dot: 'bg-sky-400' },
  { feature: undefined, label: 'PvP убийства', value: fmt(launcher.playerStats.pvpKills), sub: 'личные', accent: 'from-rose-400/40 to-rose-600/0', dot: 'bg-rose-400' },
  { feature: undefined, label: 'Убийства мобов', value: fmt(launcher.playerStats.mobKills), sub: 'всего', accent: 'from-orange-400/40 to-orange-600/0', dot: 'bg-orange-400' },
  { feature: undefined, label: 'Смерти', value: fmt(launcher.playerStats.deaths), sub: 'всего', accent: 'from-slate-400/40 to-slate-600/0', dot: 'bg-slate-400' },
  { feature: undefined, label: 'В игре', value: fmt(Math.floor((launcher.playerStats.totalPlaytimeMinutes ?? 0) / 60)), sub: 'часов', accent: 'from-teal-400/40 to-teal-600/0', dot: 'bg-teal-400' },
  { feature: 'quests', label: 'Квесты', value: fmt(launcher.playerStats.completedQuests), sub: 'выполнено', accent: 'from-emerald-400/40 to-emerald-600/0', dot: 'bg-emerald-400' },
])

const stats = computed(() => allStats.value.filter((s) => !s.feature || feat(s.feature)))
</script>

<template>
  <div class="space-y-4">
    <!-- Telegram не привязан: бонус в игре и напоминание о награде второго дня -->
    <button
      v-if="launcher.isAuthenticated && launcher.telegramLinked === false"
      type="button"
      class="flex w-full items-center gap-3 rounded-2xl border border-sky-400/25 bg-sky-500/10 px-4 py-3 text-left transition hover:border-sky-400/50"
      @click="launcher.openExternal(launcher.telegramBotUrl)"
    >
      <span class="grid h-9 w-9 shrink-0 place-items-center rounded-xl bg-sky-500 text-white">
        <svg viewBox="0 0 24 24" width="18" height="18" fill="currentColor" aria-hidden="true"><path d="M9.04 15.38 8.86 19c.38 0 .54-.16.74-.36l1.78-1.7 3.69 2.7c.68.37 1.16.18 1.34-.63l2.43-11.4c.22-1-.36-1.39-1.02-1.15L3.57 11.9c-.97.38-.96.92-.17 1.16l3.67 1.15 8.52-5.38c.4-.26.77-.12.47.15"/></svg>
      </span>
      <span class="min-w-0 flex-1">
        <span class="block text-sm font-bold text-white">Привяжи Telegram — получи бонус в игре</span>
        <span class="block text-xs text-white/50">Бот напомнит о награде за второй день. Открой бота и нажми «Привязать аккаунт».</span>
      </span>
      <span class="text-xs font-bold text-sky-300">Открыть →</span>
    </button>

    <!-- Header -->
    <div class="flex items-center justify-between gap-3">
      <div>
        <h1 class="text-xl font-bold text-white">Обзор</h1>
        <p class="mt-0.5 text-sm text-white/40">Твой прогресс на выбранном сервере</p>
      </div>
      <div class="flex items-center gap-1.5 rounded-full border border-white/10 bg-white/5 px-3 py-1.5">
        <span class="h-1.5 w-1.5 animate-pulse rounded-full bg-emerald-400"></span>
        <span class="text-xs font-medium text-white/60">{{ launcher.playerStats.minecraftNickname || '—' }}</span>
      </div>
    </div>

    <!-- No-skin nudge -->
    <RouterLink
      v-if="!launcher.skin.hasSkin"
      to="/account"
      class="flex items-center gap-3 rounded-[18px] border border-amber-500/25 bg-amber-500/8 px-4 py-3 transition hover:bg-amber-500/14"
    >
      <svg class="h-4 w-4 shrink-0 text-amber-400" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor">
        <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126ZM12 15.75h.007v.008H12v-.008Z"/>
      </svg>
      <span class="flex-1 text-sm text-amber-200/80">У вас не установлен скин — другие игроки видят вас как Стива.</span>
      <span class="shrink-0 text-xs font-semibold text-amber-400">Установить →</span>
    </RouterLink>

    <!-- Stats grid -->
    <div class="grid grid-cols-2 gap-2.5 sm:grid-cols-3">
      <div
        v-for="stat in stats"
        :key="stat.label"
        class="group relative overflow-hidden rounded-[18px] border border-white/8 bg-white/[0.03] p-4 transition hover:border-white/12 hover:bg-white/[0.05]"
      >
        <div class="absolute inset-x-0 top-0 h-[2px] rounded-t-[18px] bg-gradient-to-r" :class="stat.accent"></div>
        <div class="flex items-start justify-between gap-2">
          <p class="text-[10px] font-medium uppercase tracking-[0.18em] text-white/35">{{ stat.label }}</p>
          <span class="mt-0.5 h-1.5 w-1.5 shrink-0 rounded-full" :class="stat.dot"></span>
        </div>
        <p class="mt-2.5 truncate text-xl font-bold leading-none text-white/90">{{ stat.value }}</p>
        <p class="mt-1 text-[11px] text-white/35">{{ stat.sub }}</p>
      </div>
    </div>
  </div>
</template>
