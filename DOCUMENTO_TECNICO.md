# Documento técnico – Challenge Desarrollador .NET

Repositorio: https://github.com/nico-bebop/challenge-intuit-2 (rama `feature/cupo-nutricion-y-bugfixes`; un commit por bugfix/funcionalidad).

## 1. Cambios realizados y por qué

### Parte 1 – Problemas del código existente
| # | Problema | Corrección |
|---|----------|-----------|
| 1 | `AfiliadosController.Update` usaba `afiliado.Nombre` (inexistente): **no compilaba** | DTO `AfiliadoRequest` con `NombreCompleto` |
| 2 | Create/Update bindeaban la entidad: el cliente podía fijar `Bloqueado`, etc. (mass-assignment); DNI duplicable | DTO + validación de obligatorios y DNI único (409) |
| 3 | `DELETE` afiliado borraba físicamente y `SetNull` dejaba consultas huérfanas | Baja lógica (`IsActive=false`); el listado filtra activos |
| 4 | Cancelar era `GET` (mutación por GET), umbral 23h con mensaje 24h, permitía cancelar consultas ya atendidas | `POST /consultas/{id}/cancelar`, 24h, solo Pendiente/Confirmado |
| 5 | `RegistrarAtencion` validaba "año en curso": permitía atender consultas **futuras** | Exige consulta ya ocurrida y estado Pendiente/Confirmado; se elimina `IsInCurrentYear` |
| 6 | `MarcarInasistencia` no validaba el estado | Solo Pendiente/Confirmado y fecha pasada |
| 7 | `PUT /estado` permitía cualquier transición (reabrir canceladas, saltear reglas; además burlaría el cupo) | Tabla de transiciones (`EstadoConsultaTransiciones`); Cancelado/Atendido/Inasistencia son finales; reaplica reglas de 24h y fecha |
| 8 | `CrearConsulta` bindeaba la entidad (Id, Estado, navs), aceptaba fechas pasadas, modalidades inválidas y afiliados dados de baja | DTO `CrearConsultaRequest` + validaciones |
| 9 | `Afiliado.ConsultasUsadas` era un contador denormalizado nunca actualizado | Eliminado; el uso se calcula desde las consultas |
| 10 | Mezcla `DateTime.Now` / `UtcNow`; nombres `createdAt`/`isActive` | Todo `DateTime.Now`; PascalCase (JSON sin cambios) |
| 11 | Frontend: errores genéricos, cancelar/inasistencia sin try/catch ni refresco | Se muestra el `mensaje` de la API, se actualiza la vista, botones según estado |

### Parte 2 – Política de cupo de nutrición
`Services/CupoNutricionService`:
- Aplica a consultas **presenciales** con profesional de especialidad **Nutrición**.
- Máximo **6 por año calendario** y **1 por mes**; se evalúa sobre la fecha de la consulta a agendar.
- "No se acumulan": el cupo se calcula por año sobre las consultas reales; el 1/1 se reinicia solo (no hay saldo persistido).
- Validación en `POST /consultas` (400 con mensaje claro).
- Consulta informativa: `GET /afiliados/{id}/cupo-nutricion?anio=`; `GET /afiliados` incluye `consultasNutricionUsadas/Limite`; el frontend lo muestra.

## 2. Supuestos
- Consumen cupo: Pendiente, Confirmado, Atendido e **Inasistencia** (el cupo se "gastó"). **Cancelado libera** el cupo.
- "Por año" = año calendario (no año desde el alta del afiliado). "Por mes" = mes calendario de la consulta.
- El cupo es por afiliado; la especialidad se identifica por `Profesional.Especialidad == "Nutrición"`.
- Las consultas virtuales de nutrición no consumen ni están limitadas (la política aplica solo a presenciales).
- Sistema mono-zona horaria: se usa la hora local del servidor.
- El afiliado bloqueado ya no puede agendar (regla existente, se mantiene).

## 3. Fuera de alcance
- **Concurrencia**: dos reservas simultáneas podrían superar el cupo; solución: transacción serializable o restricción única (afiliado, año-mes) para nutrición.
- Migraciones EF (se mantiene `EnsureCreated`): tras el cambio de esquema hay que borrar `backend/consultas.db`.
- Autenticación/autorización, paginación, tests automatizados (se probó manualmente vía API: cupo anual y mensual, virtual, año siguiente, fecha pasada, transiciones inválidas).
- Cancelar automáticamente las consultas futuras al dar de baja un afiliado.
- El build del frontend no pudo ejecutarse localmente (Node 14 instalado; Vite 5 requiere Node 18+); los cambios se revisaron manualmente.

## 4. Cómo continuaría con más tiempo
Tests de integración (WebApplicationFactory) para el cupo, migraciones, índice/transacción anti-carrera, tests de componentes Vue.
