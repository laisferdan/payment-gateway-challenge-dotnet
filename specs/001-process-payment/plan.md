# Implementation Plan: Process a Payment (UC1)

**Branch**: `develop` (spec directory `specs/001-process-payment`) | **Date**: 2026-09-26 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-process-payment/spec.md`

## Summary

A merchant submits a card payment to `POST /api/payments`. `Domain/PaymentRequest.Create`
validates every field against the assessment rules; all failures come back together as a
**Rejected** `400` `ValidationProblemDetails`. A valid request is sent **once** to the acquiring
bank through the `IAcquiringBank` driven port. Authorized and Declined payments are recorded in
an in-memory repository and returned as `200 OK`, exposing only the last four card digits. Bank
failures are classified as **Bank unavailable** (`503`) or **Bank error** (`502`), never recorded
and never retried. One mapper in `Http/` translates every use-case result into HTTP; the built-in
exception handler covers the unexpected.

The template is reshaped into **one production project** with the hexagon as folders
(`Domain/`, `Application/`, `Infrastructure/`, `Http/`) and **one test project** (`Unit/`,
`Integration/`, `EndToEnd/`). UC1 also delivers the foundations every later use case relies on:
structured JSON logging and metrics, `/health`, a Dockerfile, a gateway service in
`docker-compose.yml`, a CI workflow, a `.http` file and the README.

Details: [research.md](research.md),
[data-model.md](data-model.md), [contracts/payments-api.yaml](contracts/payments-api.yaml),
[quickstart.md](quickstart.md).

## Technical Context

**Language/Version**: C# 12 / .NET 8 (`net8.0`, SDK 8.0.202 installed)

**Primary Dependencies**: ASP.NET Core Web API (controllers) and its shared framework
(`IHttpClientFactory`, Options validation, `ILogger` JSON console, `System.Diagnostics.Metrics`,
health checks, `ProblemDetails`, `TimeProvider`); Swashbuckle.AspNetCore for OpenAPI. Full list
with licences: research R13.

**Storage**: in-memory `ConcurrentDictionary` behind `IPaymentRepository` (the assessment allows
a test double; no database)

**Testing**: xUnit, hand-written fakes, `FakeTimeProvider`, `WebApplicationFactory`,
WireMock.Net, `FakeLogger` / `MetricCollector` (Microsoft.Extensions.Diagnostics.Testing),
coverlet.collector (coverage reported, no threshold); E2E against the Mountebank simulator

**Target Platform**: Linux container (`mcr.microsoft.com/dotnet/aspnet:8.0`, non-root) via
`docker compose`; also runs locally with `dotnet run`

**Project Type**: web service (REST API) – hexagonal, single project

**Performance Goals**: SC-006 – 95 % of outcomes < 2 s when the bank answers promptly;
Rejected < 1 s (no bank call on that path)

**Constraints**: bank call timeout configurable (default 10 s, 1–60 s); exactly one bank call per
valid request; no PAN/CVV in responses, storage, logs, `ToString()` or error messages; zero build
warnings; gateway host port 8090 (simulator keeps 8080/2525); no HTTPS redirection in the
container

**Scale/Scope**: take-home exercise; one merchant endpoint plus `/health` in UC1; concurrent
requests must be safe; data lost on restart (accepted)

No `NEEDS CLARIFICATION` remains – every open point is resolved in [research.md](research.md).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design, and re-run against
constitution **1.0.2** (API Design and Principle VIII amendments).*

| # | Principle | Gate | Result | Evidence |
|---|---|---|---|---|
| I | Requirements Fidelity | FRs trace to the assessment; no unrequested features (auth, idempotency, refunds, DB); operational features serve assessed competencies | ✅ | spec FRs cite sources; logs, metrics, `/health`, Docker and CI justified by XI/XII |
| II | Hexagonal Architecture | One project; `Domain/`, `Application/`, `Infrastructure/`, `Http/`; `Program.cs` the only composition root; interfaces only for driven ports; no `I<UseCase>UseCase`; value objects only where they remove a real risk; bank contract in `Infrastructure/`; rules in `Domain/` | ✅ | Project Structure; R7, R11 (masking by `ToString()` overrides instead of value objects) |
| III | Use-Case-Driven | UC1 = one feature, one concrete `ProcessPaymentService`; tasks labelled `[UC1]`; foundations limited to what UC1 needs | ✅ | the foundations (observability, Docker, CI) are needed to meet UC1's Definition of Done |
| IV | Test-First | Tests precede each implementation task in `tasks.md` | ✅ | enforced in `/speckit-tasks` |
| V | Behaviour-Focused Testing | One test project with `Unit/`, `Integration/`, `EndToEnd/`; hand-written fakes; AAA, naming, `[Theory]` boundaries, `FakeTimeProvider`; WireMock at the bank boundary covering authorized/declined/400/503/timeout/unreadable body; E2E tagged and excluded by default; coverage reported, no threshold; README explains the test strategy and the risks it covers | ✅ | Test strategy below; R12, R14 |
| VI | Clean Code & SOLID | Result types for expected outcomes; named constants; `TreatWarningsAsErrors`; `.editorconfig` untouched; `dotnet format` | ✅ | R5, R7, R11 |
| VII | Simplicity | 1 production + 1 test project; built-in features preferred; no MediatR/AutoMapper/FluentValidation/Polly/NSubstitute/generic repository; every dependency justified with its licence | ✅ | R13: one production package (Swashbuckle) |
| VIII | Card Data Protection | PAN/CVV never stored, logged or returned; `ToString()` overridden on `ProcessPaymentCommand`, `PaymentRequest`, `PostPaymentRequest`, `BankPaymentRequest`; a `FakeLogger` test proves no PAN/CVV in logs; **HttpLogging / W3C logging not enabled; `appsettings.json` keeps `Microsoft.AspNetCore` and `System.Net.Http.HttpClient` at `Warning`; an integration test proves a card number in a request path or body appears in no log entry** (1.0.2); the unreadable-body log holds binding paths only; Rejected never calls the bank; bank failures not Declined, not stored, not retried | ✅ | data-model "Configuration"; R3, R15 |
| IX | Input Validation | All fields validated, fail-closed, no coercion; all errors in one `ValidationProblemDetails`; `[Theory]` boundary tests per rule; bank response shape checked; options `ValidateOnStart` | ✅ | R2, R3, R6, R7 |
| X | Documentation | XML docs on controllers, DTOs and driven ports (`GenerateDocumentationFile` on, `CS1591` suppressed); OpenAPI covers every endpoint, required field, constraint and status code; README with the 10 required sections; `.http` file; Spec Kit linked as "how this was built" | ✅ | contract; README plan and `.http` file below |
| XI | Observability | JSON console logs with scopes; one entry per payment outcome (including `PaymentRequestUnreadable` for an unbindable body) and per bank call; no PAN/CVV; `rejected` counter scoped to requests reaching the service, unreadable bodies visible in `http.server.request.duration`; `traceId` in logs and `ProblemDetails`; `/health`; meter `PaymentGateway` with an outcome counter and a bank-duration histogram; no external stack | ✅ | R15; data-model "Log events", `PaymentGatewayMetrics` |
| – | API Design constraints (1.0.2) | `POST /api/payments` → `200`/`400`/`502`/`503`; `paymentStatus: "Rejected"` only on its `400`s (validation failures and unreadable bodies), never on another endpoint's `400`; every error response – including routing `404`/`405` via `UseStatusCodePages()` – is `ProblemDetails` with `traceId`; JSON camelCase, string enums, card fields as strings | ✅ | R2, R3, R5; contract `components/responses` |
| XII | Packaging & Hosting | Multi-stage Dockerfile, non-root; compose adds only a gateway service (simulator and `imposters/` unchanged; host port 8090); config by Options and environment variables; no HTTPS redirection; Swagger by flag; CI runs restore/build/format/tests/docker build | ✅ | R16, R17 |

**Result**: PASS – no violations; Complexity Tracking is empty.

## Project Structure

### Documentation (this feature)

```text
specs/001-process-payment/
├── spec.md
├── plan.md              # this file
├── research.md          # Phase 0 (R1–R17)
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/
│   └── payments-api.yaml
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 (/speckit-tasks – not created here)
```

### Source Code (repository root)

```text
PaymentGateway.sln
Dockerfile                                # sdk:8.0 build → aspnet:8.0 runtime, USER $APP_UID, EXPOSE 8080
.dockerignore
docker-compose.yml                        # bank_simulator unchanged; + payment_gateway (8090:8080)
.github/workflows/ci.yml                  # restore, build, format, test (Category!=E2E) + coverage, docker build
README.md                                 # rewritten – see "README plan"

