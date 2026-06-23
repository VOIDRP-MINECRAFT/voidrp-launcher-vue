const { contextBridge, ipcRenderer } = require('electron')

function subscribe(channel, callback) {
  const handler = (_event, payload) => callback(payload)
  ipcRenderer.on(channel, handler)
  return () => {
    ipcRenderer.removeListener(channel, handler)
  }
}

contextBridge.exposeInMainWorld('desktop', {
  request(method, path, body) {
    return ipcRenderer.invoke('desktop:request', { method, path, body })
  },

  getCoreStatus() {
    return ipcRenderer.invoke('desktop:get-core-status')
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
  }
})