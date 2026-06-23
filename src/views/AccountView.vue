<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from 'vue'
import { useLauncherStore } from '../stores/launcher'

const launcher = useLauncherStore()

type Tab = 'profile' | 'skin' | 'security'
const activeTab = ref<Tab>('profile')

const diagnosticsText = computed(() => launcher.diagnosticsText?.trim() || 'Диагностика пока пуста.')
const selectedVariant = ref<'classic' | 'slim'>('classic')
const selectedFile = ref<File | null>(null)
const localPreviewUrl = ref('')
const uploadBusy = ref(false)
const securityBusy = ref(false)

function fmt(value: number | string | null | undefined) {
  const n = Number(value ?? 0)
  return new Intl.NumberFormat('ru-RU').format(Number.isFinite(n) ? n : 0)
}
function fmtDate(value: string | null | undefined) {
  if (!value) return '—'
  const d = new Date(value)
  return Number.isNaN(d.getTime()) ? '—' : new Intl.DateTimeFormat('ru-RU', { dateStyle: 'medium', timeStyle: 'short' }).format(d)
}

function onFileChange(e: Event) {
  const file = (e.target as HTMLInputElement)?.files?.[0] ?? null
  selectedFile.value = file
  if (localPreviewUrl.value) URL.revokeObjectURL(localPreviewUrl.value)
  localPreviewUrl.value = file ? URL.createObjectURL(file) : ''
}

async function saveSkin() {
  if (!selectedFile.value) return
  uploadBusy.value = true
  try { await launcher.uploadSkin(selectedFile.value, selectedVariant.value); selectedFile.value = null; localPreviewUrl.value = '' }
  finally { uploadBusy.value = false }
}
async function removeSkin() {
  uploadBusy.value = true
  try { await launcher.deleteSkin(); selectedFile.value = null; localPreviewUrl.value = '' }
  finally { uploadBusy.value = false }
}
async function revokeOtherSessions() {
  securityBusy.value = true
  try { await launcher.revokeOtherSessions() }
  finally { securityBusy.value = false }
}

onBeforeUnmount(() => { if (localPreviewUrl.value) URL.revokeObjectURL(localPreviewUrl.value) })

const tabs: { key: Tab; label: string }[] = [
  { key: 'profile',  label: 'Профиль'     },
  { key: 'skin',     label: 'Скин'        },
  { key: 'security', label: 'Безопасность' },
]
</script>