src/PaymentGateway.Api/
├── PaymentGateway.Api.csproj             # Nullable, ImplicitUsings, TreatWarningsAsErrors,
│                                         #   GenerateDocumentationFile + NoWarn CS1591; Swashbuckle
├── PaymentGateway.Api.http               # authorized, declined, rejected, bank-unavailable examples
├── Program.cs                            # the only composition root: controllers + JSON options,
│                                         #   AddProblemDetails (traceId) + UseExceptionHandler +
│                                         #   UseStatusCodePages (routing 404/405 as ProblemDetails),
│                                         #   InvalidModelStateResponseFactory → UnreadableRequestHandler
│                                         #   (paymentStatus only for the processing action),
│                                         #   no HttpLogging / W3C logging,
│                                         #   typed HttpClient + AcquiringBankOptions (ValidateOnStart),
│                                         #   singletons (repository, metrics, TimeProvider.System),
│                                         #   JSON console logging with scopes, AddHealthChecks +
│                                         #   MapHealthChecks("/health"), Swagger when Swagger:Enabled;
│                                         #   no UseHttpsRedirection; `public partial class Program`
├── appsettings.json                      # AcquiringBank (BaseUrl, TimeoutSeconds), Swagger:Enabled=false,
│                                         #   LogLevel Microsoft.AspNetCore + System.Net.Http.HttpClient = Warning
├── appsettings.Development.json          # Swagger:Enabled=true
├── Domain/
│   ├── SupportedCurrencies.cs            # GBP, EUR, USD; Length = 3
│   ├── PaymentRequest.cs                 # validated request; Create aggregates all errors;
│   │                                     #   named limits; masked ToString(); CardNumberLastFour
│   ├── CreatePaymentRequestResult.cs     # PaymentRequest | non-empty list of ValidationError
│   ├── ValidationError.cs
│   ├── PaymentStatus.cs                  # Authorized, Declined
│   └── Payment.cs                        # recorded entity (safe data only)
├── Application/
│   ├── ProcessPaymentService.cs          # UC1 application service (concrete, no interface)
│   ├── ProcessPaymentService.Log.cs      # partial: LoggerMessage events 1000–1002
│   ├── ProcessPaymentCommand.cs          # raw input; masked ToString()
│   ├── ProcessPaymentResult.cs           # Processed | Rejected | BankFailed(BankFailureKind)
│   ├── IAcquiringBank.cs                 # driven port (XML-documented)
│   ├── BankAuthorizationResult.cs        # Authorized | Declined | Failed(BankFailureKind)
│   ├── BankFailureKind.cs                # Unavailable | Error
│   ├── IPaymentRepository.cs             # driven port: Add (XML-documented)
│   └── PaymentGatewayMetrics.cs          # meter "PaymentGateway": outcomes counter, bank duration histogram
├── Infrastructure/
│   ├── AcquiringBankClient.cs            # IAcquiringBank over HttpClient; logs + duration metric
│   ├── AcquiringBankClient.Log.cs        # partial: LoggerMessage events 2000–2001
│   ├── AcquiringBankOptions.cs           # BaseUrl, TimeoutSeconds (DataAnnotations, ValidateOnStart)
│   ├── BankPaymentRequest.cs             # snake_case contract (internal); masked ToString()
│   ├── BankPaymentResponse.cs            # snake_case contract (internal)
│   └── InMemoryPaymentRepository.cs      # ConcurrentDictionary; IPaymentRepository
└── Http/
    ├── PaymentsController.cs             # POST /api/payments – HTTP translation only (XML-documented)
    ├── PostPaymentRequest.cs             # nullable members; masked ToString() (XML-documented)
    ├── PaymentResponse.cs                # the payment representation (XML-documented)
    ├── PaymentRejectedProblemDetails.cs  # : ValidationProblemDetails + paymentStatus (400)
    ├── BankFailureProblemDetails.cs      # : ProblemDetails + errorCode (502/503)
    ├── PaymentResultMapper.cs            # the single result → HTTP translator (200/400/502/503);
    │                                     #   builds every Rejected / bank-failure / invalid-request body
    ├── UnreadableRequestHandler.cs       # unbindable body (InvalidModelStateResponseFactory): fixed
    │                                     #   messages; Rejected + paymentStatus on the processing action
    │                                     #   only, via PaymentResultMapper
    └── UnreadableRequestHandler.Log.cs   # partial: LoggerMessage event 1003 PaymentRequestUnreadable

