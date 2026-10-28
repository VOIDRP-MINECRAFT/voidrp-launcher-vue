<script setup lang="ts">
import { computed } from 'vue'
import { nationRoleLabel, useLauncherStore } from '../stores/launcher'
import ItemIcon from '../components/game/ItemIcon.vue'
import StatSlot from '../components/game/StatSlot.vue'

const launcher = useLauncherStore()

const hasNation = computed(() => Boolean(launcher.nation.title))
const nationImage = computed(() => launcher.nation.bannerUrl || launcher.nation.bannerPreviewUrl || '')
const nationIcon = computed(() => launcher.nation.iconUrl || launcher.nation.iconPreviewUrl || '')
const nationColor = computed(() => {
  const c = launcher.nation.accentColor
  // A white/near-white nation colour would vanish as a tint; fall back to the theme accent.
  return c && !/^#?f{3,6}$/i.test(c) ? c : 'rgb(var(--acc-rgb))'
})

function fmt(value: number | string | null | undefined) {
  const n = Number(value ?? 0)
  return new Intl.NumberFormat('ru-RU').format(Number.isFinite(n) ? n : 0)
}
function fmtH(minutes: number | null | undefined) {
  return fmt(Math.floor((minutes ?? 0) / 60)) + ' ч'
}
function fmtDate(value: string | null | undefined) {
  if (!value) return '—'
  const d = new Date(value)
  return Number.isNaN(d.getTime()) ? '—' : new Intl.DateTimeFormat('ru-RU', { dateStyle: 'medium', timeStyle: 'short' }).format(d)
}

const nationSlots = computed(() => [
  { icon: 'map', value: fmt(launcher.nationStats.territoryPoints), label: 'очков территории' },
  { icon: 'nether_star', value: fmt(launcher.nationStats.prestigeScore), label: 'престижа' },
  { icon: 'clock', value: fmtH(launcher.nationStats.totalPlaytimeMinutes), label: 'наиграно всеми' },
  { icon: 'bell', value: fmt(launcher.nationStats.eventsCompleted), label: 'событий пройдено' },
])

const mySlots = computed(() => [
  { icon: 'clock', value: fmtH(launcher.playerStats.totalPlaytimeMinutes), label: 'в игре' },
  { icon: 'diamond_sword', value: fmt(launcher.playerStats.pvpKills), label: 'побед в PvP' },
  { icon: 'rotten_flesh', value: fmt(launcher.playerStats.mobKills), label: 'мобов убито' },
  { icon: 'bone', value: fmt(launcher.playerStats.deaths), label: 'смертей' },
  { icon: 'bricks', value: fmt(launcher.playerStats.blocksPlaced), label: 'блоков поставлено' },
  { icon: 'diamond_pickaxe', value: fmt(launcher.playerStats.blocksBroken), label: 'блоков сломано' },
])
</script>

