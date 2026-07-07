<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useLauncherStore, type GameServer } from '../stores/launcher'

const launcher = useLauncherStore()
const router = useRouter()

const loading = ref(true)
const committing = ref(false)
// The row the user is currently highlighting (defaults to the active server).
const pending = ref<string | null>(null)

onMounted(async () => {
  await launcher.fetchServers()
  pending.value = launcher.selectedSlug ?? launcher.serverList.find((s) => s.isDefault)?.slug ?? launcher.serverList[0]?.slug ?? null
  loading.value = false
})

const servers = computed<GameServer[]>(() => launcher.serverList)
const pendingServer = computed(() => servers.value.find((s) => s.slug === pending.value) ?? null)

function statusOf(s: GameServer) {
  if (s.maintenance) return { label: 'Тех. работы', tone: 'amber' as const }
  return s.status?.online
    ? { label: 'Онлайн', tone: 'emerald' as const }
    : { label: 'Офлайн', tone: 'rose' as const }
}

function whitelistLabel(mode: string) {
  if (mode === 'whitelist') return 'Вайтлист'
  if (mode === 'invite') return 'По приглашению'
  return 'Открытый'
}

function pick(s: GameServer) {
  pending.value = s.slug
}

async function confirm() {
  if (!pending.value) return
  committing.value = true
  await launcher.selectServer(pending.value)
  committing.value = false
  router.push('/home')
}

function goHome() {
  router.push('/home')
}
</script>

