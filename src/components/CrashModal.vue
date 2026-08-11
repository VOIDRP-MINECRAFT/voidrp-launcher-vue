<script setup lang="ts">
import { computed, ref } from 'vue'
import { useLauncherStore } from '../stores/launcher'

const launcher = useLauncherStore()
const crash = computed(() => launcher.activeCrash)
const copied = ref(false)

function close() {
  launcher.dismissCrash()
}

async function repairAndClose() {
  launcher.dismissCrash()
  await launcher.repair()
}

async function copyDetails() {
  const c = crash.value
  if (!c) return
  const text = `VoidRP — отчёт о сбое\n${c.title}\nКод: ${c.exitCode} (${c.exitCodeHex})\n\nПричина:\n${c.cause}\n\nРешение:\n${c.solution}`
  try {
    await navigator.clipboard.writeText(text)
    copied.value = true
    window.setTimeout(() => (copied.value = false), 2000)
  } catch {
    /* clipboard unavailable — ignore */
  }
}
</script>

<template>
  <Transition name="crash-fade">
    <div
      v-if="crash"
      class="fixed inset-0 z-[200] flex items-center justify-center p-6"
      style="background: rgba(4, 6, 12, 0.72); backdrop-filter: blur(6px);"
      @click.self="close"
    >
      <div class="panel panel--strong panel--edge w-full max-w-[520px] overflow-hidden">
        <!-- Header -->
        <div class="flex items-start gap-3 px-6 pt-6">
          <div
            class="flex h-11 w-11 shrink-0 items-center justify-center rounded-[14px] border border-amber-400/25 bg-amber-400/10 text-amber-300"
          >
            <svg class="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.8">
              <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v4m0 4h.01M10.3 3.9 2.4 18a1.8 1.8 0 0 0 1.6 2.7h16a1.8 1.8 0 0 0 1.6-2.7L13.7 3.9a1.8 1.8 0 0 0-3.1 0Z" />
            </svg>
          </div>
          <div class="min-w-0 flex-1">
            <h2 class="text-[17px] font-bold leading-tight text-white">{{ crash.title }}</h2>
            <p class="mt-0.5 text-[11px] font-medium text-white/35">
              Код завершения {{ crash.exitCode }} · {{ crash.exitCodeHex }}
            </p>
          </div>
        </div>

        <!-- Body -->
        <div class="px-6 pt-4">
          <div class="rounded-[13px] border border-white/8 bg-white/[0.03] p-4">
            <p class="text-[11px] font-semibold uppercase tracking-wide text-white/35">Что произошло</p>
            <p class="mt-1 whitespace-pre-line text-[13px] leading-6 text-white/70">{{ crash.cause }}</p>
          </div>
          <div class="mt-3 rounded-[13px] border border-emerald-400/15 bg-emerald-400/[0.05] p-4">
            <p class="text-[11px] font-semibold uppercase tracking-wide text-emerald-300/70">Как исправить</p>
            <p class="mt-1 whitespace-pre-line text-[13px] leading-6 text-white/80">{{ crash.solution }}</p>
          </div>
        </div>

        <!-- Actions -->
        <div class="flex items-center justify-between gap-2 px-6 pb-6 pt-5">
          <button
            class="btn-glass rounded-[12px] px-3.5 py-2 text-[12px] font-medium text-white/60 hover:text-white"
            @click="copyDetails"
          >
            {{ copied ? 'Скопировано' : 'Скопировать отчёт' }}
          </button>
          <div class="flex gap-2">
            <button
              class="btn-glass rounded-[12px] px-4 py-2 text-[13px] font-medium text-white/70 hover:text-white"
              @click="close"
            >
              Закрыть
            </button>
            <button
              class="btn-acc rounded-[12px] px-4 py-2 text-[13px] font-bold"
              :disabled="launcher.isBusy"
              @click="repairAndClose"
            >
              Переустановить
            </button>
          </div>
        </div>
      </div>
    </div>
  </Transition>
</template>

<style scoped>
.crash-fade-enter-active,
.crash-fade-leave-active {
  transition: opacity 0.18s ease;
}
.crash-fade-enter-from,
.crash-fade-leave-to {
  opacity: 0;
}
</style>
