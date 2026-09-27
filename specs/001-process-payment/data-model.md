# Data Model: Process a Payment (UC1)

**Feature**: [spec.md](spec.md) | **Research**: [research.md](research.md) | **Date**: 2026-09-26

All types live in the single project `src/PaymentGateway.Api`; the hexagon is expressed by
folders/namespaces (Constitution, Principle II; research R11). Field types are conceptual;
"text" means a string so leading zeros survive (FR-021).

## Domain (`Domain/`, namespace `PaymentGateway.Api.Domain`)

Depends on nothing but the BCL.

### SupportedCurrencies (constants)

`GBP`, `EUR`, `USD` – compared **ordinal, case-sensitive** (lowercase is Rejected, FR-007).

| Constant | Value | FR |
|---|---|---|
| `Length` | 3 | FR-007 |

### PaymentRequest (validated request – never stored)

| Field | Type | Rules (all mandatory) | FR |
|---|---|---|---|
| CardNumber | text | `MinCardNumberLength`–`MaxCardNumberLength` characters, ASCII digits `0-9` only | FR-003 |
| ExpiryMonth | integer | `MinExpiryMonth`–`MaxExpiryMonth` | FR-004 |
| ExpiryYear | integer | ≥ current year and ≤ `MaxExpiryYear` | FR-005 |
| (combination) | – | `(ExpiryYear, ExpiryMonth) ≥ (today.Year, today.Month)` | FR-006 |
| Currency | text | exactly `SupportedCurrencies.Length` chars, uppercase, in `SupportedCurrencies` | FR-007 |
| Amount | integer (minor units) | ≥ `MinAmount` | FR-008 |
| Cvv | text | `MinCvvLength`–`MaxCvvLength` characters, ASCII digits only | FR-009 |
| CardNumberLastFour | text (derived) | last `LastFourLength` characters of `CardNumber` | FR-016 |

| Constant | Value | FR |
|---|---|---|
| `MinCardNumberLength` | 14 | FR-003 |
| `MaxCardNumberLength` | 19 | FR-003 |
| `LastFourLength` | 4 | FR-016 |
| `MinCvvLength` | 3 | FR-009 |
| `MaxCvvLength` | 4 | FR-009 |
| `MinExpiryMonth` | 1 | FR-004 |
| `MaxExpiryMonth` | 12 | FR-004 |
| `MaxExpiryYear` | 9999 | FR-005 |
| `MinAmount` | 1 | FR-008 |

**Factory**: `Create(cardNumber?, expiryMonth?, expiryYear?, currency?, amount?, cvv?, today)`
→ **`CreatePaymentRequestResult`** (`Domain/CreatePaymentRequestResult.cs`) holding either a
`PaymentRequest` or a non-empty `IReadOnlyList<ValidationError>`.

- Every field is checked; **all** failures are returned together (FR-011).
- No value is trimmed, padded, case-converted or defaulted (FR-010).
- The combination rule (FR-006) is evaluated only when month and year are individually valid,
  so one bad field does not produce a misleading second error.
- **`ToString()` is overridden** and returns only the masked card number (e.g.
  `************8877`), expiry, currency and amount – never the full card number or the CVV
  (FR-020, Principle VIII). Verified by a unit test.

### ValidationError

| Field | Type | Notes |
|---|---|---|
| Field | text | request field name in camelCase (`cardNumber`, `expiryMonth`, `expiryYear`, `currency`, `amount`, `cvv`) |
| Message | text | names the rule broken; never contains the submitted value |

### PaymentStatus (enum)

| Status | When | Recorded as a `Payment` |
|---|---|---|
| `Authorized` | the bank authorized the payment | yes (FR-015) |
| `Declined` | the bank declined the payment | yes (FR-015) |
| `Rejected` | the gateway refused invalid information – any `PaymentRequest` rule broken, e.g. an amount below `MinAmount` (FR-008); the bank is not called | no – returned in the `400` body only (FR-011, FR-012) |

### Payment (entity – recorded)

| Field | Type | Notes | FR |
|---|---|---|---|
| Id | GUID | random v4, assigned at creation | FR-016, FR-019 |
| Status | PaymentStatus | from the bank decision | FR-015 |
| CardNumberLastFour | text (4) | from `PaymentRequest.CardNumberLastFour` | FR-016, FR-021 |
| ExpiryMonth | integer | as submitted | FR-016 |
| ExpiryYear | integer | as submitted | FR-016 |
| Currency | text | as submitted | FR-016 |
| Amount | integer | as submitted, minor units | FR-016 |

- **Factory**: `Payment.Create(PaymentRequest request, PaymentStatus status)`.
- Holds no full card number, CVV or authorization code (FR-020, spec Assumptions).
- Immutable once created – there are no state transitions in UC1.

