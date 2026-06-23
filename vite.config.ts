import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  base: './',
  plugins: [vue(), tailwindcss()],
  build: {
    outDir: 'dist-renderer',
    emptyOutDir: true
  },
  server: {
    host: '127.0.0.1',
    port: 5177,
    strictPort: true
  }
})