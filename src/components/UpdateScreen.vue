<script setup lang="ts">
import { computed } from 'vue'

interface UpdaterStatus {
  available?: boolean
  downloading?: boolean
  downloaded?: boolean
  progressPercent?: number
  message?: string
  notes?: string
  version?: string
}

const props = defineProps<{ status: UpdaterStatus | null }>()

const percent = computed(() => Math.max(0, Math.min(100, Math.round(props.status?.progressPercent ?? 0))))
const done = computed(() => !!props.status?.downloaded || percent.value >= 100)
const message = computed(
  () => props.status?.message || 'Проверяем обновления…',
)
const version = computed(() => props.status?.version || '')
const notes = computed(() => (props.status?.notes || '').trim())

// Parse "• line" / "- line" bullets from the notes for a clean list render.
const notesLines = computed(() =>
  notes.value
    .split('\n')
    .map((l) => l.trim())
    .filter(Boolean),
)
</script>

<template>
  <div class="upd-root">
    <div class="upd-card panel panel--strong panel--edge">
      <!-- Emblem with animated orbit ring -->
      <div class="upd-emblem" :class="{ 'is-done': done }">
        <div class="upd-ring" />
        <div class="upd-ring upd-ring--2" />
        <div class="upd-core">
          <svg v-if="!done" viewBox="0 0 24 24" class="upd-core__mark">
            <path d="M4 5l8 15L20 5" fill="none" stroke="url(#ug)" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" />
            <defs>
              <linearGradient id="ug" x1="0" y1="0" x2="1" y2="1">
                <stop offset="0%" stop-color="var(--acc-pale)" />
                <stop offset="100%" stop-color="var(--acc-2)" />
              </linearGradient>
            </defs>
          </svg>
          <svg v-else viewBox="0 0 24 24" class="upd-core__mark upd-check">
            <path d="M4 12.5l5 5L20 6.5" fill="none" stroke="#34d399" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round" />
          </svg>
        </div>
      </div>

      <!-- Title -->
      <div class="upd-titles">
        <h1 class="upd-title">Обновление VoidRP</h1>
        <span v-if="version" class="upd-ver">v{{ version }}</span>
      </div>

      <p class="upd-msg">{{ message }}</p>

      <!-- Progress -->
      <div class="upd-progress">
        <div class="upd-bar">
          <div
            class="upd-bar__fill"
            :class="{ 'is-done': done, 'is-live': !done }"
            :style="{ width: `${Math.max(4, percent)}%` }"
          >
            <span class="upd-bar__sheen" />
          </div>
        </div>
        <div class="upd-progress__row">
          <span class="upd-progress__label">
            <span v-if="done">Готово · перезапуск</span>
            <span v-else>Загрузка обновления</span>
          </span>
          <span class="upd-progress__pct">{{ percent }}%</span>
        </div>
      </div>

      <!-- Release notes -->
      <div v-if="notesLines.length" class="upd-notes">
        <div class="upd-notes__head">Что нового</div>
        <ul class="upd-notes__list">
          <li v-for="(line, i) in notesLines" :key="i" class="upd-notes__item">
            {{ line.replace(/^[•\-–]\s*/, '') }}
          </li>
        </ul>
      </div>

      <p class="upd-foot">Не закрывайте окно — лаунчер перезапустится автоматически.</p>
    </div>
  </div>
</template>

<style scoped>
.upd-root {
  position: fixed;
  inset: 0;
  z-index: 60;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1.5rem;
}

.upd-card {
  width: 100%;
  max-width: 460px;
  border-radius: 22px;
  padding: 2rem 1.9rem 1.5rem;
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  box-shadow: 0 30px 90px rgba(0, 0, 0, 0.55), 0 0 0 1px rgba(var(--acc-rgb), 0.14);
  animation: upd-in 0.5s cubic-bezier(0.16, 1, 0.3, 1);
}
@keyframes upd-in {
  from { opacity: 0; transform: translateY(14px) scale(0.98); }
  to { opacity: 1; transform: none; }
}

