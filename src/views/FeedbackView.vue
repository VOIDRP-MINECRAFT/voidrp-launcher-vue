<script setup lang="ts">
import { ref } from 'vue'
import { API_BASE } from '../stores/launcher'

const TYPES = [
  { value: 'suggestion', label: 'Предложение', desc: 'Идея по улучшению сервера или лаунчера', color: 'violet' },
  { value: 'bug',        label: 'Баг',          desc: 'Ошибка или некорректное поведение',      color: 'red'    },
  { value: 'complaint',  label: 'Жалоба',        desc: 'Жалоба на игрока или ситуацию',          color: 'orange' },
] as const

type FeedbackType = 'suggestion' | 'bug' | 'complaint'

const selectedType = ref<FeedbackType>('suggestion')
const title = ref('')
const body = ref('')
const loading = ref(false)
const successMsg = ref('')
const errorMsg = ref('')

function validate(): string | null {
  if (!title.value.trim()) return 'Введите заголовок'
  if (title.value.trim().length > 200) return 'Заголовок не более 200 символов'
  return null
}

async function submit() {
  successMsg.value = ''
  errorMsg.value = ''
  const err = validate()
  if (err) { errorMsg.value = err; return }

  loading.value = true
  try {
    const resp = await fetch(`${API_BASE}/api/player-feedback`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        type: selectedType.value,
        title: title.value.trim(),
        body: body.value.trim() || null,
      }),
    })
    const data = await resp.json() as { ok: boolean; message: string }
    if (data.ok) {
      successMsg.value = data.message || 'Обращение отправлено!'
      title.value = ''
      body.value = ''
    } else {
      errorMsg.value = data.message || 'Ошибка при отправке'
    }
  } catch {
    errorMsg.value = 'Не удалось связаться с ядром лаунчера'
  } finally {
    loading.value = false
  }
}

const colorMap = {
  violet: 'text-violet-300 border-violet-500/30 bg-violet-500/10',
  red:    'text-red-300    border-red-500/30    bg-red-500/10',
  orange: 'text-orange-300 border-orange-500/30 bg-orange-500/10',
}
const activeColorMap = {
  violet: 'border-violet-500/60 bg-violet-500/15 text-white',
  red:    'border-red-500/60    bg-red-500/15    text-white',
  orange: 'border-orange-500/60 bg-orange-500/15 text-white',
}
const focusColorMap = {
  violet: 'focus:border-violet-500/60',
  red:    'focus:border-red-500/60',
  orange: 'focus:border-orange-500/60',
}
</script>

<template>
  <div>
    <!-- Header -->
    <div class="flex items-end justify-between gap-4">
      <div>
        <p class="text-[11px] uppercase tracking-[0.25em] text-violet-300/70">Обратная связь</p>
        <h2 class="mt-1.5 text-2xl font-semibold leading-tight">Предложения, баги, жалобы</h2>
        <p class="mt-2 text-sm leading-6 text-white/50">
          Нашли баг, хотите предложить идею или пожаловаться? Мы обязательно рассмотрим.
        </p>
      </div>
    </div>

    <!-- Type selector -->
    <div class="mt-5 flex flex-wrap gap-2">
      <button
        v-for="t in TYPES"
        :key="t.value"
        class="flex flex-col items-start gap-0.5 rounded-[14px] border px-4 py-3 text-left transition hover:brightness-110"
        :class="selectedType === t.value ? activeColorMap[t.color] : colorMap[t.color]"
        @click="selectedType = t.value as FeedbackType"
      >
        <span class="text-[13px] font-semibold">{{ t.label }}</span>
        <span class="text-[11px] opacity-60">{{ t.desc }}</span>
      </button>
    </div>

    <!-- Form -->
    <div class="mt-5 rounded-[20px] border border-white/8 bg-white/[0.03] p-5">
      <div class="flex flex-col gap-4">

        <!-- Title -->
        <div>
          <label class="mb-1.5 block text-[11px] font-semibold uppercase tracking-wider text-white/40">
            Заголовок <span class="text-red-400">*</span>
          </label>
          <input
            v-model="title"
            type="text"
            placeholder="Кратко опишите суть..."
            maxlength="200"
            class="w-full rounded-[12px] border border-white/10 bg-white/5 px-4 py-2.5 text-sm text-white placeholder-white/25 outline-none transition focus:bg-white/7"
            :class="focusColorMap[TYPES.find(t => t.value === selectedType)!.color]"
            @keydown.enter.prevent="submit"
          />
        </div>

        <!-- Body -->
        <div>
          <label class="mb-1.5 block text-[11px] font-semibold uppercase tracking-wider text-white/40">
            Подробное описание <span class="text-white/25">(необязательно)</span>
          </label>
          <textarea
            v-model="body"
            rows="4"
            placeholder="Расскажите подробнее..."
            class="w-full resize-none rounded-[12px] border border-white/10 bg-white/5 px-4 py-2.5 text-sm text-white placeholder-white/25 outline-none transition focus:bg-white/7"
            :class="focusColorMap[TYPES.find(t => t.value === selectedType)!.color]"
          />
        </div>

        <!-- Submit -->
        <div class="flex items-center gap-3">
          <button
            class="rounded-[12px] bg-violet-600 px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-violet-500 disabled:cursor-not-allowed disabled:opacity-40"
            :disabled="loading || !title.trim()"
            @click="submit"
          >
            <span v-if="loading" class="flex items-center gap-2">
              <span class="h-3.5 w-3.5 animate-spin rounded-full border-2 border-white/40 border-t-white"></span>
              Отправка...
            </span>
            <span v-else>Отправить</span>
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

    <p class="mt-3 text-xs text-white/25">
      Обращения рассматриваются администрацией сервера.
    </p>
  </div>
</template>