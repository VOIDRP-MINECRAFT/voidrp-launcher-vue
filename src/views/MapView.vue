<script setup lang="ts">
import { ref } from 'vue'
import { SITE_BASE } from '../stores/launcher'

const loaded = ref(false)
const failed = ref(false)

const MAP_URL = `${SITE_BASE}/map/`

function onLoad() { loaded.value = true }
function onLoadFail(_e: unknown, _code: number, _desc: string, _url: string, isMainFrame: boolean) {
  if (isMainFrame) failed.value = true
}

function openInBrowser() {
  window.desktopAPI?.openExternal?.(MAP_URL)
}
</script>

<template>
  <div class="flex h-full flex-col gap-3" style="height: calc(100vh - 220px); min-height: 420px;">

    <div class="flex items-center justify-between">
      <p class="text-sm font-semibold text-white/80">Онлайн карта</p>
      <button
        class="rounded-xl border border-white/10 bg-white/5 px-3 py-1.5 text-xs text-white/60 transition hover:bg-white/8 hover:text-white"
        @click="openInBrowser"
      >
        Открыть в браузере ↗
      </button>
    </div>

    <!-- map frame -->
    <div class="relative min-h-0 flex-1 overflow-hidden rounded-[18px] border border-white/10 bg-[#050a16]">

      <!-- loading state -->
      <div
        v-if="!loaded && !failed"
        class="absolute inset-0 flex items-center justify-center"
      >
        <div class="flex flex-col items-center gap-3">
          <div class="h-8 w-8 animate-spin rounded-full border-2 border-violet-500/30 border-t-violet-400"></div>
          <p class="text-xs text-white/40">Загрузка карты...</p>
        </div>
      </div>

      <!-- failed state -->
      <div
        v-if="failed"
        class="absolute inset-0 flex items-center justify-center"
      >
        <div class="flex flex-col items-center gap-4 text-center">
          <p class="text-sm text-white/60">Не удалось встроить карту.</p>
          <button
            class="rounded-xl bg-violet-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-violet-500"
            @click="openInBrowser"
          >
            Открыть в браузере
          </button>
        </div>
      </div>

      <!-- webview -->
      <webview
        v-if="!failed"
        :src="MAP_URL"
        class="h-full w-full"
        style="height: 100%; width: 100%;"
        webpreferences="webgl=yes, accelerated2dCanvas=yes"
        @did-finish-load="onLoad"
        @did-fail-load="onLoadFail"
      ></webview>
    </div>

  </div>
</template>
