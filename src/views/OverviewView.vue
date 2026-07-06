<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import { useLauncherStore } from '../stores/launcher'

const launcher = useLauncherStore()

function fmt(value: number | string | null | undefined) {
  const n = Number(value ?? 0)
  return new Intl.NumberFormat('ru-RU').format(Number.isFinite(n) ? n : 0)
}

const hasNation = computed(() => Boolean(launcher.nation.title))

const stats = computed(() => [
  { label: 'Баланс', value: fmt(launcher.walletBalance), sub: 'монет', accent: 'from-amber-400/40 to-amber-600/0', dot: 'bg-amber-400' },
  {
    label: 'Государство',
    value: hasNation.value ? (launcher.nation.tag ? `[${launcher.nation.tag}]` : launcher.nation.title) : '—',
    sub: hasNation.value ? launcher.nation.role || 'участник' : 'не состоит',
    accent: 'from-violet-400/40 to-violet-600/0', dot: 'bg-violet-400',
  },
  { label: 'Казна', value: fmt(launcher.nationStats.treasuryBalance), sub: 'монет', accent: 'from-indigo-400/40 to-indigo-600/0', dot: 'bg-indigo-400' },
  { label: 'Территория', value: fmt(launcher.nationStats.territoryPoints), sub: 'очков', accent: 'from-sky-400/40 to-sky-600/0', dot: 'bg-sky-400' },
  { label: 'PvP убийства', value: fmt(launcher.playerStats.pvpKills), sub: 'личные', accent: 'from-rose-400/40 to-rose-600/0', dot: 'bg-rose-400' },
  { label: 'Квесты', value: fmt(launcher.playerStats.completedQuests), sub: 'выполнено', accent: 'from-emerald-400/40 to-emerald-600/0', dot: 'bg-emerald-400' },
])
</script>

<template>
  <div class="space-y-4">
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
