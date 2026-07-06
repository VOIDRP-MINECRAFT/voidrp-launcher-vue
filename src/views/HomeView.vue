<script setup lang="ts">
import { computed } from 'vue'
import { useLauncherStore, type GameServer } from '../stores/launcher'

const launcher = useLauncherStore()

const servers = computed<GameServer[]>(() => launcher.serverList)
const activeServer = computed(() =>
  servers.value.find((s) => s.slug === launcher.selectedSlug) ?? servers.value.find((s) => s.isDefault) ?? servers.value[0] ?? null,
)

const bgStyle = computed(() => {
  const url = activeServer.value?.bannerUrl
  return url ? { backgroundImage: `url(${url})` } : {}
})

const online = computed(() => activeServer.value?.status?.online ?? null)

function selectServer(slug: string) {
  if (slug !== launcher.selectedSlug) launcher.selectServer(slug)
}
</script>

<template>
  <div class="relative -m-5 flex min-h-[calc(100%+2.5rem)] flex-col overflow-hidden">

    <!-- Background: server banner, crossfades on switch -->
    <Transition name="bg">
      <div :key="activeServer?.slug || 'none'" class="absolute inset-0">
        <div
          class="h-full w-full bg-cover bg-center transition-transform duration-[8000ms] ease-out"
          :style="bgStyle"
          :class="{ 'scale-105': true }"
        ></div>
      </div>
    </Transition>

    <!-- Fallback tint + readability overlays -->
    <div class="absolute inset-0 bg-gradient-to-br from-violet-950/50 via-[#070b16]/40 to-[#04070d]/70"></div>
    <div class="absolute inset-0 bg-gradient-to-t from-[#04070d] via-[#04070d]/55 to-transparent"></div>

    <!-- ── Content ─────────────────────────────────────────────── -->
    <div class="relative z-10 flex flex-1 flex-col p-6">

      <!-- Top: server ribbon + nickname -->
      <div class="flex items-start justify-between gap-4">
        <!-- Server ribbon -->
        <div v-if="servers.length > 1" class="flex min-w-0 flex-wrap gap-2">
          <button
            v-for="s in servers"
            :key="s.slug"
            type="button"
            class="group flex items-center gap-2 rounded-2xl border px-2.5 py-1.5 backdrop-blur-md transition"
            :class="activeServer?.slug === s.slug
              ? 'border-violet-400/50 bg-violet-500/20 shadow-lg shadow-violet-500/10'
              : 'border-white/10 bg-black/25 hover:border-white/25 hover:bg-black/40'"
            @click="selectServer(s.slug)"
          >
            <span class="relative h-6 w-6 shrink-0 overflow-hidden rounded-lg bg-[#0b1120]">
              <img v-if="s.iconUrl" :src="s.iconUrl" :alt="s.name" class="h-full w-full object-cover" />
              <span v-else class="flex h-full w-full items-center justify-center text-[10px] font-black text-violet-200">{{ s.name.charAt(0) }}</span>
            </span>
            <span class="max-w-[120px] truncate text-xs font-semibold"
              :class="activeServer?.slug === s.slug ? 'text-white' : 'text-white/60'">{{ s.name }}</span>
            <span
              class="h-1.5 w-1.5 shrink-0 rounded-full"
              :class="s.maintenance ? 'bg-amber-400' : s.status?.online ? 'bg-emerald-400' : 'bg-white/25'"
            ></span>
          </button>
        </div>

        <!-- Nickname -->
        <div class="ml-auto flex shrink-0 items-center gap-1.5 rounded-full border border-white/10 bg-black/30 px-3 py-1.5 backdrop-blur-md">
          <span class="h-1.5 w-1.5 animate-pulse rounded-full bg-emerald-400"></span>
          <span class="text-[11px] font-medium text-white/70">{{ launcher.playerStats.minecraftNickname || '—' }}</span>
        </div>
      </div>

      <!-- Spacer pushes identity to the bottom -->
      <div class="flex-1"></div>

      <!-- Server identity -->
      <Transition name="identity" mode="out-in">
        <div :key="activeServer?.slug || 'none'" class="flex items-end gap-4">
          <div class="h-20 w-20 shrink-0 overflow-hidden rounded-[22px] border border-white/15 bg-[#0b1120] shadow-2xl shadow-black/50">
            <img v-if="activeServer?.iconUrl" :src="activeServer.iconUrl" :alt="activeServer?.name" class="h-full w-full object-cover" />
            <div v-else class="flex h-full w-full items-center justify-center bg-gradient-to-br from-violet-600/50 to-indigo-800/50 text-3xl font-black text-violet-100">
              {{ (activeServer?.name || 'V').charAt(0) }}
            </div>
          </div>

          <div class="min-w-0 flex-1 pb-1">
            <div class="flex flex-wrap items-center gap-2">
              <h1 class="text-3xl font-black leading-none tracking-tight text-white drop-shadow-lg">
                {{ activeServer?.name || 'VoidRP' }}
              </h1>
              <span
                v-if="online !== null"
                class="inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[11px] font-semibold backdrop-blur-md"
                :class="online ? 'bg-emerald-400/15 text-emerald-300' : 'bg-rose-400/15 text-rose-300'"
              >
                <span class="h-1.5 w-1.5 rounded-full" :class="online ? 'bg-emerald-400' : 'bg-rose-400'"></span>
                <template v-if="online">{{ activeServer?.status?.playersOnline }} / {{ activeServer?.status?.playersMax }} онлайн</template>
                <template v-else>офлайн</template>
              </span>
            </div>

            <p v-if="activeServer?.description" class="mt-2 line-clamp-2 max-w-xl text-sm leading-6 text-white/60">
              {{ activeServer.description }}
            </p>

            <div class="mt-2.5 flex flex-wrap items-center gap-1.5 text-[11px] text-white/50">
              <span class="rounded-md bg-white/10 px-2 py-0.5 font-medium backdrop-blur-md">MC {{ activeServer?.mcVersion || '1.21.1' }}</span>
              <span class="rounded-md bg-white/10 px-2 py-0.5 font-medium backdrop-blur-md">{{ activeServer?.loader || 'neoforge' }}</span>
              <span v-if="activeServer" class="rounded-md bg-white/10 px-2 py-0.5 font-medium text-white/40 backdrop-blur-md">
                {{ activeServer.host }}<template v-if="activeServer.port !== 25565">:{{ activeServer.port }}</template>
              </span>
            </div>
          </div>
        </div>
      </Transition>

      <!-- Actions -->
      <div class="mt-6 flex flex-wrap items-center gap-2.5">
        <button
          class="flex items-center gap-2.5 rounded-[16px] bg-gradient-to-r from-violet-500 to-indigo-500 px-8 py-3.5 text-base font-bold text-white shadow-xl shadow-violet-500/30 transition hover:brightness-110 hover:shadow-violet-500/50 disabled:cursor-not-allowed disabled:opacity-50"
          :disabled="launcher.isBusy"
          @click="launcher.play()"
        >
          <svg v-if="!launcher.isBusy" class="h-4 w-4" fill="currentColor" viewBox="0 0 20 20">
            <path d="M6.3 2.841A1.5 1.5 0 0 0 4 4.11v11.78a1.5 1.5 0 0 0 2.3 1.269l9.344-5.89a1.5 1.5 0 0 0 0-2.538L6.3 2.84z"/>
          </svg>
          <svg v-else class="h-4 w-4 animate-spin" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="3"/>
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 0 1 8-8V0C5.373 0 0 5.373 0 12h4z"/>
          </svg>
          {{ launcher.isBusy ? 'Подготовка...' : 'Играть' }}
        </button>

        <button
          class="rounded-[16px] border border-white/15 bg-black/25 px-5 py-3.5 text-sm font-medium text-white/70 backdrop-blur-md transition hover:bg-black/40 hover:text-white disabled:cursor-not-allowed disabled:opacity-40"
          :disabled="launcher.isBusy"
          @click="launcher.repair()"
        >
          Починить клиент
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.bg-enter-active,
.bg-leave-active { transition: opacity 0.7s ease; }
.bg-enter-from,
.bg-leave-to { opacity: 0; }

.identity-enter-active,
.identity-leave-active { transition: opacity 0.35s ease, transform 0.35s ease; }
.identity-enter-from { opacity: 0; transform: translateY(8px); }
.identity-leave-to { opacity: 0; transform: translateY(-8px); }
</style>
