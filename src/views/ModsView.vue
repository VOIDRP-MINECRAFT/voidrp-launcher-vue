<script setup lang="ts">
import { onMounted } from 'vue'
import { useLauncherStore } from '../stores/launcher'

const launcher = useLauncherStore()

onMounted(() => {
  launcher.getMods()
})
</script>

<template>
  <div>
    <!-- Header -->
    <div class="flex items-end justify-between gap-4">
      <div>
        <p class="text-[11px] uppercase tracking-[0.25em] text-violet-300/70">Моды</p>
        <h2 class="mt-1.5 text-2xl font-semibold leading-tight">Управление модами</h2>
        <p class="mt-2 text-sm leading-6 text-white/50">
          Опциональные моды можно включать и отключать. Обязательные всегда активны.
          Изменения применятся при следующем запуске игры.
        </p>
      </div>

      <button
        v-if="!launcher.mods.loading && launcher.mods.list.length > 0"
        class="shrink-0 rounded-2xl border border-white/10 bg-white/5 px-3.5 py-2 text-sm text-white/60 transition hover:bg-white/8 hover:text-white"
        @click="launcher.getMods()"
      >
        Обновить
      </button>
    </div>

    <!-- Loading -->
    <div v-if="launcher.mods.loading" class="mt-8 flex items-center gap-3 text-white/40">
      <div class="h-4 w-4 animate-spin rounded-full border-2 border-violet-400 border-t-transparent"></div>
      <span class="text-sm">Загружаем список модов...</span>
    </div>

    <!-- Empty -->
    <div
      v-else-if="launcher.mods.list.length === 0"
      class="mt-8 flex flex-col items-center gap-4 rounded-[24px] border border-white/8 bg-white/[0.03] py-14 text-center"
    >
      <div class="flex h-12 w-12 items-center justify-center rounded-2xl border border-white/10 bg-white/5">
        <svg class="h-5 w-5 text-white/30" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="1.5">
          <path stroke-linecap="round" stroke-linejoin="round" d="M20.25 7.5l-.625 10.632a2.25 2.25 0 01-2.247 2.118H6.622a2.25 2.25 0 01-2.247-2.118L3.75 7.5M10 11.25h4M3.375 7.5h17.25c.621 0 1.125-.504 1.125-1.125v-.375c0-.621-.504-1.125-1.125-1.125H3.375c-.621 0-1.125.504-1.125 1.125v.375c0 .621.504 1.125 1.125 1.125z" />
        </svg>
      </div>
      <div>
        <p class="text-sm font-medium text-white/60">Опциональных модов не найдено</p>
        <p class="mt-1 text-xs text-white/30">Обновите список или проверьте подключение</p>
      </div>
      <button
        class="rounded-2xl border border-white/10 bg-white/5 px-4 py-2 text-sm text-white/60 transition hover:bg-white/8 hover:text-white"
        @click="launcher.getMods()"
      >
        Обновить
      </button>
    </div>

    <!-- Cards grid -->
    <div v-else class="mt-5 grid grid-cols-2 gap-3">
      <div
        v-for="mod in launcher.mods.list"
        :key="mod.path"
        class="group relative flex flex-col rounded-[20px] border p-4 transition-all duration-200"
        :class="mod.enabled
          ? 'border-white/10 bg-white/[0.04]'
          : 'border-white/5 bg-white/[0.02] opacity-55'"
      >
        <!-- Top row: name + toggle -->
        <div class="flex items-start justify-between gap-3">
          <p class="flex-1 min-w-0 text-sm font-semibold leading-snug" :class="mod.enabled ? 'text-white' : 'text-white/60'">
            {{ mod.displayName }}
          </p>

          <!-- Toggle -->
          <button
            v-if="!mod.required"
            class="relative mt-0.5 h-6 w-10 shrink-0 rounded-full transition-colors duration-200 focus:outline-none"
            :class="mod.enabled ? 'bg-violet-500' : 'bg-white/15'"
            :title="mod.enabled ? 'Отключить' : 'Включить'"
            @click="launcher.toggleMod(mod.path, !mod.enabled)"
          >
            <span
              class="absolute top-[3px] h-[18px] w-[18px] rounded-full bg-white shadow transition-all duration-200"
              :class="mod.enabled ? 'left-[21px]' : 'left-[3px]'"
            ></span>
          </button>

          <!-- Locked indicator -->
          <span
            v-else
            class="mt-0.5 shrink-0 text-[10px] font-medium text-amber-300/70"
          >
            Вкл
          </span>
        </div>

        <!-- Badges -->
        <div class="mt-2 flex flex-wrap gap-1.5">
          <span
            v-if="mod.required"
            class="rounded-full border border-amber-400/20 bg-amber-500/10 px-2 py-0.5 text-[10px] font-medium text-amber-300"
          >
            Обязательный
          </span>
          <span
            v-else-if="!mod.enabled"
            class="rounded-full border border-white/10 bg-white/5 px-2 py-0.5 text-[10px] font-medium text-white/35"
          >
            Отключён
          </span>
          <span
            v-else
            class="rounded-full border border-violet-400/20 bg-violet-500/10 px-2 py-0.5 text-[10px] font-medium text-violet-300"
          >
            Включён
          </span>
        </div>

        <!-- Description -->
        <p v-if="mod.description" class="mt-2.5 flex-1 text-xs leading-5 text-white/45">
          {{ mod.description }}
        </p>

        <!-- Path -->
        <p class="mt-3 truncate font-mono text-[10px] text-white/20">{{ mod.path }}</p>
      </div>
    </div>
  </div>
</template>
