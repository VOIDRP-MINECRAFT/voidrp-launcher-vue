<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { RouterLink, RouterView, useRoute } from 'vue-router'
import { useLauncherStore, BACKEND_BASE } from '../stores/launcher'

const launcher = useLauncherStore()
const route = useRoute()

const avatarLetter = computed(() => (launcher.accountNickname?.[0] ?? '?').toUpperCase())

const avatarSrc = computed(() => launcher.skin?.headPreviewUrl || null)
const mcHeadsUrl = computed(() =>
  launcher.accountNickname
    ? `https://mc-heads.net/avatar/${encodeURIComponent(launcher.accountNickname)}/44`
    : null
)
const avatarFallback = ref(false)
const avatarGone = ref(false)

watch(avatarSrc, () => { avatarFallback.value = false; avatarGone.value = false })

const effectiveAvatarSrc = computed(() => {
  if (!avatarFallback.value) return avatarSrc.value || mcHeadsUrl.value
  return mcHeadsUrl.value
})

function onAvatarError() {
  if (!avatarFallback.value && avatarSrc.value) {
    avatarFallback.value = true
  } else {
    avatarGone.value = true
  }
}

const navGroups = [
  {
    label: 'Игра',
    items: [
      {
        title: 'Главная', to: '/home',
        icon: 'M2.25 12l8.954-8.955c.44-.439 1.152-.439 1.591 0L21.75 12M4.5 9.75v10.125c0 .621.504 1.125 1.125 1.125H9.75v-4.875c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125V21h4.125c.621 0 1.125-.504 1.125-1.125V9.75M8.25 21h8.25',
      },
      {
        title: 'Моды', to: '/mods',
        icon: 'M6.429 9.75 2.25 12l4.179 2.25m0-4.5 5.571 3 5.571-3m-11.142 0L2.25 7.5 12 2.25l9.75 5.25-4.179 2.25m0 0L21.75 12l-4.179 2.25m0 0 4.179 2.25L12 21.75 2.25 16.5l4.179-2.25m11.142 0-5.571 3-5.571-3',
      },
      {
        title: 'Предложить мод', to: '/suggest-mod',
        icon: 'M12 4.5v15m7.5-7.5h-15',
      },
      {
        title: 'Обратная связь', to: '/feedback',
        icon: 'M7.5 8.25h9m-9 3H12m-9.75 1.51c0 1.6 1.123 2.994 2.707 3.227 1.129.166 2.27.293 3.423.379.35.026.67.21.865.501L12 21l2.755-4.133a1.14 1.14 0 0 1 .865-.501 48.172 48.172 0 0 0 3.423-.379c1.584-.233 2.707-1.626 2.707-3.228V6.741c0-1.602-1.123-2.995-2.707-3.228A48.394 48.394 0 0 0 12 3c-2.392 0-4.744.175-7.043.513C3.373 3.746 2.25 5.14 2.25 6.741v6.018z',
      },
    ],
  },
  {
    label: 'Сервер',
    items: [
      {
        title: 'Государство', to: '/nation',
        icon: 'M3 3v1.5M3 21v-6m0 0 2.77-.693a9 9 0 0 1 6.208.682l.108.054a9 9 0 0 0 6.086.71l3.114-.732a48.524 48.524 0 0 1-.005-10.499l-3.11.732a9 9 0 0 1-6.085-.711l-.108-.054a9 9 0 0 0-6.208-.682L3 4.5M3 15V4.5',
      },
      {
        title: 'Рейтинг', to: '/leaderboard',
        icon: 'M16.5 18.75h-9m9 0a3 3 0 0 1 3 3h-15a3 3 0 0 1 3-3m9 0v-3.375c0-.621-.503-1.125-1.125-1.125h-.871M7.5 18.75v-3.375c0-.621.504-1.125 1.125-1.125h.872m5.007 0H9.497m5.007 0a7.454 7.454 0 0 1-.982-3.172M9.497 14.25a7.454 7.454 0 0 0 .981-3.172M5.25 4.236c-.982.143-1.954.317-2.916.52A6.003 6.003 0 0 0 7.73 9.728M5.25 4.236V4.5c0 2.108.966 3.99 2.48 5.228M5.25 4.236V2.721C7.456 2.41 9.71 2.25 12 2.25c2.291 0 4.545.16 6.75.47v1.516M7.73 9.728a6.726 6.726 0 0 0 2.748 1.35m8.272-6.842V4.5c0 2.108-.966 3.99-2.48 5.228m2.48-5.492a46.32 46.32 0 0 1 2.916.52 6.003 6.003 0 0 1-5.395 4.972m0 0a6.726 6.726 0 0 1-2.749 1.35m0 0a6.772 6.772 0 0 1-3.044 0',
      },
      {
        title: 'Карта', to: '/map',
        icon: 'M9 6.75V15m6-6v8.25m.503 3.498 4.875-2.437c.381-.19.622-.58.622-1.006V4.82c0-.836-.88-1.38-1.628-1.006l-3.869 1.934c-.317.159-.69.159-1.006 0L9.503 3.252a1.125 1.125 0 0 0-1.006 0L3.622 5.689C3.24 5.88 3 6.27 3 6.695V19.18c0 .836.88 1.38 1.628 1.006l3.869-1.934c.317-.159.69-.159 1.006 0l4.994 2.497c.317.158.69.158 1.006 0z',
      },

    ],
  },
  {
    label: 'Аккаунт',
    items: [
      {
        title: 'Аккаунт', to: '/account',
        icon: 'M15.75 6a3.75 3.75 0 1 1-7.5 0 3.75 3.75 0 0 1 7.5 0zM4.501 20.118a7.5 7.5 0 0 1 14.998 0A17.933 17.933 0 0 1 12 21.75c-2.676 0-5.216-.584-7.499-1.632z',
      },
      {
        title: 'Настройки', to: '/settings',
        icon: 'M9.594 3.94c.09-.542.56-.94 1.11-.94h2.593c.55 0 1.02.398 1.11.94l.213 1.281c.063.374.313.686.645.87.074.04.147.083.22.127.324.196.72.257 1.075.124l1.217-.456a1.125 1.125 0 0 1 1.37.49l1.296 2.247a1.125 1.125 0 0 1-.26 1.431l-1.003.827c-.293.24-.438.613-.431.992a6.759 6.759 0 0 1 0 .255c-.007.378.138.75.43.99l1.005.828c.424.35.534.954.26 1.43l-1.298 2.247a1.125 1.125 0 0 1-1.369.491l-1.217-.456c-.355-.133-.75-.072-1.076.124a6.57 6.57 0 0 1-.22.128c-.331.183-.581.495-.644.869l-.213 1.28c-.09.543-.56.941-1.11.941h-2.594c-.55 0-1.02-.398-1.11-.94l-.213-1.281c-.062-.374-.312-.686-.644-.87a6.52 6.52 0 0 1-.22-.127c-.325-.196-.72-.257-1.076-.124l-1.217.456a1.125 1.125 0 0 1-1.369-.49l-1.297-2.247a1.125 1.125 0 0 1 .26-1.431l1.004-.827c.292-.24.437-.613.43-.992a6.932 6.932 0 0 1 0-.255c.007-.378-.138-.75-.43-.99l-1.004-.828a1.125 1.125 0 0 1-.26-1.43l1.297-2.247a1.125 1.125 0 0 1 1.37-.491l1.216.456c.356.133.751.072 1.076-.124.072-.044.146-.087.22-.128.332-.183.582-.495.644-.869l.214-1.281zM15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0z',
      },
    ],
  },
]