/* ── Emblem ─────────────────────────────────────────────── */
.upd-emblem {
  position: relative;
  width: 96px;
  height: 96px;
  display: grid;
  place-items: center;
  margin-bottom: 1.15rem;
}
.upd-ring {
  position: absolute;
  inset: 0;
  border-radius: 50%;
  background: conic-gradient(
    from 0deg,
    transparent 0deg,
    rgba(var(--acc-rgb), 0.05) 90deg,
    var(--acc) 300deg,
    var(--acc-pale) 355deg,
    transparent 360deg
  );
  -webkit-mask: radial-gradient(farthest-side, transparent calc(100% - 3px), #000 calc(100% - 3px));
  mask: radial-gradient(farthest-side, transparent calc(100% - 3px), #000 calc(100% - 3px));
  animation: upd-spin 2.4s linear infinite;
}
.upd-ring--2 {
  inset: 9px;
  opacity: 0.5;
  animation-duration: 3.6s;
  animation-direction: reverse;
}
.upd-emblem.is-done .upd-ring { animation-play-state: paused; opacity: 0.35; }
@keyframes upd-spin { to { transform: rotate(360deg); } }

.upd-core {
  position: relative;
  width: 64px;
  height: 64px;
  border-radius: 50%;
  display: grid;
  place-items: center;
  background: radial-gradient(circle at 50% 35%, rgba(var(--acc-rgb), 0.28), rgba(8, 10, 20, 0.9) 70%);
  box-shadow: inset 0 0 22px rgba(var(--acc-rgb), 0.3), 0 0 30px rgba(var(--acc-rgb), 0.28);
}
.upd-core__mark { width: 30px; height: 30px; filter: drop-shadow(0 0 6px rgba(var(--acc-rgb), 0.5)); }
.upd-check { animation: upd-pop 0.35s cubic-bezier(0.16, 1, 0.3, 1); }
@keyframes upd-pop { from { transform: scale(0.5); opacity: 0; } to { transform: none; opacity: 1; } }

/* ── Titles ─────────────────────────────────────────────── */
.upd-titles { display: flex; align-items: center; gap: 0.6rem; flex-wrap: wrap; justify-content: center; }
.upd-title {
  margin: 0;
  font-size: 1.32rem;
  font-weight: 800;
  letter-spacing: -0.01em;
  background: linear-gradient(135deg, #fff 0%, var(--acc-pale) 120%);
  -webkit-background-clip: text;
  background-clip: text;
  color: transparent;
}
.upd-ver {
  font-size: 0.72rem;
  font-weight: 700;
  padding: 0.18rem 0.55rem;
  border-radius: 999px;
  color: var(--acc-pale);
  background: rgba(var(--acc-rgb), 0.12);
  border: 1px solid rgba(var(--acc-rgb), 0.28);
}

.upd-msg {
  margin: 0.6rem 0 1.35rem;
  font-size: 0.86rem;
  line-height: 1.5;
  color: rgba(255, 255, 255, 0.62);
  min-height: 1.3em;
}

/* ── Progress ───────────────────────────────────────────── */
.upd-progress { width: 100%; }
.upd-bar {
  position: relative;
  height: 9px;
  border-radius: 6px;
  background: rgba(255, 255, 255, 0.06);
  overflow: hidden;
  box-shadow: inset 0 0 0 1px rgba(255, 255, 255, 0.04);
}
.upd-bar__fill {
  position: relative;
  height: 100%;
  border-radius: 6px;
  background: linear-gradient(90deg, var(--acc-2), var(--acc), var(--acc-pale));
  box-shadow: 0 0 14px rgba(var(--acc-rgb), 0.55);
  transition: width 0.6s cubic-bezier(0.16, 1, 0.3, 1);
  overflow: hidden;
}
.upd-bar__fill.is-done { background: linear-gradient(90deg, #10b981, #34d399); box-shadow: 0 0 14px rgba(52, 211, 153, 0.5); }
.upd-bar__sheen {
  position: absolute;
  inset: 0;
  background: linear-gradient(90deg, transparent, rgba(255, 255, 255, 0.45), transparent);
  transform: translateX(-100%);
}
.upd-bar__fill.is-live .upd-bar__sheen { animation: upd-sheen 1.4s ease-in-out infinite; }
@keyframes upd-sheen { to { transform: translateX(100%); } }

.upd-progress__row {
  display: flex;
  justify-content: space-between;
  align-items: baseline;
  margin-top: 0.5rem;
}
.upd-progress__label { font-size: 0.72rem; font-weight: 600; color: rgba(255, 255, 255, 0.45); }
.upd-progress__pct { font-size: 0.82rem; font-weight: 800; color: var(--acc-pale); font-variant-numeric: tabular-nums; }

/* ── Notes ──────────────────────────────────────────────── */
.upd-notes {
  width: 100%;
  margin-top: 1.4rem;
  text-align: left;
  border-radius: 14px;
  border: 1px solid rgba(255, 255, 255, 0.07);
  background: rgba(255, 255, 255, 0.025);
  padding: 0.9rem 1rem;
  max-height: 190px;
  overflow: auto;
}
.upd-notes__head {
  font-size: 0.68rem;
  font-weight: 800;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--acc-soft);
  margin-bottom: 0.55rem;
}
.upd-notes__list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 0.4rem; }
.upd-notes__item {
  position: relative;
  padding-left: 1rem;
  font-size: 0.8rem;
  line-height: 1.5;
  color: rgba(255, 255, 255, 0.78);
}
.upd-notes__item::before {
  content: '';
  position: absolute;
  left: 0;
  top: 0.55em;
  width: 5px;
  height: 5px;
  border-radius: 50%;
  background: var(--acc);
  box-shadow: 0 0 6px rgba(var(--acc-rgb), 0.7);
}

.upd-foot {
  margin: 1.25rem 0 0;
  font-size: 0.7rem;
  color: rgba(255, 255, 255, 0.32);
}

@media (prefers-reduced-motion: reduce) {
  .upd-ring, .upd-bar__fill.is-live .upd-bar__sheen { animation: none; }
}
</style>
