# ShiftFlow frontend

React + TypeScript client for the existing ShiftFlow API.

## Development

1. Start the API at `http://localhost:5023`.
2. Install dependencies with `npm install`.
3. Run `npm run dev` and open `http://localhost:5173`.

Vite proxies `/api` to the local API. Override the destination with
`VITE_API_PROXY_TARGET`, or set `VITE_API_BASE_URL` when the browser must call a
different origin directly.

Seed credentials use the password `Demo!Pass1`. Employer: `employer`; Expert:
`ada`.

## Checks

```sh
npm test
npm run build
```

The access token is stored in `sessionStorage`, cleared on logout or expiry, and
never persisted across browser sessions. There is intentionally no refresh-token
flow because the backend does not expose one.
