const { contextBridge, ipcRenderer } = require('electron')

function subscribe(channel, callback) {
  const handler = (_event, payload) => callback(payload)
  ipcRenderer.on(channel, handler)
  return () => {
    ipcRenderer.removeListener(channel, handler)
  }
}

// The main process starts the core on 38765 unless that port is taken, and passes the
// address it really used (BrowserWindow additionalArguments).
const coreUrlArg = process.argv.find((arg) => arg.startsWith('--voidrp-core-url='))

contextBridge.exposeInMainWorld('desktop', {
  coreBaseUrl: coreUrlArg ? coreUrlArg.slice('--voidrp-core-url='.length) : '',

  request(method, path, body) {
    return ipcRenderer.invoke('desktop:request', { method, path, body })
  },

  getCoreStatus() {
    return ipcRenderer.invoke('desktop:get-core-status')
  },

  getBootInfo() {
    return ipcRenderer.invoke('desktop:get-boot-info')
  },

  onUpdaterStatus(callback) {
    return subscribe('desktop:updater-status', callback)
  },

  onCoreStatus(callback) {
    return subscribe('desktop:core-status', callback)
  },

  checkForShellUpdates() {
    return ipcRenderer.invoke('desktop:check-shell-updates')
  },

  downloadAndInstallShellUpdate() {
    return ipcRenderer.invoke('desktop:download-install-shell-update')
  },

  openExternal(url) {
    return ipcRenderer.invoke('desktop:open-external', url)
  },

  openPath(targetPath) {
    return ipcRenderer.invoke('desktop:open-path', targetPath)
  },

  selectDirectory(defaultPath) {
    return ipcRenderer.invoke('desktop:select-directory', defaultPath)
  }
})