test/PaymentGateway.Api.Tests/            # the template project, reused
├── PaymentGateway.Api.Tests.csproj       # Nullable, ImplicitUsings, TreatWarningsAsErrors; packages per R13
├── Unit/
│   ├── Domain/                           # PaymentRequest rules and boundaries ([Theory]), ToString
│   │                                     #   masking, Payment.Create, SupportedCurrencies
│   ├── Application/                      # ProcessPaymentService outcomes, logs (FakeLogger),
│   │                                     #   metrics (MetricCollector), ProcessPaymentCommand.ToString
│   ├── Infrastructure/                   # BankPaymentRequestTests (ToString masking)
│   ├── Http/                             # PostPaymentRequestTests (ToString masking)
│   ├── Architecture/                     # LayerDependencyTests: Principle II dependency rule and
│   │                                     #   technology-free core, read from IL (no extra package)
│   └── Fakes/                            # FakeAcquiringBank (records calls), FakePaymentRepository
├── Integration/
│   ├── ProcessPaymentEndpointTests.cs    # 200/400/502/503 through the real pipeline; traceId in
│   │                                     #   ProblemDetails; no PAN/CVV in any log (FakeLogger);
│   │                                     #   unreadable body: paymentStatus, one 1003 log, no metric
│   ├── ErrorFormatTests.cs               # unknown route (404) and wrong method (405: GET/PUT
│   │                                     #   /api/payments) → ProblemDetails with traceId
│   ├── CardDataLoggingTests.cs           # card number in a request path or body → in no log entry
│   │                                     #   (real logging configuration)
│   ├── AcquiringBankClientTests.cs       # adapter (resolved from the factory) vs WireMock:
│   │                                     #   authorized, declined, 400, 503, timeout, refused
│   │                                     #   (unused port), unreadable body; logs + duration metric
│   ├── HealthEndpointTests.cs            # GET /health → 200
│   ├── OpenApiDocumentTests.cs           # Swagger:Enabled flag; response schemas (typed error
│   │                                     #   bodies) and a description for every request field
│   ├── StartupValidationTests.cs         # invalid AcquiringBank options fail at startup
│   └── Fixtures/                         # PaymentGatewayFactory (WebApplicationFactory<Program>,
│                                         #   FakeLogging, FakeTimeProvider) + WireMock bank fixture
└── EndToEnd/
    ├── SimulatorGatewayFactory.cs        # real simulator URL, real clock, real logging
    └── ProcessPaymentJourneyTests.cs     # [Trait("Category","E2E")] vs the real simulator
