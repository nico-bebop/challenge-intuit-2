# Consultas de Afiliados

Sistema de gestión de consultas médicas para los afiliados de un plan de salud. Permite registrar afiliados, asignar consultas con profesionales de distintas especialidades y hacer seguimiento del estado de cada consulta.

## Requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/)

---

## Backend

```bash
cd backend
dotnet run
```

El servidor levanta en **http://localhost:5000**.

La base de datos SQLite (`consultas.db`) se crea automáticamente en la primera ejecución junto con los datos iniciales.

**Swagger:** http://localhost:5000/swagger

---

## Frontend

```bash
cd frontend
npm install
npm run dev
```

El cliente levanta en **http://localhost:5173**.

---

## Estructura del proyecto

```
/
├── backend/     # API REST (.NET 8 · EF Core · SQLite)
└── frontend/    # SPA (Vue 3 · Vite)
```

---

## Con Docker (alternativa)

```bash
docker compose up --build
```

Frontend en **http://localhost:5173**, backend/Swagger en **http://localhost:5000/swagger**. La base SQLite persiste en el volumen `consultas-data` (`docker compose down -v` para reiniciarla).

## Reglas de negocio configurables

`backend/appsettings.json` → sección `BusinessRules`: `CancellationWindowHours` (24), `NutricionAnnualLimit` (6), `NutricionMonthlyLimit` (1).

> Si ya tenías un `consultas.db` de una versión anterior, borralo: el esquema cambió y se usa `EnsureCreated`.
