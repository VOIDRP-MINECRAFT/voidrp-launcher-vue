<script setup lang="ts">
import { computed } from 'vue'

// Vanilla item textures (16×16) bundled with the launcher, so they show offline and at once.
const files = import.meta.glob('../../assets/items/*.png', { eager: true, import: 'default' }) as Record<string, string>
const byName: Record<string, string> = Object.fromEntries(
  Object.entries(files).map(([path, url]) => [path.split('/').pop()!.replace('.png', ''), url]),
)

const props = withDefaults(defineProps<{ name: string; size?: number }>(), { size: 32 })
const src = computed(() => byName[props.name] ?? '')
</script>

<template>
  <img v-if="src" :src="src" alt="" class="item-icon" :style="{ width: `${size}px`, height: `${size}px` }" draggable="false" />
</template>
