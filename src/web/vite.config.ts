import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

const api = 'http://localhost:5180'

export default defineConfig({
  plugins: [react()],
  server: { proxy: { '/api': api, '/agui': api } },
  // `npm run build` drops the UI into the API so one `dotnet run` serves everything.
  build: { outDir: '../Harness.Api/wwwroot', emptyOutDir: true, chunkSizeWarningLimit: 1000 },
})