<template>
  <div class="flex h-full items-center justify-center p-6">
    <div class="flex h-full max-h-[720px] w-full max-w-[560px] flex-col">

      <!-- Header -->
      <div class="mb-5 flex items-start justify-between gap-4">
        <div>
          <div class="flex items-center gap-2">
            <div class="flex h-8 w-8 items-center justify-center rounded-xl bg-gradient-to-br from-violet-500 to-indigo-600">
              <svg class="h-4 w-4 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2">
                <rect x="3" y="4" width="18" height="7" rx="2" /><rect x="3" y="13" width="18" height="7" rx="2" />
                <circle cx="7" cy="7.5" r="0.6" fill="currentColor" /><circle cx="7" cy="16.5" r="0.6" fill="currentColor" />
              </svg>
            </div>
            <span class="text-[11px] font-bold uppercase tracking-[0.3em] text-white/60">VoidRP</span>
          </div>
          <h1 class="mt-3.5 text-[30px] font-bold leading-none tracking-tight">Выбор сервера</h1>
          <p class="mt-2 text-sm leading-6 text-white/45">
            У каждого сервера своя сборка, экономика и государства.
          </p>
        </div>

        <button
          class="btn-glass shrink-0 rounded-xl p-2 text-white/40 hover:text-white/85"
          title="Закрыть"
          @click="goHome"
        >
          <svg class="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.2">
            <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12" />
          </svg>
        </button>
      </div>

      <!-- List -->
      <div class="min-h-0 flex-1 space-y-2.5 overflow-y-auto pr-1">

        <!-- Loading -->
        <template v-if="loading">
          <div v-for="n in 3" :key="n" class="h-[92px] animate-pulse rounded-[20px] border border-white/8 bg-white/[0.03]" />
        </template>

        <!-- Empty -->
        <div v-else-if="!servers.length" class="rounded-[20px] border border-white/10 bg-white/[0.03] p-8 text-center text-sm text-white/40">
          Список серверов недоступен. Проверь подключение и попробуй позже.
        </div>

        <!-- Rows -->
        <button
          v-for="s in servers"
          v-else
          :key="s.slug"
          type="button"
          class="group relative flex w-full items-center gap-3.5 rounded-[20px] border p-3.5 text-left transition duration-150"
          :class="pending === s.slug
            ? 'border-violet-400/50 bg-violet-500/10 shadow-lg shadow-violet-500/10'
            : 'border-white/10 bg-white/[0.035] hover:border-white/20 hover:bg-white/[0.06]'"
          @click="pick(s)"
        >
          <!-- Icon -->
          <div class="relative h-14 w-14 shrink-0 overflow-hidden rounded-2xl border border-white/10 bg-[#0b1120]">
            <img v-if="s.iconUrl" :src="s.iconUrl" :alt="s.name" class="h-full w-full object-cover" />
            <div v-else class="flex h-full w-full items-center justify-center bg-gradient-to-br from-violet-600/40 to-indigo-800/40 text-xl font-black text-violet-200">
              {{ s.name.charAt(0) }}
            </div>
          </div>

          <!-- Info -->
          <div class="min-w-0 flex-1">
            <div class="flex items-center gap-2">
              <h2 class="truncate text-[15px] font-bold text-white">{{ s.name }}</h2>
              <span v-if="s.slug === launcher.selectedSlug" class="shrink-0 rounded-md bg-white/8 px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-wider text-white/50">
                текущий
              </span>
            </div>
            <p class="mt-0.5 line-clamp-1 text-xs text-white/45">
              {{ s.description || `${s.host}${s.port !== 25565 ? ':' + s.port : ''}` }}
            </p>
            <div class="mt-1.5 flex flex-wrap items-center gap-1.5">
              <span class="rounded-md bg-white/6 px-1.5 py-0.5 text-[10px] font-medium text-white/50">MC {{ s.mcVersion }}</span>
              <span class="rounded-md bg-white/6 px-1.5 py-0.5 text-[10px] font-medium text-white/50">{{ s.loader }}</span>
              <span class="rounded-md bg-white/6 px-1.5 py-0.5 text-[10px] font-medium text-white/40">{{ whitelistLabel(s.whitelistMode) }}</span>
            </div>
          </div>

          <!-- Status + select -->
          <div class="flex shrink-0 flex-col items-end gap-2">
            <span
              class="inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[10px] font-semibold"
              :class="{
                'bg-emerald-400/10 text-emerald-300': statusOf(s).tone === 'emerald',
                'bg-rose-400/10 text-rose-300': statusOf(s).tone === 'rose',
                'bg-amber-400/10 text-amber-300': statusOf(s).tone === 'amber',
              }"
            >
              <span
                class="h-1.5 w-1.5 rounded-full"
                :class="{
                  'bg-emerald-400': statusOf(s).tone === 'emerald',
                  'bg-rose-400': statusOf(s).tone === 'rose',
                  'bg-amber-400': statusOf(s).tone === 'amber',
                }"
              />
              {{ statusOf(s).label }}
            </span>
            <span v-if="s.status?.online" class="text-[11px] tabular-nums text-white/40">
              {{ s.status.playersOnline }} / {{ s.status.playersMax }} игроков
            </span>

            <!-- Radio indicator -->
            <span
              class="mt-0.5 flex h-5 w-5 items-center justify-center rounded-full border transition"
              :class="pending === s.slug ? 'border-violet-400 bg-violet-500' : 'border-white/20 bg-transparent group-hover:border-white/40'"
            >
              <svg v-if="pending === s.slug" class="h-3 w-3 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="3">
                <path stroke-linecap="round" stroke-linejoin="round" d="m5 13 4 4L19 7" />
              </svg>
            </span>
          </div>
        </button>
      </div>

      <!-- Footer action -->
      <div class="mt-4 flex items-center gap-3">
        <div class="min-w-0 flex-1 text-xs text-white/35">
          <template v-if="pendingServer">
            Выбран: <span class="font-semibold text-white/70">{{ pendingServer.name }}</span>
          </template>
        </div>
        <button
          class="btn-acc flex shrink-0 items-center gap-2 rounded-[14px] px-6 py-2.5 text-sm font-bold"
          :disabled="!pending || committing"
          @click="confirm"
        >
          <svg v-if="!committing" class="h-3.5 w-3.5" fill="currentColor" viewBox="0 0 20 20">
            <path d="M6.3 2.841A1.5 1.5 0 0 0 4 4.11v11.78a1.5 1.5 0 0 0 2.3 1.269l9.344-5.89a1.5 1.5 0 0 0 0-2.538L6.3 2.84z" />
          </svg>
          <svg v-else class="h-3.5 w-3.5 animate-spin" fill="none" viewBox="0 0 24 24">
            <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="3" />
            <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 0 1 8-8V0C5.373 0 0 5.373 0 12h4z" />
          </svg>
          {{ committing ? 'Сохраняем...' : 'Продолжить' }}
        </button>
      </div>

    </div>
  </div>
</template>
