# AI Collaboration

Este documento describe el uso de herramientas de IA durante el desarrollo de la API, incluyendo las herramientas empleadas, los prompts utilizados, las decisiones tomadas frente a las propuestas de la IA y una reflexión final sobre el proceso.

---

## 1. Herramientas utilizadas

### Claude Desktop
Se utilizó para realizar consultas conceptuales previo a iniciar la implementación. Antes de escribir una sola línea de código, se dedicó tiempo a clarificar dudas de diseño, principalmente relacionadas al servicio de idempotencia.

Las consultas realizadas fueron:

- **a.** ¿Cómo implementar un test de concurrencia para una API de ASP.NET Core 10?
- **b.** ¿Cuál es la capa correcta donde debe existir la validación de idempotencia? ¿Tiene sentido usar el `Idempotency-Key` como clave identificadora de la entidad de dominio `Transfer`?
- **c.** ¿Qué ocurre si quiero añadir tests unitarios sobre el dominio de mi aplicación? Quisiera poder abstraerme del concepto de idempotencia para enfocarme en la orquestación del flujo.
- **d.** ¿Es posible mantener la tabla de idempotencia en memoria y asegurar que 2 requests concurrentes no pasen el check inicial?

### Claude Code (CLI)
Una vez aclarado mentalmente el diseño a grandes rasgos, se utilizó Claude Code para llevar a cabo la implementación, organizando el trabajo en prompts y usando el agente `/plan` para validar las sugerencias antes de ejecutarlas.

### Gemini
Se utilizó para consultas puntuales orientadas a corregir problemas en los tests de integración.

---

## 2. Prompts utilizados

### Prompt 1 — Foundation
> Utilizado para crear la estructura de proyectos completa, sin entrar en detalles finos.

```
/plan Estoy construyendo una API .NET 10 que procese transferencias entre cuentas.
El estado puede vivir en memoria (no se require una base de datos), pero debe comportarse correctamente bajo carga concurrente.

Quiero organizar mi API en una estructura sencilla de librerías de clases.
Puesto que no voy a escalar esta api a un ambiente productivo, simplemente necesito una solución sencilla que me permita organizar el Código

Mi idea es organizar la api en 5 capas o librerías de clases:
Los nombres de las clases, interfacez y métodos Deben estar en inglés

a-TransfersApp es el proyecto web que tiene los controladores (actualmente contiene el proyecto autogenerado por Visual Studio)
b-Application es la capa de orquestación de lógica de negocio
c-Domain contiene las entidades de dominio
d-Repositorios contiene el acceso a la base de datos en memoria.
e-Dos Proyectos de tests, uno para tests unitarios (referencia directa a aplicacion) y uno para tests de integración

------
Clases a crear en cada capa:
1-TransferApp:
Agrega un controlador TransfersController con dos endpoints:

POST /api/transfers
Header obligatorio: Idempotency-Key: <string> .
Body (json): { "sourceAccountId": "11111111-1111-1111-1111-111111111111", "destinationAccountId": "22222222-2222-2222-
2222-222222222222", "amount": 100.00, "currency": "USD" }
La respuesta exitosa debe retornar un 201 Created

GET /api/transfers/id
Devuelve una transferencia por id, o 404 .


Agrega un controlador AccountsController con 1 endpoint:
GET /api/accounts/{id}
Devuelve una cuenta con su saldo actual, o 404

Configura Swagger para poder validar estos endpoints desde el browser


2-Application
Crear un servicio TransfersService con una interfaz ITransfersService
Este servicio debería tener métodos que permitan:

Aplicar una trasferencia de la cuenta origen a la cuenta destino
Obtener una trasferencia por ID
Obtener una cuenta por ID

3-Domain
Tiene las entidades de dominio

Account debería tener un ID (GUID), un Name (string) y un balance (posiblemente decimal)
Transfer debería tener un ID (GUID autogenerado), un ID de cuenta de origen, un ID de cuenta de destino, una cantidad transferida (posiblemente decimal), un tipo de moneda (string) y una fecha de operación

La capa de dominio debe definir la interfaz ITransferRepository con los métodos necesarios para aplicar una trasferencia, obtener una cuenta por id y obtener una transferencia por id
La capa de dominio puede definir excepciones de negocio

4-Repositorio
El repositorio implementa la interfaz ITransferRepository en memoria.
Un punto importante es que el mismo debe garantizar que las operaciones de balance soporten requests concurrentes. Ya que esta implementación es simplemente una prueba sencilla de concepto, 
mi idea es implementarlo utilizando simplemente un diccionario concurrente de transferencias y otro diccionario concurrente de cuentas además de un objeto lock para garantizar que el balance de origen y destino se actualizan en conjunto (thread-safe).

El repositorio debe realizar ciertas validaciones:
Tanto la cuenta de origen como destino existen
El balance de la cuenta origen nunca debe ser negativo al efectuar la transferencia

5-Tests
En el proyecto de tests unitarios simplemente arma la estructura incial con un happy flow que invoque el método de aplicar una transferencia
En el proyecto de tests de integración usa WebApplicationFactory<T> de Microsoft.AspNetCore.Mvc.Testing para levantar el servidor en memoria y lanzar múltiples requests en paralelo con Task.WhenAll.
Por ahora crea un único tests sencillo que llama al endpoint de /api/transfers y valida que la respuesta sea 201 ok

Nota adicional: La aplicación debe validar la idempotencia de la clave enviada en POST/api/transfers, por ahora no estoy resolviendo este aspecto y lo voy a implementar en una siguiente iteración.

Hazme cualquier pregunta que necesites para clarificar aspectos del problema
```

