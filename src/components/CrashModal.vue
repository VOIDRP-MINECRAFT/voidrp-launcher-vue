<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { SERVER_SIDE_CRASH_ACTIONS, useLauncherStore, type CrashAction } from '../stores/launcher'

const launcher = useLauncherStore()
const router = useRouter()
const crash = computed(() => launcher.activeCrash)
const copied = ref(false)
const runningIndex = ref<number | null>(null)
// Server-side fixes applied in this window: after one succeeds, "Запустить снова" becomes the main button.
const fixedIndexes = ref<number[]>([])

watch(() => crash.value?.id, () => {
  runningIndex.value = null
  fixedIndexes.value = []
  copied.value = false
})

const actions = computed<CrashAction[]>(() => (crash.value?.actions ?? []).filter((a) => a.type !== 'copy_report'))
const wantsCopyHighlight = computed(() => (crash.value?.actions ?? []).some((a) => a.type === 'copy_report'))
const anyFixed = computed(() => fixedIndexes.value.length > 0)

// The first not-yet-applied fix is the main button; once something is fixed, relaunching is.
const primaryIndex = computed(() => {
  const list = actions.value
  if (anyFixed.value) return list.find((a) => a.type === 'relaunch')?.index ?? -1
  return (list.find((a) => SERVER_SIDE_CRASH_ACTIONS.has(a.type) || a.type === 'open_settings') ?? list[0])?.index ?? -1
})

function close() {
  launcher.dismissCrash()
}

async function copyDetails() {
  const c = crash.value
  if (!c) return
  const text = `VoidRP — отчёт о сбое\n${c.title}\nКод: ${c.exitCode} (${c.exitCodeHex})\nПравило: ${c.ruleKey || 'не распознано'}, повторов: ${c.repeatCount}\n\nПричина:\n${c.cause}\n\nРешение:\n${c.solution}`
  try {
    await navigator.clipboard.writeText(text)
    copied.value = true
    window.setTimeout(() => (copied.value = false), 2000)
  } catch {
    /* clipboard unavailable — ignore */
  }
}

async function runAction(action: CrashAction) {
  const c = crash.value
  if (!c || runningIndex.value !== null) return

  switch (action.type) {
    case 'relaunch':
      close()
      await launcher.play()
      return
    case 'open_settings':
      close()
      await router.push('/settings')
      return
    case 'copy_report':
      await copyDetails()
      return
  }

  if (!SERVER_SIDE_CRASH_ACTIONS.has(action.type)) return
  runningIndex.value = action.index
  try {
    const response = await launcher.runCrashAction(c.id, action.index)
    if (response?.ok) fixedIndexes.value = [...fixedIndexes.value, action.index]
  } finally {
    runningIndex.value = null
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
      <div class="panel panel--strong panel--edge w-full max-w-[540px] overflow-hidden">
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
            <p class="mt-0.5 flex flex-wrap items-center gap-2 text-[11px] font-medium text-white/35">
              <span>Код завершения {{ crash.exitCode }} · {{ crash.exitCodeHex }}</span>
              <span
                v-if="crash.repeatCount > 1"
                class="rounded-full border border-rose-400/25 bg-rose-400/10 px-2 py-0.5 text-rose-300"
              >Повторяется ×{{ crash.repeatCount }}</span>
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
          <p v-if="anyFixed" class="mt-3 text-[12px] font-medium text-emerald-300">
            Исправление применено — запустите игру.
          </p>
        </div>

        <!-- Actions -->
        <div class="flex flex-wrap items-center justify-between gap-2 px-6 pb-6 pt-5">
          <button
            class="rounded-[12px] px-3.5 py-2 text-[12px] font-medium"
            :class="wantsCopyHighlight ? 'btn-acc font-bold' : 'btn-glass text-white/60 hover:text-white'"
            @click="copyDetails"
          >
            {{ copied ? 'Скопировано' : 'Скопировать отчёт' }}
          </button>
          <div class="flex flex-wrap justify-end gap-2">
            <button
              class="btn-glass rounded-[12px] px-4 py-2 text-[13px] font-medium text-white/70 hover:text-white"
              @click="close"
            >
              Закрыть
            </button>
            <button
              v-for="action in actions"
              :key="action.index"
              class="rounded-[12px] px-4 py-2 text-[13px] disabled:cursor-not-allowed disabled:opacity-50"
              :class="action.index === primaryIndex ? 'btn-acc font-bold' : 'btn-glass font-medium text-white/80 hover:text-white'"
              :disabled="launcher.isBusy || runningIndex !== null || fixedIndexes.includes(action.index)"
              @click="runAction(action)"
            >
              <template v-if="runningIndex === action.index">Выполняем…</template>
              <template v-else-if="fixedIndexes.includes(action.index)">✓ {{ action.label }}</template>
              <template v-else>{{ action.label }}</template>
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