```

**Removed from the template** (research R11): `test/PaymentGateway.Api.Tests/PaymentsControllerTests.cs`
and `Usings.cs` if unused; `src/PaymentGateway.Api/Enums/`, `Services/` and `Models/` (numeric
card fields, a repository storing the HTTP DTO); `Controllers/` (the controller moves to `Http/`)
and the template `GET` action (retrieval is re-delivered by UC2); `UseHttpsRedirection()` and
`UseAuthorization()` (no auth in scope). The test project's packages are upgraded
(`Microsoft.AspNetCore.Mvc.Testing` 6.0.24 → 8.0.x, xunit, Test SDK, coverlet.collector).

**Structure Decision**: one production project and one test project, as required by
Constitution Principles II, V and VII (research R11). UC2 adds `RetrievePaymentService`,
`IPaymentRepository.GetById`, a `GET` action and its tests without restructuring.

## Test strategy

| Level | Folder | Uses | Proves in UC1 | Risks covered |
|---|---|---|---|---|
| Unit | `Unit/` | Domain types and `ProcessPaymentService` with **hand-written fakes**; `FakeAcquiringBank` records its calls; `FakeTimeProvider`; `FakeLogger`; `MetricCollector` | every validation rule at its boundaries (`[Theory]` cases include card number 13/14/19/20 digits, CVV 2/3/4/5 digits, expiry month 0/1/12/13, expiry year last year/this year/9999/10000, current and previous month, amount 0/1, currency `GBP`/`gbp`/`JPY`); each use-case outcome; exactly one bank call for a valid request, none for a rejected one; outcome log and counter per result; `ToString()` masks the PAN and CVV | validation boundaries; card data leakage; a rejected payment reaching the bank |
| Integration | `Integration/` | the **real adapters** (`AcquiringBankClient`, `InMemoryPaymentRepository`) through `WebApplicationFactory<Program>`; **only the bank** is replaced, by WireMock at the HTTP boundary | 200 Authorized/Declined; 400 Rejected (rules and unbindable body, with `paymentStatus`, one `PaymentRequestUnreadable` log, no outcome metric); 502 Bank error (bank 400, unreadable body); 503 Bank unavailable (bank 503, timeout); `traceId` in `ProblemDetails`; routing `404`/`405` as `ProblemDetails` with `traceId`; no PAN/CVV in any log, including a card number sent in a request path or body; `/health`; startup validation | a bank failure misreported as Declined; serialization of the bank contract; card data leakage in logs (application and framework); inconsistent error format; misconfiguration |
| E2E | `EndToEnd/` | the gateway in-process against the **real simulator** (`docker compose up -d bank_simulator`); `[Trait("Category","E2E")]`, excluded by default | Authorized, Declined and Bank unavailable via `POST` only | drift between our bank contract and the real simulator |

Coverage is collected with coverlet.collector and reported; there is no threshold (Principle V).
Added in UC2: `IPaymentRepository.GetById`, an "add then retrieve" repository test and the
process-then-retrieve E2E journey.

## README plan (Principle X)

The README is rewritten; the template's "Instructions for candidates" are replaced.

| # | Section | Content for UC1 |
|---|---|---|
| 1 | Overview | what the gateway does; the Authorized / Declined / Rejected outcomes; bank failures |
| 2 | Run locally | simulator via `docker compose up -d bank_simulator`, `dotnet run`, Swagger URL |
| 3 | Run with Docker | `docker compose up --build`; ports (gateway 8090, simulator 8080/2525); environment variables |
| 4 | Test commands | unit, integration, E2E, coverage collection (quickstart §2) |
| 5 | Test strategy | the table above: what each level proves and which risks it covers |
| 6 | Architecture | Mermaid diagram: `Http` → `Application` → `Domain`, driven ports ← `Infrastructure` adapters, bank simulator; plus the payment flow sequence |
| 7 | API usage | `POST /api/payments` examples for 200/400/502/503; link to `PaymentGateway.Api.http` and Swagger |
| 8 | Observability | log events and fields (incl. `PaymentRequestUnreadable`), `traceId` correlation, metrics and `dotnet-counters` command – the `rejected` counter covers requests reaching the service; unreadable bodies appear as `400` on `api/payments` in `http.server.request.duration` (also the latency percentiles for SC-006 – research R18); framework log levels kept at `Warning` and why; `/health` |
| 9 | Design Decisions & Assumptions | 200 not 201; Rejected shape (`paymentStatus` only on `POST /api/payments`); every error is `ProblemDetails` with `traceId` (incl. routing errors); 502 vs 503; no retries; expiry rule; supported currencies; amount > 0; single project; TLS upstream; Swagger flag |
| 10 | Production next steps (not built) | idempotency keys, merchant authentication, persistent storage, PCI DSS scope, circuit breaker, OpenTelemetry exporters |

A final "How this was built" section links to `specs/` and `.specify/memory/constitution.md`.

## `.http` file

`src/PaymentGateway.Api/PaymentGateway.Api.http`, with `@base = http://localhost:5067` (switch to
`http://localhost:8090` for compose) and one request each for: **Authorized** (card ending 7),
**Declined** (card ending 8), **Rejected** (card `1234`, currency `gbp`, amount `0`),
**Bank unavailable** (card ending 0), plus `GET {{base}}/health`.

