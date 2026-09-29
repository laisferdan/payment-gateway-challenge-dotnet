# Research: Process a Payment (UC1)

> **Superseded in part** – this records the plan. Where the code differs, the code, the README and
> the constitution are current; see [what changed since the plan](../README.md).

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-09-26

Each decision resolves an open point from the spec (items deferred to `/speckit-plan`) or a
technology choice constrained by the constitution. Format: Decision / Rationale / Alternatives.

## R1. Endpoint and success response

- **Decision**: `POST /api/payments`. Authorized and Declined both return **`200 OK`** with the
  payment body; there is no `Location` header. The outcome is carried in the `status` field
  (`Authorized` | `Declined`), and the merchant uses the `id` in the body to retrieve the
  payment later (UC2).
- **Rationale**: the assessment asks for a processed payment and its details, not for
  resource-creation semantics. The acquiring bank answers `200 OK` for both authorized and
  unauthorized payments; the gateway mirrors that and conveys the outcome in `status`. Keeping
  the `api/payments` prefix preserves the route of the provided template.
- **Alternatives**:
  - `201 Created` + `Location: /api/payments/{id}` – rejected: it adds resource-creation
    semantics the assessment does not ask for, and the `Location` URL would point to an endpoint
    that only exists once UC2 ships. The `id` in the body already gives the merchant everything
    needed to retrieve the payment.
  - `402 Payment Required` for Declined – rejected: misuses a reserved code and makes a normal
    business outcome look like a client error.

## R2. Rejected response

- **Decision**: **`400 Bad Request`** with an RFC 7807 `ValidationProblemDetails` body:
  `type`, `title` ("Payment rejected"), `status` (400), `errors` (field → messages, camelCase
  field names matching the request), `traceId`, plus the extension member
  **`paymentStatus: "Rejected"`**. The body is a **typed class**,
  `Http/PaymentRejectedProblemDetails : ValidationProblemDetails` with a `PaymentStatus` property
  fixed to `"Rejected"`; `PaymentResultMapper` returns it and `[ProducesResponseType]` declares it
  for `400`, so the generated OpenAPI schema carries `paymentStatus` exactly as the contract.
- **Rationale**: the merchant supplied invalid information – a client error. The constitution
  (Principle IX) mandates `ProblemDetails`; the extension member makes the assessment's
  `Rejected` status explicit without clashing with the RFC's numeric `status` member.
- **Alternatives**: `422 Unprocessable Entity` (valid, but ASP.NET Core and most merchants
  expect 400 for validation); a custom `{ status, errors }` body (violates Principle IX's
  consistent error shape); a plain `ValidationProblemDetails` with an `Extensions["paymentStatus"]`
  entry (works at runtime, but the OpenAPI document cannot show the member – analysis F1).

## R3. Malformed or unbindable request body (FR-013)

- **Decision**: the HTTP request DTO uses **nullable** members (`string?` for card number,
  currency, CVV; `int?` for expiry month, expiry year, amount) so that *missing* values reach
  the core and are reported by the domain rules. Values the JSON serializer cannot bind (text
  for a number, a number for a string, amount beyond `int` range, invalid JSON) are rejected by
  ASP.NET Core model binding; `ApiBehaviorOptions.InvalidModelStateResponseFactory` calls
  `PaymentResultMapper.ToUnreadableBodyResult`, which produces the **same**
  `ValidationProblemDetails` shape as R2,
  including `paymentStatus: "Rejected"`. The bank is never called in either case.
- **`paymentStatus` only on `POST /api/payments`** (Constitution 1.0.2, API Design): the factory
  is shared by every controller action, so it adds `paymentStatus: "Rejected"` (and the title
  "Payment rejected") **only when the failing action is the payment-processing action**. It
  identifies it by `context.ActionDescriptor is ControllerActionDescriptor d` and
  `d.MethodInfo.Name == nameof(PaymentsController.<ProcessAction>)` – not by `ActionName`, which
  drops the `Async` suffix by default, and not by HTTP method or path string. Any other action's
  binding failure is a plain `ValidationProblemDetails` with `traceId` and no `paymentStatus`
  (UC2 uses this for a malformed payment id).
