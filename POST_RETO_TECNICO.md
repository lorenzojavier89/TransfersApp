# Post Reto Técnico

Este documento recoge dos reflexiones independientes realizadas luego de finalizar el reto: la cadena de pensamiento durante la etapa de diseño y un análisis crítico propio sobre la implementación entregada.

---

## Parte 1 — Cadena de pensamiento en la etapa de diseño

### 1. Preguntas iniciales sobre estrategia general

Se comenzó con preguntas amplias para definir el enfoque general de la solución.

- Claude recomendó `WebApplicationFactory<T>` como el enfoque más adecuado para tests de integración, consistente con lo que pedía el reto.
- Se propuso una implementación sencilla usando `Task.WhenAll`.
- También se mencionó `Parallel.ForEachAsync` como alternativa más compleja, ya que permite configuraciones de paralelismo más finas. Fue descartada por exceder el scope del reto.
- Se mencionó la posibilidad de implementar tests para tiempos de respuesta bajo carga concurrente (también descartado por estar fuera de scope).

---

### 2. Ubicación correcta de la validación de idempotencia

Se planteó la duda de en qué capa debía residir la responsabilidad de validar la clave de idempotencia: ¿en el controlador o en la capa de aplicación? También surgió la pregunta de si la `Idempotency-Key` podía ser parte de las entidades de dominio.

Claude explicó conceptualmente cómo gestionar la idempotencia y por qué la capa de aplicación es el lugar correcto:

- El controlador es un orquestador de requests HTTP, una capa liviana que solo maneja request/response y no debe contener lógica de este peso.
- La capa de aplicación tiene el contexto suficiente para validar la idempotencia. En implementaciones posteriores, si la API fuera reemplazada por un sistema de colas, la validación de idempotencia debería seguir viviendo en el servicio de aplicación.
- Respecto al uso de `Idempotency-Key` como ID de la transferencia: son conceptos distintos.
  - `Idempotency-Key` → artefacto del protocolo, generado por el cliente. Su único propósito es detectar reintentos.
  - `TransferId` → identidad de la entidad de dominio.
  - Lo correcto es gestionarlos de forma separada y mantener la relación `Idempotency-Key → TransferId` en el servicio de idempotencia.

> En este punto Claude comenzó a derivar hacia una implementación con tablas SQL gestionadas por EF Core (constraints unique + transacciones). Fue necesario acotar la discusión.

---

### 3. Gestión de la tabla de idempotencia en memoria con concurrencia

Se aclaró que la tabla de idempotencia debía ser una estructura en memoria capaz de manejar requests concurrentes.

- Claude propuso un `ConcurrentDictionary` con `GetOrAdd` para inserción atómica (añade la clave o devuelve la existente, sin necesidad de locks manuales).
- La solución incorporaba un `TaskCompletionSource` (TCS) para gestionar hilos concurrentes en espera del resultado.
- El código resultante era confuso: no había una separación clara de responsabilidades (el manejo de excepciones del procesamiento de transferencias quedaba en el controlador).
- El código evidenció un aspecto no considerado previamente: la necesidad de remover claves del diccionario cuando la request no se procesa exitosamente.
- Claude también señaló el problema de un ambiente de producción con múltiples instancias y balanceador de carga (donde el chequeo de idempotencia falla entre instancias), proponiendo una implementación con Redis. Descartado por estar fuera de scope.

---

### 4. Repositorio como Singleton en memoria

Se reforzó la idea de acotar la solución a una POC con un repositorio Singleton que mantiene estado entre requests HTTP.

- Claude propuso gestionar el repositorio en memoria usando un `lock` para garantizar que las operaciones de guardado de transferencias y actualización de balances ocurran siempre en un solo hilo de ejecución.
- Solución aceptable para una POC: el `lock` podría ser un problema de performance bajo alta concurrencia real, pero ese no era el caso del reto.
- El código seguía sin una organización clara entre capas.

---

### 5. Organización en 4 capas

Se solicitó a Claude que estructurara el código en un proyecto de 4 capas (API / Aplicación / Dominio / Repositorio).

- El código quedó mejor organizado.
- Sin embargo, persistía un problema de diseño: `TransferService` se encargaba tanto de orquestar la idempotencia como el repositorio de transferencias. Esto implicaba que los tests unitarios de `TransferService` debían conocer el concepto de idempotencia, lo cual no era deseable.

---

### 6. Aislamiento del concepto de idempotencia para los tests unitarios

Se planteó la necesidad de poder escribir tests unitarios sobre `TransferService` sin que estos supieran nada sobre claves de idempotencia.

