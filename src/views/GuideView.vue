<script setup lang="ts">
const limits = [
  { label: 'Приватных чанков', value: '100', hint: '1 чанк = 16×16 блоков' },
  { label: 'Force-load чанков', value: '25', hint: 'грузятся без игроков' },
  { label: 'Домов (/sethome)', value: '2', hint: 'на аккаунт' },
]

const regionCommands = [
  { cmd: 'Миникарта', desc: 'В углу экрана — карта FTB Chunks с сеткой чанков' },
  { cmd: 'Клавиша карты', desc: 'Открыть большую карту (забинди в «Управление» → FTB Chunks)' },
  { cmd: 'ЛКМ по чанку', desc: 'Заклеймить чанк — приват, защита от чужих' },
  { cmd: 'ПКМ по чанку', desc: 'Снять клейм с чанка' },
  { cmd: 'Shift + ЛКМ', desc: 'Force-load: чанк работает, даже когда тебя нет рядом' },
  { cmd: '/ftbteams party create <имя>', desc: 'Создать команду — общий доступ к приватам' },
  { cmd: '/ftbteams party invite <игрок>', desc: 'Пригласить игрока в свою команду' },
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
  { epoch: 'Магия',       item: 'Заклинания, мана и урон',        mod: 'Puffish Skills', color: '#a78bfa' },
  { epoch: 'Ближний бой', item: 'Урон и выживаемость',            mod: 'Puffish Skills', color: '#f87171' },
  { epoch: 'Дальний бой', item: 'Луки, арбалеты, точность',       mod: 'Puffish Skills', color: '#34d399' },
  { epoch: 'Атлетика',    item: 'Скорость, прыжок, выносливость', mod: 'Puffish Skills', color: '#38bdf8' },
  { epoch: 'Добыча',      item: 'Копание и бонусы к руде',        mod: 'Puffish Skills', color: '#fbbf24' },
  { epoch: 'Защита',      item: 'Броня, сопротивление, HP',       mod: 'Puffish Skills', color: '#fb923c' },
]

const modCategories = [
  {
    name: 'Технологии',
    mods: [
      { name: 'Create', info: 'Шестерни, пресс, миксер, deployer, конвейер. Основа прогрессии.' },
      { name: 'Immersive Engineering', info: 'Сталь через коксовую и доменную печи. Единственный источник стали.' },
      { name: 'Mekanism', info: '2–5× обогащение руды, цифровой шахтёр, телепортер, генераторы.' },
      { name: 'Applied Energistics 2', info: 'ME-сеть: централизованное хранение и автокрафт через процессоры.' },
      { name: 'Industrial Foregoing', info: 'Фермы растений и мобов, лазерный бур, пластиковая цепочка.' },
    ],
  },
  {
    name: 'Навыки и RPG',
    mods: [
      { name: 'Puffish Skills', info: 'Дерево навыков: Магия, Ближний/Дальний бой, Атлетика, Добыча, Защита.' },
      { name: 'Puffish Attributes', info: 'Расширенные атрибуты под навыки и снаряжение.' },
      { name: 'Сигилы сброса', info: 'Обнуляют выбранную ветку навыков — в магазине и Battle Pass.' },
    ],
  },
  {
    name: 'Боссы и эндгейм',
    mods: [
      { name: "L_Ender's Cataclysm", info: 'Игнис, Левиафан, Сцилла, Харбингер — боссы с уникальным дропом.' },
      { name: 'Draconic Evolution', info: 'Дракониевые ядра, крафт слияния, реактор, огромная энергия.' },
      { name: 'Эволюция (FTB Evolution)', info: 'Эксклюзив: сингулярность, арканум, трансцендентство — вершина.' },
    ],
  },
  {
    name: 'Комфорт',
    mods: [
      { name: 'Sophisticated Backpacks', info: 'Рюкзаки с апгрейдами: авто-подбор, сортировка, хранение.' },
      { name: "Farmer's Delight", info: 'Готовка, блюда и урожай — стабильная еда на старте.' },
      { name: 'Supplementaries', info: 'Верёвки, флаги, фонари, доски объявлений, декор.' },
      { name: 'Waystones', info: 'Камни путешественника — телепортация между точками. Бесплатная на спавн.' },
    ],
  },
]

const progressionRoute = [
  'Выживание + Farmer\'s Delight',
  'Create / первая механика',
  'Immersive Engineering / сталь',
  'Mekanism / энергия',
  'AE2 / хранение и автокрафт',
  'Industrial Foregoing / фермы',
  "L_Ender's Cataclysm / боссы",
  'Draconic Evolution',
  'Эволюция / эндгейм',
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
          <p class="text-xs font-semibold text-white/80">Приваты (FTB Chunks)</p>
        </div>
        <p class="mb-3 text-[11px] leading-5 text-white/40">
          Открой карту FTB Chunks и кликай по клеткам-чанкам. Союзники добавляются через команды FTB Teams.
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
      <p class="mb-1 text-[10px] uppercase tracking-[0.22em] text-white/35">Навыки персонажа (Puffish Skills)</p>
      <p class="mb-4 text-[11px] text-white/40">6 веток навыков — вкладывай очки под свой стиль. Дерево навыков открывается клавишей (по умолчанию K). Сброс ветки — Сигилом из магазина или Battle Pass.</p>
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
