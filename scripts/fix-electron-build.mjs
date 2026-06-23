import fs from 'node:fs'
import path from 'node:path'

const rootDir = process.cwd()
const distDir = path.join(rootDir, 'dist-electron')
const sourcePreload = path.join(rootDir, 'electron', 'preload.cjs')
const targetPreload = path.join(distDir, 'preload.cjs')
const compiledPreloadJs = path.join(distDir, 'preload.js')

if (!fs.existsSync(distDir)) {
  throw new Error(`dist-electron not found: ${distDir}`)
}

if (!fs.existsSync(sourcePreload)) {
  throw new Error(`source preload not found: ${sourcePreload}`)
}

fs.copyFileSync(sourcePreload, targetPreload)

if (fs.existsSync(compiledPreloadJs)) {
  fs.rmSync(compiledPreloadJs, { force: true })
}

console.log(`Copied preload: ${sourcePreload} -> ${targetPreload}`)