<script setup lang="ts">
import { computed } from 'vue'
import { useLauncherStore } from '../stores/launcher'

const launcher = useLauncherStore()
const toasts = computed(() => launcher.toasts)

function toastStyle(tone: string) {
  switch (tone) {
    case 'success': return { wrap: 'border-emerald-400/20 bg-[#060f0a]/90',  bar: 'bg-emerald-400', text: 'text-emerald-300' }
    case 'warning': return { wrap: 'border-amber-300/20 bg-[#0f0d06]/90',   bar: 'bg-amber-400',   text: 'text-amber-300'  }
    case 'error':   return { wrap: 'border-red-400/20 bg-[#0f0609]/90',      bar: 'bg-red-400',     text: 'text-red-300'   }
    default:        return { wrap: 'border-white/10 bg-[#080f1a]/90',        bar: 'bg-violet-400',  text: 'text-violet-300' }
  }
}

// SVG icon paths per tone
function toastIcon(tone: string): string {
  switch (tone) {
    case 'success': return 'M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0z'
    case 'warning': return 'M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z'
    case 'error':   return 'M9.75 9.75l4.5 4.5m0-4.5-4.5 4.5M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0z'
    default:        return 'M11.25 11.25l.041-.02a.75.75 0 0 1 1.063.852l-.708 2.836a.75.75 0 0 0 1.063.853l.041-.021M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0z'
  }
}
</script>

<template>
  <div class="pointer-events-none fixed right-4 top-4 z-[999] flex w-[340px] max-w-[calc(100vw-2rem)] flex-col gap-2">
    <TransitionGroup name="toast">
      <div
        v-for="toast in toasts"
        :key="toast.id"
        class="pointer-events-auto relative overflow-hidden rounded-[18px] border shadow-2xl shadow-black/40 backdrop-blur-xl"
        :class="toastStyle(toast.tone).wrap"
      >
        <!-- Accent left bar -->
        <div class="absolute inset-y-0 left-0 w-[3px] rounded-l-[18px]"
          :class="toastStyle(toast.tone).bar"></div>

        <div class="flex items-start gap-3 px-4 py-3 pl-5">
          <!-- Icon -->
          <svg class="mt-0.5 h-4 w-4 shrink-0" :class="toastStyle(toast.tone).text"
            fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor">
            <path stroke-linecap="round" stroke-linejoin="round" :d="toastIcon(toast.tone)" />
          </svg>

          <!-- Content -->
          <div class="min-w-0 flex-1">
            <p class="text-sm font-semibold text-white/90">{{ toast.title }}</p>
            <p class="mt-0.5 text-xs leading-5 text-white/55">{{ toast.message }}</p>
          </div>

          <!-- Close -->
          <button
            class="ml-1 mt-0.5 shrink-0 rounded-lg p-1 text-white/30 transition hover:bg-white/8 hover:text-white/70"
            @click="launcher.dismissToast(toast.id)"
          >
            <svg class="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke-width="2.5" stroke="currentColor">
              <path stroke-linecap="round" stroke-linejoin="round" d="M6 18 18 6M6 6l12 12"/>
            </svg>
          </button>
        </div>
      </div>
    </TransitionGroup>
  </div>
</template>

<style scoped>
.toast-enter-active { transition: all 0.25s cubic-bezier(0.16, 1, 0.3, 1); }
.toast-leave-active { transition: all 0.18s ease; }
.toast-enter-from   { opacity: 0; transform: translateX(16px) scale(0.97); }
.toast-leave-to     { opacity: 0; transform: translateX(10px) scale(0.97); }
.toast-move         { transition: transform 0.2s ease; }
</style>
