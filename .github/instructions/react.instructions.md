---
applyTo: "src/web/**/*.{ts,tsx}"
---

- React 19 function components, TypeScript strict. No UI library: plain elements + `index.css`.
- All REST calls go through `api.ts`. Streaming chat uses `@ag-ui/client` `HttpAgent`, not a custom SSE parser.
- No state libraries; lift state to `App.tsx` only when two tabs need it.
- `npm run lint` (oxlint) and `npm run build` must pass. Fix lint warnings, don't disable them.
