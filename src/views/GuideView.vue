<script setup lang="ts">
const limits = [
  { label: 'Макс. объём привата', value: '20 000 000', hint: '≈ 200×200×500 блоков' },
  { label: 'Приватов на игрока', value: '16', hint: 'по умолчанию' },
  { label: 'Домов (/sethome)', value: '2', hint: 'на аккаунт' },
]

const regionCommands = [
  { cmd: '//wand', desc: 'Палочка выделения (деревянный топор)' },
  { cmd: 'ЛКМ по блоку', desc: 'Первый угол выделения' },
  { cmd: 'ПКМ по блоку', desc: 'Второй угол выделения' },
  { cmd: '/rg claim <название>', desc: 'Создать привата в выделённой области' },
  { cmd: '/rg info', desc: 'Информация о привате под ногами' },
  { cmd: '/rg list', desc: 'Список своих приватов' },
  { cmd: '/rg addmember <привата> <игрок>', desc: 'Добавить участника' },
  { cmd: '/rg removemember <привата> <игрок>', desc: 'Убрать участника' },
  { cmd: '/rg flag <привата> <флаг> <значение>', desc: 'Настроить флаг (allow / deny / none)' },
  { cmd: '/rg remove <название>', desc: 'Удалить привата' },
]

const tpCommands = [
  { cmd: '/sethome <название>', desc: 'Поставить точку дома (лимит: 2)' },
  { cmd: '/home <название>', desc: 'Телепортироваться домой' },
  { cmd: '/homes', desc: 'Список всех своих домов' },
  { cmd: '/delhome <название>', desc: 'Удалить точку дома' },
  { cmd: '/spawn', desc: 'Телепортироваться на спавн' },
  { cmd: '/tpa <игрок>', desc: 'Запрос телепортации к игроку' },
  { cmd: '/tpahere <игрок>', desc: 'Позвать игрока к себе' },
  { cmd: '/tpaccept', desc: 'Принять запрос телепортации' },
  { cmd: '/tpdeny', desc: 'Отклонить запрос' },
]

const nationMemberCommands = [
  { cmd: '/nationtreasury', desc: 'Баланс казны, территория и престиж государства (алиас: /ntreasury)' },
  { cmd: '/nationtreasuryhistory', desc: 'Последние 5 операций с казной (алиас: /ntreasuryhistory)' },
  { cmd: '/nationdonate <сумма> [комментарий]', desc: 'Задонатить деньги в казну своего государства (алиас: /ndonate)' },
  { cmd: '/marketprice [предмет]', desc: 'Рыночная цена предмета в руке или по названию (алиас: /mprice, /price)' },
  { cmd: '/nmarket', desc: 'Открыть рынок государств в GUI (алиас: /nm, /nationmarket)' },
]

const nationOfficerCommands = [
  { cmd: '/nationwithdraw <сумма> [комментарий]', desc: 'Снять деньги из казны на свой баланс (алиас: /nwithdraw)' },
  { cmd: '/nmarket sell <кол-во|all> <цена>', desc: 'Выставить предмет из руки на рынок своего государства' },
  { cmd: '/nmarket listings', desc: 'Список активных лотов своего государства' },
  { cmd: '/nmarket cancel <id>', desc: 'Снять лот с рынка и вернуть предметы' },
  { cmd: '/nmarket confirm', desc: 'Подтвердить выставление лота с нестандартной ценой' },
  { cmd: '/nsetcapital', desc: 'Установить столицу в текущей позиции — только для главы государства' },
  { cmd: 'Сайт → Студия → Участники', desc: 'Выдать звание участнику: офицер — рядовым, глава — всем. Отображается в чате.', web: true },
]

