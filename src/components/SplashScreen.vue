<script setup lang="ts">
defineProps<{ statusText: string; progress?: number }>()

function particleStyle(i: number) {
  // Golden-angle spiral distribution for uniform spread
  const angle = (i * 137.508) % 360
  const dist  = 8 + (i * 6.18) % 42   // 8–50 % from center
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
  <div class="splash-root fixed inset-0 z-50 flex flex-col items-center justify-center overflow-hidden bg-[#04070d]">

    <!-- Dot-grid texture -->
    <div class="dot-grid pointer-events-none absolute inset-0"></div>

    <!-- Ambient glow orbs -->
    <div class="pointer-events-none absolute inset-0 overflow-hidden">
      <div class="absolute left-1/2 top-[-18%] h-[680px] w-[960px] -translate-x-1/2 rounded-full bg-violet-600/15 blur-[140px]"></div>
      <div class="absolute bottom-[-18%] right-[-8%] h-[480px] w-[520px] rounded-full bg-fuchsia-700/11 blur-[120px]"></div>
      <div class="absolute left-[-8%] top-[22%] h-[380px] w-[440px] rounded-full bg-indigo-600/9 blur-[110px]"></div>
    </div>

    <!-- Floating particles -->
    <div class="pointer-events-none absolute inset-0 overflow-hidden">
      <span
        v-for="i in 24"
        :key="i"
        class="particle absolute rounded-full"
        :style="particleStyle(i)"
      ></span>
    </div>

    <!-- ── Main visual ────────────────────────────────────── -->
    <div class="relative h-[260px] w-[260px]">

      <!-- SVG rings -->
      <svg class="absolute inset-0 h-full w-full overflow-visible" viewBox="0 0 260 260">
        <defs>
          <filter id="glow-xs" x="-80%" y="-80%" width="260%" height="260%">
            <feGaussianBlur stdDeviation="1.5" result="b"/>
            <feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge>
          </filter>
          <filter id="glow-md" x="-120%" y="-120%" width="340%" height="340%">
            <feGaussianBlur stdDeviation="4" result="b"/>
            <feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge>
          </filter>
          <filter id="glow-lg" x="-160%" y="-160%" width="420%" height="420%">
            <feGaussianBlur stdDeviation="7" result="b"/>
            <feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge>
          </filter>
        </defs>

        <!-- Ghost tracks -->
        <circle cx="130" cy="130" r="110" fill="none" stroke="rgba(139,92,246,0.10)" stroke-width="1"/>
        <circle cx="130" cy="130" r="78"  fill="none" stroke="rgba(99,102,241,0.10)"  stroke-width="1"/>
        <circle cx="130" cy="130" r="48"  fill="none" stroke="rgba(192,132,252,0.10)" stroke-width="1"/>

        <!-- Outer ring group (CW 10s) — arc starts at 3 o'clock, dot at leading edge -->
        <!-- C = 2π×110 ≈ 691 | arc 60% = 415, gap = 276 -->
        <g class="rg-1">
          <circle cx="130" cy="130" r="110" fill="none"
            stroke="#7c3aed" stroke-width="1.5" stroke-linecap="round"
            stroke-dasharray="415 276"
            filter="url(#glow-xs)" opacity="0.75"/>
          <!-- dot at 3 o'clock (240,130) in local coords -->
          <circle cx="240" cy="130" r="4.5" fill="#8b5cf6" filter="url(#glow-md)"/>
          <circle cx="240" cy="130" r="7"   fill="rgba(139,92,246,0.25)" filter="url(#glow-lg)"/>
        </g>

        <!-- Mid ring group (CCW 6.5s) -->
        <!-- C = 2π×78 ≈ 490 | arc 60% = 294, gap = 196 -->
        <g class="rg-2">
          <circle cx="130" cy="130" r="78" fill="none"
            stroke="#4f46e5" stroke-width="1.5" stroke-linecap="round"
            stroke-dasharray="294 196"
            filter="url(#glow-xs)" opacity="0.75"/>
          <circle cx="208" cy="130" r="3.8" fill="#6366f1" filter="url(#glow-md)"/>
          <circle cx="208" cy="130" r="6"   fill="rgba(99,102,241,0.25)" filter="url(#glow-lg)"/>
        </g>

        <!-- Inner ring group (CW 4s) -->
        <!-- C = 2π×48 ≈ 302 | arc 60% = 181, gap = 121 -->
        <g class="rg-3">
          <circle cx="130" cy="130" r="48" fill="none"
            stroke="#9333ea" stroke-width="1.5" stroke-linecap="round"
            stroke-dasharray="181 121"
            filter="url(#glow-xs)" opacity="0.75"/>
          <circle cx="178" cy="130" r="3"   fill="#c084fc" filter="url(#glow-md)"/>
          <circle cx="178" cy="130" r="4.8" fill="rgba(192,132,252,0.25)" filter="url(#glow-lg)"/>
        </g>
      </svg>

      <!-- Conic sweep (synced with outer ring CW 10s) -->
      <div class="conic-sweep absolute inset-0 rounded-full"></div>

      <!-- Core glow -->
      <div class="core absolute rounded-full" style="inset: 107px"></div>

      <!-- Logo text -->
      <div class="absolute inset-0 flex flex-col items-center justify-center">
        <p class="logo-main">VoidRP</p>
        <p class="logo-sub">Launcher</p>
      </div>

    </div>
    <!-- ────────────────────────────────────────────────────── -->

    <!-- Status -->
    <div class="mt-14 flex flex-col items-center gap-3">
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

      <p v-if="progress && progress > 0" class="progress-pct">{{ Math.round(progress) }}%</p>
    </div>

  </div>
</template>

<style scoped>
/* ── Root entrance ──────────────────────────────────────────── */
.splash-root {
  animation: splash-in 0.35s ease both;
}
@keyframes splash-in {
  from { opacity: 0; }
  to   { opacity: 1; }
}

/* ── Dot grid background ────────────────────────────────────── */
.dot-grid {
  background-image: radial-gradient(circle, rgba(255,255,255,0.028) 1px, transparent 1px);
  background-size: 36px 36px;
}

/* ── Particles ──────────────────────────────────────────────── */
.particle {
  background: radial-gradient(circle, rgba(167,139,250,0.7) 0%, transparent 70%);
  animation: particle-drift linear infinite;
  opacity: 0;
}
@keyframes particle-drift {
  0%   { transform: translateY(12px) scale(0.8); opacity: 0; }
  14%  { opacity: 0.65; }
  86%  { opacity: 0.65; }
  100% { transform: translateY(-38px) scale(1.1); opacity: 0; }
}

/* ── SVG ring rotation ──────────────────────────────────────── */
.rg-1 { transform-origin: 130px 130px; animation: cw  10s  linear infinite; }
.rg-2 { transform-origin: 130px 130px; animation: ccw 6.5s linear infinite; }
.rg-3 { transform-origin: 130px 130px; animation: cw  4s   linear infinite; }

@keyframes cw  { to { transform: rotate(360deg);  } }
@keyframes ccw { to { transform: rotate(-360deg); } }

/* ── Conic sweep ────────────────────────────────────────────── */
.conic-sweep {
  background: conic-gradient(
    transparent        190deg,
    rgba(139,92,246,0.06) 250deg,
    rgba(139,92,246,0.12) 280deg,
    transparent        360deg
  );
  animation: cw 10s linear infinite;
  pointer-events: none;
}

/* ── Core ───────────────────────────────────────────────────── */
.core {
  background: radial-gradient(circle at 40% 36%, rgba(216,180,254,0.95) 0%, rgba(99,102,241,0.65) 55%, transparent 100%);
  box-shadow:
    0 0 22px  5px  rgba(139,92,246,0.55),
    0 0 50px  14px rgba(99,102,241,0.25),
    0 0 100px 35px rgba(139,92,246,0.09);
  animation: core-pulse 2.6s ease-in-out infinite;
}
@keyframes core-pulse {
  0%, 100% { transform: scale(1);    opacity: 0.85; }
  50%       { transform: scale(1.16); opacity: 1;    }
}

/* ── Logo ───────────────────────────────────────────────────── */
.logo-main {
  font-size: 20px;
  font-weight: 800;
  letter-spacing: 0.22em;

  /* Shimmer sweep */
  background: linear-gradient(
    110deg,
    #7c3aed 0%,
    #a78bfa 28%,
    #ede9fe 42%,
    #a78bfa 56%,
    #818cf8 100%
  );
  background-size: 220% auto;
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
  animation: shimmer 3.5s linear infinite, fade-up 0.9s cubic-bezier(0.16,1,0.3,1) both;
}
@keyframes shimmer {
  from { background-position: 220% center; }
  to   { background-position: -220% center; }
}

.logo-sub {
  font-size: 8.5px;
  letter-spacing: 0.4em;
  text-transform: uppercase;
  color: rgba(255,255,255,0.28);
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
  color: rgba(255,255,255,0.27);
}
.dot {
  display: block;
  width: 4px;
  height: 4px;
  border-radius: 50%;
  background: rgba(167,139,250,0.6);
  animation: dot-pulse 1.1s ease-in-out infinite;
}
@keyframes dot-pulse {
  0%, 100% { transform: scale(0.5);  opacity: 0.3; }
  50%       { transform: scale(1.05); opacity: 1;   }
}

/* ── Progress bar ───────────────────────────────────────────── */
.progress-track {
  width: 200px;
  height: 3px;
  border-radius: 999px;
  background: rgba(255,255,255,0.07);
  overflow: hidden;
  position: relative;
}

.progress-fill {
  height: 100%;
  border-radius: 999px;
  background: linear-gradient(90deg, #7c3aed, #a78bfa);
  box-shadow: 0 0 8px rgba(139,92,246,0.7);
  transition: width 0.35s ease;
}

.progress-indeterminate {
  position: absolute;
  inset: 0;
  background: linear-gradient(90deg, transparent 0%, #7c3aed 40%, #a78bfa 60%, transparent 100%);
  animation: indeterminate 1.6s ease-in-out infinite;
  border-radius: 999px;
}

@keyframes indeterminate {
  0%   { transform: translateX(-100%); }
  100% { transform: translateX(300%); }
}

.progress-pct {
  font-size: 10px;
  letter-spacing: 0.06em;
  color: rgba(167,139,250,0.5);
  margin-top: -1px;
}
</style>