---

### Prompt 2 — Idempotency
> Utilizado para crear el servicio de idempotencia siguiendo los lineamientos analizados durante la etapa de diseño.

```
/plan Ahora quiero agregar un servicio que me permita validar la idempotencia en las requests de transferencia.
El servicio debe ser credo en la capa de aplicación y debe inyectar como interfaz en el TransfersController

Una vez que se validó que la impotencyKey está presente en la request, se debería llamar al servicio para asegurarnos que la misma request no se va a procesar 2 veces
Si una request llega más de una vez con el mismo idempotency-key y el mismo body se debe devolver el resultado original sin Volver a procesar
Si una request llega más de una vez con el mismo idempotency-key y un body distinto, la segunda request debería retornar un 409 conflict

Tener en cuenta nuevamente que la API no va a escalar a producción y por ende una implementación sencilla basada en un diccionario concurrente en memoria debería ser suficiente.
Tambien se debe tener en cuenta que si una request inicial fallara, deberíamos permitir reintentos en la solicitud para evitar estados inconsistentes

Es importante escribir tests de integración que permitan validar ambos casos de uso. 
Multiples requests concurrentes con la misma clave pero distinto body devuelven 409 (al menos 1 debe devolver 201 created)
Multiples requests concurrentes con la misma clave y el mismo body devuelven 201 created (con el mismo resultado)

Hazme saber si tienes dudas antes de crear el plan
```

---

### Prompt 3 — Extensión de los tests de concurrencia
> Utilizado para la escritura de los tests de integración y el test de concurrencia.

```
/plan 
Agrega tests de integración para validar los siguientes escenarios:

1-Si la clave de idempotencia falta la respuesta debe ser un 400. La misma debe devolver un objeto ProblemDetails de manera similar a otras requests.


2-Test de concurrencia --> Si se ejecutan multiples requests de transferencia sobre una cuenta, el balance debería quedar en un estado consistente:
Es decir, si se tiene un balance original de 1000 y se reciben 50 pedidos concurrentes de 100, el balance final debería ser 0,
10 requests deben devolver el 201, 40 requests deben devolver 422 acorde a la lógica ya implementada
```

---

### Prompt 4 — Nuevas reglas de negocio
> Utilizado para implementar las principales reglas de negocio definidas para la capa de aplicación, junto con sus tests unitarios.