## Application (`Application/`, namespace `PaymentGateway.Api.Application`)

Depends only on `Domain/` (plus `ILogger`, `TimeProvider` and `System.Diagnostics.Metrics`).
There is **no use-case interface**: the controller depends on the concrete service (R11).

### ProcessPaymentService (application service – UC1)

`ProcessAsync(ProcessPaymentCommand command) → ProcessPaymentResult`

Dependencies: `IAcquiringBank`, `IPaymentRepository`, `TimeProvider`, `PaymentGatewayMetrics`,
`ILogger<ProcessPaymentService>`.

### ProcessPaymentCommand (input)

Raw, possibly missing values as received from the merchant: `CardNumber` (text?),
`ExpiryMonth` (int?), `ExpiryYear` (int?), `Currency` (text?), `Amount` (int?), `Cvv` (text?).
**`ToString()` is overridden** and never includes the card number or CVV (Principle VIII).

### ProcessPaymentResult (output – closed set)

| Case | Carries | Meaning |
|---|---|---|
| `Processed` | `Payment` | bank decided; payment recorded (Authorized or Declined) |
| `Rejected` | list of `ValidationError` | invalid input; bank not called; nothing recorded |
| `BankFailed` | `BankFailureKind` | bank failed; nothing recorded; no retry |

### BankFailureKind (enum)

`Unavailable` (503, timeout, connection failure) · `Error` (400, other status, unreadable body).
Returned by `IAcquiringBank` and carried by `ProcessPaymentResult.BankFailed`.

### Driven port – `IAcquiringBank` (XML-documented)

`RequestAuthorizationAsync(PaymentRequest request) → BankAuthorizationResult`

`BankAuthorizationResult`: `Authorized` · `Declined` · `Failed(BankFailureKind)`.

### Driven port – `IPaymentRepository` (XML-documented)

`Add(Payment payment)` – UC1 only needs to record. (`GetById` is added by UC2.)

### PaymentGatewayMetrics

Created via `IMeterFactory`; meter `PaymentGateway` (research R15).

| Instrument | Type | Unit | Tags |
|---|---|---|---|
| `paymentgateway.payments.outcomes` | `Counter<long>` | `{payment}` | `result`: `authorized`, `declined`, `rejected`, `bank_unavailable`, `bank_error` |
| `paymentgateway.bank.request.duration` | `Histogram<double>` | `s` | `outcome`: `authorized`, `declined`, `bank_unavailable`, `bank_error` |

### Use case flow – `ProcessPaymentService`

```text
command ──► PaymentRequest.Create(…, today from TimeProvider)
              ├─ errors ──► log PaymentRejected (field names) · count result=rejected
              │             └──► Rejected(errors)                (bank NOT called, nothing stored)
              └─ valid  ──► IAcquiringBank.RequestAuthorizationAsync (exactly once)
                              ├─ Authorized / Declined ──► Payment.Create ──► IPaymentRepository.Add
                              │     log PaymentProcessed · count result=authorized|declined
                              │                                           └──► Processed(payment)
                              └─ Failed(kind) ──► log PaymentBankFailed · count result=bank_*
                                                  └──► BankFailed(kind)  (nothing stored)
```

## Log events (research R15)

| EventId | Event | Level | Fields |
|---|---|---|---|
| 1000 | `PaymentProcessed` | Information | `paymentId`, `status`, `currency`, `amount` |
| 1001 | `PaymentRejected` | Information | `invalidFields` (names only) |
| 1002 | `PaymentBankFailed` | Warning | `failureKind`, `currency`, `amount` |
| 1003 | `PaymentRequestUnreadable` | Information | `invalidFields` (binding paths only, e.g. `$.amount`) – written by `UnreadableRequestHandler` for an unbindable `POST` body (research R3) |
| 2000 | `BankCallCompleted` | Information | `durationMs`, `outcome` |
| 2001 | `BankCallFailed` | Warning | `durationMs`, `failureKind`, `httpStatusCode?` |

Every entry also carries `TraceId`/`SpanId` from the request scope. No entry contains the card
number, CVV, body content or a raw invalid value.

**Metric scope**: `result=rejected` counts requests that reached `ProcessPaymentService`. An
unreadable body is not counted in the `PaymentGateway` meter; it shows as `400` on route
`api/payments` in the built-in `http.server.request.duration` (research R3, R15).

## Infrastructure (`Infrastructure/`, namespace `PaymentGateway.Api.Infrastructure`)

### AcquiringBankOptions (section `AcquiringBank`)

