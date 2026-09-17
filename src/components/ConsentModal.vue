<script setup lang="ts">
import { computed, nextTick, reactive, ref, watch } from 'vue'
import { SITE_BASE, useLauncherStore } from '../stores/launcher'

// Blocks the whole launcher until the current offer and personal data consent are accepted.
// The documents open in the browser; the optional choice of what is shown publicly is asked here
// too, so the player doesn't meet a second dialog on the site.
const launcher = useLauncherStore()

const open = computed(() => launcher.isAuthenticated && Boolean(launcher.consents?.missing?.length))

const offer = ref(false)
const personal = ref(false)
const distribution = reactive({ profile: false, map: false, purchases: false })
const saving = ref(false)
const tried = ref(false)
const error = ref('')
const requiredBox = ref<HTMLElement | null>(null)

watch(open, (value) => {
  if (!value) return
  offer.value = false
  personal.value = false
  tried.value = false
  error.value = ''
  Object.assign(distribution, launcher.consents?.distribution ?? {})
}, { immediate: true })

const CHANGES = [
  'Согласие на обработку персональных данных теперь отдельный документ.',
  'Появились «Условия платных услуг»: как выдаются покупки и как вернуть деньги.',
  'В политике для каждой цели указано, какие данные нужны, кто их получает и сколько они хранятся.',
  'Наказание назначает человек, его можно обжаловать в течение 30 дней.',
  'Изменения документов вступают в силу через 14 дней после публикации.',
  'Вы сами решаете, что о вас видно публично.',
]

const DOCS = [
  { path: '/offer', label: 'Договор оферты' },
  { path: '/paid-terms', label: 'Условия платных услуг' },
  { path: '/privacy', label: 'Политика конфиденциальности' },
  { path: '/consent', label: 'Согласие на обработку данных' },
  { path: '/distribution', label: 'Согласие на распространение' },
]

const CATEGORIES = [
  { key: 'profile', title: 'Публичный профиль', desc: 'Имя, описание, статус, изображения и ссылки на вашей странице профиля.' },
  { key: 'map', title: 'Место на карте мира', desc: 'Где вы находитесь, пока вы на сервере, и подпись ника у ваших личных территорий.' },
  { key: 'purchases', title: 'Покупки в магазине', desc: 'Ваш ник рядом с покупкой в списке последних покупок и в топе покупателей.' },
] as const

const canSubmit = computed(() => offer.value && personal.value)

function openDoc(path: string) {
  launcher.openExternal(`${SITE_BASE}${path}`)
}

