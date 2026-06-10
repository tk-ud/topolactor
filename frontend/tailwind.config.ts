import type { Config } from "tailwindcss";

export default {
  content: [
    "./routes/**/*.{ts,tsx}",
    "./islands/**/*.{ts,tsx}",
    "./components/**/*.{ts,tsx}",
    "./styles/**/*.css",
    "./runtime/**/*.ts",
  ],
  theme: {
    extend: {},
  },
  plugins: [],
} satisfies Config;