const tierGates = [
  { epoch: 'Эпоха механизмов', item: 'Андезитовая машинная рама', mod: 'Create', color: '#f59e0b' },
  { epoch: 'Эпоха стали',      item: 'Стальной корпус',           mod: 'Mekanism', color: '#38bdf8' },
  { epoch: 'Эпоха автоматизации', item: 'МЭ-контроллер',          mod: 'AE2', color: '#a78bfa' },
  { epoch: 'Квантовая эпоха',  item: 'Квантовая схема',           mod: 'AE2 + Mekanism', color: '#22d3ee' },
  { epoch: 'Эпоха реакторов',  item: 'Ядро реактора',             mod: 'Extreme Reactors 2', color: '#fb923c' },
  { epoch: 'Эпоха дракона',    item: 'Дракониевое ядро',          mod: 'Draconic Evolution', color: '#f472b6' },
  { epoch: 'Эндгейм',          item: 'Катализатор бесконечности', mod: 'Avaritia', color: '#4ade80' },
]

const modCategories = [
  {
    name: 'Технологии',
    mods: [
      { name: 'Create', info: 'Шестерни, пресс, миксер, deployer, конвейер. Основа прогрессии.' },
      { name: 'Immersive Engineering', info: 'Сталь через коксовую и доменную печи. Единственный источник стали.' },
      { name: 'Mekanism', info: '4–5× обогащение руды, цифровой шахтёр, телепортер, генераторы.' },
      { name: 'Applied Energistics 2', info: 'ME-сеть: централизованное хранение и автокрафт через процессоры.' },
      { name: 'Industrial Foregoing', info: 'Фермы растений и мобов, лазерный бур, пластиковая цепочка.' },
      { name: 'Extreme Reactors 2', info: 'Многоблочный ядерный реактор и турбина — энергия для финала.' },
    ],
  },
  {
    name: 'Магия',
    mods: [
      { name: 'Mahou Tsukai', info: 'Знаки (махоудзин), бабочка-фамильяр, телепортация, щит. Нужна для рецептов.' },
      { name: 'Aether', info: 'Небесное измерение. Данжи, небесный янтарь и нефрит для техно-магических крафтов.' },
    ],
  },
  {
    name: 'Боссы и измерения',
    mods: [
      { name: "L_Ender's Cataclysm", info: 'Игнис, Левиафан, Сцилла, Харбингер — боссы с уникальным дропом для финала.' },
      { name: 'Twilight Forest', info: 'Параллельное измерение с цепочкой боссов. Тёмное ядро и поздние ворота.' },
      { name: 'Deeper Darker', info: 'Расширение Древних Городов: новые боссы, блоки Sculk и поздние печати.' },
      { name: 'Draconic Evolution', info: 'Драконьи и хаотические ядра, реактор, броня — предпоследний уровень.' },
      { name: 'Avaritia', info: 'Финал: Экстремальный верстак, слиток бесконечности, катализатор.' },
    ],
  },
  {
    name: 'Комфорт',
    mods: [
      { name: 'Sophisticated Backpacks', info: 'Рюкзаки с апгрейдами: авто-подбор, сортировка, компактное хранение.' },
      { name: "Tom's Simple Storage", info: 'Простая сеть хранения без сложной настройки — используй до AE2.' },
      { name: 'Supplementaries', info: 'Верёвки, флаги, пушки, фонари. Пушки не ломают блоки в приватах.' },
      { name: 'Waystones', info: 'Камни путешественника — телепортация между точками. Бесплатная на спавн.' },
      { name: 'Carry On', info: 'Поднять блок-сущность (сундук, хопер) пустой рукой + Shift. Блокируется в приватах.' },
    ],
  },
]

const progressionRoute = [
  'Ваниль+ / выживание',
  "Farmer's Delight / еда",
  'Create / первая механика',
  'Aether + Mahou Tsukai / магия',
  'Immersive Engineering / сталь',
  'Twilight Forest + Deeper Darker',
  'Mekanism / промышленность',
  'AE2 / хранение и автокрафт',
  'Industrial Foregoing / автоматизация',
  "L_Ender's Cataclysm / боссы",
  'Draconic Evolution',
  'Avaritia / финал',
]
</script>

