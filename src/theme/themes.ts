// Visual themes for the launcher. Each theme is just a set of the CSS custom
// properties the whole UI already runs on (--acc* drive every accent, --bg*/
// --panel*/--hairline drive the glass background), so switching a theme is a
// pure variable swap — no per-view changes. Applied to :root in App.vue.
//
// The "server" theme keeps the existing behaviour: its accent follows the active
// server's accent_color (per-server branding); the others are fixed palettes.

export interface LauncherTheme {
  id: string
  name: string
  /** Two-stop gradient shown in the picker swatch. */
  swatch: [string, string]
  /** Light vs dark surface mode — drives the [data-mode] overrides in main.css. */
  mode?: 'light' | 'dark'
  /** When true, --acc* come from the active server accent (set in App.vue). */
  followsServerAccent?: boolean
  /** CSS custom properties applied to :root for this theme. */
  vars: Record<string, string>
}

export const STORAGE_KEY = 'voidrp_launcher_theme_v1'
export const DEFAULT_THEME_ID = 'server'

const HAIRLINE = 'rgba(255, 255, 255, 0.08)'

function rgb(hex: string): string {
  const m = /^#?([0-9a-f]{6})$/i.exec(hex)
  if (!m) return '139, 92, 246'
  const n = parseInt(m[1], 16)
  return `${(n >> 16) & 255}, ${(n >> 8) & 255}, ${n & 255}`
}

/** Build the --acc* family from four hex stops. */
function accent(acc: string, acc2: string, soft: string, pale: string): Record<string, string> {
  return {
    '--acc': acc,
    '--acc-2': acc2,
    '--acc-soft': soft,
    '--acc-pale': pale,
    '--acc-rgb': rgb(acc),
    '--acc-2-rgb': rgb(acc2),
    '--acc-soft-rgb': rgb(soft),
  }
}

/** Build the background/panel family for a theme's dark base. */
function surface(bg0: string, bg1: string, panel: string, panelStrong: string): Record<string, string> {
  return {
    '--bg-0': bg0,
    '--bg-1': bg1,
    '--panel': panel,
    '--panel-strong': panelStrong,
    '--hairline': HAIRLINE,
  }
}

export const LAUNCHER_THEMES: LauncherTheme[] = [
  {
    id: 'server',
    name: 'По серверу',
    swatch: ['#8b5cf6', '#6d55e8'],
    followsServerAccent: true,
    // Accent injected from the server; only the (default deep-dark) surface here.
    vars: surface('#030509', '#060a15', 'rgba(10, 14, 27, 0.62)', 'rgba(9, 13, 25, 0.86)'),
  },
  {
    id: 'abyss',
    name: 'Абисс',
    swatch: ['#22d3ee', '#0e7490'],
    vars: {
      ...accent('#22d3ee', '#0e7490', '#7ee7f5', '#b8f1fa'),
      ...surface('#030a0e', '#05121a', 'rgba(8, 20, 28, 0.62)', 'rgba(7, 18, 26, 0.86)'),
    },
  },
  {
    id: 'magma',
    name: 'Магма',
    swatch: ['#f97316', '#b91c1c'],
    vars: {
      ...accent('#f97316', '#b91c1c', '#fbaf6b', '#fdd7b3'),
      ...surface('#0a0603', '#140b06', 'rgba(26, 16, 10, 0.62)', 'rgba(22, 13, 8, 0.86)'),
    },
  },
  {
    id: 'emerald',
    name: 'Изумруд',
    swatch: ['#10b981', '#047857'],
    vars: {
      ...accent('#10b981', '#047857', '#6ee7b7', '#b2f1d6'),
      ...surface('#030b08', '#05130d', 'rgba(8, 24, 18, 0.62)', 'rgba(6, 20, 15, 0.86)'),
    },
  },
  {
    id: 'rose',
    name: 'Роза',
    swatch: ['#f43f5e', '#9f1239'],
    vars: {
      ...accent('#f43f5e', '#9f1239', '#fb8fa3', '#fdc4cf'),
      ...surface('#0a0409', '#140812', 'rgba(26, 10, 20, 0.62)', 'rgba(22, 8, 17, 0.86)'),
    },
  },
  {
    id: 'day',
    name: 'Дневная',
    swatch: ['#a5b4fc', '#6d28d9'],
    mode: 'light',
    vars: {
      // Accent kept darker so accent text stays readable on white surfaces.
      ...accent('#6d28d9', '#4c1d95', '#7c3aed', '#6d28d9'),
      // Light surfaces; [data-mode="light"] in main.css inverts the white
      // text/overlay/border utilities the views use.
      '--bg-0': '#eef1f7',
      '--bg-1': '#f6f8fc',
      '--panel': 'rgba(255, 255, 255, 0.72)',
      '--panel-strong': 'rgba(255, 255, 255, 0.88)',
      '--hairline': 'rgba(15, 23, 42, 0.10)',
    },
  },
]

export function getTheme(id: string | null | undefined): LauncherTheme {
  return LAUNCHER_THEMES.find((t) => t.id === id) ?? LAUNCHER_THEMES[0]
}

export function loadThemeId(): string {
  try {
    return localStorage.getItem(STORAGE_KEY) || DEFAULT_THEME_ID
  } catch {
    return DEFAULT_THEME_ID
  }
}

export function saveThemeId(id: string): void {
  try {
    localStorage.setItem(STORAGE_KEY, id)
  } catch {
    /* ignore */
  }
}
