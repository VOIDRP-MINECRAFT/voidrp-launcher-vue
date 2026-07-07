<script setup lang="ts">
defineProps<{ statusText: string; progress?: number }>()

function particleStyle(i: number) {
  // Golden-angle spiral distribution for uniform spread
  const angle = (i * 137.508) % 360
  const dist  = 10 + (i * 6.18) % 44   // 10–54 % from center
  const size  = 1 + (i % 4) * 0.6
  const x = 50 + Math.cos((angle * Math.PI) / 180) * dist
  const y = 50 + Math.sin((angle * Math.PI) / 180) * dist
  return {
    width:             `${size}px`,
    height:            `${size}px`,
    left:              `${x}%`,
    top:               `${y}%`,
    animationDelay:    `${(i * 0.31) % 5}s`,
    animationDuration: `${5 + (i % 5)}s`,
  }
}
</script>

<template>
  <div class="splash-root fixed inset-0 z-50 flex flex-col items-center justify-center overflow-hidden">

    <!-- Ambient aurora -->
    <div class="pointer-events-none absolute inset-0 overflow-hidden">
      <div class="aurora aurora--1"></div>
      <div class="aurora aurora--2"></div>
    </div>

    <!-- Dot-grid texture + vignette -->
    <div class="dot-grid pointer-events-none absolute inset-0"></div>
    <div class="vignette"></div>

    <!-- Floating particles -->
    <div class="pointer-events-none absolute inset-0 overflow-hidden">
      <span
        v-for="i in 20"
        :key="i"
        class="particle absolute rounded-full"
        :style="particleStyle(i)"
      ></span>
    </div>

    <!-- ── Main visual ────────────────────────────────────── -->
    <div class="relative h-[250px] w-[250px]">

      <!-- Orbit arcs -->
      <svg class="absolute inset-0 h-full w-full overflow-visible" viewBox="0 0 250 250">
        <defs>
          <filter id="sg-xs" x="-80%" y="-80%" width="260%" height="260%">
            <feGaussianBlur stdDeviation="1.4" result="b"/>
            <feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge>
          </filter>
          <filter id="sg-md" x="-120%" y="-120%" width="340%" height="340%">
            <feGaussianBlur stdDeviation="4" result="b"/>
            <feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge>
          </filter>
          <linearGradient id="arc-grad" x1="0" y1="0" x2="1" y2="1">
            <stop offset="0%"  stop-color="var(--acc)"/>
            <stop offset="100%" stop-color="var(--acc-soft)"/>
          </linearGradient>
        </defs>

        <!-- Ghost tracks -->
        <circle cx="125" cy="125" r="108" fill="none" stroke="rgba(255,255,255,0.05)" stroke-width="1"/>
        <circle cx="125" cy="125" r="82"  fill="none" stroke="rgba(255,255,255,0.04)" stroke-width="1"/>

        <!-- Outer arc (CW 9s) — C = 2π×108 ≈ 679 -->
        <g class="rg-1">
          <circle cx="125" cy="125" r="108" fill="none"
            stroke="url(#arc-grad)" stroke-width="1.4" stroke-linecap="round"
            stroke-dasharray="380 299"
            filter="url(#sg-xs)" opacity="0.8"/>
          <circle cx="233" cy="125" r="4" fill="var(--acc-soft)" filter="url(#sg-md)"/>
        </g>

        <!-- Inner arc (CCW 5.5s) — C = 2π×82 ≈ 515 -->
        <g class="rg-2">
          <circle cx="125" cy="125" r="82" fill="none"
            stroke="rgba(255,255,255,0.32)" stroke-width="1.1" stroke-linecap="round"
            stroke-dasharray="180 335"
            filter="url(#sg-xs)" opacity="0.6"/>
          <circle cx="207" cy="125" r="2.8" fill="#fff" opacity="0.75" filter="url(#sg-md)"/>
        </g>
      </svg>

      <!-- Conic sweep (synced with outer arc) -->
      <div class="conic-sweep absolute inset-0 rounded-full"></div>

      <!-- Core tile with monogram -->
      <div class="core-tile absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2">
        <span class="core-tile__mark">V</span>
      </div>

    </div>
    <!-- ────────────────────────────────────────────────────── -->

    <!-- Wordmark -->
    <div class="mt-10 flex flex-col items-center gap-1.5">
      <p class="logo-main">VOIDRP</p>
      <p class="logo-sub">Launcher</p>
    </div>

    <!-- Status -->
    <div class="mt-9 flex flex-col items-center gap-3">
      <p class="status-text">{{ statusText || 'Инициализация...' }}</p>

      <!-- Progress bar -->
      <div class="progress-track">
        <div
          v-if="progress && progress > 0"
          class="progress-fill"
          :style="{ width: `${Math.min(progress, 100)}%` }"
        />
        <div v-else class="progress-indeterminate" />
      </div>

      <p class="progress-pct" :class="{ 'opacity-0': !progress || progress <= 0 }">
        {{ Math.round(progress || 0) }}%
      </p>
    </div>

  </div>
</template>

