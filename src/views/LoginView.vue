<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useLauncherStore } from '../stores/launcher'
import { useLauncherSessionStore } from '../stores/launcherSession'

const launcher = useLauncherStore()
const session = useLauncherSessionStore()

const login = ref(session.rememberedLogin)
const password = ref('')

watch(login, (value) => { session.setRememberedLogin(value) })

const canSubmit = computed(() =>
  !launcher.isBusy && login.value.trim().length > 0 && password.value.trim().length > 0
)

async function submit() {
  const result = await launcher.login(login.value, password.value)
  if (result?.ok) password.value = ''
}

const coreOnline = computed(() => launcher.initialized)
</script>

<template>
  <div class="flex h-full items-center justify-center p-6">

    <!-- Card -->
    <div class="relative w-full max-w-[400px]">

      <!-- Card glow -->
      <div class="pointer-events-none absolute -inset-px rounded-[32px] bg-gradient-to-b from-violet-500/8 to-transparent opacity-0 transition duration-500"
        :class="{ 'opacity-100': canSubmit }"></div>

      <div class="panel panel--strong panel--edge relative rounded-[30px] p-8 shadow-2xl shadow-black/40">

        <!-- Brand -->
        <div class="flex items-start justify-between gap-4">
          <div>
            <div class="flex items-center gap-2">
              <!-- Simple logo mark -->
              <div class="flex h-8 w-8 items-center justify-center rounded-xl bg-gradient-to-br from-violet-500 to-indigo-600">
                <svg class="h-4 w-4 text-white" fill="currentColor" viewBox="0 0 20 20">
                  <path d="M6.3 2.841A1.5 1.5 0 0 0 4 4.11v11.78a1.5 1.5 0 0 0 2.3 1.269l9.344-5.89a1.5 1.5 0 0 0 0-2.538L6.3 2.84z"/>
                </svg>
              </div>
              <span class="text-[11px] font-bold uppercase tracking-[0.3em] text-white/60">VoidRP</span>
            </div>
            <h1 class="mt-4 text-[36px] font-bold leading-none tracking-tight">Вход</h1>
            <p class="mt-2.5 text-sm leading-6 text-white/45">
              Войдите чтобы открыть лаунчер и запустить игру.
            </p>
          </div>

          <!-- Core status -->
          <div
            class="shrink-0 rounded-2xl border px-3 py-1.5 text-[10px] font-semibold uppercase tracking-wider transition-colors duration-300"
            :class="coreOnline
              ? 'border-emerald-400/25 bg-emerald-400/10 text-emerald-300'
              : 'border-white/10 bg-white/5 text-white/35'"
          >
            {{ coreOnline ? '● Онлайн' : '○ Ожидание' }}
          </div>
        </div>

        <!-- Form -->
        <form class="mt-7 space-y-3" @submit.prevent="submit">
          <label class="block">
            <span class="mb-1.5 block text-[11px] font-medium uppercase tracking-[0.12em] text-white/40">Логин или почта</span>
            <input
              v-model="login"
              type="text"
              autocomplete="username"
              placeholder="Введите логин"
              class="input-modern h-11 w-full rounded-[14px] px-4 text-sm outline-none placeholder:text-white/20"
            />
          </label>

          <label class="block">
            <span class="mb-1.5 block text-[11px] font-medium uppercase tracking-[0.12em] text-white/40">Пароль</span>
            <input
              v-model="password"
              type="password"
              autocomplete="current-password"
              placeholder="Введите пароль"
              class="input-modern h-11 w-full rounded-[14px] px-4 text-sm outline-none placeholder:text-white/20"
            />
          </label>

          <button
            type="submit"
            :disabled="!canSubmit"
            class="btn-acc mt-1 h-11 w-full rounded-[14px] text-sm font-bold"
          >
            {{ launcher.isBusy ? 'Выполняем вход...' : 'Войти' }}
          </button>
        </form>

        <!-- Links -->
        <div class="mt-4 flex flex-wrap gap-2">
          <button
            v-for="{ label, url } in [
              { label: 'Регистрация', url: launcher.links.registerUrl },
              { label: 'Забыли пароль', url: launcher.links.forgotPasswordUrl },
              { label: 'Подтвердить почту', url: launcher.links.verifyEmailUrl },
            ]"
            :key="label"
            class="btn-glass rounded-xl px-3 py-1.5 text-xs text-white/50 hover:text-white/85"
            @click="launcher.openExternal(url)"
          >
            {{ label }}
          </button>
        </div>

        <!-- Progress -->
        <div
          v-if="launcher.shouldShowProgress"
          class="mt-5 rounded-[18px] border border-white/10 bg-white/[0.04] p-4"
        >
          <div class="flex items-start justify-between gap-3">
            <div>
              <p class="text-sm font-semibold text-white/90">{{ launcher.progress.title || 'Подготовка' }}</p>
              <p class="mt-0.5 text-xs text-white/45">{{ launcher.progress.details || launcher.statusText }}</p>
            </div>
            <span class="shrink-0 text-xs tabular-nums text-white/45">{{ Math.round(launcher.progress.percent) }}%</span>
          </div>
          <div class="mt-3 h-1.5 overflow-hidden rounded-full bg-white/8">
            <div
              class="progress-acc h-full rounded-full transition-all duration-300"
              :style="{ width: `${launcher.progress.percent}%` }"
            ></div>
          </div>
        </div>

        <!-- Footer -->
        <div class="mt-5 flex items-center justify-between text-[11px] text-white/25">
          <span>v{{ launcher.launcherVersionText }}</span>
          <span class="truncate text-right">{{ launcher.statusText }}</span>
        </div>

      </div>
    </div>
  </div>
</template>