## Revision – alignment with constitution 1.0.2 and UC2 (2026-09-26)

- `paymentStatus: "Rejected"` only on `POST /api/payments` `400`s; the invalid-model factory is
  action-aware and other actions get a plain `ValidationProblemDetails` (research R3, R5;
  data-model `PaymentResultMapper`; contract).
- `PostPaymentResponse` renamed `PaymentResponse` (tree, data model, contract, UC2 references).
- Unreadable body: one `PaymentRequestUnreadable` (1003) log entry, not counted in the
  `PaymentGateway` meter; `rejected` scoped to requests reaching the service; unreadable bodies
  visible in `http.server.request.duration` (research R3, R15; data model; quickstart §6).
- Framework logs: no HTTP logging; `Microsoft.AspNetCore` and `System.Net.Http.HttpClient` at
  `Warning` in `appsettings.json`; integration test `CardDataLoggingTests` (card number in a path
  or body in no log entry) (research R15; data model "Configuration"; quickstart §5).
- `UseStatusCodePages()` moved into UC1 as a foundation; integration test `ErrorFormatTests`
  (unknown route, wrong method → `ProblemDetails` with `traceId`); contract "Errors" section and
  `components/responses/RoutingProblem` (research R5; quickstart §5).
- Constitution Check re-run against 1.0.2 (rows VIII, XI and API Design updated): PASS.

## Revision – `/speckit-analyze` remediation (2026-09-26)

- Tasks: test-first order restored (T013 split; T036 removed – each type is created in the Green
  step of the first test needing it; path-in-log test before the log-level task), T053 split in
  three Green steps, E2E factory with real clock and logging, extra-member edge case – see
  `tasks.md` "Revision".
- Error bodies are typed – `PaymentRejectedProblemDetails`, `BankFailureProblemDetails` – so the
  OpenAPI document matches the contract (research R2, R4, R12; data model).
- SC-006 is verified manually with `curl -w` and read from `http.server.request.duration`
  (research R18; quickstart §5).
- Tree updated with `CreatePaymentRequestResult`, `ProcessPaymentService.Log.cs`, the typed error
  bodies, `test/Unit/Infrastructure/`, `test/Unit/Http/`, `OpenApiDocumentTests.cs` and
  `SimulatorGatewayFactory.cs`.
- Constitution Check unchanged: PASS.

## Complexity Tracking

No constitution violations – nothing to justify.
