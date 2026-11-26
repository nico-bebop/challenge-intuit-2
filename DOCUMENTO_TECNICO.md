# Documento técnico – Challenge Desarrollador .NET

Repositorio: https://github.com/nico-bebop/challenge-intuit-2
Setup: `docker compose up --build` (frontend http://localhost:5173, Swagger http://localhost:5000/swagger) o, sin Docker, `dotnet run` en `backend/` y `npm run dev` en `frontend/` (ver README).

Metodología: una rama por bugfix / funcionalidad, un commit atómico con mensaje explicativo por cambio, y PR hacia `main` por cada rama.

## 1. Qué decidí priorizar
Intervención acotada y verificable por sobre un refactor arquitectónico completo: en un sistema en producción, primero corregir defectos y entregar la política pedida, y refactorizar de forma incremental. Aun así incorporé tres piezas estructurales porque la política nueva las necesita: reglas configurables, excepciones de dominio con middleware global y lógica de transición dentro del modelo.

## 2. Cambios realizados y por qué

### Parte 1 – Problemas del código existente
| # | Problema detectado | Corrección |
|---|---|---|
| 1 | `AfiliadosController.Update` usaba `afiliado.Nombre` (inexistente): **no compilaba** | DTO `AfiliadoRequest` con `NombreCompleto` |
| 2 | Create/Update bindeaban la entidad (sobreposting: el cliente podía fijar `Bloqueado`…); DNI duplicable | DTO + obligatorios + DNI único (409) |
| 3 | `DELETE` de afiliado era físico y `SetNull` dejaba consultas huérfanas | Baja lógica (`IsActive=false`); el listado muestra solo activos |
| 4 | Cancelar era `GET` (mutación por GET); umbral de 23h con mensaje de 24h; permitía cancelar consultas ya atendidas | `POST /consultas/{id}/cancelar`; ventana configurable (24h); solo desde Pendiente/Confirmado |
| 5 | `RegistrarAtencion` validaba "año en curso": permitía atender consultas **futuras** | Exige consulta ya ocurrida; se elimina `IsInCurrentYear` |
| 6 | `MarcarInasistencia` no validaba el estado de origen | Mismas reglas centralizadas |
| 7 | `PUT /estado` aceptaba cualquier transición (reabrir canceladas, saltear la regla de 24h, y burlar el cupo) | Tabla de transiciones; todos los endpoints pasan por `Consulta.CambiarEstado` |
| 8 | `CrearConsulta` bindeaba la entidad; aceptaba fecha pasada, modalidad inválida y afiliado dado de baja | DTO `CrearConsultaRequest` + validaciones |
| 9 | `Afiliado.ConsultasUsadas`: contador denormalizado que nunca se actualizaba | Eliminado; el uso se calcula desde las consultas |
| 10 | Mezcla de `DateTime.Now`/`UtcNow`; nombres `createdAt`/`isActive` | `DateTime.Now` en todo; PascalCase (el JSON no cambia) |
| 11 | Frontend: errores genéricos que ocultaban el mensaje de la API; acciones sin try/catch ni refresco | Interceptor de axios (distingue red / negocio / servidor), refresco de vista, botones según estado |

### Parte 2 – Política de cupo de nutrición
`CupoNutricionService`: **6 consultas presenciales de nutrición por año calendario, máximo 1 por mes**, sin acumulación. Se valida en `POST /consultas` (400 con mensaje claro). El cupo se **calcula** desde las consultas (no hay saldo persistido), por lo que no puede desincronizarse y "no se acumula" es consecuencia del diseño: el 1/1 el conteo parte de cero. Se expone en `GET /afiliados/{id}/cupo-nutricion?anio=` y en el listado de afiliados.

### Mejoras estructurales
- **Reglas configurables** (`BusinessRules` en `appsettings.json`): ventana de cancelación, cupo anual y tope mensual.
- **Excepciones de dominio + middleware global**: `NotFound/Business/Conflict` → 404/400/409 con cuerpo `{ mensaje }`; errores no controlados → 500 genérico y log.
- **Lógica en el modelo**: `Consulta.Cancelar/RegistrarAtencion/MarcarInasistencia/CambiarEstado` validan internamente; ninguna vía puede dejar una consulta en estado inválido.
- **Frontend**: interceptor de axios, validación de fecha futura en el formulario, confirmación al dar de baja un afiliado.
- **Docker**: `docker compose up --build`.

## 3. Ambigüedades del requerimiento y decisión tomada
El enunciado ("6 consultas de nutrición por año, no más de 1 por mes, no se acumulan, aplica a presenciales") deja abiertas preguntas de **dominio** que en un proyecto real se validarían con el negocio. Cada una está resuelta con el criterio más conservador y es fácil de cambiar:

| # | Ambigüedad | Alternativas | Decisión y justificación |
|---|---|---|---|
| 1 | **¿Qué es "por año"?** | Año calendario · 12 meses desde el alta del afiliado · vigencia del plan | Año calendario de la fecha de la consulta. Es la lectura más simple de "no se acumulan" y no requiere una fecha de vigencia que el modelo no tiene. |
| 2 | **¿Qué es "por mes"?** | Mes calendario · 30 días móviles entre consultas | Mes calendario. Coherente con (1). Consecuencia: 30/11 y 1/12 son válidas aunque estén a 1 día. |
| 3 | **¿Qué consume cupo?** | Solo atendidas · agendadas (pendientes) también · canceladas también | Consumen Pendiente, Confirmado y Atendido: si no, se podrían agendar 12 turnos futuros y superar el cupo. **Cancelado libera** el cupo. |
| 4 | **¿La inasistencia consume cupo?** | Sí (el recurso se perdió) · No (no se usó) | **Sí consume.** Si no, no habría desincentivo al ausentismo y el profesional ya reservó el turno. |
| 5 | **Cancelación tardía (<24h)**: ¿consume? | Sí · No | Hoy **no se puede cancelar** con menos de 24h (regla existente), por lo que el caso se resuelve como inasistencia (consume). |
| 6 | **"Aplica a presenciales"**: ¿qué pasa con las virtuales de nutrición? | Ilimitadas · prohibidas · otro cupo | **No cuentan ni se limitan**: la política se aplica solo a presenciales, tal como dice el texto. |
| 7 | **¿Qué fecha se imputa?** | Fecha de agendado · fecha de la consulta | Fecha **de la consulta**: permite agendar en diciembre para enero del año siguiente contra el cupo del año siguiente. |
| 8 | **"No se acumulan"**: ¿el mes sin usar libera un 2° turno el mes siguiente? | Sí (recuperable) · No | **No.** El tope mensual es duro; los meses sin uso se pierden. |
| 9 | **Alcance de "el plan"** | Todos los afiliados · según plan | Todos tienen "Plan Integral"; el límite es global y configurable. Si hubiera otros planes habría que parametrizarlo por plan. |
| 10 | **¿Pendiente → Atendido directo?** | Exigir Confirmado antes · permitirlo | Se **permite** (el enunciado no obliga a confirmar). Finales: Cancelado, Atendido, Inasistencia. |
| 11 | **Zona horaria** | UTC · hora local | Hora local del servidor (sistema mono-zona). |

Los límites de (1), (2) y la ventana de 24h son **configurables**, y el borde de las 24h es inclusivo (exactamente 24h se puede cancelar).

## 4. Fuera de alcance
- **Concurrencia**: dos reservas simultáneas podrían superar el cupo. Solución: transacción serializable o restricción única (afiliado, año-mes) para nutrición presencial.
- **Migraciones EF** (se mantiene `EnsureCreated`): tras el cambio de esquema hay que borrar `consultas.db`.
- Autenticación/autorización, paginación, logging persistente.
- Arquitectura multi-proyecto (Domain/Application/Infrastructure) y repositories: se evaluó, pero se priorizó la corrección y la política antes que un refactor mayor; sería el paso siguiente, de forma incremental.
- Reprogramación de consultas y cancelación automática de futuras al dar de baja a un afiliado.
- **Tests automatizados:** la verificación fue manual contra la API (cupo anual y mensual, virtual, año siguiente, fecha pasada, transiciones inválidas, DNI duplicado).
- **Docker:** los Dockerfiles y el compose se escribieron pero no pude levantarlos en mi entorno (el daemon de Docker Desktop no estaba corriendo); el backend y el frontend sí se probaron localmente.

## 5. Cómo continuaría con más tiempo
1. Tests de integración (`WebApplicationFactory`) para el cupo y las transiciones.
2. Restricción/transacción contra doble reserva y migraciones EF.
3. Validación de las ambigüedades 1–9 con el negocio y ajuste por configuración.
4. Refactor incremental a capas con repositories, una rama y PR por paso.
5. Toasts en lugar de `alert` y desbloqueo/reactivación de afiliados desde la UI.