```
/plan

Quiero agregar validaciones a la capa de aplicación, principalmente al método ApplyTransferAsync
Estas son validaciones de negocio y deberían ser cubiertas por los tests unitarios. Agrega un test por cada validación

1-El monto de la transferencia debe ser siempre positive
2-La trasnferencia entre cuentas debe ser siempre usando una misma moneda. Para validar este caso posiblemente sea necesario obtener las cuentas desde el servicio TransfersService usando el GetAccountByIdAsync. Además, agrega al seed del repositorio en memoria 2 nuevas cuentas en moneda "ARS"
3-El origen y el destino no Deben coincidir

Cada caso debe retornar un HTTP correcto siguiendo el estandar ProblemDetails aplicado anteriormente
```

---

### Prompt 5 — Revisión de código RFC 9457
> Utilizado para revisar el código y encontrar posibles omisiones en las respuestas HTTP respecto al estándar RFC 9457.

```
Revisa el código y dime si existe algun caso de uso donde no se esté devolviendo un resultado correcto según la especificación RFC 9457 (ProblemDetails)
```

---

### Prompt 6 — Actualización del README
> Utilizado para la creación de un archivo `README.md` estructurado.

```
Actualiza el archive README.md para indicar de manera sencilla en qué consiste la aplicación, como ejecutarla y probarla:

Aspectos a tener en cuenta --> 
Abrib Visual Studio (modo administrador)
Ejecutar la aplicación en la configuración http -> Abrir el navegador en http://localhost:5210/scalar/v1

Cuentas de prueba: Son automáticamente cargadas en memoria (crea una table para visualizar este contenido)

11111111-1111-1111-1111-111111111111 - USD - 1000.00m - Alice
22222222-2222-2222-2222-222222222222 - USD - 1000.00m - Bob
33333333-3333-3333-3333-333333333333 - ARS - 1000.00m - Carlos
44444444-4444-4444-4444-444444444444 - ARS - 1000.00m - Diana

Agrega cualquier detalle faltante en el README.md tìpico de esta documentación
```

---

## 3. Decisiones tomadas frente a las propuestas de la IA

Durante la etapa de diseño, mientras se diagramaba la solución, surgieron diversas propuestas de Claude Desktop sobre cómo implementar la idempotencia. Estas propuestas tendían a contemplar escenarios de producción real y, por ende, derivaban en soluciones considerablemente más complejas. En ese punto fue necesario acotar la discusión al problema concreto de una API local, evitando sobreingeniería. El criterio adoptado fue encontrar la solución más sencilla posible que funcionara en entorno local con un repositorio en memoria.

Por otro lado, luego de la primera iteración en la que Claude Code generó la estructura de proyectos, los tests de integración no compilaban correctamente debido a un problema vinculado al directorio de compilación. En ese momento se tomó la decisión de no continuar utilizando la CLI de Claude Code para no desviar el contexto de la conversación. Se identificó que el problema no estaba en el código generado sino en la estructura de directorios, por lo que se recurrió a Gemini para encontrar un workaround sencillo que permitiera retomar la implementación sin interrupciones.

---

## 4. Reflexión final

La IA aceleró el trabajo al permitir enfocarse en los aspectos principales de la implementación: la gestión de concurrencia, la idempotencia y las respuestas HTTP acordes al estándar RFC 9457. Gran parte del código generado correspondía a la comunicación entre las capas del proyecto y no requería análisis profundo. No fue necesario realizar ningún cambio manual, salvo el workaround mencionado anteriormente.

El principal inconveniente estuvo en la etapa inicial de diseño: encontrar y entender la dirección correcta de la solución a partir de las consultas realizadas resultó complejo, dado que no se tenía claridad conceptual sobre algunos de los conceptos planteados por el reto.

Tanto la documentación del `README.md` como este archivo `AI_COLLABORATION.md` también fueron generados con asistencia de IA.
