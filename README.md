# TransfersApp

API REST en .NET 10 para procesar transferencias de dinero entre cuentas. El estado vive completamente en memoria — no requiere base de datos.

## Estructura del proyecto

```
TransfersApp/
├── TransfersApp/                   # Proyecto web: controladores y configuración
├── TransfersApp.Application/       # Capa de orquestación: servicios e idempotencia
├── TransfersApp.Domain/            # Entidades, interfaces y excepciones de negocio
├── TransfersApp.Repositories/      # Repositorio en memoria (thread-safe)
├── TransfersApp.UnitTests/         # Tests unitarios (xUnit + Moq)
└── TransfersApp.IntegrationTests/  # Tests de integración (xUnit + WebApplicationFactory)
```

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Visual Studio 2022 o superior (recomendado: ejecutar como **administrador**)

## Cómo ejecutar

1. Abrir `TransfersApp.slnx` en Visual Studio **como administrador**.
2. Seleccionar la configuración de inicio **`http`**.
3. Ejecutar con <kbd>F5</kbd> o el botón de inicio.
4. El navegador se abrirá automáticamente en:

```
http://localhost:5210/scalar/v1
```

Desde ahí se pueden probar todos los endpoints de forma interactiva.

## Endpoints

### Transferencias

| Método | Ruta | Descripción |
|--------|------|-------------|
| `POST` | `/api/transfers` | Crea una transferencia |
| `GET` | `/api/transfers/{id}` | Consulta una transferencia por ID |

#### POST /api/transfers

**Headers requeridos:**

| Header | Descripción |
|--------|-------------|
| `Idempotency-Key` | Clave única por request. Mismo key + mismo body → devuelve el resultado original. Mismo key + body distinto → 409 Conflict. |

**Body (JSON):**

```json
{
  "sourceAccountId": "11111111-1111-1111-1111-111111111111",
  "destinationAccountId": "22222222-2222-2222-2222-222222222222",
  "amount": 100.00,
  "currency": "USD"
}
```

**Respuesta exitosa:** `201 Created` con el objeto transferencia en el body y el header `Location` apuntando al recurso creado.

### Cuentas

| Método | Ruta | Descripción |
|--------|------|-------------|
| `GET` | `/api/accounts/{id}` | Consulta una cuenta y su saldo actual |

## Cuentas de prueba

Las siguientes cuentas se cargan automáticamente en memoria al iniciar la aplicación:

| ID | Nombre | Moneda | Saldo inicial |
|----|--------|--------|---------------|
| `11111111-1111-1111-1111-111111111111` | Alice | USD | 1000.00 |
| `22222222-2222-2222-2222-222222222222` | Bob | USD | 1000.00 |
| `33333333-3333-3333-3333-333333333333` | Carlos | ARS | 1000.00 |
| `44444444-4444-4444-4444-444444444444` | Diana | ARS | 1000.00 |

## Reglas de negocio

- El monto de la transferencia debe ser **positivo**.
- La cuenta de origen y destino **no pueden ser la misma**.
- Ambas cuentas deben operar en la **misma moneda**, que además debe coincidir con la moneda indicada en el request.
- El saldo de la cuenta de origen **no puede quedar negativo**.

## Errores — formato ProblemDetails (RFC 9457)

Todos los errores siguen el estándar `application/problem+json`:

| Situación | Código HTTP |
|-----------|-------------|
| Header `Idempotency-Key` ausente | `400 Bad Request` |
| Cuenta no encontrada | `404 Not Found` |
| Mismo key, body distinto | `409 Conflict` |
| Monto inválido (≤ 0) | `422 Unprocessable Entity` |
| Misma cuenta origen y destino | `422 Unprocessable Entity` |
| Moneda incompatible entre cuentas o con el request | `422 Unprocessable Entity` |
| Saldo insuficiente | `422 Unprocessable Entity` |

## Cómo ejecutar los tests

Desde la terminal en la raíz del repositorio:

```bash
dotnet test TransfersApp.slnx
```

O desde Visual Studio: **Test → Run All Tests**.

### Cobertura de tests

| Proyecto | Tipo | Qué se verifica |
|----------|------|-----------------|
| `TransfersApp.UnitTests` | Unitarios | Validaciones de negocio en `TransfersService` (monto, misma cuenta, moneda) |
| `TransfersApp.IntegrationTests` | Integración | Endpoints HTTP, idempotencia, concurrencia, consistencia de saldo, header `Location` |
