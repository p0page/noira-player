import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

const hmrHost = process.env.VITE_HMR_HOST?.trim();
const hmrClientPort = process.env.VITE_HMR_CLIENT_PORT
  ? Number(process.env.VITE_HMR_CLIENT_PORT)
  : undefined;

export default defineConfig({
  plugins: [react()],
  base: './',
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
  server: {
    host: '0.0.0.0',
    port: 5173,
    strictPort: true,
    ws: hmrHost
      ? {
          host: hmrHost,
          clientPort: hmrClientPort,
        }
      : undefined,
  },
  preview: {
    host: '0.0.0.0',
    port: 4173,
    strictPort: true,
  },
});
