<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useLauncherStore, type CrashAction, type PreflightWarning } from '../stores/launcher'

const launcher = useLauncherStore()
const router = useRouter()
const open = computed(() => launcher.preflightOpen)
const warnings = computed<PreflightWarning[]>(() => launcher.preflightWarnings)
const mute = ref(false)

watch(open, (value) => { if (value) mute.value = false })

const hasCritical = computed(() => warnings.value.some((w) => w.severity === 'critical'))
const canMute = computed(() => warnings.value.some((w) => w.severity !== 'critical'))

const tone: Record<PreflightWarning['severity'], string> = {
  critical: 'border-rose-400/25 bg-rose-400/[0.07] text-rose-300',
  warning: 'border-amber-400/25 bg-amber-400/[0.06] text-amber-300',
  info: 'border-sky-400/20 bg-sky-400/[0.05] text-sky-300',
}

async function runAction(action: CrashAction) {
  if (action.type === 'open_settings') {
    launcher.closePreflight()
    await router.push('/settings')
  } else if (action.type === 'show_crash') {
    launcher.closePreflight()
    await launcher.restoreCrash()
  }
}
</script>

<template>
  <Transition name="preflight-fade">
    <div
      v-if="open"
      class="fixed inset-0 z-[190] flex items-center justify-center p-6"
      style="background: rgba(4, 6, 12, 0.7); backdrop-filter: blur(6px);"
      @click.self="launcher.closePreflight()"
    >
      <div class="panel panel--strong panel--edge w-full max-w-[520px] overflow-hidden">
        <div class="px-6 pt-6">
          <h2 class="text-[17px] font-bold leading-tight text-white">Перед запуском</h2>
          <p class="mt-1 text-[12px] text-white/45">
            Лаунчер нашёл то, из-за чего игра может упасть. Лучше исправить сейчас, чем ждать загрузку зря.
          </p>
        </div>

        <div class="flex flex-col gap-3 px-6 pt-4">
          <div
            v-for="warning in warnings"
            :key="warning.id"
            class="rounded-[13px] border p-4"
            :class="tone[warning.severity] ?? tone.warning"
          >
            <p class="text-[13px] font-semibold">{{ warning.title }}</p>
            <p class="mt-1 text-[13px] leading-6 text-white/75">{{ warning.message }}</p>
            <div v-if="warning.actions.length" class="mt-3 flex flex-wrap gap-2">
              <button
                v-for="action in warning.actions"
                :key="action.index"
                class="btn-glass rounded-[11px] px-3.5 py-1.5 text-[12px] font-semibold text-white/85 hover:text-white"
                @click="runAction(action)"
              >
                {{ action.label }}
              </button>
            </div>
          </div>
        </div>

        <div class="flex flex-wrap items-center justify-between gap-3 px-6 pb-6 pt-5">
          <label v-if="canMute" class="flex cursor-pointer items-center gap-2 text-[12px] text-white/50">
            <input v-model="mute" type="checkbox" class="accent-[var(--acc)]" />
            Больше не предупреждать об этом
          </label>
          <span v-else></span>
          <div class="flex gap-2">
            <button
              class="btn-glass rounded-[12px] px-4 py-2 text-[13px] font-medium text-white/70 hover:text-white"
              @click="launcher.closePreflight()"
            >
              Отмена
            </button>
            <button
              class="rounded-[12px] px-4 py-2 text-[13px] font-bold"
              :class="hasCritical ? 'btn-glass text-white/70 hover:text-white' : 'btn-acc'"
              :disabled="launcher.isBusy"
              @click="launcher.playDespitePreflight(mute)"
            >
              Всё равно запустить
            </button>
          </div>
        </div>
      </div>
    </div>
  </Transition>
</template>

<style scoped>
.preflight-fade-enter-active,
.preflight-fade-leave-active {
  transition: opacity 0.18s ease;
}
.preflight-fade-enter-from,
.preflight-fade-leave-to {
  opacity: 0;
}
</style>