<template>
  <div class="space-y-4">

    <!-- Tab bar -->
    <div class="flex gap-1 rounded-[18px] border border-white/10 bg-white/[0.03] p-1">
      <button
        v-for="t in tabs"
        :key="t.key"
        class="flex-1 rounded-[13px] py-2 text-xs font-semibold transition"
        :class="activeTab === t.key
          ? 'bg-violet-500/20 text-violet-200'
          : 'text-white/40 hover:text-white/70'"
        @click="activeTab = t.key"
      >{{ t.label }}</button>
    </div>

    <!-- ── PROFILE ───────────────────────────────────────────── -->
    <template v-if="activeTab === 'profile'">
      <section class="rounded-[22px] border border-white/10 bg-white/[0.035] p-5">
        <div class="flex items-center gap-4">
          <!-- Avatar letter -->
          <div class="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-violet-500 to-indigo-600 text-xl font-bold text-white shadow-lg shadow-violet-500/20">
            {{ (launcher.accountNickname?.[0] ?? '?').toUpperCase() }}
          </div>
          <div class="min-w-0">
            <p class="truncate text-lg font-bold">{{ launcher.accountNickname }}</p>
            <p class="text-sm text-white/45">{{ launcher.accountMeta.login }}</p>
            <p class="text-sm text-white/45">{{ launcher.accountMeta.email }}</p>
          </div>
        </div>

        <div class="mt-5 grid grid-cols-2 gap-3">
          <div class="rounded-[16px] border border-white/8 bg-white/[0.03] p-4 text-center">
            <p class="text-2xl font-bold">{{ fmt(launcher.walletBalance) }}</p>
            <p class="mt-1 text-[10px] uppercase tracking-[0.18em] text-white/35">Баланс</p>
          </div>
          <div class="rounded-[16px] border border-white/8 bg-white/[0.03] p-4 text-center">
            <p class="text-2xl font-bold">{{ launcher.security.activeRefreshSessions }}</p>
            <p class="mt-1 text-[10px] uppercase tracking-[0.18em] text-white/35">Активные сессии</p>
          </div>
        </div>

        <p class="mt-4 text-xs text-white/35">{{ launcher.emailVerifiedText }}</p>
      </section>

      <!-- Battle Pass -->
      <section class="rounded-[22px] border border-white/10 bg-white/[0.035] p-5">
        <div class="flex items-center justify-between">
          <p class="text-sm font-semibold">Battle Pass</p>
          <span
            v-if="launcher.bpProfile?.has_premium"
            class="rounded-full border border-amber-400/30 bg-amber-500/15 px-2.5 py-0.5 text-[11px] font-bold text-amber-300"
          >✦ Premium</span>
          <span v-else-if="launcher.bpProfile" class="rounded-full border border-white/10 bg-white/5 px-2.5 py-0.5 text-[11px] text-white/35">Бесплатный</span>
        </div>

        <div v-if="launcher.bpProfile" class="mt-4 space-y-3">
          <div class="grid grid-cols-2 gap-3">
            <div class="rounded-[16px] border border-white/8 bg-white/[0.03] p-4 text-center">
              <p class="text-2xl font-bold">{{ launcher.bpProfile.level }}</p>
              <p class="mt-1 text-[10px] uppercase tracking-[0.18em] text-white/35">Уровень / 120</p>
            </div>
            <div class="rounded-[16px] border border-white/8 bg-white/[0.03] p-4 text-center">
              <p class="text-2xl font-bold">{{ launcher.bpProfile.xp.toLocaleString('ru-RU') }}</p>
              <p class="mt-1 text-[10px] uppercase tracking-[0.18em] text-white/35">Опыт</p>
            </div>
          </div>

          <!-- Progress bar -->
          <div class="rounded-[16px] border border-white/8 bg-white/[0.03] px-4 py-3">
            <div class="mb-2 flex items-center justify-between text-[11px]">
              <span class="text-white/40">Сезон {{ launcher.bpProfile.season }}</span>
              <span class="text-white/30">{{ Math.round((launcher.bpProfile.level / 120) * 100) }}%</span>
            </div>
            <div class="h-1.5 overflow-hidden rounded-full bg-white/8">
              <div
                :style="{
                  width: Math.min(100, Math.round((launcher.bpProfile.level / 120) * 100)) + '%',
                  background: launcher.bpProfile.has_premium
                    ? 'linear-gradient(90deg, #f59e0b, #f97316)'
                    : 'linear-gradient(90deg, #6366f1, #8b5cf6)',
                  height: '100%',
                  borderRadius: '999px',
                  transition: 'width 0.5s ease',
                }"
              ></div>
            </div>
          </div>

          <div
            v-if="launcher.bpProfile.has_premium && launcher.bpProfile.premium_expires_at"
            class="flex items-center justify-between rounded-[16px] border border-amber-400/15 bg-amber-500/8 px-4 py-3 text-xs"
          >
            <span class="text-amber-400/70">Premium активен до</span>
            <span class="font-semibold text-amber-300">{{ fmtDate(launcher.bpProfile.premium_expires_at) }}</span>
          </div>
        </div>

        <p v-else class="mt-3 text-center text-xs text-white/25">Зайди на сервер чтобы появился прогресс</p>
      </section>

      <!-- Diagnostics -->
      <section class="rounded-[22px] border border-white/10 bg-white/[0.035] p-5">
        <div class="mb-3 flex items-center justify-between">
          <p class="text-sm font-semibold text-white/60">Диагностика</p>
          <button
            class="rounded-xl border border-white/10 bg-white/5 px-3 py-1.5 text-xs text-white/50 transition hover:bg-white/8 hover:text-white"
            @click="launcher.clearDiagnostics()"
          >Очистить</button>
        </div>
        <pre class="max-h-[200px] overflow-auto whitespace-pre-wrap break-all rounded-[16px] border border-white/8 bg-[#060912] p-4 text-xs leading-5 text-white/55">{{ diagnosticsText }}</pre>
      </section>
    </template>

    <!-- ── SKIN ─────────────────────────────────────────────── -->
    <template v-else-if="activeTab === 'skin'">
      <section class="rounded-[22px] border border-white/10 bg-white/[0.035] p-5">
        <div class="flex items-center justify-between gap-3">
          <div>
            <p class="text-sm font-semibold">Игровой скин</p>
            <p class="mt-0.5 text-xs text-white/40">Загрузи PNG 64×64. Сервер подхватит автоматически.</p>
          </div>
          <p class="shrink-0 text-xs text-white/30">{{ fmtDate(launcher.skin.updatedAt) }}</p>
        </div>

        <div class="mt-4 grid grid-cols-2 gap-4">
          <!-- Previews -->
          <div class="space-y-3">
            <div class="flex h-[130px] items-center justify-center rounded-[16px] border border-white/8 bg-[#060b14]">
              <img v-if="launcher.skin.headPreviewUrl" :src="launcher.skin.headPreviewUrl" alt="head" class="max-h-[110px] max-w-[110px] object-contain pixelated"/>
              <span v-else class="text-xs text-white/25">Скин не загружен</span>
            </div>
            <div class="flex h-[160px] items-center justify-center rounded-[16px] border border-white/8 bg-[#060b14]">
              <img
                :src="localPreviewUrl || launcher.skin.bodyPreviewUrl || ''"
                v-if="localPreviewUrl || launcher.skin.bodyPreviewUrl"
                alt="body" class="max-h-[140px] object-contain pixelated"
              />
              <span v-else class="text-xs text-white/25">Выберите PNG</span>
            </div>
          </div>

          <!-- Upload form -->
          <div class="space-y-3">
            <div class="rounded-[16px] border border-white/8 bg-white/[0.03] p-3">
              <p class="mb-2 text-[10px] uppercase tracking-[0.15em] text-white/35">Файл</p>
              <input type="file" accept=".png,image/png" class="w-full text-xs text-white/60" @change="onFileChange"/>
            </div>

            <div class="rounded-[16px] border border-white/8 bg-white/[0.03] p-3">
              <p class="mb-2 text-[10px] uppercase tracking-[0.15em] text-white/35">Модель</p>
              <select v-model="selectedVariant" class="w-full rounded-xl border border-white/10 bg-[#0a1020] px-3 py-2 text-sm text-white">
                <option value="classic">Classic</option>
                <option value="slim">Slim</option>
              </select>
            </div>

            <div class="flex flex-col gap-2">
              <button
                class="rounded-[12px] border border-emerald-400/20 bg-emerald-400/10 px-4 py-2.5 text-sm font-semibold text-emerald-200 transition hover:bg-emerald-400/15 disabled:opacity-50"
                :disabled="uploadBusy || !selectedFile"
                @click="saveSkin"
              >{{ uploadBusy ? 'Сохраняем...' : 'Сохранить скин' }}</button>

              <button
                class="rounded-[12px] border border-white/10 bg-white/5 px-4 py-2 text-sm text-white/55 transition hover:bg-white/8 hover:text-white disabled:opacity-50"
                :disabled="uploadBusy"
                @click="launcher.refreshSkin()"
              >Обновить</button>

              <button
                class="rounded-[12px] border border-red-400/15 bg-red-400/8 px-4 py-2 text-sm text-red-300 transition hover:bg-red-400/15 disabled:opacity-50"
                :disabled="uploadBusy || !launcher.skin.hasSkin"
                @click="removeSkin"
              >Удалить скин</button>
            </div>

            <div class="rounded-[16px] border border-white/8 bg-white/[0.03] p-3 text-xs">
              <div class="grid grid-cols-2 gap-2 text-white/40">
                <div>
                  <p class="text-[9px] uppercase tracking-wider text-white/25">Статус</p>
                  <p class="mt-1">{{ launcher.skin.hasSkin ? 'Активен' : 'Не загружен' }}</p>
                </div>
                <div>
                  <p class="text-[9px] uppercase tracking-wider text-white/25">Размер</p>
                  <p class="mt-1">{{ launcher.skin.width || 0 }}×{{ launcher.skin.height || 0 }}</p>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>
    </template>

    <!-- ── SECURITY ──────────────────────────────────────────── -->
    <template v-else-if="activeTab === 'security'">
      <section class="rounded-[22px] border border-white/10 bg-white/[0.035] p-5">
        <p class="text-sm font-semibold">Защита аккаунта</p>

        <div class="mt-4 grid grid-cols-2 gap-3">
          <div class="rounded-[16px] border border-white/8 bg-white/[0.03] p-4">
            <p class="text-xs font-semibold text-white/60">Режим входа</p>
            <p class="mt-2 text-sm text-white/75">
              {{ launcher.security.mustUseLauncher ? 'Только через лаунчер' : 'Лаунчер и legacy-вход' }}
            </p>
          </div>
          <div class="rounded-[16px] border border-white/8 bg-white/[0.03] p-4">
            <p class="text-xs font-semibold text-white/60">Legacy</p>
            <p class="mt-2 text-sm text-white/75">
              <span v-if="launcher.security.legacyReady">Настроена и готова</span>
              <span v-else-if="launcher.security.legacyHashPresent">Хэш есть, настройка не завершена</span>
              <span v-else>Не настроена</span>
            </p>
          </div>
        </div>

        <div class="mt-5 flex flex-wrap gap-2.5">
          <button
            class="rounded-[12px] border border-white/10 bg-white/5 px-4 py-2.5 text-sm text-white/60 transition hover:bg-white/8 hover:text-white disabled:opacity-50"
            :disabled="securityBusy"
            @click="launcher.openExternal(launcher.links.verifyEmailUrl)"
          >Подтвердить почту</button>

          <button
            class="rounded-[12px] border border-white/10 bg-white/5 px-4 py-2.5 text-sm text-white/60 transition hover:bg-white/8 hover:text-white disabled:opacity-50"
            :disabled="securityBusy"
            @click="launcher.openExternal(launcher.links.forgotPasswordUrl)"
          >Сменить пароль</button>

          <button
            class="rounded-[12px] border border-amber-400/15 bg-amber-400/10 px-4 py-2.5 text-sm text-amber-200 transition hover:bg-amber-400/15 disabled:opacity-50"
            :disabled="securityBusy"
            @click="revokeOtherSessions"
          >{{ securityBusy ? 'Завершаем...' : 'Завершить другие сессии' }}</button>

          <button
            class="rounded-[12px] border border-red-400/15 bg-red-400/8 px-4 py-2.5 text-sm text-red-300 transition hover:bg-red-400/15"
            @click="launcher.logout()"
          >Выйти из аккаунта</button>
        </div>
      </section>
    </template>

  </div>
</template>

<style scoped>
.pixelated { image-rendering: pixelated; }
</style>
