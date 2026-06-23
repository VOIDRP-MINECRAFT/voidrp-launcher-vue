<script setup lang="ts">
import { ref } from 'vue'
import { useLauncherStore, API_BASE } from '../stores/launcher'

const launcher = useLauncherStore()

const url = ref('')
const comment = ref('')
const loading = ref(false)
const successMsg = ref('')
const errorMsg = ref('')

const ALLOWED_DOMAINS = ['modrinth.com', 'curseforge.com', 'minecraft-inside.ru']

function detectSource(u: string): string {
  if (u.includes('modrinth.com')) return 'Modrinth'
  if (u.includes('curseforge.com')) return 'CurseForge'
  if (u.includes('minecraft-inside.ru')) return 'Minecraft-Inside'
  return ''
}

function validateUrl(u: string): string | null {
  u = u.trim()
  if (!u) return 'Вставьте ссылку на мод'
  try {
    const src = u.includes('://') ? u : `https://${u}`
    const host = new URL(src).hostname.toLowerCase().replace(/^www\./, '')
    if (!ALLOWED_DOMAINS.includes(host)) {
      return 'Разрешены только: modrinth.com, curseforge.com, minecraft-inside.ru'
    }
  } catch {
    return 'Некорректная ссылка'
  }
  return null
}

async function submit() {
  successMsg.value = ''
  errorMsg.value = ''
  const err = validateUrl(url.value)
  if (err) { errorMsg.value = err; return }

  loading.value = true
  try {
    const resp = await fetch(`${API_BASE}/api/mod-suggestions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ url: url.value.trim(), comment: comment.value.trim() || null }),
    })
    const data = await resp.json() as { ok: boolean; message: string }
    if (data.ok) {
      successMsg.value = data.message || 'Предложение отправлено!'
      url.value = ''
      comment.value = ''
    } else {
      errorMsg.value = data.message || 'Ошибка при отправке'
    }
  } catch {
    errorMsg.value = 'Не удалось связаться с ядром лаунчера'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div>
    <!-- Header -->
    <div class="flex items-end justify-between gap-4">
      <div>
        <p class="text-[11px] uppercase tracking-[0.25em] text-violet-300/70">Моды</p>
        <h2 class="mt-1.5 text-2xl font-semibold leading-tight">Предложить мод</h2>
        <p class="mt-2 text-sm leading-6 text-white/50">
          Нашли полезный мод? Отправьте ссылку — мы рассмотрим его для добавления на сервер.
        </p>
      </div>
    </div>

    <!-- Allowed sources -->
    <div class="mt-5 flex flex-wrap gap-2">
      <a
        v-for="d in [
          { name: 'Modrinth', href: 'https://modrinth.com', color: 'text-emerald-300 border-emerald-500/25 bg-emerald-500/8' },
          { name: 'CurseForge', href: 'https://www.curseforge.com', color: 'text-orange-300 border-orange-500/25 bg-orange-500/8' },
          { name: 'Minecraft-Inside', href: 'https://minecraft-inside.ru', color: 'text-sky-300 border-sky-500/25 bg-sky-500/8' },
        ]"
        :key="d.name"
        :href="d.href"
        target="_blank"
        rel="noopener noreferrer"
        class="inline-flex items-center gap-1.5 rounded-full border px-3 py-1 text-[11px] font-medium transition hover:brightness-125"
        :class="d.color"
      >
        <svg class="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
          <path stroke-linecap="round" stroke-linejoin="round" d="M13.5 6H5.25A2.25 2.25 0 003 8.25v10.5A2.25 2.25 0 005.25 21h10.5A2.25 2.25 0 0018 18.75V10.5m-10.5 6L21 3m0 0h-5.25M21 3v5.25"/>
        </svg>
        {{ d.name }}
      </a>
    </div>

    <!-- Form -->
    <div class="mt-5 rounded-[20px] border border-white/8 bg-white/[0.03] p-5">
      <div class="flex flex-col gap-4">

        <!-- URL field -->
        <div>
          <label class="mb-1.5 block text-[11px] font-semibold uppercase tracking-wider text-white/40">
            Ссылка на мод <span class="text-red-400">*</span>
          </label>
          <div class="relative">
            <input
              v-model="url"
              type="url"
              placeholder="https://modrinth.com/mod/..."
              class="w-full rounded-[12px] border border-white/10 bg-white/5 px-4 py-2.5 pr-24 text-sm text-white placeholder-white/25 outline-none transition focus:border-violet-500/60 focus:bg-white/7"
              @keydown.enter.prevent="submit"
            />
            <span
              v-if="detectSource(url)"
              class="absolute right-3 top-1/2 -translate-y-1/2 rounded-full bg-violet-500/15 px-2 py-0.5 text-[10px] font-semibold text-violet-300"
            >
              {{ detectSource(url) }}
            </span>
          </div>
        </div>

        <!-- Comment field -->
        <div>
          <label class="mb-1.5 block text-[11px] font-semibold uppercase tracking-wider text-white/40">
            Комментарий <span class="text-white/25">(необязательно)</span>
          </label>
          <textarea
            v-model="comment"
            rows="3"
            placeholder="Для чего этот мод? Почему стоит добавить его на сервер?"
            class="w-full resize-none rounded-[12px] border border-white/10 bg-white/5 px-4 py-2.5 text-sm text-white placeholder-white/25 outline-none transition focus:border-violet-500/60 focus:bg-white/7"
          />
        </div>

        <!-- Submit -->
        <div class="flex items-center gap-3">
          <button
            class="rounded-[12px] bg-violet-600 px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-violet-500 disabled:cursor-not-allowed disabled:opacity-40"
            :disabled="loading || !url.trim()"
            @click="submit"
          >
            <span v-if="loading" class="flex items-center gap-2">
              <span class="h-3.5 w-3.5 animate-spin rounded-full border-2 border-white/40 border-t-white"></span>
              Отправка...
            </span>
            <span v-else>Отправить предложение</span>
          </button>
        </div>

        <!-- Messages -->
        <div v-if="successMsg" class="flex items-center gap-2 rounded-[10px] bg-emerald-500/10 border border-emerald-500/20 px-4 py-2.5 text-sm text-emerald-300">
          <svg class="h-4 w-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
            <path stroke-linecap="round" stroke-linejoin="round" d="M4.5 12.75l6 6 9-13.5"/>
          </svg>
          {{ successMsg }}
        </div>
        <div v-if="errorMsg" class="flex items-center gap-2 rounded-[10px] bg-red-500/10 border border-red-500/20 px-4 py-2.5 text-sm text-red-300">
          <svg class="h-4 w-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" stroke-width="2.5">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z"/>
          </svg>
          {{ errorMsg }}
        </div>
      </div>
    </div>

    <!-- Note -->
    <p class="mt-3 text-xs text-white/25">
      Предложения рассматриваются администрацией сервера. Мы не гарантируем добавление каждого предложенного мода.
    </p>
  </div>
</template>
