<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useLauncherStore } from '../stores/launcher'

const launcher = useLauncherStore()

onMounted(() => {
  launcher.getMods()
})

// The player decides only about the optional ones; the required ones are listed so they know what runs.
const optionalMods = computed(() => launcher.mods.list.filter((m) => !m.required))
const requiredMods = computed(() => launcher.mods.list.filter((m) => m.required))
const enabledCount = computed(() => optionalMods.value.filter((m) => m.enabled).length)
</script>

<template>
  <div class="space-y-4">
    <!-- Header -->
    <div class="flex items-end justify-between gap-4">
      <div>
        <h1 class="font-display text-[26px] font-bold leading-tight text-white">Моды</h1>
        <p class="mt-1.5 text-sm leading-6 text-white/50">
          Дополнительные моды можно выключить. Изменения применятся при следующем запуске игры.
        </p>
      </div>
      <button
        v-if="!launcher.mods.loading && launcher.mods.list.length > 0"
        class="shrink-0 rounded-[12px] border border-white/10 bg-white/5 px-3.5 py-2 text-sm text-white/60 transition hover:bg-white/8 hover:text-white"
        @click="launcher.getMods()"
      >
        Обновить список
      </button>
    </div>

    <!-- Loading -->
    <div v-if="launcher.mods.loading" class="flex items-center gap-3 text-white/40">
      <div class="h-4 w-4 animate-spin rounded-full border-2 border-[rgb(var(--acc-rgb))] border-t-transparent"></div>
      <span class="text-sm">Загружаем список модов…</span>
    </div>

    <!-- Empty -->
    <div
      v-else-if="launcher.mods.list.length === 0"
      class="flex flex-col items-start gap-3 rounded-[20px] border border-white/8 bg-white/[0.03] p-6"
    >
      <p class="text-sm font-medium text-white/70">Список модов не загрузился</p>
      <p class="text-[13px] text-white/40">Проверь подключение к интернету и обнови список.</p>
      <button
        class="rounded-[12px] border border-white/10 bg-white/5 px-4 py-2 text-sm text-white/60 transition hover:bg-white/8 hover:text-white"
        @click="launcher.getMods()"
      >
        Обновить список
      </button>
    </div>

    <template v-else>
      <!-- Optional: the player's choice -->
      <section v-if="optionalMods.length" class="rounded-[20px] border border-white/10 bg-white/[0.035] p-2">
        <div class="flex items-baseline justify-between px-3 pb-1 pt-2">
          <h2 class="font-display text-[16px] font-semibold text-white">Можно выключить</h2>
          <span class="text-[12px] text-white/40">включено {{ enabledCount }} из {{ optionalMods.length }}</span>
        </div>
        <div class="grid grid-cols-2 gap-1">
          <label
            v-for="mod in optionalMods"
            :key="mod.path"
            class="flex cursor-pointer items-center gap-3 rounded-[14px] px-3 py-2.5 transition hover:bg-white/[0.04]"
            :title="mod.path"
          >
            <div class="min-w-0 flex-1">
              <p class="truncate text-[14px] font-semibold" :class="mod.enabled ? 'text-white' : 'text-white/45'">{{ mod.displayName }}</p>
              <p class="truncate text-[12px]" :class="mod.enabled ? 'text-white/45' : 'text-white/30'">
                {{ mod.description || (mod.enabled ? 'Включён' : 'Выключен') }}
              </p>
            </div>
            <button
              type="button"
              role="switch"
              :aria-checked="mod.enabled"
              class="relative h-6 w-10 shrink-0 rounded-full transition-colors duration-200"
              :class="mod.enabled ? 'bg-[rgb(var(--acc-rgb))]' : 'bg-white/15'"
              :title="mod.enabled ? 'Выключить' : 'Включить'"
              @click.prevent="launcher.toggleMod(mod.path, !mod.enabled)"
            >
              <span
                class="absolute top-[3px] h-[18px] w-[18px] rounded-full bg-white shadow transition-all duration-200"
                :class="mod.enabled ? 'left-[19px]' : 'left-[3px]'"
              ></span>
            </button>
          </label>
        </div>
      </section>

      <!-- Required: listed, not switchable -->
      <section v-if="requiredMods.length" class="rounded-[20px] border border-white/8 bg-white/[0.02] p-2">
        <div class="flex items-baseline justify-between px-3 pb-1 pt-2">
          <h2 class="font-display text-[16px] font-semibold text-white/80">Всегда включены</h2>
          <span class="text-[12px] text-white/35">нужны для игры на сервере</span>
        </div>
        <div class="grid grid-cols-2 gap-1">
          <div
            v-for="mod in requiredMods"
            :key="mod.path"
            class="rounded-[14px] px-3 py-2"
            :title="mod.path"
          >
            <p class="truncate text-[13px] font-medium text-white/70">{{ mod.displayName }}</p>
            <p v-if="mod.description" class="truncate text-[12px] text-white/35">{{ mod.description }}</p>
          </div>
        </div>
      </section>
    </template>
  </div>
</template>