async function submit() {
  tried.value = true
  error.value = ''
  if (!canSubmit.value) {
    await nextTick()
    requiredBox.value?.scrollIntoView({ behavior: 'smooth', block: 'center' })
    return
  }
  if (saving.value) return
  saving.value = true
  try {
    await launcher.submitConsents({ accept_offer: true, accept_personal_data: true, distribution: { ...distribution } })
  } catch (e: unknown) {
    error.value = e instanceof Error && e.message ? e.message : 'Не удалось сохранить согласие. Проверьте интернет и попробуйте ещё раз.'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <Transition name="consent-fade">
    <div
      v-if="open"
      class="fixed inset-0 z-[260] flex items-center justify-center p-6"
      style="background: rgba(4, 6, 12, 0.82); backdrop-filter: blur(8px);"
      role="dialog"
      aria-modal="true"
      aria-labelledby="consent-title"
    >
      <div class="panel panel--strong panel--edge flex max-h-[calc(100vh-48px)] w-full max-w-[600px] flex-col overflow-hidden">
        <div class="overflow-y-auto px-6 pb-2 pt-6">
          <p class="text-[12px] font-semibold text-[var(--acc-soft)]">Обновление документов</p>
          <h2 id="consent-title" class="mt-1 text-[20px] font-bold leading-tight text-white">Мы обновили правила проекта</h2>
          <p class="mt-1.5 text-[13px] leading-6 text-white/60">
            Прочитайте, что поменялось, и подтвердите новые условия, чтобы продолжить играть.
          </p>

          <div class="mt-4 rounded-[13px] border border-[rgba(var(--acc-rgb),0.25)] bg-[rgba(var(--acc-rgb),0.07)] p-4">
            <p class="text-[13px] font-semibold text-white">Что изменилось</p>
            <ul class="mt-2 flex flex-col gap-1.5">
              <li v-for="item in CHANGES" :key="item" class="flex gap-2 text-[12.5px] leading-5 text-white/75">
                <span class="mt-[7px] h-1.5 w-1.5 flex-none rounded-full bg-[var(--acc)]"></span>{{ item }}
              </li>
            </ul>
            <div class="mt-3 flex flex-wrap gap-1.5">
              <button
                v-for="doc in DOCS"
                :key="doc.path"
                type="button"
                class="btn-glass rounded-[10px] px-2.5 py-1 text-[11.5px] font-semibold text-white/80 hover:text-white"
                @click="openDoc(doc.path)"
              >
                {{ doc.label }} ↗
              </button>
            </div>
          </div>

          <fieldset
            ref="requiredBox"
            class="mt-4 rounded-[13px] border p-4"
            :class="tried && !canSubmit ? 'border-rose-400/50' : 'border-white/10'"
          >
            <legend class="px-1.5 text-[12.5px] font-bold text-white">Обязательные согласия</legend>
            <label class="flex cursor-pointer items-start gap-2.5 text-[13px] leading-5 text-white/80">
              <input v-model="offer" type="checkbox" class="mt-0.5 h-4 w-4 flex-none accent-[var(--acc)]" />
              <span>
                Принимаю условия
                <a href="#" class="font-semibold text-[var(--acc-pale)] underline underline-offset-2" @click.prevent="openDoc('/offer')">Договора оферты</a>
              </span>
            </label>
            <label class="mt-2.5 flex cursor-pointer items-start gap-2.5 text-[13px] leading-5 text-white/80">
              <input v-model="personal" type="checkbox" class="mt-0.5 h-4 w-4 flex-none accent-[var(--acc)]" />
              <span>
                Даю
                <a href="#" class="font-semibold text-[var(--acc-pale)] underline underline-offset-2" @click.prevent="openDoc('/consent')">согласие на обработку персональных данных</a>
                и подтверждаю, что ознакомлен(а) с
                <a href="#" class="font-semibold text-[var(--acc-pale)] underline underline-offset-2" @click.prevent="openDoc('/privacy')">Политикой конфиденциальности</a>
              </span>
            </label>
            <p v-if="tried && !canSubmit" class="mt-2.5 text-[12px] text-rose-300" role="alert">Отметьте оба обязательных согласия.</p>
          </fieldset>

          <fieldset class="mt-4 rounded-[13px] border border-white/10 p-4">
            <legend class="px-1.5 text-[12.5px] font-bold text-white">Что можно показывать всем (по желанию)</legend>
            <label
              v-for="cat in CATEGORIES"
              :key="cat.key"
              class="mt-2 flex cursor-pointer items-start gap-2.5 rounded-[11px] border p-3 first-of-type:mt-0"
              :class="distribution[cat.key] ? 'border-[rgba(var(--acc-rgb),0.35)] bg-[rgba(var(--acc-rgb),0.08)]' : 'border-white/[0.06] bg-white/[0.02]'"
            >
              <input v-model="distribution[cat.key]" type="checkbox" class="mt-0.5 h-4 w-4 flex-none accent-[var(--acc)]" />
              <span class="flex flex-col gap-0.5">
                <span class="text-[13px] font-semibold text-white">{{ cat.title }}</span>
                <span class="text-[12px] leading-5 text-white/55">{{ cat.desc }}</span>
              </span>
            </label>
            <p class="mt-2.5 text-[11.5px] leading-5 text-white/45">
              Без отметки эти сведения не публикуются. Выбор можно изменить в любой момент на сайте в оформлении профиля.
            </p>
          </fieldset>

          <p v-if="error" class="mt-3 rounded-[11px] border border-rose-400/25 bg-rose-400/[0.07] px-3 py-2 text-[12.5px] text-rose-200" role="alert">{{ error }}</p>
        </div>

        <div class="flex flex-wrap items-center justify-between gap-3 border-t border-white/[0.06] px-6 py-4">
          <p class="max-w-[280px] text-[11px] leading-4 text-white/40">
            Не согласны — выйдите из аккаунта. Удалить аккаунт можно письмом на support@void-rp.ru.
          </p>
          <div class="flex gap-2">
            <button
              type="button"
              class="btn-glass rounded-[12px] px-4 py-2 text-[13px] font-medium text-white/70 hover:text-white"
              :disabled="saving"
              @click="launcher.logout()"
            >
              Выйти
            </button>
            <button
              type="button"
              class="btn-acc rounded-[12px] px-4 py-2 text-[13px] font-bold"
              :disabled="saving"
              @click="submit"
            >
              {{ saving ? 'Сохраняем…' : 'Подтвердить и продолжить' }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </Transition>
</template>

<style scoped>
.consent-fade-enter-active,
.consent-fade-leave-active {
  transition: opacity 0.2s ease;
}
.consent-fade-enter-from,
.consent-fade-leave-to {
  opacity: 0;
}
</style>
