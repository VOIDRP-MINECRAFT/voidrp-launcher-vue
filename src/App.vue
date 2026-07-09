<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, watch } from 'vue'
import { RouterView, useRoute, useRouter } from 'vue-router'
import { useLauncherStore } from './stores/launcher'
import ToastHost from './components/ToastHost.vue'
import SplashScreen from './components/SplashScreen.vue'

const launcher = useLauncherStore()
const router = useRouter()
const route = useRoute()

// Prefer CoreHost's live download progress (real % + current file) during the
// bootstrap phase; fall back to the coarse init steps when it's not reporting.
const splashProgress = computed(() => {
  const p = launcher.progress
  return p?.visible && p.percent > 0 ? p.percent : launcher.initProgress
})
const splashDetail = computed(() => (launcher.progress?.visible ? launcher.progress.details : ''))

onMounted(() => {
  void launcher.initializeApp()
  void launcher.fetchServers()
})

onBeforeUnmount(() => {
  launcher.dispose()
})

watch(
  () => [launcher.isAuthenticated, route.path] as const,
  ([isAuthenticated, currentPath]) => {
    if (isAuthenticated) {
      if (currentPath === '/login') {
        router.replace('/home')
      }
      return
    }

    if (currentPath !== '/login') {
      router.replace('/login')
    }
  },
  { immediate: true }
)

// ── Per-server accent theme ────────────────────────────────────────────────
// The active server's accent_color drives the --acc* CSS vars on the app
// root; every accent in the UI (incl. remapped violet utilities) follows it.
const ACCENT_FALLBACK = '#8b5cf6'

function parseHex(hex: string | null | undefined): [number, number, number] {
  const m = /^#?([0-9a-f]{6})$/i.exec(hex || '')
  if (!m) return [139, 92, 246]
  const n = parseInt(m[1], 16)
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255]
}

function mixRgb(a: number[], b: number[], t: number) {
  return a.map((v, i) => Math.round(v + (b[i] - v) * t))
}

const themeVars = computed(() => {
  const active = launcher.serverList.find((s) => s.slug === launcher.selectedSlug)
  const base = parseHex(active?.accentColor || ACCENT_FALLBACK)
  const second = mixRgb(base, [30, 20, 90], 0.3) // глубже и чуть синее — второй стоп градиентов
  const soft = mixRgb(base, [255, 255, 255], 0.3)
  const pale = mixRgb(base, [255, 255, 255], 0.56)
  const rgb = (c: number[]) => c.join(', ')
  const hex = (c: number[]) => '#' + c.map((v) => v.toString(16).padStart(2, '0')).join('')
  return {
    '--acc': hex(base),
    '--acc-2': hex(second),
    '--acc-soft': hex(soft),
    '--acc-pale': hex(pale),
    '--acc-rgb': rgb(base),
    '--acc-2-rgb': rgb(second),
    '--acc-soft-rgb': rgb(soft),
  }
})
</script>

<template>
  <div class="min-h-screen w-full overflow-hidden bg-[var(--bg-0)] text-white" :style="themeVars">
    <!-- Ambient background: aurora + noise + vignette -->
    <div class="pointer-events-none absolute inset-0 overflow-hidden">
      <div class="aurora aurora--1"></div>
      <div class="aurora aurora--2"></div>
      <div class="aurora aurora--3"></div>
    </div>
    <div class="bg-noise"></div>
    <div class="vignette"></div>

    <div class="relative z-10 h-screen w-full">
      <RouterView />
      <ToastHost />
    </div>

    <Transition name="splash">
      <SplashScreen
        v-if="!launcher.initialized"
        :status-text="launcher.statusText"
        :detail="splashDetail"
        :progress="splashProgress"
      />
    </Transition>
  </div>
</template>

<style>
.splash-enter-active {
  transition: opacity 0.3s ease;
}
.splash-enter-from {
  opacity: 0;
}
.splash-leave-active {
  transition: opacity 0.7s cubic-bezier(0.4, 0, 0.2, 1), transform 0.7s cubic-bezier(0.4, 0, 0.2, 1);
}
.splash-leave-to {
  opacity: 0;
  transform: scale(1.05);
}
</style>
