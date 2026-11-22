import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// Mantiene el puerto autorizado por CORS; falla si otra instancia ya lo ocupa.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
  },
})
