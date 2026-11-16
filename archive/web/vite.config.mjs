import { defineConfig } from 'vite';

// Relative base so the same build works on GitHub Pages project sites
// (https://user.github.io/Luna-Quest/) and on any static host or file server.
export default defineConfig({
  base: './',
  publicDir: 'public',
  server: {
    host: '0.0.0.0',
    port: 5173,
    strictPort: false,
    // The sandbox preview is served from a proxied host name, so host checking is off.
    allowedHosts: true,
    fs: { strict: false },
  },
  preview: {
    host: '0.0.0.0',
    port: 4173,
    allowedHosts: true,
  },
  build: {
    outDir: 'dist',
    target: 'es2022',
    assetsInlineLimit: 0,
    chunkSizeWarningLimit: 1600,
  },
});