- **Logging an unreadable body**: the processing action never runs, so `ProcessPaymentService`
  cannot log it. When the factory builds the Rejected response for an unbindable payload it
  writes **one** log entry, `PaymentRequestUnreadable` (R15), through the `ILogger` injected into
  `PaymentResultMapper`: the invalid field **paths** only (e.g. `$.amount`), never body content,
  with `TraceId` from the request scope. It is **not** counted in the `PaymentGateway` meter –
  the `rejected` counter covers requests that reached `ProcessPaymentService` (R15); unreadable
  bodies are visible as `400` on the `api/payments` route in the built-in
  `http.server.request.duration` metric (meter `Microsoft.AspNetCore.Hosting`).
- **Rationale**: rules stay in `Domain/` (Principle II) while `Http/` only rejects what it
  cannot bind; the merchant sees one error format (FR-011, FR-013). System.Text.Json does not
  coerce numbers into strings, which gives the fail-closed behaviour required by FR-010.
  Keeping the `rejected` counter inside the use case keeps one meaning per instrument (a
  validation outcome of the core) without the adapter reaching into application metrics.
- **Alternatives**: an all-string DTO parsed in the core (moves JSON parsing concerns into the
  core); a non-nullable DTO (missing values silently become `0`/`null` defaults – violates
  FR-010); counting unreadable bodies as `rejected` from `Http/` (the adapter would record an
  application metric, and the built-in HTTP metric already shows them); logging the offending
  values (untrusted input that may contain card data).

## R4. Bank failure classification and HTTP mapping (FR-017)

- **Decision**:

  | Bank behaviour | Classification | Gateway response |
  |---|---|---|
  | `503 Service Unavailable`, or a connection that could not be established | Bank unavailable | `503 Service Unavailable`, `errorCode: "bank_unavailable"` |
  | no answer within the configured timeout, a connection lost after the request was sent, or a `200` whose body fails the shape check (R6) | Outcome unknown | `504 Gateway Timeout`, `errorCode: "bank_outcome_unknown"` |
  | `400 Bad Request` or any other status | Bank error | `502 Bad Gateway`, `errorCode: "bank_error"` |

  *Updated after implementation*: the plan folded timeouts into *unavailable* and an unreadable
  `200` into *error*. Both may hide an authorization, so a merchant must not be invited to
  retry them: they are now *outcome unknown*.

  Both are `ProblemDetails` bodies with a stable `type` URI, a merchant-readable `title` and
  `detail`, `traceId`, and the `errorCode` member. No `paymentStatus` member is
  present: a bank failure is not a payment status (Clarifications, FR-002). The body is a **typed
  class**, `Http/BankFailureProblemDetails : ProblemDetails` with an `ErrorCode` property
  (`bank_unavailable` | `bank_error`), returned by `PaymentResultMapper` and declared with
  `[ProducesResponseType]` for `502` and `503`, so the OpenAPI document shows `errorCode`
  (analysis F1).
- **Rationale**: `503` tells the merchant "try again later"; `502` tells them "an upstream
  party answered badly – retrying the same request will not help", which is exactly the
  distinction the clarification asked for. Timeouts are folded into *unavailable* to keep two
  classes (no `504`).
- **Alternatives**: `504 Gateway Timeout` for timeouts (a third class – rejected as
  over-engineering by the clarification); `500` for everything (loses the classification).

## R5. Error handling – one consistent place

- **Decision**: expected outcomes are modelled as **result types**, never exceptions
  (Principle VI). A single component, **`Http/PaymentResultMapper`** (the "error handler" asked
  for in the clarification), translates every `ProcessPaymentResult` – `Processed`, `Rejected`,
  `BankFailed` – into the HTTP response and is the only place that builds the Rejected and
  bank-failure `ProblemDetails`. It maps results; it does not handle exceptions. Truly
  unexpected exceptions are handled by the built-in **`AddProblemDetails()` +
  `UseExceptionHandler()`** in `Program.cs`, which return a generic `500` `ProblemDetails` with
  no exception message or card data.
- **`paymentStatus` scope**: `PaymentResultMapper` adds `paymentStatus: "Rejected"` only to the
  `400` responses of `POST /api/payments` – domain validation failures and unreadable bodies
  (R3). Every other `400` of the service is a plain `ProblemDetails` / `ValidationProblemDetails`
  with `traceId` (Constitution 1.0.2, API Design).