<style scoped>
/* ── Root entrance ──────────────────────────────────────────── */
.splash-root {
  background: var(--bg-0, #030509);
  animation: splash-in 0.35s ease both;
}
@keyframes splash-in {
  from { opacity: 0; }
  to   { opacity: 1; }
}

/* ── Dot grid background ────────────────────────────────────── */
.dot-grid {
  background-image: radial-gradient(circle, rgba(255,255,255,0.026) 1px, transparent 1px);
  background-size: 34px 34px;
  mask-image: radial-gradient(ellipse 75% 70% at 50% 45%, black 30%, transparent 100%);
}

/* ── Particles ──────────────────────────────────────────────── */
.particle {
  background: radial-gradient(circle, rgba(var(--acc-soft-rgb), 0.7) 0%, transparent 70%);
  animation: particle-drift linear infinite;
  opacity: 0;
}
@keyframes particle-drift {
  0%   { transform: translateY(12px) scale(0.8); opacity: 0; }
  14%  { opacity: 0.6; }
  86%  { opacity: 0.6; }
  100% { transform: translateY(-40px) scale(1.1); opacity: 0; }
}

/* ── Arc rotation ───────────────────────────────────────────── */
.rg-1 { transform-origin: 125px 125px; animation: cw  9s   linear infinite; }
.rg-2 { transform-origin: 125px 125px; animation: ccw 5.5s linear infinite; }

@keyframes cw  { to { transform: rotate(360deg);  } }
@keyframes ccw { to { transform: rotate(-360deg); } }

/* ── Conic sweep ────────────────────────────────────────────── */
.conic-sweep {
  background: conic-gradient(
    transparent          195deg,
    rgba(var(--acc-rgb), 0.05) 255deg,
    rgba(var(--acc-rgb), 0.11) 285deg,
    transparent          360deg
  );
  animation: cw 9s linear infinite;
  pointer-events: none;
}

/* ── Core tile ──────────────────────────────────────────────── */
.core-tile {
  width: 92px; height: 92px;
  border-radius: 26px;
  display: flex; align-items: center; justify-content: center;
  background:
    linear-gradient(180deg, rgba(255,255,255,0.08) 0%, rgba(255,255,255,0.02) 55%, transparent 100%),
    rgba(10, 14, 27, 0.72);
  border: 1px solid rgba(var(--acc-rgb), 0.32);
  box-shadow:
    0 0 34px rgba(var(--acc-rgb), 0.32),
    0 22px 50px rgba(0, 0, 0, 0.55),
    inset 0 1px 0 rgba(255, 255, 255, 0.12);
  backdrop-filter: blur(16px);
  animation: tile-pulse 2.8s ease-in-out infinite, fade-up 0.9s cubic-bezier(0.16,1,0.3,1) both;
}
@keyframes tile-pulse {
  0%, 100% { box-shadow: 0 0 26px rgba(var(--acc-rgb), 0.26), 0 22px 50px rgba(0,0,0,0.55), inset 0 1px 0 rgba(255,255,255,0.12); }
  50%       { box-shadow: 0 0 52px rgba(var(--acc-rgb), 0.5),  0 22px 50px rgba(0,0,0,0.55), inset 0 1px 0 rgba(255,255,255,0.14); }
}

.core-tile__mark {
  font-size: 42px;
  font-weight: 900;
  line-height: 1;
  letter-spacing: -0.02em;
  background: linear-gradient(160deg, #fff 0%, var(--acc-pale) 40%, var(--acc) 100%);
  -webkit-background-clip: text;
  background-clip: text;
  -webkit-text-fill-color: transparent;
  filter: drop-shadow(0 4px 14px rgba(var(--acc-rgb), 0.45));
  transform: translateY(-1px);
}

/* ── Wordmark ───────────────────────────────────────────────── */
.logo-main {
  font-size: 22px;
  font-weight: 800;
  letter-spacing: 0.34em;
  margin-right: -0.34em; /* компенсация трекинга для оптического центра */

  background: linear-gradient(
    110deg,
    var(--acc) 0%,
    var(--acc-soft) 28%,
    #f4f0ff 42%,
    var(--acc-soft) 56%,
    var(--acc-2) 100%
  );
  background-size: 220% auto;
  -webkit-background-clip: text;
  background-clip: text;
  -webkit-text-fill-color: transparent;
  animation: shimmer 3.5s linear infinite, fade-up 0.9s cubic-bezier(0.16,1,0.3,1) both;
}
@keyframes shimmer {
  from { background-position: 220% center; }
  to   { background-position: -220% center; }
}

.logo-sub {
  font-size: 9px;
  letter-spacing: 0.52em;
  margin-right: -0.52em;
  text-transform: uppercase;
  color: rgba(255,255,255,0.3);
  animation: fade-up 0.9s cubic-bezier(0.16,1,0.3,1) 0.13s both;
}
@keyframes fade-up {
  from { opacity: 0; transform: translateY(8px); }
  to   { opacity: 1; transform: translateY(0);   }
}

/* ── Status ─────────────────────────────────────────────────── */
.status-text {
  font-size: 11px;
  letter-spacing: 0.07em;
  color: rgba(255,255,255,0.3);
}

/* ── Progress bar ───────────────────────────────────────────── */
.progress-track {
  width: 230px;
  height: 4px;
  border-radius: 999px;
  background: rgba(255,255,255,0.06);
  box-shadow: inset 0 1px 2px rgba(0,0,0,0.4);
  overflow: hidden;
  position: relative;
}

.progress-fill {
  height: 100%;
  border-radius: 999px;
  background: linear-gradient(90deg, var(--acc), var(--acc-soft));
  box-shadow: 0 0 12px rgba(var(--acc-rgb), 0.7);
  transition: width 0.35s ease;
}

.progress-indeterminate {
  position: absolute;
  inset: 0;
  background: linear-gradient(90deg, transparent 0%, var(--acc) 40%, var(--acc-soft) 60%, transparent 100%);
  animation: indeterminate 1.6s ease-in-out infinite;
  border-radius: 999px;
}

@keyframes indeterminate {
  0%   { transform: translateX(-100%); }
  100% { transform: translateX(300%); }
}

.progress-pct {
  font-size: 10px;
  letter-spacing: 0.08em;
  font-variant-numeric: tabular-nums;
  color: rgba(var(--acc-soft-rgb), 0.6);
  margin-top: -2px;
  transition: opacity 0.3s;
}
</style>
