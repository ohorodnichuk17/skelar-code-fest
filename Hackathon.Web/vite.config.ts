import react, { reactCompilerPreset } from '@vitejs/plugin-react'
import babel from '@rolldown/plugin-babel'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    babel({ presets: [reactCompilerPreset()] })
  ],
  server: {
    // Браузер ходить лише на наш бекенд; ключ Gemma ніколи не потрапляє на фронтенд.
    proxy: {
      '/api': 'http://localhost:5150',
    },
  },
})