- **Routing errors**: `app.UseStatusCodePages()` is registered in `Program.cs` (foundational,
  delivered with UC1). Responses the framework produces **without a body** – `404` for an unknown
  route, `405 Method Not Allowed` (e.g. `GET /api/payments`, `PUT /api/payments`) – are written as
  `ProblemDetails` with `traceId` through the `IProblemDetailsService` registered by
  `AddProblemDetails()`, so every error response of the service has the same format
  (Constitution 1.0.2, API Design). Responses that already have a body are untouched.
- **Rationale**: satisfies the clarification ("translated for the merchant in one consistent
  place") with the framework's built-in mechanisms; no middleware or handler class of our own,
  no exception-driven control flow. There is no database, so there are no storage errors to
  handle; the in-memory repository cannot fail in a recoverable way.
- **Alternatives**: throwing `BankUnavailableException` and mapping it in an exception handler
  (exceptions for expected outcomes – violates Principle VI); a custom `IExceptionHandler` class
  (does what the built-in handler already does); per-action `if/else` mapping in the controller
  (duplicates logic once UC2 arrives); leaving routing errors body-less (no `traceId`, a second
  error format).

## R6. Acquiring bank adapter

- **Decision**: a typed `HttpClient` (`IHttpClientFactory`), `Infrastructure/AcquiringBankClient`,
  implementing the `IAcquiringBank` port. Configuration `AcquiringBank:BaseUrl` (required
  absolute URI) and `AcquiringBank:TimeoutSeconds` (1–60, default 10) bound through the Options
  pattern with DataAnnotations and **`ValidateOnStart()`** (Principle IX). The adapter:
  - maps the domain request to the bank's snake_case contract inside the adapter
    (`card_number`, `expiry_date` as `MM/yyyy` with zero-padded month, `currency`, `amount`,
    `cvv`);
  - classifies the response: `200` + valid body → Authorized/Declined; `503` or a connection
    never established → Unavailable; a timeout or a connection lost after sending →
    OutcomeUnknown; any other status → Error (updated after implementation, see R4);
  - checks the body shape: `authorized` MUST be present; when `authorized` is `true`,
    `authorization_code` MUST be non-empty (the simulator returns `""` when declined);
    otherwise → OutcomeUnknown, because a `200` means the bank processed it;
  - takes no cancellation token: once a valid request reaches the gateway, the bank call runs
    to completion and its outcome is recorded whether or not the merchant is still connected –
    the payment happens regardless of the merchant's connection. The only cancellation is
    `HttpClient.Timeout` (`OperationCanceledException` → OutcomeUnknown);
  - makes **exactly one** call – no retry policies (Principle VIII, FR-017).
- **Rationale**: built-in, testable at the HTTP boundary with WireMock.Net; the bank contract
  never leaks into the core (Principle II).
- **Alternatives**: Polly/resilience handlers (retries are forbidden; a timeout needs only
  `HttpClient.Timeout`); Refit (extra dependency for one call).

## R7. Domain model and validation

- **Decision**: validation lives in `Domain/` in a factory, **`PaymentRequest.Create(...)`**,
  taking the raw nullable values plus *today's date* and returning either a valid
  `PaymentRequest` or the full list of `ValidationError`s (all fields checked, not fail-fast).
  Card number and CVV are plain strings. Each limit is a **named constant on `PaymentRequest`**:
  `MinCardNumberLength` (14), `MaxCardNumberLength` (19), `LastFourLength` (4), `MinCvvLength`
  (3), `MaxCvvLength` (4), `MinExpiryMonth` (1), `MaxExpiryMonth` (12), `MaxExpiryYear` (9999), `MinAmount` (1).
  `SupportedCurrencies` holds the currency list and its `Length` (3), next to the codes it
  describes. The expiry year has an **upper bound, `MaxExpiryYear` = 9999**: without it a
  five-digit year (e.g. `20270`) is "not in the past" and would pass, but it cannot be sent in
  the bank contract's `MM/yyyy` format and exceeds .NET date types (`DateOnly` max year 9999), so
  it would fail later as a 500 instead of a Rejected outcome. There is still no lower digit-count
  rule: a two-digit year (e.g. `27`) is in the past and is already Rejected by the month + year
  rule (FR-006). `PaymentRequest` and
  `ProcessPaymentCommand` **override `ToString()`** to mask the PAN and CVV (Principle VIII).
  The recorded **`Payment`** entity holds only safe data and takes its last four digits from
  the card number string.
- **Rationale**: one type to read for every payment rule, and one place that aggregates all
  errors. The domain stays framework-free. Masking by a tested `ToString()` override on the
  records that carry card data gives the Principle VIII guarantee without extra types.
- **Alternatives**: `CardNumber` and `Cvv` value objects (type-level masking, but two more
  types whose only job was the same `ToString()` guarantee); a value object for every field
  (more types than the rules need); a shared `PaymentRules` constants class (separates each
  limit from the type that uses it); an exact four-digit expiry-year rule (its lower half can
  never change an outcome – only the 9999 upper bound matters);
  FluentValidation (dependency, rules outside the domain model); DataAnnotations on the HTTP DTO
  (rules in the adapter – violates Principle II).

## R8. Time and expiry

- **Decision**: `ProcessPaymentService` receives `TimeProvider` (BCL, .NET 8) and passes
  `DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime)` to the domain. A card is valid
  while `(expiryYear, expiryMonth) >= (today.Year, today.Month)`. Tests use `FakeTimeProvider`.
- **Rationale**: deterministic tests (Principle V); matches the confirmed rule "valid until the
  end of the expiry month", evaluated in UTC (spec Assumptions).
- **Alternatives**: `DateTime.UtcNow` (non-deterministic); an `IClock` port (duplicates the BCL
  abstraction).

## R9. Payment identifier and storage

- **Decision**: `Guid.NewGuid()` (version 4, random) assigned when the `Payment` is created.
  Storage is an in-memory `ConcurrentDictionary<Guid, Payment>` behind `IPaymentRepository`,
  registered as a singleton. UC1 needs only `Add`; `GetById` is added by UC2
  (Interface Segregation, YAGNI).
- **Rationale**: unique and not guessable (FR-019); thread-safe for concurrent merchants
  (Edge Cases); explicitly allowed by the assessment.
- **Alternatives**: sequential ids (guessable); the template's `List<T>` repository (not
  thread-safe, stores the HTTP DTO); adding `GetById` in UC1 only so a test can call it (an
  abstraction with no real consumer yet – Principle VII).

