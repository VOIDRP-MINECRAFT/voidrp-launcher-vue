<script setup lang="ts">
import { computed, ref } from 'vue'
import { BACKEND_BASE } from '../../stores/launcher'

// The player's own skin, front view in full height (rendered by the backend on upload).
// Without an uploaded skin there is no body render: the head from the public endpoint stands in.
const props = defineProps<{ bodyUrl?: string; nickname: string }>()
const bodyFailed = ref(false)
const showBody = computed(() => Boolean(props.bodyUrl) && !bodyFailed.value)
const headUrl = computed(() => `${BACKEND_BASE}/public/player-head/${encodeURIComponent(props.nickname || 'Steve')}`)
</script>

<template>
  <div class="relative flex h-full w-full items-end justify-center">
    <!-- floor glow the figure stands on -->
    <div class="pointer-events-none absolute bottom-1 left-1/2 h-6 w-32 -translate-x-1/2 rounded-[50%] bg-black/50 blur-md"></div>
    <img
      v-if="showBody"
      :src="bodyUrl"
      alt=""
      class="item-icon relative h-full max-h-[270px] w-auto object-contain drop-shadow-[0_18px_30px_rgba(0,0,0,0.55)]"
      draggable="false"
      @error="bodyFailed = true"
    />
    <img
      v-else
      :src="headUrl"
      alt=""
      class="item-icon relative mb-8 h-28 w-28 rounded-[14px] drop-shadow-[0_18px_30px_rgba(0,0,0,0.55)]"
      draggable="false"
    />
  </div>
</template>