<template>
  <div class="space-y-3">
    <template v-if="hasNation">
      <!-- ── Banner ─────────────────────────────────────────────── -->
      <section
        class="nation-hero relative overflow-hidden rounded-[24px] border border-white/10"
        :style="{ '--nation': nationColor, ...(nationImage ? { backgroundImage: `linear-gradient(90deg, rgba(4,6,15,.88) 0%, rgba(4,6,15,.55) 55%, rgba(4,6,15,.2) 100%), url(${nationImage})` } : {}) }"
      >
        <!-- the tag, set huge behind the name: the nation's mark on its own page -->
        <span v-if="launcher.nation.tag && !nationImage" class="nation-mark font-display" aria-hidden="true">{{ launcher.nation.tag }}</span>

        <div class="relative flex items-center gap-5 p-6">
          <div class="nation-emblem grid h-[76px] w-[76px] shrink-0 place-items-center overflow-hidden rounded-[20px]">
            <img v-if="nationIcon" :src="nationIcon" alt="" class="h-full w-full object-cover" />
            <ItemIcon v-else name="bell" :size="48" />
          </div>
          <div class="min-w-0 flex-1">
            <h1 class="font-display truncate text-[34px] font-bold leading-[1.05] text-white">{{ launcher.nation.title }}</h1>
            <div class="mt-2.5 flex flex-wrap items-center gap-2 text-[13px]">
              <span v-if="launcher.nation.tag" class="tag-chip rounded-full px-2.5 py-0.5 font-semibold text-white">{{ launcher.nation.tag }}</span>
              <span class="text-white/70">{{ nationRoleLabel(launcher.nation.role) }}</span>
              <span v-if="launcher.nation.allianceTitle" class="text-white/50">
                · альянс {{ launcher.nation.allianceTitle }}{{ launcher.nation.allianceTag ? ` [${launcher.nation.allianceTag}]` : '' }}
              </span>
            </div>
          </div>
          <div class="shrink-0 text-right">
            <p class="text-[12px] text-white/50">Казна</p>
            <div class="mt-1 flex items-center justify-end gap-2">
              <ItemIcon name="emerald" :size="32" />
              <span class="font-display text-[28px] font-bold leading-none text-white">{{ fmt(launcher.nationStats.treasuryBalance) }}</span>
            </div>
          </div>
        </div>
      </section>

      <!-- ── Nation numbers ─────────────────────────────────────── -->
      <div class="grid grid-cols-4 gap-2.5">
        <StatSlot v-for="s in nationSlots" :key="s.label" :icon="s.icon" :value="s.value" :label="s.label" />
      </div>

      <!-- ── Personal + Activity ────────────────────────────────── -->
      <div class="grid grid-cols-12 gap-3">
        <section class="col-span-7 rounded-[20px] border border-white/10 bg-white/[0.035] p-4">
          <div class="flex items-baseline justify-between gap-3">
            <h2 class="font-display text-[17px] font-semibold text-white">Твой вклад</h2>
            <span class="text-[12px] text-white/40">последний раз в игре {{ fmtDate(launcher.playerStats.lastSeenAt) }}</span>
          </div>
          <div class="mt-3 grid grid-cols-2 gap-2">
            <StatSlot v-for="s in mySlots" :key="s.label" :icon="s.icon" :value="s.value" :label="s.label" />
          </div>
        </section>

        <section class="col-span-5 rounded-[20px] border border-white/10 bg-white/[0.035] p-4">
          <h2 class="font-display text-[17px] font-semibold text-white">Последние события</h2>

          <p v-if="!launcher.recentActivity.length" class="mt-4 text-[13px] leading-6 text-white/45">
            Пока тихо. Здесь появятся вступления, взносы в казну и решения государства.
          </p>

          <ol v-else class="event-rail mt-3">
            <li
              v-for="item in launcher.recentActivity"
              :key="`${item.eventType}_${item.createdAt}`"
              class="relative pb-3.5 pl-5 last:pb-0"
            >
              <p class="text-[13px] font-medium leading-5 text-white/85">{{ item.message || item.eventType }}</p>
              <p class="mt-0.5 text-[11px] text-white/35">{{ fmtDate(item.createdAt) }}</p>
            </li>
          </ol>
        </section>
      </div>
    </template>

    <!-- ── No nation ──────────────────────────────────────────── -->
    <section v-else class="nation-hero relative overflow-hidden rounded-[24px] border border-white/10 p-7" :style="{ '--nation': 'rgb(var(--acc-rgb))' }">
      <div class="flex items-center gap-5">
        <div class="mc-slot h-[76px] w-[76px] shrink-0"><ItemIcon name="bell" :size="48" /></div>
        <div>
          <h1 class="font-display text-[26px] font-bold text-white">Ты пока не в государстве</h1>
          <p class="mt-2 max-w-[560px] text-sm leading-6 text-white/55">
            Вступи в государство или создай своё на сайте. Здесь появятся казна, территория, престиж, твоя роль и лента событий.
          </p>
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped>
.nation-hero {
  background-color: rgba(255, 255, 255, 0.02);
  background-image:
    radial-gradient(ellipse 55% 140% at 0% 50%, color-mix(in srgb, var(--nation) 34%, transparent), transparent 70%),
    linear-gradient(180deg, rgba(255, 255, 255, 0.04), rgba(255, 255, 255, 0.01));
  background-size: cover;
  background-position: center;
}
.nation-mark {
  position: absolute;
  right: -0.06em;
  bottom: -0.32em;
  font-size: 150px;
  font-weight: 800;
  line-height: 1;
  white-space: nowrap;
  color: color-mix(in srgb, var(--nation) 22%, transparent);
  pointer-events: none;
  user-select: none;
}
.nation-emblem {
  background: color-mix(in srgb, var(--nation) 26%, rgba(0, 0, 0, 0.35));
  box-shadow: inset 0 0 0 1px color-mix(in srgb, var(--nation) 50%, transparent), 0 14px 30px rgba(0, 0, 0, 0.35);
}
.tag-chip {
  background: color-mix(in srgb, var(--nation) 28%, rgba(0, 0, 0, 0.3));
  box-shadow: inset 0 0 0 1px color-mix(in srgb, var(--nation) 50%, transparent);
}
/* a thin rail with a dot per event */
.event-rail li::before {
  content: '';
  position: absolute;
  left: 4px;
  top: 7px;
  width: 7px;
  height: 7px;
  border-radius: 2px;
  background: rgb(var(--acc-rgb));
}
.event-rail li:not(:last-child)::after {
  content: '';
  position: absolute;
  left: 7px;
  top: 18px;
  bottom: 2px;
  width: 1px;
  background: rgba(255, 255, 255, 0.12);
}
:global([data-mode="light"]) .event-rail li:not(:last-child)::after { background: rgba(15, 23, 42, 0.14); }
</style>