- Claude propuso implementar un `IdempotencyDecorator` que se resolviera en el contenedor de DI.
- La solución parecía lógica, pero inyectaba la clave de idempotencia directamente en el decorador, rompiendo el encapsulamiento.

---

### 7. Corrección del encapsulamiento

Se señaló el code smell de la solución anterior.

- Claude propuso añadir la clave de idempotencia a una entidad `CreateTransferCommand`.
- En este punto se dio por finalizada la discusión de diseño.

> **Nota:** En la implementación final este problema no requirió ese enfoque, ya que fue resuelto mediante un delegado, que resultó conceptualmente más claro y directo.

---

## Parte 2 — Auto-feedback sobre la implementación entregada

### Error 1 — Idempotencia y concurrencia tratadas como problemas independientes. Paralelismo correctamente implementado mediante Barrier

La idempotencia no está garantizada en escenarios de alta concurrencia. El único test de concurrencia implementado utiliza `Guid.NewGuid()` como clave de idempotencia en cada request, lo que asegura que no haya corrupción de balances, pero no evalúa escenarios donde múltiples requests llegan con la misma clave al mismo tiempo.

Los tests de integración que validan idempotencia existen, pero tienen una limitación: `Task.WhenAll` lanza las tareas, pero el HTTP client y el scheduler las distribuyen en el tiempo. No hay garantía de que sean verdaderamente paralelas ni de que no se procesen secuencialmente.

La clase `Barrier` de .NET resuelve esto: actúa como una barrera de largada que garantiza que todos los requests salgan exactamente en el mismo instante, en lugar de en ráfagas separadas como ocurre con `Task.WhenAll` solo.

---

### Error 2 — Clave de idempotencia como `string` sin normalización

Al ser un `string`, la comparación de claves es sensible a mayúsculas y minúsculas. `ConcurrentDictionary` utiliza comparación ordinal case-sensitive por defecto, por lo que `"abc-key"` y `"ABC-KEY"` son dos entradas distintas y una misma transferencia podría procesarse dos veces.

La corrección es pasar `StringComparer.OrdinalIgnoreCase` al constructor del diccionario, que es el estándar en HTTP (los valores de `Idempotency-Key` se normalizan por convención, al igual que los nombres de header per RFC 7230).

Adicionalmente, existen dos gaps en las validaciones actuales independientemente del formato:

- **Longitud máxima** — un cliente puede enviar una clave de varios MB en el header sin que sea rechazada.
- **Caracteres permitidos** — no se valida que el string sea seguro para usar como clave de diccionario.

> Sobre el formato `string` vs `GUID`: los estándares de la industria (Stripe, PayPal) tratan la `Idempotency-Key` como un identificador opaco del cliente. Forzar GUID acopla al cliente a un formato concreto y rompe casos legítimos como claves derivadas (`order-123-retry-2`). Forzar UUID v4 tiene sentido en APIs internas donde se controla al cliente (como hace PayPal explícitamente).

---

### Error 3 — Manejo de excepciones como control de flujo

Las excepciones están diseñadas para situaciones inesperadas. "Saldo insuficiente" o "monedas distintas" son outcomes esperados del negocio, no errores excepcionales. Usarlas para control de flujo es un antipatrón con dos consecuencias concretas:

- **Semántica incorrecta** — cada excepción captura un stack trace, que es overhead innecesario para reglas de negocio que se violan frecuentemente.
- **Costo de performance** — menor en este contexto in-memory, pero significativo bajo carga real.

La alternativa correcta es el **Result pattern**: devolver explícitamente todos los outcomes posibles en la firma del método, haciendo que el compilador obligue a manejar todos los casos en el controller mediante un `switch` exhaustivo.

```csharp
// Todos los resultados posibles son visibles en el tipo de retorno
public Task<TransferResult> ApplyTransferAsync(...);

public abstract record TransferResult;
public sealed record TransferSuccess(Transfer Transfer) : TransferResult;
public sealed record InsufficientFunds(Guid AccountId, decimal Available, decimal Requested) : TransferResult;
public sealed record AccountNotFound(Guid AccountId) : TransferResult;
public sealed record CurrencyMismatch(string Expected, string Actual) : TransferResult;
public sealed record InvalidAmount(decimal Amount) : TransferResult;
public sealed record SameAccount(Guid AccountId) : TransferResult;
```

```csharp
var result = await _service.ApplyTransferAsync(...);
return result switch
{
    TransferSuccess(var t)   => CreatedAtAction(...),
    InsufficientFunds r      => Problem(detail: ..., statusCode: 422),
    AccountNotFound r        => Problem(detail: ..., statusCode: 404),
    CurrencyMismatch r       => Problem(detail: ..., statusCode: 422),
    InvalidAmount r          => Problem(detail: ..., statusCode: 422),
    SameAccount r            => Problem(detail: ..., statusCode: 422),
};
```

