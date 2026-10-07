import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": "http://localhost:5080",
      "/healthz": "http://localhost:5080"
    }
  },
  build: {
    outDir: "dist",
    emptyOutDir: true
  }
});