## R10. JSON contract

- **Decision**: ASP.NET Core defaults – camelCase property names; `PaymentStatus` serialised
  as a **string** (`"Authorized"`, `"Declined"`) via `JsonStringEnumConverter`. Card number,
  CVV and last four are JSON strings. Unknown request members are ignored (Edge Cases).
- **Rationale**: conventional for a .NET API and consistent with the template; string enums
  match the assessment's status values; strings preserve leading zeros (FR-021).
- **Alternatives**: snake_case like the bank (that is the bank's contract, not ours).

## R11. Project structure and template clean-up

- **Decision**: a single production project, `src/PaymentGateway.Api`, with the hexagon as
  folders/namespaces – `Domain/`, `Application/`, `Infrastructure/`, `Http/` – and `Program.cs`
  as the only composition root. A single test project, `test/PaymentGateway.Api.Tests` (the
  template project, reused), with `Unit/`, `Unit/Fakes/`, `Integration/`,
  `Integration/Fixtures/` and `EndToEnd/`. Interfaces exist only for the driven ports
  (`IAcquiringBank`, `IPaymentRepository`); `PaymentsController` depends on the concrete
  `ProcessPaymentService`. `BankFailureKind` is an Application type returned by the bank port
  and carried by `ProcessPaymentResult.BankFailed`.
  Template artefacts removed: `PaymentsControllerTests.cs`; `src/PaymentGateway.Api/Enums/`,
  `Services/` and `Models/` (numeric card fields, a non-thread-safe repository storing the HTTP
  DTO); `Controllers/` (the controller moves to `Http/`) and the template `GET` action
  (retrieval is re-delivered test-first by UC2 – the template's own 404 test currently fails).
- **Rationale**: the assessment says "we do not want to encourage over-engineering". One
  project for two endpoints avoids extra `.csproj` files and references with no second consumer.
  The hexagon stays visible through folders and namespaces, and the dependency rule is checked in
  review (a `using` of an outer namespace from `Domain/` or `Application/` is a violation). A
  use-case interface with one implementation and one caller would add indirection without a
  substitution point; the controller is covered by integration tests through the real service.
- **Alternatives**: four production projects plus three test projects (compile-time
  enforcement of the dependency rule, at the cost of seven projects); an `IProcessPaymentUseCase`
  interface with `Ports/Driving/` and `Ports/Driven/` folders (only useful with a second
  implementation or consumer); a driving-side copy of the failure enum (pure duplication inside
  one project); keeping the template `GET` wired to the new repository (UC2's scope, untested).

## R12. Build, documentation and quality gates

- **Decision**:
  - Each `.csproj` sets `Nullable`, `ImplicitUsings` and `TreatWarningsAsErrors`. The Api
    project also sets `GenerateDocumentationFile` (it feeds OpenAPI) with `NoWarn` `CS1591`;
    XML docs are written on controllers, DTOs and driven ports (Principle X).
  - Swashbuckle includes the Api XML comment file; the action declares every status code with
    `[ProducesResponseType]` and the typed body for each (`PaymentResponse`,
    `PaymentRejectedProblemDetails`, `BankFailureProblemDetails`, `ProblemDetails`) (FR-022).
  - Field constraints (Constitution X): the rules live in `Domain/`, and DataAnnotations on the
    HTTP DTO would validate in `Http/` (Principle II), so each `PostPaymentRequest` property's XML
    comment states its rule in words. This repetition is deliberate and recorded in the README's
    design decisions; `OpenApiDocumentTests` asserts every field has a description.
  - Coverage is collected with `coverlet.collector` (`--collect:"XPlat Code Coverage"`) and
    reported as Cobertura XML – **no threshold** (Principle V). E2E tests are excluded from the
    default run with `--filter "Category!=E2E"`.
  - `dotnet format --verify-no-changes` against the untouched `.editorconfig`.
- **Rationale**: every Definition of Done gate is a single command; XML docs go where they are
  read (OpenAPI and port contracts).
- **Alternatives**: `Directory.Build.props` (not needed for two projects); a 90 % coverage gate
  with `coverlet.msbuild` (rewards line count over risk; the README's test strategy names the
  risks instead); XML docs on every member (noise where names already say it).

## R13. Dependencies (all permissive licences)

| Package | Project | Licence | Why |
|---|---|---|---|
| Swashbuckle.AspNetCore (6.9.0; the template's 6.2.3 dropped `application/problem+json` response types from the document) | `PaymentGateway.Api` | MIT | OpenAPI + XML comments |
| xunit, xunit.runner.visualstudio | `PaymentGateway.Api.Tests` | Apache-2.0 | test framework |
| Microsoft.NET.Test.Sdk | `PaymentGateway.Api.Tests` | MIT | test host |
| Microsoft.AspNetCore.Mvc.Testing (8.0.x; replaces the template's 6.0.24) | `PaymentGateway.Api.Tests` | MIT | `WebApplicationFactory` |
| WireMock.Net | `PaymentGateway.Api.Tests` | Apache-2.0 | bank stub at the HTTP boundary |
| Microsoft.Extensions.TimeProvider.Testing | `PaymentGateway.Api.Tests` | MIT | `FakeTimeProvider` |
| Microsoft.Extensions.Diagnostics.Testing | `PaymentGateway.Api.Tests` | MIT | `FakeLogger` / `FakeLogCollector`, `MetricCollector<T>` |
| coverlet.collector (already present, updated) | `PaymentGateway.Api.Tests` | MIT | coverage collection |

Framework features used instead of packages (all in the ASP.NET Core shared framework):
`IHttpClientFactory`, Options + DataAnnotations validation, `ILogger` + JSON console,
`System.Diagnostics.Metrics` / `IMeterFactory`, health checks, `ProblemDetails`, `TimeProvider`.

No mediator, mapper, validation, resilience or mocking library is used (Principle VII).
Rejected: NSubstitute (R14), coverlet.msbuild and ReportGenerator (no coverage gate or HTML
report needed – R12).

## R14. Testing approach

- **Decision**:
  - **Unit** (`Unit/`): domain types and `ProcessPaymentService` with **hand-written fakes**
    only (`Unit/Fakes/`). `FakeAcquiringBank` records its calls, so "a rejected payment never
    calls the bank" and "exactly one bank call" are asserted on it.
  - **Integration** (`Integration/`): the **real adapters** (`AcquiringBankClient`,
    `InMemoryPaymentRepository`) through `WebApplicationFactory<Program>`; **only the bank** is
    replaced, by WireMock at the HTTP boundary. Besides the endpoint outcomes, integration
    covers: an unreadable body → Rejected with `paymentStatus`, one `PaymentRequestUnreadable`
    log entry and no outcome measurement (R3, R15); an **unknown route** and a **wrong method**
    (`GET /api/payments`, `PUT /api/payments`) → `ProblemDetails` with `traceId` matching the
    log scope (R5); a card number **in a request path or body** appears in no log entry (R15).
  - **E2E** (`EndToEnd/`): the real bank simulator; in UC1, Authorized, Declined and
    Bank unavailable via `POST` only.
  - Added in UC2: `IPaymentRepository.GetById`, a single "add then retrieve" repository test
    (no concurrency test – `ConcurrentDictionary` is framework code) and the full
    process-then-retrieve E2E journey.
- **Rationale**: each level has one clear purpose and no overlap. The ports are tiny (one method
  each), so a fake is a few lines, reads like the domain and needs no library. UC1's repository
  has only `Add`, so there is nothing observable to assert on it directly; the endpoint
  integration tests exercise it.
- **Alternatives**: NSubstitute for the two interaction checks (a library for what a counter on a
  fake already proves); a repository test in UC1 (needs a `GetById` with no real consumer yet);
  E2E journeys calling a retrieval endpoint that does not exist yet.

## R15. Observability (foundational, delivered with UC1)

- **Decision**:
  - **Logging**: built-in JSON console formatter with scopes
    (`AddJsonConsole(o => o.IncludeScopes = true)`). ASP.NET Core's host already puts
    `TraceId`/`SpanId` into the logging scope, so every entry of a request carries the trace id.
    Log calls use `LoggerMessage` source-generated methods. Events:

    | EventId | Event | Level | Fields | Written by |
    |---|---|---|---|---|
    | 1000 | `PaymentProcessed` | Information | `paymentId`, `status` (Authorized/Declined), `currency`, `amount` | `ProcessPaymentService` |
    | 1001 | `PaymentRejected` | Information | `invalidFields` (field **names** only, e.g. `cardNumber,currency`) | `ProcessPaymentService` |
    | 1002 | `PaymentBankFailed` | Warning | `failureKind` (Unavailable/Error), `currency`, `amount` | `ProcessPaymentService` |
    | 1003 | `PaymentRequestUnreadable` | Information | `invalidFields` (binding **paths** only, e.g. `$.amount`; never body content) | `PaymentResultMapper.ToUnreadableBodyResult`, when the invalid-model factory builds the Rejected response for `POST /api/payments` (R3) |
    | 2000 | `BankCallCompleted` | Information | `durationMs`, `outcome` (authorized/declined) | `AcquiringBankClient` |
    | 2001 | `BankCallFailed` | Warning | `durationMs`, `failureKind`, `httpStatusCode` (nullable) | `AcquiringBankClient` |

    No event has a card number, CVV or raw invalid value. A rejected request logs field names
    only, because its values are untrusted and may contain card data.
  - **Hosting scope** (found during implementation): the host opens a log scope holding
    `RequestPath` whenever `Microsoft.AspNetCore.Hosting.Diagnostics` is enabled at any level; with
    `IncludeScopes` that path – which may hold a pasted card number – would be printed on every
    entry of the request. That category is therefore `None`. The host then starts a request
    `Activity` only if its `ActivitySource` has a listener, so `Program.cs` registers one for
    `Microsoft.AspNetCore`; without it there is no trace id in logs or `ProblemDetails`.
  - **Framework logs** (Constitution 1.0.2, Principle VIII): framework request logging
    (`AddHttpLogging` / `UseHttpLogging`, W3C logging) is **not enabled**, and
    `appsettings.json` keeps `Logging:LogLevel:Microsoft.AspNetCore` **and**
    `Logging:LogLevel:System.Net.Http.HttpClient` at **`Warning`**. At `Information` the host
    logs every request path ("Request starting …") and `HttpClient` logs request URIs; a merchant
    can paste a card number into a path or a body, so those lines must never be written. No
    environment overrides these levels (`appsettings.Development.json` does not set them).
  - **Correlation**: `AddProblemDetails(o => o.CustomizeProblemDetails = …)` sets the
    `traceId` extension to `Activity.Current?.TraceId` (the same 32-hex id the log scope uses;
    the framework default is the full W3C `traceparent`-style id, which would not match the
    logs). The customisation applies to every `ProblemDetails` – the 400 from
    `ValidationProblem()`, the 502/503 from `Problem()` and the 500 from `UseExceptionHandler()` –
    because they all go through `ProblemDetailsFactory` / `IProblemDetailsService`.
  - **Metrics**: `Application/PaymentGatewayMetrics`, created via `IMeterFactory`, meter
    **`PaymentGateway`**:

    | Instrument | Type | Unit | Tags | Recorded by |
    |---|---|---|---|---|
    | `paymentgateway.payments.outcomes` | `Counter<long>` | `{payment}` | `result` = `authorized` \| `declined` \| `rejected` \| `bank_unavailable` \| `bank_error` | `ProcessPaymentService` (once per request) |
    | `paymentgateway.bank.request.duration` | `Histogram<double>` | `s` | `outcome` = `authorized` \| `declined` \| `bank_unavailable` \| `bank_error` | `AcquiringBankClient` (once per bank call) |

    The `rejected` result counts requests that **reached `ProcessPaymentService`** and failed a
    domain rule. An unreadable body never reaches the service and is **not** counted in the
    `PaymentGateway` meter (R3); it is visible as a `400` on route `api/payments` in the built-in
    ASP.NET Core metric `http.server.request.duration` (meter `Microsoft.AspNetCore.Hosting`,
    tags `http.route`, `http.response.status_code`), and in the `PaymentRequestUnreadable` log.

    Duration is measured with `TimeProvider.GetTimestamp()` / `GetElapsedTime()`, so tests are
    deterministic. Metrics are viewed with
    `dotnet-counters monitor -n PaymentGateway.Api --counters PaymentGateway`.
  - **Health**: `AddHealthChecks()` + `MapHealthChecks("/health")` – liveness only (the
    process is up and serving). The bank is **not** checked, so a bank outage does not make the
    gateway look dead; bank health is visible in the metrics instead.
  - **Tests**: `FakeLogger` (`AddFakeLogging()` in the integration factory) asserts that no
    collected log entry – message or structured state – contains the PAN or the CVV, for
    authorized, rejected and bank-failure requests; `MetricCollector<long>` asserts that
    `paymentgateway.payments.outcomes` records one measurement with the right `result` tag;
    `MetricCollector<double>` asserts the bank duration is recorded. An unreadable body logs
    `PaymentRequestUnreadable` once, with `TraceId`, and records **no** outcome measurement.
    **Card numbers in framework logs** (Principle VIII): with the application's real logging
    configuration (the `appsettings.json` filters apply to the fake provider too), an
    integration test sends a card number **in a request path** (e.g.
    `GET /api/payments/4111111111111111`) and **in a body** (a valid `POST`, and an unreadable
    `POST` whose body contains the card number) and asserts that it appears in **no** collected
    log entry.
- **Rationale**: Principle XI; everything is built into .NET 8, so no dependency is added.
  Logging field names for rejections (not values) removes the only path by which raw card data
  could reach a log.
- **Alternatives**: Serilog (extra dependency for what the JSON console already does);
  OpenTelemetry exporters, Prometheus, Grafana (listed as production next steps); a readiness
  check that calls the bank (would send traffic to the bank on every probe and mark the gateway
  unhealthy during bank outages that it already reports as 503).

## R16. Packaging and hosting (foundational, delivered with UC1)

- **Decision**:
  - **`Dockerfile`** at the repository root, multi-stage:
    1. `build` – `mcr.microsoft.com/dotnet/sdk:8.0`: copy the `.csproj`, `dotnet restore`,
       copy the source, `dotnet publish src/PaymentGateway.Api -c Release -o /app
       /p:UseAppHost=false`.
    2. `final` – `mcr.microsoft.com/dotnet/aspnet:8.0`: install `curl` (for the compose
       healthcheck only), copy `/app`, `USER $APP_UID` (the image's built-in non-root `app`
       user), `EXPOSE 8080` (the .NET 8 image default, `ASPNETCORE_HTTP_PORTS=8080`),
       `ENTRYPOINT ["dotnet", "PaymentGateway.Api.dll"]`.

    A `.dockerignore` excludes `bin/`, `obj/`, `test/`, `specs/` and `.git/`.
  - **`docker-compose.yml`** (existing file read first): the `bank_simulator` service, the
    `version` key and `imposters/` stay unchanged. One service is added:

    ```yaml
      payment_gateway:
        build: .
        container_name: payment_gateway
        ports:
          - "8090:8080"            # host 8090 – does not clash with the simulator's 8080/2525
        environment:
          AcquiringBank__BaseUrl: http://bank_simulator:8080
          Swagger__Enabled: "true"
        depends_on:
          - bank_simulator
        healthcheck:
          test: ["CMD", "curl", "-fsS", "http://localhost:8080/health"]
          interval: 10s
          timeout: 3s
          retries: 3
    ```

    `depends_on` uses the short form because the simulator has no healthcheck and its service
    must not change.
  - **Configuration**: `AcquiringBank:BaseUrl`, `AcquiringBank:TimeoutSeconds` and
    `Swagger:Enabled` are Options bound from configuration and environment variables
    (`AcquiringBank__BaseUrl`, `Swagger__Enabled`). `appsettings.json` holds the local simulator
    URL (`http://localhost:8080`) and `Swagger:Enabled=false`; `appsettings.Development.json`
    sets `Swagger:Enabled=true` for local runs.
  - **HTTPS redirection only where HTTPS exists**: `UseHttpsRedirection()` is kept. The launch
    profile listens on `https://localhost:7092` and `http://localhost:5067`, so local HTTP requests
    are redirected (`307`); this needs the developer certificate (`dotnet dev-certs https`) or
    startup fails. The container listens on HTTP only – TLS is terminated upstream (load balancer /
    ingress), a documented assumption – so the middleware finds no HTTPS port, logs one warning and
    serves HTTP. A redirect does not protect a `POST`: its card data has already crossed plain HTTP,
    so clients must call the HTTPS address directly. `UseAuthorization()` is removed
    (authentication is out of scope). The container never sets
    `ASPNETCORE_ENVIRONMENT=Development` (that would enable the developer exception page), so
    Swagger is turned on only by the flag.
- **Rationale**: Principle XII – one `docker compose up` runs the whole system; the official
  image's non-root user and default port avoid custom setup; configuration by environment
  variables makes the same image work locally, in compose and in any orchestrator.
- **Alternatives**: a chiseled/distroless runtime image (smaller, but no shell or `curl` for the
  compose healthcheck); a `bash /dev/tcp` healthcheck without `curl` (fragile); host port `5000`
  (collides with AirPlay Receiver on macOS); `ASPNETCORE_ENVIRONMENT=Development` in compose to
  get Swagger (leaks exception details).

## R17. Continuous integration

- **Decision**: `.github/workflows/ci.yml`, triggered on `push` and `pull_request`, one job on
  `ubuntu-latest`:
  1. `actions/checkout@v4`, `actions/setup-dotnet@v4` (`8.0.x`)
  2. `dotnet restore`
  3. `dotnet build --no-restore -c Release` (warnings are errors via `TreatWarningsAsErrors`)
  4. `dotnet format --verify-no-changes --no-restore`
  5. `dotnet test --no-build -c Release --filter "Category!=E2E"
     --collect:"XPlat Code Coverage" --results-directory ./coverage`
  6. `actions/upload-artifact@v4` – the Cobertura coverage file (report only, no threshold)
  7. `docker build -t payment-gateway .`
- **Rationale**: Principle XII and the Definition of Done ("CI is green"); every quality gate is
  one command, and the Docker build proves the image still builds. E2E tests need the simulator
  and stay out of CI's default run.
- **Alternatives**: running E2E in CI with `docker compose up` (slower and more fragile; can be
  added later); a coverage threshold (not required by the constitution – R12).

## R18. Verifying the latency criterion (SC-006)

- **Decision**: **no automated performance test**. SC-006 ("95 % of outcomes in under 2 s when
  the bank answers promptly; Rejected in under 1 s") is verified **manually** with
  `curl -w "%{time_total}"` against the running gateway (quickstart §5) as part of the Definition
  of Done (tasks T084). In a running system the percentiles are read from the built-in ASP.NET
  Core histogram `http.server.request.duration` (meter `Microsoft.AspNetCore.Hosting`, tagged by
  `http.route` and `http.response.status_code`), viewable with `dotnet-counters`; the README's
  observability section says so.
- **Rationale**: the gateway adds an in-memory validation and one HTTP call; latency is dominated
  by the bank, which the simulator answers in milliseconds. A load-test harness (tooling,
  thresholds, flaky timings in CI) would be over-engineering for this exercise (Principle VII),
  while the built-in histogram already gives production percentiles without new code.
- **Alternatives**: a load test with NBomber/k6 (new dependency and CI job); a timing assertion in
  integration tests (non-deterministic – Principle V).
