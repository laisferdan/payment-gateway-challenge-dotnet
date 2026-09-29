# Data Model: Retrieve a Payment's Details (UC2)

> **Superseded in part** – this records the plan. Where the code differs, the code, the README and
> the constitution are current; see [what changed since the plan](../README.md).

**Feature**: [spec.md](spec.md) | **Research**: [research.md](research.md) | **Date**: 2026-09-26
(revised 2026-09-27 – constitution 1.1.0 role folders, async port, `InvalidModelStateResponder`,
whitespace refused)

UC2 adds no entity and changes no field. It reads the `Payment` recorded by UC1 and adds one
port method, one application service with its result type, three log events, one HTTP action and
its id binder. Everything else is defined in [UC1's data model](../001-process-payment/data-model.md)
and is referenced, not repeated.

## Domain (`Domain/Payments/`) – unchanged

| Type | Use in UC2 |
|---|---|
| `Payment` | read-only: `Id` (GUID), `Status`, `CardNumberLastFour` (text, 4), `ExpiryMonth`, `ExpiryYear`, `Currency`, `Amount`. Immutable, so a reader always sees a complete record (FR-012, research R7). Holds no PAN, CVV or authorization code. |
| `PaymentStatus` | `Authorized`, `Declined` – the only statuses a retrieved payment can have (FR-004). |

There are no state transitions: retrieval never changes a payment (FR-009).

## Application

### Driven port – `Application/Ports/IPaymentRepository` (XML-documented) – extended

| Member | Added by | Contract |
|---|---|---|
| `AddAsync(Payment payment) → Task` | UC1 | records a decided payment |
| `GetByIdAsync(Guid id) → Task<Payment?>` | **UC2** | returns the payment recorded under `id`, or `null` when none exists; no side effects; safe to call concurrently with `AddAsync` |

### RetrievePaymentService (`Application/RetrievePayment/` – UC2)

`RetrieveAsync(Guid id) → Task<RetrievePaymentResult>`

Dependencies: `IPaymentRepository`, `ILogger<RetrievePaymentService>`. **No `IAcquiringBank`**
(FR-010), **no metrics** (Clarifications Q5), no `TimeProvider` (nothing is time-dependent).
Concrete class, no interface (UC1 research R11). Asynchronous because the port is (research R6).
Its log partial `RetrievePaymentService.Log.cs` sits next to it.

### RetrievePaymentResult (`Application/RetrievePayment/` – output, closed set)

| Case | Carries | Meaning | HTTP (research) |
|---|---|---|---|
| `Found` | `Payment` | a recorded payment has this id | `200` (R2) |
| `NotFound` | – | no recorded payment has this id | `404` (R3) |

The **invalid identifier** outcome is not a case: a malformed id fails model binding in `Http/`
and never reaches the service (research R4).

### Use case flow – `RetrievePaymentService`

```text
GET /api/payments/{id}
  │
  ├─ id is not a GUID in any form, or has ──► Http: log PaymentIdInvalid ──► 400 invalid id
  │  surrounding whitespace                   (service NOT called, store NOT searched)
  └─ Guid id ──► RetrievePaymentService.RetrieveAsync(id)
                   └─ IPaymentRepository.GetByIdAsync(id)
                        ├─ payment ──► log PaymentRetrieved ──► Found(payment)  ──► 200
                        └─ null    ──► log PaymentNotFound  ──► NotFound        ──► 404
```

## Log events (research R8)

| EventId | Event | Level | Fields | Written by |
|---|---|---|---|---|
| 3000 | `PaymentRetrieved` | Information | `paymentId`, `status` | `RetrievePaymentService` |
| 3001 | `PaymentNotFound` | Information | `paymentId` (parsed GUID) | `RetrievePaymentService` |
| 3002 | `PaymentIdInvalid` | Information | – (raw value never logged) | `Http/Payments/InvalidModelStateResponder` |

Every entry also carries `TraceId`/`SpanId` from the request scope. UC1's events 1000–2001 are
unchanged. No retrieval metric; `PaymentGatewayMetrics` is unchanged.

## Infrastructure (`Infrastructure/Persistence/`)

### InMemoryPaymentRepository – extended

`GetByIdAsync` → `ConcurrentDictionary<Guid, Payment>.TryGetValue`, returned as a completed task.
The key is the parsed `Guid`, so letter case and textual notation of the merchant's id cannot
affect the lookup (FR-006).

## Http (`Http/Payments/`)

HTTP contract – see [contracts/payments-api.yaml](contracts/payments-api.yaml).

### PaymentsController – new action

| Item | Value |
|---|---|
| Action | `RetrievePaymentAsync` |
| Route | `GET /api/payments/{id}` – **no `:guid` constraint** (research R1) |
| Parameter | `[PaymentIdFromRoute] Guid id`; accepted forms `D`, `N`, `B`, `P`, `X`, any letter case; surrounding whitespace **refused** (research R1) |
| Invalid request | `[RespondsToInvalidRequest(InvalidRequestResponse.InvalidPaymentId)]` |
| Depends on | `RetrievePaymentService`, `PaymentResultMapper` |
| Responses | `200` `PaymentResponse` · `400` `ValidationProblemDetails` · `404` `ProblemDetails` · `500` `ProblemDetails` (`[ProducesResponseType]`, XML docs) |

### PaymentIdModelBinder and PaymentIdFromRouteAttribute – new

| Type | Responsibility |
|---|---|
| `PaymentIdModelBinder` | reads the `id` route value as sent; binds it when it has no surrounding whitespace and `Guid.TryParse` accepts it; otherwise adds a model error on `id` (message `PaymentResultMapper.InvalidIdMessage`), which triggers the automatic `400` |
| `PaymentIdFromRouteAttribute` | a `ModelBinderAttribute` selecting `PaymentIdModelBinder` **and** keeping the binding source `Path`, so the OpenAPI document lists `id` as a required `uuid` path parameter |

### PaymentResponse (UC1) – reused unchanged

`id` (GUID, canonical lowercase), `status`, `cardNumberLastFour` (text), `expiryMonth`,
`expiryYear`, `currency`, `amount`. Built from `Payment` by the **same mapping** UC1 uses, so the
retrieval response equals the processing response by construction (FR-003, research R2).

### PaymentResultMapper and InvalidModelStateResponder – extended

| Input | Handled by | Output |
|---|---|---|
| `RetrievePaymentResult.Found(payment)` | `PaymentResultMapper.ToActionResult` | `200` + `PaymentResponse` |
| `RetrievePaymentResult.NotFound` | `PaymentResultMapper.ToActionResult` | `404` `ProblemDetails`: `title` "Payment not found", `detail` "No payment exists with the given id.", `traceId`; the id is not echoed (R3) |
| invalid model state, action marked `InvalidPaymentId` (retrieval) | `InvalidModelStateResponder` → logs `PaymentIdInvalid` → `PaymentResultMapper.InvalidPaymentId` | `400` `ValidationProblemDetails`: `title` "Invalid payment id", `errors.id` = fixed message (never the submitted value), `traceId`, **no `paymentStatus`** (R4) |
| invalid model state, action marked `PaymentRejected` (processing) | `InvalidModelStateResponder` → `PaymentResultMapper.PaymentRejected` | UC1's Rejected body, unchanged (`paymentStatus: "Rejected"`) |
| invalid model state, action without the attribute | `InvalidModelStateResponder` → `PaymentResultMapper.ValidationProblem` | plain `400` `ValidationProblemDetails` |

`InvalidModelStateResponder` is wired as `ApiBehaviorOptions.InvalidModelStateResponseFactory`
in `Program.cs` (UC1). It decides from the target action's `[RespondsToInvalidRequest]`
attribute; `PaymentResultMapper` remains the single builder of response bodies.

### Constants (named, no magic strings – Principle VI)

| Constant | Value | Where |
|---|---|---|
| invalid-id title / error key | `Invalid payment id` / `id` | `PaymentResultMapper` (private) |
| invalid-id message | `The payment id must be a GUID, e.g. 3fa85f64-5717-4562-b3fc-2c963f66afa6.` | `PaymentResultMapper.InvalidIdMessage` (public – also used by `PaymentIdModelBinder`) |
| not-found title / detail | `Payment not found` / `No payment exists with the given id.` | `PaymentResultMapper` (private) |

## Composition (`Program.cs`)

| Change | Why |
|---|---|
| register `RetrievePaymentService` (scoped) | UC2 use case |

`PaymentIdModelBinder` needs no registration: the attribute on the parameter selects it.

Relied on, **delivered by UC1**: `app.UseStatusCodePages()` – the `405` for an empty id is a
`ProblemDetails` with `traceId` (research R5); `Microsoft.AspNetCore` and
`System.Net.Http.HttpClient` at `Warning`, no HTTP logging – a card number pasted into the path
is never logged by the framework (research R8); `InvalidModelStateResponder` registered and wired.
No new configuration keys.