const currentTierLabel = ref<string | null>(null)

async function fetchTier(nick: string) {
  try {
    const resp = await fetch(
      `${BACKEND_BASE}/progression/player/${encodeURIComponent(nick)}`,
      { cache: 'no-store' }
    )
    if (resp.ok) {
      const data = await resp.json()
      currentTierLabel.value = data.current_tier_label ?? null
    }
  } catch { }
}

watch(
  () => launcher.playerStats.minecraftNickname,
  (nick) => { if (nick) fetchTier(nick) },
  { immediate: true }
)
</script>

<template>
  <div class="mx-auto flex h-full max-w-[1180px] flex-col gap-3 p-4">

    <!-- ── Header ─────────────────────────────────────────────────── -->
    <header class="rounded-[24px] border border-white/10 bg-[#091022]/90 px-5 py-4 backdrop-blur-xl">
      <div class="flex items-center gap-4">

        <!-- Avatar -->
        <div class="relative h-11 w-11 shrink-0 overflow-hidden rounded-[14px] shadow-lg shadow-violet-500/20">
          <img
            v-if="effectiveAvatarSrc && !avatarGone"
            :src="effectiveAvatarSrc"
            :alt="launcher.accountNickname"
            class="h-full w-full object-cover"
            @error="onAvatarError"
          />
          <div
            v-else
            class="flex h-full w-full items-center justify-center bg-gradient-to-br from-violet-500 to-indigo-600 text-base font-bold text-white"
          >
            {{ avatarLetter }}
          </div>
        </div>

        <!-- User info -->
        <div class="min-w-0 flex-1">
          <div class="flex flex-wrap items-center gap-1.5">
            <h1 class="truncate text-[17px] font-bold leading-tight">{{ launcher.accountNickname }}</h1>

            <span
              v-if="launcher.nation.title"
              class="rounded-full border border-violet-400/25 bg-violet-500/12 px-2 py-0.5 text-[10px] font-medium text-violet-300"
            >
              {{ launcher.nation.tag ? `[${launcher.nation.tag}]` : launcher.nation.title }}
            </span>

            <span
              v-if="currentTierLabel"
              class="rounded-full border border-indigo-400/25 bg-indigo-500/12 px-2 py-0.5 text-[10px] font-medium text-indigo-300"
            >
              {{ currentTierLabel }}
            </span>

            <span class="flex items-center gap-1 rounded-full border border-emerald-400/25 bg-emerald-400/10 px-2 py-0.5 text-[10px] font-medium text-emerald-300">
              <span class="h-1.5 w-1.5 rounded-full bg-emerald-400"></span>
              Готов
            </span>
          </div>

          <p class="mt-0.5 truncate text-[11px] text-white/35">
            {{ launcher.accountMeta.login }} · {{ launcher.accountMeta.email }}
          </p>
        </div>

        <!-- Actions -->
        <div class="flex shrink-0 items-center gap-2">
          <button
            class="rounded-[14px] border border-white/10 bg-white/5 px-3.5 py-2 text-sm text-white/60 transition hover:bg-white/8 hover:text-white"
            @click="launcher.logout()"
          >
            Выйти
          </button>

          <button
            class="flex items-center gap-2 rounded-[14px] bg-gradient-to-r from-violet-500 to-indigo-500 px-5 py-2 text-sm font-semibold text-white shadow-lg shadow-violet-500/25 transition hover:brightness-110 hover:shadow-violet-500/40 disabled:cursor-not-allowed disabled:opacity-50"
            :disabled="launcher.isBusy"
            @click="launcher.play()"
          >
            <svg v-if="!launcher.isBusy" class="h-3.5 w-3.5" fill="currentColor" viewBox="0 0 20 20">
              <path d="M6.3 2.841A1.5 1.5 0 0 0 4 4.11v11.78a1.5 1.5 0 0 0 2.3 1.269l9.344-5.89a1.5 1.5 0 0 0 0-2.538L6.3 2.84z" />
            </svg>
            <svg v-else class="h-3.5 w-3.5 animate-spin" fill="none" viewBox="0 0 24 24">
              <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="3" />
              <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 0 1 8-8V0C5.373 0 0 5.373 0 12h4z" />
            </svg>
            {{ launcher.isBusy ? 'Подготовка...' : 'Играть' }}
          </button>
        </div>

      </div>
    </header>

    <!-- ── Progress ────────────────────────────────────────────────── -->
    <section
      v-if="launcher.shouldShowProgress"
      class="rounded-[20px] border border-white/10 bg-[#091022]/85 px-5 py-3.5 backdrop-blur-xl"
    >
      <div class="flex items-center justify-between gap-3">
        <div class="min-w-0">
          <p class="text-sm font-medium text-white/90">{{ launcher.progress.title || 'Подготовка' }}</p>
          <p class="mt-0.5 truncate text-xs text-white/45">{{ launcher.progress.details || launcher.statusText }}</p>
        </div>
        <span class="shrink-0 text-xs font-medium tabular-nums text-white/50">{{ Math.round(launcher.progress.percent) }}%</span>
      </div>

      <div class="mt-3 h-1.5 overflow-hidden rounded-full bg-white/8">
        <div
          class="h-full rounded-full bg-gradient-to-r from-violet-500 to-indigo-400 transition-all duration-300"
          :style="{ width: `${launcher.progress.percent}%` }"
        ></div>
      </div>
    </section>

    <!-- ── Body ────────────────────────────────────────────────────── -->
    <div class="flex min-h-0 flex-1 gap-3">

      <!-- Sidebar -->
      <aside class="flex w-[200px] shrink-0 flex-col rounded-[24px] border border-white/10 bg-[#091022]/85 p-3 backdrop-blur-xl">
        <nav class="flex-1 space-y-4">
          <div v-for="group in navGroups" :key="group.label">
            <p class="mb-1 px-3 text-[9px] font-semibold uppercase tracking-[0.2em] text-white/25">
              {{ group.label }}
            </p>

            <div class="space-y-0.5">
              <RouterLink
                v-for="item in group.items"
                :key="item.to"
                :to="item.to"
                class="relative flex items-center gap-2.5 rounded-[12px] px-3 py-2 text-sm transition-all duration-150"
                :class="route.path === item.to
                  ? 'bg-violet-500/15 text-white'
                  : 'text-white/50 hover:bg-white/5 hover:text-white/85'"
              >
                <span
                  v-if="route.path === item.to"
                  class="absolute left-0 top-1/2 h-4 w-[3px] -translate-y-1/2 rounded-r-full bg-violet-400"
                ></span>

                <svg
                  class="h-[15px] w-[15px] shrink-0 transition-colors"
                  :class="route.path === item.to ? 'text-violet-300' : 'text-white/35'"
                  fill="none"
                  viewBox="0 0 24 24"
                  stroke-width="1.8"
                  stroke="currentColor"
                >
                  <path stroke-linecap="round" stroke-linejoin="round" :d="item.icon" />
                </svg>

                {{ item.title }}
              </RouterLink>
            </div>
          </div>
        </nav>

        <!-- Version footer -->
        <div class="mt-3 rounded-[14px] border border-white/8 bg-white/[0.03] px-3 py-2.5">
          <p class="text-[10px] font-medium text-white/30">VoidRP · v{{ launcher.launcherVersionText }}</p>
          <p class="mt-0.5 truncate text-[11px] leading-4 text-white/40">{{ launcher.statusText }}</p>
        </div>
      </aside>

      <!-- Main content -->
      <main class="min-h-0 flex-1 overflow-hidden rounded-[24px] border border-white/10 bg-[#091022]/85 backdrop-blur-xl">
        <div class="h-full overflow-auto p-5">
          <RouterView />
        </div>
      </main>

    </div>
  </div>
</template>