<template>
  <div class="space-y-4">

    <!-- Header -->
    <div>
      <p class="text-[10px] uppercase tracking-[0.25em] text-violet-300/70">Справочник</p>
      <h2 class="mt-1 text-xl font-semibold">Гайд по серверу</h2>
      <p class="mt-1 text-xs text-white/50">Команды, приваты, сетхом, обзор модов и маршрут прогрессии.</p>
    </div>

    <!-- Limits -->
    <div class="grid grid-cols-3 gap-2">
      <div
        v-for="lim in limits"
        :key="lim.label"
        class="rounded-[16px] border border-white/10 bg-white/[0.035] p-3"
      >
        <p class="text-[10px] uppercase tracking-[0.18em] text-white/35">{{ lim.label }}</p>
        <p class="mt-1.5 text-lg font-bold text-white">{{ lim.value }}</p>
        <p class="mt-0.5 text-[11px] text-white/40">{{ lim.hint }}</p>
      </div>
    </div>

    <!-- Commands: 2 columns -->
    <div class="grid grid-cols-2 gap-3">

      <!-- Region commands -->
      <div class="rounded-[18px] border border-white/10 bg-white/[0.035] p-4">
        <div class="mb-3 flex items-center gap-2">
          <span class="h-2 w-2 rounded-full bg-violet-400"></span>
          <p class="text-xs font-semibold text-white/80">Приваты (WorldGuard)</p>
        </div>
        <p class="mb-3 text-[11px] leading-5 text-white/40">
          Выдели область деревянным топором (//wand), затем создай привата командой /rg claim.
        </p>
        <div class="space-y-1.5">
          <div
            v-for="row in regionCommands"
            :key="row.cmd"
            class="flex flex-wrap items-start gap-2 rounded-xl bg-white/[0.03] px-2.5 py-1.5"
          >
            <code class="shrink-0 rounded-md border border-emerald-400/15 bg-emerald-400/8 px-1.5 py-0.5 font-mono text-[11px] font-semibold text-emerald-300">{{ row.cmd }}</code>
            <span class="pt-0.5 text-[11px] leading-4 text-white/50">{{ row.desc }}</span>
          </div>
        </div>
      </div>

      <!-- TP / home commands -->
      <div class="rounded-[18px] border border-white/10 bg-white/[0.035] p-4">
        <div class="mb-3 flex items-center gap-2">
          <span class="h-2 w-2 rounded-full bg-emerald-400"></span>
          <p class="text-xs font-semibold text-white/80">Дома и телепортация</p>
        </div>
        <p class="mb-3 text-[11px] leading-5 text-white/40">
          Лимит домов — 2 на аккаунт. Команда /back на сервере отключена.
        </p>
        <div class="space-y-1.5">
          <div
            v-for="row in tpCommands"
            :key="row.cmd"
            class="flex flex-wrap items-start gap-2 rounded-xl bg-white/[0.03] px-2.5 py-1.5"
          >
            <code class="shrink-0 rounded-md border border-emerald-400/15 bg-emerald-400/8 px-1.5 py-0.5 font-mono text-[11px] font-semibold text-emerald-300">{{ row.cmd }}</code>
            <span class="pt-0.5 text-[11px] leading-4 text-white/50">{{ row.desc }}</span>
          </div>
        </div>
      </div>

    </div>

    <!-- Nation commands: 2 columns -->
    <div class="grid grid-cols-2 gap-3">

      <!-- Nation member commands -->
      <div class="rounded-[18px] border border-white/10 bg-white/[0.035] p-4">
        <div class="mb-3 flex items-center gap-2">
          <span class="h-2 w-2 rounded-full bg-amber-400"></span>
          <p class="text-xs font-semibold text-white/80">Государство — казна и рынок <span class="text-white/35">(все участники)</span></p>
        </div>
        <p class="mb-3 text-[11px] leading-5 text-white/40">
          Доступны всем игрокам, состоящим в государстве.
        </p>
        <div class="space-y-1.5">
          <div
            v-for="row in nationMemberCommands"
            :key="row.cmd"
            class="flex flex-wrap items-start gap-2 rounded-xl bg-white/[0.03] px-2.5 py-1.5"
          >
            <code class="shrink-0 rounded-md border border-amber-400/15 bg-amber-400/8 px-1.5 py-0.5 font-mono text-[11px] font-semibold text-amber-300">{{ row.cmd }}</code>
            <span class="pt-0.5 text-[11px] leading-4 text-white/50">{{ row.desc }}</span>
          </div>
        </div>
      </div>

      <!-- Nation officer/leader commands -->
      <div class="rounded-[18px] border border-white/10 bg-white/[0.035] p-4">
        <div class="mb-3 flex items-center gap-2">
          <span class="h-2 w-2 rounded-full bg-red-400"></span>
          <p class="text-xs font-semibold text-white/80">Государство — управление <span class="text-white/35">(офицеры и глава)</span></p>
        </div>
        <p class="mb-3 text-[11px] leading-5 text-white/40">
          Снятие из казны и управление лотами. /nsetcapital — только для главы.
        </p>
        <div class="space-y-1.5">
          <div
            v-for="row in nationOfficerCommands"
            :key="row.cmd"
            class="flex flex-wrap items-start gap-2 rounded-xl bg-white/[0.03] px-2.5 py-1.5"
          >
            <code
              class="shrink-0 rounded-md px-1.5 py-0.5 font-mono text-[11px] font-semibold"
              :class="row.web
                ? 'border border-violet-400/20 bg-violet-400/8 text-violet-300'
                : 'border border-red-400/15 bg-red-400/8 text-red-300'"
            >{{ row.cmd }}</code>
            <span class="pt-0.5 text-[11px] leading-4 text-white/50">{{ row.desc }}</span>
          </div>
        </div>
      </div>

    </div>

    <!-- Tier gates -->
    <div class="rounded-[18px] border border-white/10 bg-white/[0.035] p-4">
      <p class="mb-1 text-[10px] uppercase tracking-[0.22em] text-white/35">Эпохи прогрессии — что нужно скрафтить</p>
      <p class="mb-4 text-[11px] text-white/40">При первом крафте предмета сервер фиксирует эпоху и сообщает всем. Каждая открывается один раз.</p>
      <div class="grid grid-cols-4 gap-2">
        <div
          v-for="gate in tierGates"
          :key="gate.epoch"
          class="rounded-[14px] border border-white/[0.07] bg-white/[0.025] p-3"
        >
          <div class="mb-1.5 flex items-center gap-1.5">
            <span class="h-1.5 w-1.5 shrink-0 rounded-full" :style="{ background: gate.color }"></span>
            <span class="text-[9px] font-bold uppercase tracking-[0.12em]" :style="{ color: gate.color }">{{ gate.epoch }}</span>
          </div>
          <p class="text-[11px] font-semibold leading-4 text-white/85">{{ gate.item }}</p>
          <p class="mt-0.5 text-[10px] text-white/35">{{ gate.mod }}</p>
        </div>
      </div>
    </div>

    <!-- Mods reference -->
    <div class="rounded-[18px] border border-white/10 bg-white/[0.035] p-4">
      <p class="mb-4 text-[10px] uppercase tracking-[0.22em] text-white/35">Справочник по модам</p>
      <div class="grid grid-cols-2 gap-3">
        <div v-for="cat in modCategories" :key="cat.name">
          <p class="mb-2 text-[10px] uppercase tracking-[0.18em] text-violet-300/60">{{ cat.name }}</p>
          <div class="space-y-1.5">
            <div
              v-for="mod in cat.mods"
              :key="mod.name"
              class="rounded-xl border border-white/[0.06] bg-white/[0.025] px-3 py-2"
            >
              <p class="text-[12px] font-semibold text-white/85">{{ mod.name }}</p>
              <p class="mt-0.5 text-[11px] leading-4 text-white/45">{{ mod.info }}</p>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Progression route -->
    <div class="rounded-[18px] border border-white/10 bg-white/[0.035] p-4">
      <p class="mb-3 text-[10px] uppercase tracking-[0.22em] text-white/35">Маршрут прогрессии</p>
      <div class="grid grid-cols-3 gap-1.5">
        <div
          v-for="(step, i) in progressionRoute"
          :key="step"
          class="flex items-center gap-2 rounded-xl border border-white/[0.06] bg-white/[0.025] px-3 py-2"
        >
          <span class="w-5 shrink-0 text-center text-[11px] font-black text-white/20">{{ String(i + 1).padStart(2, '0') }}</span>
          <span class="text-[11px] leading-4 text-white/65">{{ step }}</span>
        </div>
      </div>
    </div>

  </div>
</template>
