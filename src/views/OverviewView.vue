<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { RouterLink } from 'vue-router'
import { nationRoleLabel, useLauncherStore } from '../stores/launcher'
import ItemIcon from '../components/game/ItemIcon.vue'
import SkinFigure from '../components/game/SkinFigure.vue'
import StatSlot from '../components/game/StatSlot.vue'

const launcher = useLauncherStore()

function fmt(value: number | string | null | undefined) {
  const n = Number(value ?? 0)
  return new Intl.NumberFormat('ru-RU').format(Number.isFinite(n) ? n : 0)
}
function fmtDay(value: string | null | undefined) {
  if (!value) return ''
  const d = new Date(value)
  if (Number.isNaN(d.getTime())) return ''
  return new Intl.DateTimeFormat('ru-RU', { day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' }).format(d)
}

// Per-server feature gating (mirrors AppLayout): a feature is enabled unless the
// active server explicitly disables it.
const activeServer = computed(() =>
  launcher.serverList.find((s) => s.slug === launcher.selectedSlug) ?? null,
)
function feat(key: string) {
  const f = activeServer.value?.features
  return !f || f[key] !== false
}

const nickname = computed(() => launcher.playerStats.minecraftNickname || launcher.accountNickname || '')
const hasNation = computed(() => Boolean(launcher.nation.title))
const nationColor = computed(() => {
  const c = launcher.nation.accentColor
  // A white/near-white nation colour would vanish as a tint; fall back to the theme accent.
  return c && !/^#?f{3,6}$/i.test(c) ? c : 'rgb(var(--acc-rgb))'
})
const hours = computed(() => Math.floor((launcher.playerStats.totalPlaytimeMinutes ?? 0) / 60))
const lastSeen = computed(() => fmtDay(launcher.playerStats.lastSeenAt))

// ── Epochs: where the player is on the pack's progression ──────────────────
type Tier = { tier_name: string; tier_label: string }
const allTiers = ref<Tier[]>([])
const currentTier = ref<string | null>(null)

async function loadProgression() {
  allTiers.value = []
  currentTier.value = null
  if (!feat('progression') || !nickname.value) return
  try {
    const [mine, board] = await Promise.all([
      launcher.backendFetch(`/progression/player/${encodeURIComponent(nickname.value)}`),
      launcher.backendFetch('/progression/leaderboard'),
    ])
    if (mine.ok) currentTier.value = ((await mine.json()) as { current_tier?: string | null }).current_tier ?? null
    if (board.ok) allTiers.value = (((await board.json()) as { tiers?: Tier[] }).tiers ?? []).map((t) => ({ tier_name: t.tier_name, tier_label: t.tier_label }))
  } catch {
    // progression is a nice-to-have on this screen
  }
}
onMounted(loadProgression)

// The bars fill once when the page opens: the one moment of motion on this screen.
const filled = ref(false)
onMounted(() => requestAnimationFrame(() => { filled.value = true }))
watch(() => [nickname.value, launcher.selectedSlug], loadProgression)

const tierIndex = computed(() => allTiers.value.findIndex((t) => t.tier_name === currentTier.value))
const tierLabel = computed(() => (tierIndex.value >= 0 ? allTiers.value[tierIndex.value].tier_label : ''))
const nextTier = computed(() => (tierIndex.value >= 0 ? allTiers.value[tierIndex.value + 1]?.tier_label ?? '' : allTiers.value[0]?.tier_label ?? ''))
const tierPercent = computed(() =>
  allTiers.value.length ? Math.round(((tierIndex.value + 1) / allTiers.value.length) * 100) : 0)

// ── Battle pass ────────────────────────────────────────────────────────────
const bp = computed(() => (feat('battlepass') ? launcher.bpProfile : null))
const bpMax = computed(() => bp.value?.max_level || 120)
const bpPercent = computed(() => Math.min(100, Math.round(((bp.value?.level ?? 0) / bpMax.value) * 100)))

// Combat/time stats make sense on every server; quests only where the feature is on.
const slots = computed(() => [
  { icon: 'diamond_sword', value: fmt(launcher.playerStats.pvpKills), label: 'побед в PvP' },
  { icon: 'rotten_flesh', value: fmt(launcher.playerStats.mobKills), label: 'мобов убито' },
  { icon: 'bone', value: fmt(launcher.playerStats.deaths), label: 'смертей' },
  ...(feat('quests') ? [{ icon: 'writable_book', value: fmt(launcher.playerStats.completedQuests), label: 'квестов' }] : []),
  { icon: 'clock', value: `${fmt(hours.value)} ч`, label: 'в игре' },
])
</script>

<template>
  <div class="space-y-3">
    <!-- Telegram не привязан: бонус в игре и напоминание о награде второго дня -->
    <button
      v-if="launcher.isAuthenticated && launcher.telegramLinked === false"
      type="button"
      class="flex w-full items-center gap-3 rounded-[16px] border border-sky-400/20 bg-sky-500/[0.07] px-4 py-2.5 text-left transition hover:border-sky-400/40"
      @click="launcher.openExternal(launcher.telegramBotUrl)"
    >
      <span class="grid h-8 w-8 shrink-0 place-items-center rounded-[10px] bg-sky-500 text-white">
        <svg viewBox="0 0 24 24" width="16" height="16" fill="currentColor" aria-hidden="true"><path d="M9.04 15.38 8.86 19c.38 0 .54-.16.74-.36l1.78-1.7 3.69 2.7c.68.37 1.16.18 1.34-.63l2.43-11.4c.22-1-.36-1.39-1.02-1.15L3.57 11.9c-.97.38-.96.92-.17 1.16l3.67 1.15 8.52-5.38c.4-.26.77-.12.47.15"/></svg>
      </span>
      <span class="min-w-0 flex-1">
        <span class="block text-sm font-semibold text-white">Привяжи Telegram и получи бонус в игре</span>
        <span class="block truncate text-xs text-white/50">Бот напомнит о награде за второй день. Открой бота и нажми «Привязать аккаунт».</span>
      </span>
      <span class="shrink-0 text-xs font-semibold text-sky-300">Открыть бота</span>
    </button>

    <!-- ── Player card ─────────────────────────────────────────── -->
    <section
      class="player-card relative overflow-hidden rounded-[24px] border border-white/10"
      :style="{ '--nation': nationColor }"
    >
      <div class="relative grid grid-cols-[200px_1fr] gap-2">
        <!-- the figure stands on the card's floor -->
        <div class="figure-well relative h-[290px] pb-4">
          <SkinFigure :body-url="launcher.skin.hasSkin ? launcher.skin.bodyPreviewUrl : ''" :nickname="nickname" />
        </div>

        <div class="flex min-w-0 flex-col justify-center py-6 pr-6">
          <h1 class="font-display truncate text-[34px] font-bold leading-[1.05] text-white">{{ nickname || 'Игрок' }}</h1>

          <div class="mt-3 flex flex-wrap items-center gap-2">
            <RouterLink
              v-if="feat('nations') && hasNation"
              to="/nation"
              class="nation-chip flex items-center gap-2 rounded-full py-1 pl-1 pr-3 text-[13px] font-semibold text-white transition"
            >
              <span class="grid h-6 w-6 place-items-center overflow-hidden rounded-full bg-black/30">
                <img v-if="launcher.nation.iconPreviewUrl || launcher.nation.iconUrl" :src="launcher.nation.iconPreviewUrl || launcher.nation.iconUrl" alt="" class="h-full w-full object-cover" />
                <ItemIcon v-else name="bell" :size="16" />
              </span>
              {{ launcher.nation.title }}
              <span class="font-normal text-white/60">· {{ nationRoleLabel(launcher.nation.role).toLowerCase() }}</span>
            </RouterLink>
            <span v-if="lastSeen" class="text-[12px] text-white/40">В игре {{ lastSeen }}</span>
          </div>

          <div v-if="feat('economy')" class="mt-5 flex items-center gap-2.5">
            <ItemIcon name="gold_ingot" :size="32" />
            <span class="font-display text-[28px] font-bold leading-none text-gold">{{ fmt(launcher.walletBalance) }}</span>
            <span class="mt-1 text-sm text-white/45">монет</span>
          </div>

          <div v-if="tierLabel || allTiers.length" class="mt-5 max-w-[460px]">
            <div class="mb-2 flex items-baseline justify-between gap-3 text-[13px]">
              <span class="font-semibold text-white/85">{{ tierLabel || 'Эпохи ещё впереди' }}</span>
              <span class="tabular-nums text-white/40">{{ Math.max(tierIndex + 1, 0) }} из {{ allTiers.length }} эпох</span>
            </div>
            <div class="xp-bar"><div class="xp-bar__fill" :style="{ width: `${filled ? tierPercent : 0}%` }"></div></div>
            <p v-if="nextTier" class="mt-2 text-[12px] text-white/40">Следующая: {{ nextTier }}</p>
          </div>

          <RouterLink
            v-if="!launcher.skin.hasSkin"
            to="/account"
            class="mt-4 w-fit rounded-[12px] border border-white/12 bg-white/5 px-3.5 py-2 text-[13px] font-medium text-white/80 transition hover:bg-white/10 hover:text-white"
          >
            Поставить свой скин — сейчас тебя видят Стивом
          </RouterLink>
        </div>
      </div>
    </section>

    <!-- ── Stats as an inventory row ────────────────────────────── -->
    <div class="grid gap-2.5" :style="{ gridTemplateColumns: `repeat(${slots.length}, minmax(0, 1fr))` }">
      <StatSlot v-for="s in slots" :key="s.icon" :icon="s.icon" :value="s.value" :label="s.label" />
    </div>

    <!-- ── Nation + battle pass ─────────────────────────────────── -->
    <div class="grid gap-3" :class="feat('nations') && bp ? 'grid-cols-2' : 'grid-cols-1'">
      <RouterLink
        v-if="feat('nations')"
        to="/nation"
        class="group rounded-[20px] border border-white/10 bg-white/[0.035] p-4 transition hover:border-white/20"
      >
        <template v-if="hasNation">
          <div class="flex items-center justify-between gap-3">
            <p class="font-display truncate text-[17px] font-semibold text-white">{{ launcher.nation.title }}</p>
            <span class="shrink-0 text-xs text-white/40 transition group-hover:text-white/70">Открыть</span>
          </div>
          <div class="mt-3 grid grid-cols-2 gap-2">
            <div class="flex items-center gap-2.5">
              <div class="mc-slot h-10 w-10"><ItemIcon name="emerald" :size="24" /></div>
              <div>
                <p class="font-display text-[15px] font-semibold leading-none text-white">{{ fmt(launcher.nationStats.treasuryBalance) }}</p>
                <p class="mt-1 text-[11px] text-white/40">в казне</p>
              </div>
            </div>
            <div class="flex items-center gap-2.5">
              <div class="mc-slot h-10 w-10"><ItemIcon name="map" :size="24" /></div>
              <div>
                <p class="font-display text-[15px] font-semibold leading-none text-white">{{ fmt(launcher.nationStats.territoryPoints) }}</p>
                <p class="mt-1 text-[11px] text-white/40">очков территории</p>
              </div>
            </div>
          </div>
        </template>
        <template v-else>
          <p class="font-display text-[17px] font-semibold text-white">Без государства</p>
          <p class="mt-2 text-[13px] text-white/45">Вступи в государство или создай своё на сайте — общая казна, территория и союзы.</p>
        </template>
      </RouterLink>

      <RouterLink
        v-if="bp"
        to="/account"
        class="group rounded-[20px] border border-white/10 bg-white/[0.035] p-4 transition hover:border-white/20"
      >
        <div class="flex items-center justify-between gap-3">
          <p class="font-display truncate text-[17px] font-semibold text-white">Боевой пропуск</p>
          <span v-if="bp.has_premium" class="shrink-0 rounded-full bg-amber-400/15 px-2.5 py-0.5 text-[11px] font-semibold text-amber-300">Премиум</span>
        </div>
        <div class="mt-3 flex items-center gap-3">
          <div class="mc-slot h-10 w-10 shrink-0"><ItemIcon name="experience_bottle" :size="24" /></div>
          <div class="min-w-0 flex-1">
            <div class="mb-1.5 flex items-baseline justify-between text-[12px]">
              <span><span class="font-display text-[15px] font-semibold text-white">{{ bp.level }}</span> <span class="text-white/40">уровень из {{ bpMax }}</span></span>
              <span class="tabular-nums text-white/40">{{ bpPercent }}%</span>
            </div>
            <div class="xp-bar" :class="{ 'xp-bar--gold': bp.has_premium }"><div class="xp-bar__fill" :style="{ width: `${filled ? bpPercent : 0}%` }"></div></div>
          </div>
        </div>
      </RouterLink>
    </div>
  </div>
</template>

<style scoped>
/* The nation's colour tints the card behind the player: your nation is the backdrop you stand in. */
.player-card {
  background:
    radial-gradient(ellipse 60% 90% at 12% 100%, color-mix(in srgb, var(--nation) 30%, transparent), transparent 70%),
    radial-gradient(ellipse 70% 120% at 100% 0%, rgba(var(--acc-rgb), 0.12), transparent 60%),
    linear-gradient(180deg, rgba(255, 255, 255, 0.04), rgba(255, 255, 255, 0.015));
}
.figure-well {
  background: radial-gradient(ellipse 70% 55% at 50% 78%, color-mix(in srgb, var(--nation) 22%, transparent), transparent 70%);
}
.nation-chip {
  background: color-mix(in srgb, var(--nation) 22%, rgba(0, 0, 0, 0.25));
  box-shadow: inset 0 0 0 1px color-mix(in srgb, var(--nation) 45%, transparent);
}
.nation-chip:hover { background: color-mix(in srgb, var(--nation) 32%, rgba(0, 0, 0, 0.25)); }
:global([data-mode="light"]) .nation-chip { background: color-mix(in srgb, var(--nation) 16%, white); }
</style>
