<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink } from 'vue-router'
import { useLauncherStore } from '../stores/launcher'

const launcher = useLauncherStore()

const serverOnline = computed(() => launcher.serverStatus?.online ?? null)
const serverPlayers = computed(() => launcher.serverStatus?.playersOnline ?? 0)
const serverMax = computed(() => launcher.serverStatus?.playersMax ?? 0)

function fmt(value: number | string | null | undefined) {
  const n = Number(value ?? 0)
  return new Intl.NumberFormat('ru-RU').format(Number.isFinite(n) ? n : 0)
}

const hasNation = computed(() => Boolean(launcher.nation.title))

const stats = computed(() => [
  {
    label: 'Баланс',
    value: fmt(launcher.walletBalance),
    sub: 'монет',
    accent: 'from-amber-400/40 to-amber-600/0',
    dot: 'bg-amber-400',
  },
  {
    label: 'Государство',
    value: hasNation.value ? (launcher.nation.tag ? `[${launcher.nation.tag}]` : launcher.nation.title) : '—',
    sub: hasNation.value ? launcher.nation.role || 'участник' : 'не состоит',
    accent: 'from-violet-400/40 to-violet-600/0',
    dot: 'bg-violet-400',
  },
  {
    label: 'Казна',
    value: fmt(launcher.nationStats.treasuryBalance),
    sub: 'монет',
    accent: 'from-indigo-400/40 to-indigo-600/0',
    dot: 'bg-indigo-400',
  },
  {
    label: 'Территория',
    value: fmt(launcher.nationStats.territoryPoints),
    sub: 'очков',
    accent: 'from-sky-400/40 to-sky-600/0',
    dot: 'bg-sky-400',
  },
  {
    label: 'PvP убийства',
    value: fmt(launcher.playerStats.pvpKills),
    sub: 'личные',
    accent: 'from-rose-400/40 to-rose-600/0',
    dot: 'bg-rose-400',
  },
  {
    label: 'Квесты',
    value: fmt(launcher.playerStats.completedQuests),
    sub: 'выполнено',
    accent: 'from-emerald-400/40 to-emerald-600/0',
    dot: 'bg-emerald-400',
  },
])
</script>

<template>
  <div class="space-y-3">

    <!-- ── Launch hero ──────────────────────────────────────── -->
    <section class="relative overflow-hidden rounded-[22px] border border-violet-500/20 bg-gradient-to-br from-violet-950/40 via-indigo-950/20 to-transparent p-6">
      <!-- Glow orb -->
      <div class="pointer-events-none absolute -right-16 -top-16 h-64 w-64 rounded-full bg-violet-600/12 blur-[80px]"></div>

      <!-- Corner server badge -->
      <div class="absolute right-4 top-4 flex items-center gap-1.5 rounded-full border border-white/10 bg-black/20 px-3 py-1 backdrop-blur-md">
        <span class="h-1.5 w-1.5 animate-pulse rounded-full bg-emerald-400"></span>
        <span class="text-[10px] font-medium text-white/55">{{ launcher.playerStats.minecraftNickname || '—' }}</span>
      </div>

      <p class="text-[11px] font-semibold uppercase tracking-[0.28em] text-violet-300/70">VoidRP · 1.21.1</p>

      <h2 class="mt-2 text-2xl font-bold leading-tight text-white">
        {{ launcher.isBusy ? 'Подготовка к запуску...' : 'Всё готово к запуску' }}
      </h2>

      <p class="mt-2 max-w-lg text-sm leading-6 text-white/50">
        Лаунчер проверит клиент, синхронизирует файлы и запустит Minecraft с твоим аккаунтом. Обновления применяются автоматически.
      </p>

      <div class="mt-5 flex flex-wrap items-center gap-2.5">
        <button
          class="flex items-center gap-2 rounded-[14px] bg-gradient-to-r from-violet-500 to-indigo-500 px-5 py-2.5 text-sm font-bold text-white shadow-lg shadow-violet-500/25 transition hover:brightness-110 hover:shadow-violet-500/40 disabled:cursor-not-allowed disabled:opacity-50"
          :disabled="launcher.isBusy"
          @click="launcher.play()"
        >
          <svg v-if="!launcher.isBusy" class="h-3.5 w-3.5" fill="currentColor" viewBox="0 0 20 20">
            <path d="M6.3 2.841A1.5 1.5 0 0 0 4 4.11v11.78a1.5 1.5 0 0 0 2.3 1.269l9.344-5.89a1.5 1.5 0 0 0 0-2.538L6.3 2.84z"/>
          </svg>
          <svg v-else class="h-3.5 w-3.5 animate-spin" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="3"/>
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 0 1 8-8V0C5.373 0 0 5.373 0 12h4z"/>
          </svg>
          {{ launcher.isBusy ? 'Подготовка...' : 'Играть' }}
        </button>

        <button
          class="rounded-[14px] border border-white/10 bg-white/5 px-4 py-2.5 text-sm text-white/60 transition hover:bg-white/8 hover:text-white disabled:cursor-not-allowed disabled:opacity-40"
          :disabled="launcher.isBusy"
          @click="launcher.repair()"
        >
          Починить клиент
        </button>

        <!-- Server status -->
        <div class="ml-auto hidden items-center gap-2 sm:flex">
          <!-- Loading -->
          <template v-if="serverOnline === null">
            <span class="h-1.5 w-1.5 animate-pulse rounded-full bg-white/20"></span>
            <span class="text-xs text-white/25">void-rp.ru</span>
          </template>
          <!-- Online -->
          <template v-else-if="serverOnline">
            <span class="h-1.5 w-1.5 rounded-full bg-emerald-400 shadow-[0_0_6px_2px_rgba(52,211,153,0.5)]"></span>
            <span class="text-xs font-medium text-emerald-300/80">{{ serverPlayers }} / {{ serverMax }}</span>
            <span class="text-xs text-white/25">онлайн</span>
          </template>
          <!-- Offline -->
          <template v-else>
            <span class="h-1.5 w-1.5 rounded-full bg-red-500/70"></span>
            <span class="text-xs text-red-400/60">сервер недоступен</span>
          </template>
        </div>
      </div>
    </section>

    <!-- ── No-skin nudge ──────────────────────────────────────── -->
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

    <!-- ── Stats grid ────────────────────────────────────────── -->
    <div class="grid grid-cols-3 gap-2.5">
      <div
        v-for="stat in stats"
        :key="stat.label"
        class="group relative overflow-hidden rounded-[18px] border border-white/8 bg-white/[0.03] p-4 transition hover:border-white/12 hover:bg-white/[0.05]"
      >
        <!-- Accent stripe -->
        <div class="absolute inset-x-0 top-0 h-[2px] rounded-t-[18px] bg-gradient-to-r"
          :class="stat.accent"></div>

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
