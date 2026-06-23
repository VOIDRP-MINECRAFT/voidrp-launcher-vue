<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useLauncherStore } from '../stores/launcher'

const launcher = useLauncherStore()
const memoryMb = ref(launcher.currentMemoryMb)

watch(() => launcher.currentMemoryMb, (v) => { memoryMb.value = v }, { immediate: true })

const MEMORY_MIN = 2048
const MEMORY_MAX = 16384
const MEMORY_STEP = 512

const memoryGb = computed(() => (memoryMb.value / 1024).toFixed(1))
const memoryPct = computed(() => ((memoryMb.value - MEMORY_MIN) / (MEMORY_MAX - MEMORY_MIN)) * 100)

const presets = [
  { label: '4 GB', mb: 4096 },
  { label: '6 GB', mb: 6144 },
  { label: '8 GB', mb: 8192 },
]

function increase() { memoryMb.value = Math.min(MEMORY_MAX, memoryMb.value + MEMORY_STEP) }
function decrease() { memoryMb.value = Math.max(MEMORY_MIN, memoryMb.value - MEMORY_STEP) }

const folderActions = [
  { label: 'Логи',   action: () => launcher.openPath(launcher.logsDirectory) },
  { label: 'Данные', action: () => launcher.openPath(launcher.dataDirectory)  },
  { label: 'Игра',   action: () => launcher.openPath(launcher.gameDirectory)  },
]
</script>

<template>
  <div class="max-w-[720px] space-y-4">

    <div>
      <p class="text-[11px] uppercase tracking-[0.25em] text-violet-300/70">Настройки</p>
      <h2 class="mt-1.5 text-2xl font-semibold">Параметры лаунчера</h2>
    </div>

    <!-- Memory -->
    <section class="rounded-[22px] border border-white/10 bg-white/[0.035] p-5">
      <div class="flex items-start justify-between gap-4">
        <div>
          <p class="text-sm font-semibold">Память для Minecraft</p>
          <p class="mt-1 text-xs text-white/45">Рекомендуется 4–8 GB. Больше не всегда лучше.</p>
        </div>
        <div class="text-right">
          <p class="text-2xl font-bold tabular-nums text-white">{{ memoryGb }}</p>
          <p class="text-[10px] text-white/35">GB</p>
        </div>
      </div>

      <!-- Visual bar -->
      <div class="mt-4 h-2 overflow-hidden rounded-full bg-white/8">
        <div
          class="h-full rounded-full bg-gradient-to-r from-violet-500 to-indigo-400 transition-all duration-150"
          :style="{ width: `${memoryPct}%` }"
        ></div>
      </div>
      <div class="mt-1.5 flex justify-between text-[10px] text-white/25">
        <span>2 GB</span>
        <span>16 GB</span>
      </div>

      <!-- Controls -->
      <div class="mt-4 flex items-center gap-2">
        <button
          class="flex h-10 w-10 items-center justify-center rounded-[12px] border border-white/10 bg-white/5 text-lg text-white/60 transition hover:bg-white/8 hover:text-white"
          @click="decrease"
        >−</button>

        <div class="flex-1 rounded-[12px] border border-white/10 bg-white/5 py-2.5 text-center text-sm font-semibold tabular-nums">
          {{ memoryMb }} MB
        </div>

        <button
          class="flex h-10 w-10 items-center justify-center rounded-[12px] border border-white/10 bg-white/5 text-lg text-white/60 transition hover:bg-white/8 hover:text-white"
          @click="increase"
        >+</button>
      </div>

      <!-- Presets -->
      <div class="mt-3 flex gap-2">
        <button
          v-for="p in presets"
          :key="p.mb"
          class="rounded-xl border px-3 py-1.5 text-xs font-medium transition"
          :class="memoryMb === p.mb
            ? 'border-violet-400/30 bg-violet-500/15 text-violet-300'
            : 'border-white/8 bg-white/[0.03] text-white/40 hover:bg-white/6 hover:text-white/70'"
          @click="memoryMb = p.mb"
        >{{ p.label }}</button>
      </div>

      <div class="mt-4 flex gap-2.5">
        <button
          class="rounded-[12px] bg-gradient-to-r from-violet-500 to-indigo-500 px-4 py-2 text-sm font-semibold text-white transition hover:brightness-110"
          @click="launcher.saveMemory(memoryMb)"
        >Сохранить</button>
        <button
          class="rounded-[12px] border border-white/10 bg-white/5 px-4 py-2 text-sm text-white/55 transition hover:bg-white/8 hover:text-white"
          @click="launcher.resetMemory()"
        >Сбросить</button>
      </div>
    </section>

    <!-- Updates -->
    <section class="rounded-[22px] border border-white/10 bg-white/[0.035] p-5">
      <p class="text-sm font-semibold">Обновление оболочки</p>
      <p class="mt-1 text-xs text-white/45">Обновление самого лаунчера, независимо от клиентской сборки.</p>

      <div class="mt-4 flex flex-wrap gap-2.5">
        <button
          class="rounded-[12px] border border-white/10 bg-white/5 px-4 py-2 text-sm text-white/60 transition hover:bg-white/8 hover:text-white"
          @click="launcher.checkShellUpdates()"
        >Проверить обновления</button>
        <button
          class="rounded-[12px] border border-white/10 bg-white/5 px-4 py-2 text-sm text-white/60 transition hover:bg-white/8 hover:text-white"
          @click="launcher.installShellUpdate()"
        >Установить обновление</button>
      </div>
    </section>

    <!-- Folders + Repair -->
    <section class="rounded-[22px] border border-white/10 bg-white/[0.035] p-5">
      <p class="text-sm font-semibold">Служебные действия</p>

      <div class="mt-4 flex flex-wrap gap-2.5">
        <button
          class="rounded-[12px] border border-white/10 bg-white/5 px-4 py-2 text-sm text-white/60 transition hover:bg-white/8 hover:text-white"
          @click="launcher.repair()"
        >Починить клиент</button>

        <button
          v-for="f in folderActions"
          :key="f.label"
          class="rounded-[12px] border border-white/10 bg-white/5 px-4 py-2 text-sm text-white/60 transition hover:bg-white/8 hover:text-white"
          @click="f.action()"
        >Открыть {{ f.label }}</button>
      </div>
    </section>

  </div>
</template>
