# Phase 0 — Project Foundation

**Goal:** Runnable local environment — backend, client shell, database, Docker.

## TODO

- [x] `Game/Beep.OilGasSim.sln` with Domain, Simulation, Application, Infrastructure, AI, Api, Tests
- [ ] PostgreSQL + EF Core DbContext scaffold
- [ ] Docker Compose (`api`, `db`, `web`)
- [ ] `GET /health` endpoint
- [ ] Vite + React + TypeScript client calling health
- [ ] `Game/content/` folder structure
- [ ] `.gitignore`, README in `Game/`
- [ ] CI: build + test on push

## Target Files

| Area | Path |
|------|------|
| API | `Game/src/Beep.OilGasSim.Api/Program.cs` |
| Docker | `Game/deploy/docker/docker-compose.yml` |
| Client | `Game/client/beep-oil-gas-sim-web/` |
| Content | `Game/content/scenarios/`, `balance/`, `gameplay-modes/` |

## Verification

```bash
cd Game && dotnet build
docker compose -f deploy/docker/docker-compose.yml up
curl http://localhost:5000/health
cd client/beep-oil-gas-sim-web && npm run dev
```

Client health check succeeds.
