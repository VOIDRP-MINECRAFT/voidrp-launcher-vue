<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { useLauncherStore } from '../stores/launcher'
import { LAUNCHER_THEMES, getTheme } from '../theme/themes'

const launcher = useLauncherStore()
const themes = LAUNCHER_THEMES
const open = ref(false)
const rootEl = ref<HTMLElement | null>(null)

const current = computed(() => getTheme(launcher.themeId))

function pick(id: string) {
  launcher.setTheme(id)
  open.value = false
}
function onDocClick(e: MouseEvent) {
  if (open.value && rootEl.value && !rootEl.value.contains(e.target as Node)) open.value = false
}
function onKey(e: KeyboardEvent) {
  if (e.key === 'Escape') open.value = false
}
onMounted(() => {
  document.addEventListener('click', onDocClick)
  document.addEventListener('keydown', onKey)
})
onBeforeUnmount(() => {
  document.removeEventListener('click', onDocClick)
  document.removeEventListener('keydown', onKey)
})
</script>

<template>
  <div ref="rootEl" class="relative">
    <button
      class="btn-glass flex h-9 w-9 items-center justify-center rounded-[14px]"
      title="Тема оформления"
      @click.stop="open = !open"
    >
      <span
        class="h-4 w-4 rounded-full ring-2 ring-white/15"
        :style="{ backgroundImage: `linear-gradient(135deg, ${current.swatch[0]}, ${current.swatch[1]})` }"
      ></span>
    </button>

    <Transition name="fade">
      <div
        v-if="open"
        class="panel panel--strong absolute right-0 top-[calc(100%+8px)] z-50 w-52 p-2"
        style="position: absolute"
      >
        <p class="px-2 pb-1.5 pt-1 text-[10px] uppercase tracking-[0.2em] text-white/40">Тема</p>
        <button
          v-for="t in themes"
          :key="t.id"
          class="flex w-full items-center gap-2.5 rounded-[11px] px-2 py-2 text-left transition hover:bg-white/8"
          @click="pick(t.id)"
        >
          <span
            class="h-5 w-5 shrink-0 rounded-full ring-2 ring-white/10"
            :style="{ backgroundImage: `linear-gradient(135deg, ${t.swatch[0]}, ${t.swatch[1]})` }"
          ></span>
          <span class="flex-1 text-[13px] text-white/80">{{ t.name }}</span>
          <svg
            v-if="launcher.themeId === t.id"
            class="h-3.5 w-3.5 shrink-0"
            :style="{ color: 'var(--acc-soft)' }"
            fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="3"
          ><path stroke-linecap="round" stroke-linejoin="round" d="M5 13l4 4L19 7" /></svg>
        </button>
      </div>
    </Transition>
  </div>
</template>