Ventajas: la firma comunica exactamente qué puede ocurrir, sin overhead de stack traces, y el `ExceptionHandler` queda reservado solo para errores genuinamente inesperados.
Desventaja:
- Más código inicial; los tests unitarios cambian de Assert.ThrowsAsync a Assert.IsType<T>
Para esta app en su estado actual, el impacto práctico es mínimo — es in-memory y de bajo tráfico. Pero si fuera a producción con volumen real, el Result pattern sería el camino correcto

---

### Error 4 — El lock no protege las lecturas concurrentes sobre el balance

El lock protege las escrituras entre sí, pero no protege las lecturas concurrentes sobre el mismo objeto mutado. En una frase: **el lock garantiza consistencia entre escritores, no entre escritores y lectores**.

El problema concreto es que `GetAccountByIdAsync` retorna la referencia viva del objeto dentro del `ConcurrentDictionary`, no una copia:

```csharp
public Task<Account?> GetAccountByIdAsync(Guid id)
{
    _accounts.TryGetValue(id, out var account);
    return Task.FromResult(account);  // referencia viva, no una copia
}
```

Si un `GET /api/accounts/{id}` ocurre mientras un `POST /api/transfers` está dentro del lock modificando el balance, la lectura puede obtener un valor parcialmente actualizado. `decimal` en .NET ocupa 16 bytes y no existe instrucción de CPU que lo lea de forma atómica.

Este bug no es detectado por el test de concurrencia existente porque el `GET` siempre ocurre después del `Task.WhenAll`, cuando ya no hay escrituras en curso.

La solución mínima para la POC es retornar una copia del objeto en lugar de la referencia:

```csharp
public Task<Account?> GetAccountByIdAsync(Guid id)
{
    if (!_accounts.TryGetValue(id, out var account))
        return Task.FromResult<Account?>(null);

    return Task.FromResult<Account?>(new Account
    {
        Id = account.Id,
        Name = account.Name,
        Balance = account.Balance,
        Currency = account.Currency
    });
}
```

---

### Error 5 — Algoritmo de hashing del body construido manualmente

El hash del body se construye concatenando los campos con un delimitador `|`. Esto no genera problemas en el dominio actual (los campos son GUIDs, un decimal y un string de moneda corto, ninguno puede contener `|`), pero introduce fragilidad de mantenimiento:

- Si `CreateTransferRequest` agrega un campo nuevo, el hash no lo incluirá automáticamente. Dos requests con distinto contenido podrían ser tratadas como idénticas.
- Si algún campo fuera un string libre, un valor como `"foo|bar"` podría generar colisiones artificiales.

La alternativa más robusta es hashear el JSON serializado del body directamente:

```csharp
var bodyHash = Convert.ToHexString(
    System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(
            System.Text.Json.JsonSerializer.Serialize(request))));
```

Esto captura automáticamente todos los campos del modelo, independientemente de cuántos se agreguen en el futuro. Para el estado actual de la aplicación el impacto es mínimo, pero vale la pena si el modelo de request es susceptible de crecer.

---

### Error 6 — Propagación de errores entre requests en espera

Existe un problema de propagación de excepciones en `InMemoryIdempotencyService`: si una segunda request está esperando el resultado de la primera a través del TCS y la primera falla, la segunda recibe la misma excepción propagada, sin haber tenido oportunidad de procesarse de forma independiente.

```csharp
// Request 1 (winner): falla al procesar
catch (Exception ex)
{
    _store.TryRemove(key, out _);
    actual.Tcs.SetException(ex);  // propaga la excepción al TCS
    throw;
}

// Request 2 (waiter): estaba esperando el TCS
if (actual != newEntry)
{
    var cached = await actual.Tcs.Task;  // recibe la excepción de Request 1
    return new CachedTransfer(cached);   // nunca llega acá
}
```

Para errores de negocio deterministas (fondos insuficientes, monedas distintas), el comportamiento es correcto por coincidencia: la segunda request con el mismo body hubiera fallado igual. Sin embargo, si el error de la primera request es transitorio (timeout de red, fallo de infraestructura momentáneo), la segunda request recibe ese error como si fuera propio, sin posibilidad de reintentar.

La implementación elimina la entrada del store en el `catch` para permitir reintentos, pero los waiters ya en vuelo no se benefician de esa lógica: reciben el error y punto. El diseño actual no distingue entre errores de negocio (deterministas) y errores de infraestructura (recuperables).
