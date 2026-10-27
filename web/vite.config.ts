import { defineConfig, loadEnv } from 'vite';
import { svelte } from '@sveltejs/vite-plugin-svelte';
import { appName } from './src/lib/localization.ts';
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, '.', 'FREETRAIN_ONLINE_');
  const proxy = { '/api': { target: env.FREETRAIN_ONLINE_API_TARGET ?? 'http://127.0.0.1:5080', ws: true } };
  return { build: { rollupOptions: { input: { app: 'index.html', contentGallery: 'content-gallery.html' } } }, plugins: [svelte(), { name: 'app-name', transformIndexHtml: (html: string) => html.replace('%APP_NAME%', appName) }], server: { proxy }, preview: { proxy } };
});
