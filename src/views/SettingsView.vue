<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useLauncherStore, type GameLocationInfo } from '../stores/launcher'
import { LAUNCHER_THEMES } from '../theme/themes'

const launcher = useLauncherStore()
const themes = LAUNCHER_THEMES
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

// ── Game files location ────────────────────────────────────────────────────
const location = ref<GameLocationInfo | null>(null)
const pendingPath = ref<string | null>(null)   // chosen folder awaiting confirmation; '' = back to default
const moveFiles = ref(true)
const sizeLoading = ref(false)

function formatGb(bytes: number | null | undefined) {
  if (bytes == null) return '—'
  return `${(bytes / 1024 ** 3).toFixed(1)} ГБ`
}

async function loadLocation(includeSize = false) {
  if (includeSize) sizeLoading.value = true
  const info = await launcher.getGameLocation(includeSize)
  if (info) location.value = info
  sizeLoading.value = false
}

async function pickFolder() {
  const chosen = await launcher.selectDirectory(location.value?.currentRoot || '')
  if (!chosen) return
  pendingPath.value = chosen
  moveFiles.value = true
  loadLocation(true)
}

function resetToDefault() {
  pendingPath.value = ''
  moveFiles.value = true
  loadLocation(true)
}

async function confirmMove() {
  if (pendingPath.value === null) return
  const response = await launcher.changeGameLocation(pendingPath.value || null, moveFiles.value)
  if (response?.ok) pendingPath.value = null
  loadLocation()
}

onMounted(() => loadLocation())

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

    <!-- Theme -->
    <section class="rounded-[22px] border border-white/10 bg-white/[0.035] p-5">
      <p class="text-sm font-semibold">Тема оформления</p>
      <p class="mt-1 text-xs text-white/45">Цвет акцента и фон интерфейса. Применяется сразу.</p>

      <div class="mt-4 grid grid-cols-2 gap-2.5 sm:grid-cols-3">
        <button
          v-for="t in themes"
          :key="t.id"
          class="flex items-center gap-3 rounded-[14px] border p-3 text-left transition"
          :class="launcher.themeId === t.id
            ? 'bg-white/[0.06]'
            : 'border-white/8 bg-white/[0.02] hover:bg-white/[0.05]'"
          :style="launcher.themeId === t.id ? { borderColor: 'var(--acc)' } : {}"
          @click="launcher.setTheme(t.id)"
        >
          <span
            class="h-8 w-8 shrink-0 rounded-full ring-2 ring-white/10"
            :style="{ backgroundImage: `linear-gradient(135deg, ${t.swatch[0]}, ${t.swatch[1]})` }"
          ></span>
          <span class="min-w-0 flex-1">
            <span class="block truncate text-sm font-medium text-white/85">{{ t.name }}</span>
            <span class="block text-[10px] text-white/40">{{ launcher.themeId === t.id ? 'Выбрана' : 'Нажмите' }}</span>
          </span>
          <svg
            v-if="launcher.themeId === t.id"
            class="h-4 w-4 shrink-0"
            :style="{ color: 'var(--acc-soft)' }"
            fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5"
          ><path stroke-linecap="round" stroke-linejoin="round" d="M5 13l4 4L19 7" /></svg>
        </button>
      </div>
    </section>

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

    <!-- Game files location -->
    <section class="rounded-[22px] border border-white/10 bg-white/[0.035] p-5">
      <p class="text-sm font-semibold">Папка с файлами игры</p>
      <p class="mt-1 text-xs text-white/45">
        Моды, ресурсы и Java занимают много места — их можно перенести на другой диск. Путь лучше выбирать латиницей, например D:\Games\VoidRP.
      </p>

      <div class="mt-3 rounded-[13px] border border-white/8 bg-white/[0.03] px-3.5 py-2.5">
        <p class="break-all font-mono text-[12px] text-white/75">{{ location?.currentRoot || '…' }}</p>
        <p class="mt-1 text-[11px] text-white/40">
          {{ location?.isCustom ? 'Своя папка' : 'Папка по умолчанию' }} · свободно на диске {{ formatGb(location?.freeBytes) }}
        </p>
      </div>

      <div v-if="pendingPath !== null" class="mt-3 rounded-[13px] border p-3.5" style="border-color: var(--acc)">
        <p class="text-[13px] font-semibold">
          {{ pendingPath ? 'Новая папка:' : 'Вернуть в папку по умолчанию:' }}
          <span class="break-all font-mono font-normal text-white/80">{{ pendingPath || location?.defaultRoot }}</span>
        </p>
        <label class="mt-2 flex cursor-pointer items-center gap-2 text-[12px] text-white/70">
          <input v-model="moveFiles" type="checkbox" class="accent-[var(--acc)]" />
          Перенести уже скачанные файлы
          <span class="text-white/40">({{ sizeLoading ? 'считаем размер…' : formatGb(location?.sizeBytes) }})</span>
        </label>
        <p v-if="!moveFiles" class="mt-1 text-[11px] text-white/40">
          Лаунчер просто начнёт использовать эту папку: если в ней уже есть игра — возьмёт её, иначе скачает сборку заново.
        </p>
        <p v-if="launcher.isBusy && launcher.progress.visible" class="mt-2 text-[12px] text-white/60">{{ launcher.progress.details }}</p>
        <div class="mt-3 flex flex-wrap gap-2">
          <button
            class="rounded-[12px] px-4 py-2 text-sm font-semibold btn-acc disabled:cursor-not-allowed disabled:opacity-50"
            :disabled="launcher.isBusy"
            @click="confirmMove"
          >{{ launcher.isBusy ? 'Переносим…' : moveFiles ? 'Перенести' : 'Сменить папку' }}</button>
          <button
            class="rounded-[12px] border border-white/10 bg-white/5 px-4 py-2 text-sm text-white/60 transition hover:bg-white/8 hover:text-white"
            :disabled="launcher.isBusy"
            @click="pendingPath = null"
          >Отмена</button>
        </div>
      </div>

      <div v-else class="mt-3 flex flex-wrap gap-2.5">
        <button
          class="rounded-[12px] border border-white/10 bg-white/5 px-4 py-2 text-sm text-white/60 transition hover:bg-white/8 hover:text-white"
          :disabled="launcher.isBusy"
          @click="pickFolder"
        >Выбрать другую папку</button>
        <button
          v-if="location?.isCustom"
          class="rounded-[12px] border border-white/10 bg-white/5 px-4 py-2 text-sm text-white/60 transition hover:bg-white/8 hover:text-white"
          :disabled="launcher.isBusy"
          @click="resetToDefault"
        >Вернуть по умолчанию</button>
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
