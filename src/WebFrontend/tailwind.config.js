/** @type {import('tailwindcss').Config} */
export default {
  darkMode: "class",
  content: [
    "./**/*.razor",
    "./Layout/**/*.razor",
    "./Pages/**/*.razor",
    "./wwwroot/**/*.html",
    "./wwwroot/**/*.js",
    "./App.razor"
  ],
  theme: {
    extend: {
      colors: {
        "primary": "#3b82f6",
        "background-black": "#000000",
        "background-dark": "#0a0a0a",
        "card-dark": "#121212",
        "border-dark": "#1f1f1f",
      },
      fontFamily: {
        "display": ["Inter", "sans-serif"]
      },
      borderRadius: {
        "DEFAULT": "0.25rem",
        "lg": "0.5rem",
        "xl": "0.75rem",
        "full": "9999px"
      },
    },
  },
}
