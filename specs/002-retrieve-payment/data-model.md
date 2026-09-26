# Data Model: Retrieve a Payment's Details (UC2)

**Feature**: [spec.md](spec.md) | **Research**: [research.md](research.md) | **Date**: 2026-09-26

UC2 adds no entity and changes no field. It reads the `Payment` recorded by UC1 and adds one
port method, one application service with its result type, three log events and one HTTP action.
Everything else is defined in [UC1's data model](../001-process-payment/data-model.md) and is
referenced, not repeated.

## Domain (`Domain/`) – unchanged

| Type | Use in UC2 |
|---|---|
| `Payment` | read-only: `Id` (GUID), `Status`, `CardNumberLastFour` (text, 4), `ExpiryMonth`, `ExpiryYear`, `Currency`, `Amount`. Immutable, so a reader always sees a complete record (FR-012, research R7). Holds no PAN, CVV or authorization code. |
| `PaymentStatus` | `Authorized`, `Declined` – the only statuses a retrieved payment can have (FR-004). |

There are no state transitions: retrieval never changes a payment (FR-009).

## Application (`Application/`)

### Driven port – `IPaymentRepository` (XML-documented) – extended

| Member | Added by | Contract |
|---|---|---|
| `Add(Payment payment)` | UC1 | records a decided payment |
| `GetById(Guid id) → Payment?` | **UC2** | returns the payment recorded under `id`, or `null` when none exists; no side effects; safe to call concurrently with `Add` |

### RetrievePaymentService (application service – UC2)

`Retrieve(Guid id) → RetrievePaymentResult`

Dependencies: `IPaymentRepository`, `ILogger<RetrievePaymentService>`. **No `IAcquiringBank`**
(FR-010), **no metrics** (Clarifications Q5), no `TimeProvider` (nothing is time-dependent).
Concrete class, no interface (UC1 research R11).

### RetrievePaymentResult (output – closed set)

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
  ├─ id is not a GUID in any form ──► Http: log PaymentIdInvalid ──► 400 invalid id
  │                                    (service NOT called, store NOT searched)
  └─ Guid id ──► RetrievePaymentService.Retrieve(id)
                   └─ IPaymentRepository.GetById(id)
                        ├─ payment ──► log PaymentRetrieved ──► Found(payment)  ──► 200
                        └─ null    ──► log PaymentNotFound  ──► NotFound        ──► 404
```

## Log events (research R8)

| EventId | Event | Level | Fields | Written by |
|---|---|---|---|---|
| 3000 | `PaymentRetrieved` | Information | `paymentId`, `status` | `RetrievePaymentService` |
| 3001 | `PaymentNotFound` | Information | `paymentId` (parsed GUID) | `RetrievePaymentService` |
| 3002 | `PaymentIdInvalid` | Information | – (raw value never logged) | `Http/` invalid-model response factory |

Every entry also carries `TraceId`/`SpanId` from the request scope. UC1's events 1000–2001 are
unchanged. No retrieval metric; `PaymentGatewayMetrics` is unchanged.

## Infrastructure (`Infrastructure/`)

### InMemoryPaymentRepository – extended

`GetById` → `ConcurrentDictionary<Guid, Payment>.TryGetValue`. The key is the parsed `Guid`, so
letter case and textual notation of the merchant's id cannot affect the lookup (FR-006).

## Http (`Http/`)

HTTP contract – see [contracts/payments-api.yaml](contracts/payments-api.yaml).

### PaymentsController – new action

| Item | Value |
|---|---|
| Route | `GET /api/payments/{id}` – **no `:guid` constraint** (research R1) |
| Parameter | `Guid id` from the route; accepted forms `D`, `N`, `B`, `P`, `X`, any letter case, surrounding whitespace ignored |
| Depends on | `RetrievePaymentService`, `PaymentResultMapper` |
| Responses | `200` `PaymentResponse` · `400` `ValidationProblemDetails` · `404` `ProblemDetails` · `500` `ProblemDetails` (`[ProducesResponseType]`, XML docs) |

### PaymentResponse (UC1) – reused unchanged

`id` (GUID, canonical lowercase), `status`, `cardNumberLastFour` (text), `expiryMonth`,
`expiryYear`, `currency`, `amount`. Built from `Payment` by the **same mapping** UC1 uses, so the
retrieval response equals the processing response by construction (FR-003, research R2).

### PaymentResultMapper – extended

| Input | Output |
|---|---|
| `RetrievePaymentResult.Found(payment)` | `200` + `PaymentResponse` |
| `RetrievePaymentResult.NotFound` | `404` `ProblemDetails`: `title` "Payment not found", `detail` "No payment exists with the given id.", `traceId`; the id is not echoed (R3) |
| invalid model state on the **retrieval** action | `400` `ValidationProblemDetails`: `title` "Invalid payment id", `errors.id` = fixed message (never the submitted value), `traceId`, **no `paymentStatus`** (R4) |
| invalid model state on the **processing** action | UC1's Rejected body, unchanged (`paymentStatus: "Rejected"`) |

The action-aware branching (processing vs any other action) is UC1's (UC1 research R3); UC2 adds
the retrieval row above. The action is identified by `ControllerActionDescriptor.MethodInfo.Name`
compared with `nameof` of the controller method (not `ActionName`, which drops the `Async`
suffix).

### Constants (named, no magic strings – Principle VI)

| Constant | Value | Where |
|---|---|---|
| id route parameter / error key | `id` | `PaymentsController` |
| invalid-id message | `The payment id must be a GUID, e.g. 3fa85f64-5717-4562-b3fc-2c963f66afa6.` | `PaymentResultMapper` |
| not-found title / detail | `Payment not found` / `No payment exists with the given id.` | `PaymentResultMapper` |

## Composition (`Program.cs`)

| Change | Why |
|---|---|
| register `RetrievePaymentService` (scoped or singleton – stateless) | UC2 use case |

Relied on, **delivered by UC1** (constitution 1.0.2): `app.UseStatusCodePages()` – the `405` for
an empty id is a `ProblemDetails` with `traceId` (research R5); `Microsoft.AspNetCore` and
`System.Net.Http.HttpClient` at `Warning`, no HTTP logging – a card number pasted into the path
is never logged by the framework (research R8). No new configuration keys.
