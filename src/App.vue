<script setup lang="ts">
import { onBeforeUnmount, onMounted, watch } from 'vue'
import { RouterView, useRoute, useRouter } from 'vue-router'
import { useLauncherStore } from './stores/launcher'
import ToastHost from './components/ToastHost.vue'
import SplashScreen from './components/SplashScreen.vue'

const launcher = useLauncherStore()
const router = useRouter()
const route = useRoute()

onMounted(() => {
  void launcher.initializeApp()
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
</script>

<template>
  <div class="min-h-screen w-full overflow-hidden bg-[#04070d] text-white">
    <div class="pointer-events-none absolute inset-0 overflow-hidden">
      <div class="absolute left-1/2 top-0 h-[360px] w-[720px] -translate-x-1/2 bg-violet-600/10 blur-3xl"></div>
      <div class="absolute bottom-0 right-0 h-[320px] w-[320px] bg-fuchsia-500/10 blur-3xl"></div>
      <div class="absolute left-0 top-1/3 h-[260px] w-[260px] bg-cyan-400/10 blur-3xl"></div>
    </div>

    <div class="relative z-10 h-screen w-full">
      <RouterView />
      <ToastHost />
    </div>

    <Transition name="splash">
      <SplashScreen
        v-if="!launcher.initialized"
        :status-text="launcher.statusText"
        :progress="launcher.initProgress"
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
