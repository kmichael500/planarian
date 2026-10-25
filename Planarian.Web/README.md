# Planarian web client

This directory contains the React/TypeScript frontend. The repository root [`README.md`](../README.md) is the canonical guide for the full Docker development environment and hosted deployment configuration.

## Native frontend development

Use a Node version that satisfies the `engines` requirement in [`package.json`](package.json). For the normal native HTTPS topology:

```bash
cp .env.example .env
npm ci
npm start
```

`Planarian.Web/.env.example` enables HTTPS and maps `localhost` to the local API at `https://localhost:7111`. Keep that HTTPS topology when testing authentication: Planarian's authentication and antiforgery cookies are `Secure` and `SameSite=Lax`. Start the backend separately as described in the root README.

Do not commit local certificate/key material or `.env`.

## Validation and build commands

```bash
npm run check:client-routes
npm test -- --watchAll=false --runInBand
npm run build
```

The npm lifecycle hooks automatically regenerate/check the shared client-route projections and prepare the local PDF.js assets where required. The route manifest and generation contract are documented in [`../Contracts/README.md`](../Contracts/README.md); do not hand-edit generated route projections.

The complete cross-stack pull-request sequence is owned by [`../.github/workflows/pr-validation.yml`](../.github/workflows/pr-validation.yml). Keep this README focused on frontend-local commands rather than duplicating the entire CI workflow.
