/// <reference types="vite/client" />

import type { DesktopApi } from './src/types';

declare global {
  interface Window {
    desktop?: DesktopApi;
  }
}

export {};
