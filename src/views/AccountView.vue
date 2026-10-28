<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from 'vue'
import { BACKEND_BASE, useLauncherStore } from '../stores/launcher'
import ItemIcon from '../components/game/ItemIcon.vue'

const launcher = useLauncherStore()

const activeServer = computed(() =>
  launcher.serverList.find((s) => s.slug === launcher.selectedSlug) ?? null,
)
// Battle pass показываем, только если он не выключен у активного сервера.
const bpEnabled = computed(() => {
  const f = activeServer.value?.features
  return !f || f.battlepass !== false
})
// The season length comes with the profile (the current one has 500 levels); 120 was the old fixed value.
const bpMaxLevel = computed(() => launcher.bpProfile?.max_level || 120)
const bpPercent = computed(() =>
  Math.min(100, Math.round(((launcher.bpProfile?.level ?? 0) / bpMaxLevel.value) * 100)))
// Own head: the uploaded skin's render, else the public endpoint (default skin for players without one).
const headUrl = computed(() => launcher.skin.hasSkin && launcher.skin.headPreviewUrl
  ? launcher.skin.headPreviewUrl
  : `${BACKEND_BASE}/public/player-head/${encodeURIComponent(launcher.accountNickname || 'Steve')}`)
const emailVerified = computed(() => !/не подтвержд/i.test(launcher.emailVerifiedText || ''))

type Tab = 'profile' | 'skin' | 'security'
const activeTab = ref<Tab>('profile')

const diagnosticsText = computed(() => launcher.diagnosticsText?.trim() || 'Диагностика пока пуста.')
const selectedVariant = ref<'classic' | 'slim'>('classic')
const selectedFile = ref<File | null>(null)
const localPreviewUrl = ref('')
const uploadBusy = ref(false)
const securityBusy = ref(false)

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
          <div class="mc-slot h-[68px] w-[68px] shrink-0 overflow-hidden">
            <img :src="headUrl" alt="" class="item-icon h-12 w-12 rounded-[6px]" draggable="false" />
          </div>
          <div class="min-w-0 flex-1">
            <p class="font-display truncate text-[24px] font-bold leading-tight text-white">{{ launcher.accountNickname }}</p>
            <p class="mt-1 truncate text-sm text-white/45">
              {{ launcher.accountMeta.login }}<template v-if="launcher.accountMeta.email"> · {{ launcher.accountMeta.email }}</template>
            </p>
          </div>
        </div>
        <div class="mt-4 flex flex-wrap gap-2 text-[12px]">
          <span
            class="rounded-full px-3 py-1"
            :class="emailVerified ? 'bg-emerald-500/12 text-emerald-300' : 'bg-amber-500/12 text-amber-300'"
          >{{ launcher.emailVerifiedText || (emailVerified ? 'Почта подтверждена' : 'Почта не подтверждена') }}</span>
          <button
            type="button"
            class="rounded-full bg-white/5 px-3 py-1 text-white/60 transition hover:bg-white/10 hover:text-white"
            @click="activeTab = 'security'"
          >Активных входов: {{ launcher.security.activeRefreshSessions }}</button>
        </div>
      </section>

      <!-- Battle Pass -->
      <section v-if="bpEnabled" class="rounded-[22px] border border-white/10 bg-white/[0.035] p-5">
        <div class="flex items-center justify-between gap-3">
          <div class="min-w-0">
            <p class="font-display text-[18px] font-semibold text-white">Боевой пропуск</p>
            <p v-if="launcher.bpProfile" class="mt-0.5 truncate text-[13px] text-white/45">
              {{ launcher.bpProfile.season_name || `Сезон ${launcher.bpProfile.season ?? ''}` }}
            </p>
          </div>
          <span
            v-if="launcher.bpProfile?.has_premium"
            class="shrink-0 rounded-full bg-amber-400/15 px-3 py-1 text-[12px] font-semibold text-amber-300"
          >Премиум</span>
          <span v-else-if="launcher.bpProfile" class="shrink-0 rounded-full bg-white/5 px-3 py-1 text-[12px] text-white/45">Бесплатный</span>
        </div>

        <div v-if="launcher.bpProfile" class="mt-4">
          <div class="flex items-end gap-4">
            <div class="mc-slot h-[60px] w-[60px] shrink-0"><ItemIcon name="experience_bottle" :size="40" /></div>
            <div class="min-w-0 flex-1">
              <div class="mb-2 flex items-baseline justify-between gap-3">
                <p>
                  <span class="font-display text-[30px] font-bold leading-none text-white">{{ launcher.bpProfile.level }}</span>
                  <span class="ml-1.5 text-sm text-white/45">уровень из {{ bpMaxLevel }}</span>
                </p>
                <span class="tabular-nums text-[13px] text-white/40">{{ launcher.bpProfile.xp.toLocaleString('ru-RU') }} опыта</span>
              </div>
              <div class="xp-bar" :class="{ 'xp-bar--gold': launcher.bpProfile.has_premium }">
                <div class="xp-bar__fill" :style="{ width: bpPercent + '%' }"></div>
              </div>
            </div>
          </div>
          <p
            v-if="launcher.bpProfile.has_premium && launcher.bpProfile.premium_expires_at"
            class="mt-3 text-[13px] text-amber-300/80"
          >Премиум активен до {{ fmtDate(launcher.bpProfile.premium_expires_at) }}</p>
        </div>

        <p v-else class="mt-3 text-[13px] text-white/40">Зайди на сервер — здесь появится твой уровень и опыт.</p>
      </section>

      <!-- Diagnostics: for support, folded away -->
      <details class="group rounded-[22px] border border-white/10 bg-white/[0.035] p-5">
        <summary class="flex cursor-pointer list-none items-center justify-between gap-3">
          <span>
            <span class="block text-sm font-semibold text-white/75">Для поддержки</span>
            <span class="block text-[12px] text-white/40">Технический журнал лаунчера — пригодится, если что-то не работает</span>
          </span>
          <span class="text-xs text-white/40 transition group-open:rotate-180">▾</span>
        </summary>
        <div class="mt-4 flex justify-end">
          <button
            class="rounded-xl border border-white/10 bg-white/5 px-3 py-1.5 text-xs text-white/50 transition hover:bg-white/8 hover:text-white"
            @click="launcher.clearDiagnostics()"
          >Очистить журнал</button>
        </div>
        <pre class="mt-2 max-h-[220px] overflow-auto whitespace-pre-wrap break-all rounded-[16px] border border-white/8 bg-black/30 p-4 text-xs leading-5 text-white/55">{{ diagnosticsText }}</pre>
      </details>
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
