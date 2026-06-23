import { defineStore } from 'pinia'

const STORAGE_KEY = 'voidrp_launcher_session_v1'

interface SessionState {
  rememberedLogin: string
}

function loadRememberedLogin(): string {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return ''
    const parsed = JSON.parse(raw)
    return typeof parsed?.rememberedLogin === 'string' ? parsed.rememberedLogin : ''
  } catch {
    return ''
  }
}

function persist(state: SessionState) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(state))
}

export const useLauncherSessionStore = defineStore('launcherSession', {
  state: (): SessionState => ({
    rememberedLogin: loadRememberedLogin(),
  }),
  actions: {
    setRememberedLogin(value: string) {
      this.rememberedLogin = String(value || '')
      persist({ rememberedLogin: this.rememberedLogin })
    },
  },
})
