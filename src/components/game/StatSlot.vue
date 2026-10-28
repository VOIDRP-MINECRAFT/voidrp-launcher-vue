<script setup lang="ts">
import ItemIcon from './ItemIcon.vue'

// One stat as an inventory slot: the item in a sunken well, the number beside it.
defineProps<{ icon: string; value: string; label: string }>()
</script>

<template>
  <div class="flex min-w-0 items-center gap-3 rounded-[16px] border border-white/8 bg-white/[0.03] p-2.5 pr-4">
    <div class="mc-slot h-[52px] w-[52px] shrink-0">
      <ItemIcon :name="icon" :size="32" />
    </div>
    <div class="min-w-0">
      <p class="font-display truncate text-[19px] font-semibold leading-none text-white">{{ value }}</p>
      <p class="mt-1.5 truncate text-[12px] text-white/45">{{ label }}</p>
    </div>
  </div>
</template>