| Field | Type | Rules | Default |
|---|---|---|---|
| BaseUrl | absolute URI | required | `http://localhost:8080` in `appsettings.json`; `http://bank_simulator:8080` in compose |
| TimeoutSeconds | integer | 1–60 | 10 |

Validated with DataAnnotations and `ValidateOnStart()`.

### AcquiringBankClient (implements `IAcquiringBank`)

Typed `HttpClient`; one call per request; classifies the response (research R4, R6); records
`paymentgateway.bank.request.duration` and logs `BankCallCompleted` / `BankCallFailed`.

### Bank contract models (internal to the adapter)

- **BankPaymentRequest**: `card_number` (text), `expiry_date` (text `MM/yyyy`), `currency`
  (text), `amount` (integer), `cvv` (text). **`ToString()` is overridden** to mask card number
  and CVV (Principle VIII).
- **BankPaymentResponse**: `authorized` (bool, required), `authorization_code` (text; required
  non-empty when `authorized` is `true`).

### InMemoryPaymentRepository (implements `IPaymentRepository`)

`ConcurrentDictionary<Guid, Payment>`; singleton.

## Http (`Http/`, namespace `PaymentGateway.Api.Http`)

HTTP contract and mapping only – see [contracts/payments-api.yaml](contracts/payments-api.yaml).
Controllers and DTOs are XML-documented (they feed OpenAPI).

- **PaymentsController**: `POST /api/payments`; depends on `ProcessPaymentService` and
  `PaymentResultMapper`.
- **PostPaymentRequest**: nullable members mirroring `ProcessPaymentCommand`; `ToString()`
  overridden to mask card number and CVV; each property's XML comment states its validation rule
  so the OpenAPI document shows the field constraints (research R12).
- **PaymentResponse**: `id`, `status` (`PaymentStatus`; always `Authorized` or `Declined` here), `cardNumberLastFour`,
  `expiryMonth`, `expiryYear`, `currency`, `amount`. The single merchant-facing representation of
  a payment – returned by processing and, from UC2, by retrieval.
- **PaymentRejectedProblemDetails** `: ValidationProblemDetails` – adds `paymentStatus`
  (`PaymentStatus`, always `Rejected`); the body of every `400` of `POST /api/payments` (research R2).
- **BankFailureProblemDetails** `: ProblemDetails` – adds `errorCode` (`bank_unavailable` |
  `bank_error`); the body of `503` / `502` (research R4).
- **PaymentResultMapper**: the single place translating `ProcessPaymentResult` into
  `200` `PaymentResponse` / `400` `PaymentRejectedProblemDetails` / `502`–`503`
  `BankFailureProblemDetails` (research R1, R2, R4, R5). It builds every response body,
  including the plain invalid-request `ValidationProblemDetails`.
- **UnreadableRequestHandler**: the `InvalidModelStateResponseFactory` for unbindable payloads
  (research R3). It turns the `ModelState` into field errors with fixed messages (never the
  submitted values) and picks the response, which `PaymentResultMapper` builds:
  - failing action = the payment-processing action (identified by
    `ControllerActionDescriptor.MethodInfo.Name`) → `PaymentRejectedProblemDetails` (**with**
    `paymentStatus: "Rejected"`), and one `PaymentRequestUnreadable` log entry;
  - any other action → a plain `ValidationProblemDetails` with `traceId`, **no**
    `paymentStatus`.

  `paymentStatus` therefore appears only on `400` responses of `POST /api/payments`
  (Constitution 1.0.2).

## Configuration (`Program.cs`)

| Key | Env var | Default | Purpose |
|---|---|---|---|
| `AcquiringBank:BaseUrl` | `AcquiringBank__BaseUrl` | `http://localhost:8080` | bank simulator URL |
| `AcquiringBank:TimeoutSeconds` | `AcquiringBank__TimeoutSeconds` | `10` | bank call timeout |
| `Swagger:Enabled` | `Swagger__Enabled` | `false` (`true` in `appsettings.Development.json` and compose) | expose Swagger UI and OpenAPI JSON |
| `Logging:LogLevel:Microsoft.AspNetCore` | – | `Warning` (`appsettings.json`; never lowered) | the host must not log request paths, which may contain a pasted card number (Principle VIII) |
| `Logging:LogLevel:System.Net.Http.HttpClient` | – | `Warning` (`appsettings.json`; never lowered) | `HttpClient` must not log request URIs (Principle VIII) |

Pipeline (`Program.cs`): `UseExceptionHandler()` → `UseStatusCodePages()` (body-less routing
errors such as `404` unknown route and `405` become `ProblemDetails` with `traceId`, research
R5) → controllers and `/health`. Framework request logging (`HttpLogging`, W3C logging) is
**not** registered.
