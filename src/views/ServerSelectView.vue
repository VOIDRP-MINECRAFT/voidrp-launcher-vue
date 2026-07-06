<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useLauncherStore, type GameServer } from '../stores/launcher'

const launcher = useLauncherStore()
const router = useRouter()
const loading = ref(true)
const busy = ref<string | null>(null)

onMounted(async () => {
  await launcher.fetchServers()
  loading.value = false
})

const servers = computed<GameServer[]>(() => launcher.serverList)

function statusText(s: GameServer) {
  if (s.maintenance) return 'Тех. работы'
  return s.status?.online ? 'Онлайн' : 'Офлайн'
}

async function choose(s: GameServer) {
  busy.value = s.slug
  await launcher.selectServer(s.slug)
  busy.value = null
  router.push('/home')
}
</script>

<template>
  <div class="min-h-screen bg-[#0a0e1a] text-slate-100 px-6 py-10">
    <div class="mx-auto max-w-4xl">
      <div class="mb-8 text-center">
        <h1 class="text-2xl font-black tracking-tight">Выбор сервера</h1>
        <p class="mt-1 text-sm text-slate-500">У каждого сервера своя сборка, экономика и государства</p>
      </div>

      <div v-if="loading" class="grid gap-4 sm:grid-cols-2">
        <div v-for="n in 2" :key="n" class="h-52 animate-pulse rounded-2xl bg-white/5" />
      </div>

      <div v-else-if="!servers.length" class="rounded-2xl border border-white/10 bg-white/5 p-10 text-center text-slate-400">
        Список серверов недоступен. Проверь подключение.
      </div>

      <div v-else class="grid gap-4 sm:grid-cols-2">
        <article
          v-for="s in servers"
          :key="s.slug"
          class="group overflow-hidden rounded-2xl border border-white/10 bg-gradient-to-b from-white/[0.06] to-transparent transition hover:border-violet-500/40"
          :class="{ 'ring-1 ring-violet-500/50': launcher.selectedSlug === s.slug }"
        >
          <div
            class="h-24 bg-cover bg-center"
            :style="s.bannerUrl ? { backgroundImage: `url(${s.bannerUrl})` } : { background: 'linear-gradient(135deg,#1a1440,#0d1020)' }"
          >
            <div class="flex justify-end p-2">
              <span
                class="rounded-full bg-black/60 px-2.5 py-1 text-xs font-bold backdrop-blur"
                :class="s.status?.online ? 'text-emerald-300' : s.maintenance ? 'text-amber-300' : 'text-rose-300'"
              >
                ● {{ statusText(s) }}
              </span>
            </div>
          </div>

          <div class="p-4">
            <div class="-mt-10 mb-3 flex items-center gap-3">
              <img v-if="s.iconUrl" :src="s.iconUrl" class="h-12 w-12 rounded-xl border-2 border-[#0a0e1a] object-cover" alt="" />
              <div v-else class="flex h-12 w-12 items-center justify-center rounded-xl border-2 border-[#0a0e1a] bg-violet-900 text-lg font-black text-violet-300">
                {{ s.name.charAt(0) }}
              </div>
              <div class="min-w-0 pt-8">
                <h2 class="truncate text-base font-bold">{{ s.name }}</h2>
                <div class="text-xs text-slate-500">MC {{ s.mcVersion }} · {{ s.loader }}</div>
              </div>
            </div>

            <p v-if="s.description" class="mb-3 line-clamp-2 text-sm text-slate-400">{{ s.description }}</p>

            <div class="mb-4 flex items-center gap-4 text-xs text-slate-400">
              <span>
                <span class="font-bold text-slate-200">
                  {{ s.status?.online ? `${s.status.playersOnline}/${s.status.playersMax}` : '—' }}
                </span>
                игроков
              </span>
              <span class="text-slate-600">{{ s.host }}<template v-if="s.port !== 25565">:{{ s.port }}</template></span>
            </div>

            <button
              type="button"
              class="w-full rounded-xl bg-violet-600 py-2.5 text-sm font-bold text-white transition hover:bg-violet-500 disabled:opacity-50"
              :disabled="busy === s.slug"
              @click="choose(s)"
            >
              {{ busy === s.slug ? 'Выбираю…' : launcher.selectedSlug === s.slug ? 'Продолжить' : 'Выбрать' }}
            </button>
          </div>
        </article>
      </div>
    </div>
  </div>
</template>
