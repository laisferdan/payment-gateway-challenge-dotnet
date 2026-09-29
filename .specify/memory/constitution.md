# Payment Gateway Constitution

The Payment Gateway is an API-based application that allows a merchant to offer a way for their
shoppers to pay for their products. It validates payment requests, forwards valid ones to the
acquiring bank, and lets merchants retrieve the details of previously made payments.

This constitution governs every specification, plan, task and line of code in this repository.
It is the first document every Spec Kit command (`/speckit-specify`, `/speckit-clarify`,
`/speckit-plan`, `/speckit-tasks`, `/speckit-analyze`, `/speckit-implement`) MUST read and
comply with.

**Assessed competencies.** The solution is reviewed on six competencies. Every principle serves
at least one of them, and a rule that serves none does not belong here – the assessment says
"we do not want to encourage over-engineering".

| Competency | Served by |
|---|---|
| Observability | XI |
| Packaging / Hosting | XII |
| Technical Documentation | I, X |
| API Design | I, IX, API Design constraints |
| Code Design | II, III, VI, VII, VIII |
| Testing Mindset | IV, V, VIII |

## Core Principles

### I. Requirements Fidelity

- The assessment in `docs/requirements/assessment.md` is the single source of truth for
  functional requirements. Every spec MUST trace each of its requirements back to it.
- The API design and architecture MUST focus on meeting those functional requirements.
  Features not requested by the assessment (e.g. refunds, captures, authentication, real
  databases) MUST NOT be built unless a spec records the reason and it is approved.
- Operational features that serve an assessed competency – logging, metrics, a health endpoint,
  a container image, CI (Principles XI and XII) – are in scope and need no further justification.
- Where the assessment is silent or ambiguous (e.g. HTTP status for a bank outage), the
  decision MUST be resolved during `/speckit-clarify` or `/speckit-plan` and recorded as an
  assumption in the spec and in the README's "Design Decisions & Assumptions" section.
- The code MUST compile and all non-E2E tests MUST pass on every commit to `main`.

**Rationale**: the reviewers assess the solution against the assessment; traceability makes
every design choice defensible during the review.

### II. Hexagonal Architecture (Ports & Adapters)

There is **one production project**, `src/PaymentGateway.Api`. The hexagon is expressed as
folders and namespaces inside it:

| Folder / namespace | Hexagon role | May depend on |
|---|---|---|
| `Domain/` | Core: entities, domain rules, constants | nothing (BCL only) |
| `Application/` | Core: one application service per use case, driven ports, result types | Domain |
| `Infrastructure/` | Driven adapters: acquiring bank HTTP client, in-memory repository | Application, Domain |
| `Http/` | Driving adapter: controllers, request/response DTOs, result → HTTP mapping | Application, Domain |

- `Program.cs` is the only composition root; wiring happens nowhere else.
- The dependency rule points inwards only: a dependency on an outer namespace from `Domain/` or
  `Application/` (or on `Http/` from `Infrastructure/` and vice versa) is a constitution
  violation. It is enforced by architecture tests (`Unit/Architecture/`), which read every
  type's signatures and IL, and by code review.
- **Folders inside a layer are named by role, never by kind.** Every file of a layer lives in a
  sub-folder named after what it is *for*:

  | Role | Example |
  |---|---|
  | a domain concept | `Domain/Payments/`, `Domain/PaymentRequests/`, `Domain/CardData/` |
  | a use case | `Application/ProcessPayment/` (command, service, result, log partial) |
  | the driven ports and their contract types | `Application/Ports/` |
  | a cross-cutting concern owned by a use case | `Application/ProcessPayment/PaymentMetrics.cs` |
  | a driven adapter, named after its capability | `Infrastructure/AcquiringBank/`, `Infrastructure/Persistence/` |
  | an API resource | `Http/Payments/` (controller, DTOs, result → HTTP mapping) |

  Folders named after what a file *is* – `Services/`, `Repositories/`, `Clients/`,
  `Controllers/`, `Requests/`, `Responses/`, `Results/`, `Commands/`, `Models/`, `Dtos/`,
  `Interfaces/`, `Helpers/`, `Utils/`, `Common/`, `Shared/`, `Logs/` – MUST NOT be used. No type
  lives directly in a layer's root folder, and `Program.cs` is the only source file at the
  project root. Namespaces match folders; a partial file (e.g. `ProcessPaymentService.Log.cs`)
  sits next to its class. Unit tests mirror the production folders. Architecture tests enforce
  that no type is in a layer-root namespace and that no namespace segment is a kind name.
- **A role folder holds only what is specific to that role** (DRY). As soon as a second role
  needs something, it moves to its single owner: business rules and their messages to
  `Domain/`, capabilities the core needs to `Application/Ports/`, a cross-cutting concern to its
  own role folder. Similar-looking types that change for different reasons – e.g. the merchant's
  request DTO, the use-case command and the bank's request contract – are separate contracts,
  not duplication, and MUST NOT be merged across layers.
- **Domain** MUST NOT use ASP.NET Core, HTTP, JSON, logging or persistence types.
- **Interfaces exist only for driven ports** – capabilities the core needs from the outside,
  named after the capability, never the technology (`IAcquiringBank`, `IPaymentRepository`, not
  `IBankHttpClient`). Use cases are **concrete application services** (e.g.
  `ProcessPaymentService`); an `I<UseCase>UseCase` interface is added only when a second
  implementation or consumer exists.
- **Value objects are optional**: introduce one only when it removes a real risk (e.g. a card
  number whose `ToString()` must never print the full value), not for every primitive.
- The acquiring bank's snake_case contract stays inside `Infrastructure/`; HTTP DTOs, status-code
  mapping and serialization settings stay inside `Http/`; business validation rules from the
  assessment stay in `Domain/`, unit-testable without HTTP. `Http/` MAY only reject payloads it
  cannot bind (Principle IX).

**Rationale**: the acquiring bank and the storage are simulated today and will change tomorrow;
ports isolate the core from them. Folders give the same separation as projects at a fraction of
the ceremony, which is what a single small service needs. Role folders are what lets that hold as
the service grows: a new use case, adapter or resource adds one folder instead of growing every
kind folder, what changes together lives together, and a role folder has clean seams to become
its own project should a future amendment of Principle VII allow more than one.

### III. Use-Case-Driven Development

- Work is decomposed into use cases. Each use case is one Spec Kit feature
  (`specs/<NNN>-<use-case-name>/`) with its own `spec.md`, `plan.md` and `tasks.md`.
- The initial use cases derived from the assessment are:
  - **UC1 – Process a payment** (outcomes: Authorized, Declined, Rejected)
  - **UC2 – Retrieve a payment's details**
- A feature that changes an existing use case without introducing a new one is its own Spec
  Kit feature with its own `spec.md`,
  `plan.md` and `tasks.md`; its stories are labelled with the extended use case ID (`[UC1]`)
  and it MUST keep the extended use case's existing behaviour and tests intact.
- Within `tasks.md`, the "user story" grouping of the Spec Kit template maps 1:1 to use cases
  and MUST be labelled with the use case ID (e.g. `[UC1]`) instead of `[US1]`.
- Each use case MUST be independently implementable, testable and demonstrable.
  Foundational tasks (restructuring, shared domain types, hosting) are allowed only to the extent
  the use case being delivered needs them.
- Each use case is implemented as a single concrete application service in its own role folder,
  `Application/<UseCase>/` (Principle II).

**Rationale**: use cases map directly to the merchant's needs and to reviewable, incremental
deliveries.

### IV. Test-First Development (NON-NEGOTIABLE)

- TDD is mandatory for all production code: **Red → Green → Refactor**.
  1. Write a test that expresses the next behaviour and watch it fail for the right reason.
  2. Write the minimum code to make it pass.
  3. Refactor production and test code while keeping all tests green.
- This principle overrides the Spec Kit template note that tests are optional: in this project
  tests are always required, and in `tasks.md` every implementation task MUST be preceded by
  the test task(s) that drive it.
- No production code may be merged without the tests that drove it.

**Rationale**: TDD keeps the design driven by behaviour, keeps the code minimal and produces the
safety net needed to refactor freely.

### V. Behaviour-Focused Testing

There is **one test project**, `test/PaymentGateway.Api.Tests` (the existing project, reused),
with three folders:

**`Unit/`** – the base of the pyramid:
- MUST test observable behaviour through the public API of the unit (domain type or application
  service), never private methods or incidental implementation details.
- Driven ports are replaced by **hand-written fakes**. A fake MAY record its calls when the
  interaction is itself the behaviour (e.g. "a rejected payment never calls the acquiring bank",
  "exactly one bank call").
- MUST follow **Arrange / Act / Assert**, with explicit `// Arrange`, `// Act`, `// Assert`
  sections, exactly one Act per test and no conditional logic or loops in tests.
- MUST be named `<Unit>_<Scenario>_<ExpectedBehaviour>`
  (e.g. `ProcessPayment_WhenCardIsExpired_ReturnsRejectedWithoutCallingBank`).
- MUST be deterministic: time is injected via `TimeProvider` (`FakeTimeProvider` in tests);
  no real network, clock, randomness or shared mutable state.
- Boundaries and data variations of the same behaviour MUST use `[Theory]` rather than
  duplicated tests.

**`Integration/`**:
- MUST exercise the API in-process (`WebApplicationFactory`) through the real
  Http → Application → Domain → Infrastructure path, with the real adapters.
- Only the acquiring bank is replaced, at the HTTP boundary, by WireMock.Net, so the real bank
  adapter and its serialization are exercised.
- The bank adapter MUST be covered for every simulator outcome: authorized, declined,
  400 Bad Request and 503 Service Unavailable, plus timeout and an unreadable body.
- Logs and metrics required by Principle XI are asserted with `FakeLogger` / `MetricCollector`.

**`EndToEnd/`**:
- MUST exercise merchant journeys against the real bank simulator (`docker compose up`).
- MUST be tagged `[Trait("Category", "E2E")]` and runnable with a single documented command
  (`dotnet test --filter "Category=E2E"`). CI runs them against the simulator it starts with
  `docker compose`; a plain `dotnet test` includes them, so it needs the simulator running.

**Coverage** is collected by coverlet.collector as Cobertura XML and published as a CI
artifact; it is **not enforced** by a threshold.
Tests that execute code without asserting behaviour are not acceptable.

The README MUST explain the test strategy: what each level proves and which risks it covers –
at least card data leakage, a bank failure misreported as Declined, and validation boundaries.

**Rationale**: behaviour-focused tests survive refactoring; naming the risks each level covers
shows a testing mindset better than a coverage percentage.

### VI. Clean Code & SOLID

- **Single Responsibility**: one use case per application service; one reason to change per
  class; controllers only translate HTTP.
- **Open/Closed**: new outcomes or rules are added by extending the domain model, not by
  editing unrelated adapters.
- **Liskov Substitution**: every adapter and fake MUST honour its port's contract.
- **Interface Segregation**: driven ports are small and specific to what the core needs.
- **Dependency Inversion**: the core depends on driven ports it owns; adapters depend on the
  core; wiring happens only in `Program.cs`.
- Names MUST reveal intent and use the ubiquitous language of the assessment
  (Merchant, Payment, Acquiring Bank, Authorized, Declined, Rejected).
- Methods are small and do one thing; guard clauses over nested conditionals; no magic numbers
  or strings (limits and supported currencies are named constants in `Domain/`).
- Expected business outcomes (rejected, declined, not found, bank unavailable) MUST be modelled
  as explicit result types, not exceptions. Exceptions are reserved for truly exceptional,
  unrecoverable situations.
- Nullable reference types are enabled and the build MUST produce zero warnings
  (`TreatWarningsAsErrors`). No commented-out code, no dead code.
- Inline comments explain *why*, never restate *what* the code does.
- The provided `.editorconfig` MUST NOT be modified and `dotnet format --verify-no-changes`
  MUST pass.

**Rationale**: the reviewers explicitly value simple, maintainable code; these rules make that
measurable.

### VII. Simplicity – No Over-Engineering

- Allowed projects: **one production project and one test project**. Nothing else.
- YAGNI applies everywhere: every abstraction MUST have at least one real consumer and a reason
  to exist now. Speculative generality is forbidden.
- **Prefer built-in platform features** over libraries: `ILogger`, `System.Diagnostics.Metrics`,
  health checks, `ProblemDetails`, Options validation, `IHttpClientFactory`, `TimeProvider`.
- NOT permitted without an approved amendment: mediator/pipeline libraries (e.g. MediatR),
  object mappers (e.g. AutoMapper), FluentValidation, Polly/resilience libraries, mocking
  libraries (e.g. NSubstitute), generic repositories, CQRS buses, event sourcing, message
  brokers, real databases.
- A new NuGet dependency MUST be justified in the plan, including its licence; commercially
  licensed libraries (e.g. FluentAssertions v8+) MUST NOT be used.
- Mapping between layers is written by hand and kept next to the adapter that needs it.

**Rationale**: the assessment explicitly states "we do not want to encourage over-engineering";
complexity must earn its place.

### VIII. Card Data Protection

- The full card number (PAN) and CVV MUST NOT be persisted, logged, or returned in any
  response. They exist only in memory for the duration of the request and are passed to the
  acquiring bank adapter only.
- Stored payments and API responses expose only the last four card digits, represented as a
  `string` so leading zeros are preserved (card number, CVV and last four are never numeric
  types).
- Log messages, exception messages, `ToString()` output and validation error messages MUST NOT
  include the PAN or CVV. **Every type that carries card data** (commands, validated requests,
  value objects, adapter request models) **MUST override `ToString()`** so the PAN and CVV are
  never printed – C# records print every property by default.
- The absence of the PAN and CVV from logs MUST be verified by a test (`FakeLogger`).
- Framework request logging (`HttpLogging`) MAY log one entry per request with **only** the
  method, status code and duration – never the path, query, headers or body, because they can
  contain a pasted card number. `Microsoft.AspNetCore.Hosting.Diagnostics` MUST stay off (its
  logging scope carries the path), W3C logging MUST NOT be enabled, and the other
  `Microsoft.AspNetCore` and `System.Net.Http.HttpClient` log categories MUST stay at `Warning`
  or above. An integration test MUST send a card number in a request path and assert that it
  appears in no log entry.
- The bank's authorization code is not card data: it is stored with the payment for
  reconciliation and disputes, but not returned, because the assessment's response fields do not
  include it.
- Rejected requests MUST NOT call the acquiring bank and MUST NOT create a stored payment.
- Acquiring bank failures (e.g. 503, timeout) MUST NOT be reported as `Declined` nor stored as a
  processed payment. A payment request MUST NOT be retried automatically, because a retry could
  charge the shopper twice.

**Rationale**: returning or storing a full card number is "a serious compliance risk" per the
assessment; misreporting a bank outage as a decline misleads the merchant.

### IX. Input Validation

- Every user input MUST be validated before it is used: every field of every request body,
  every route and query parameter, and every header the service reads. There are no trusted
  inputs from merchants. A route constraint is enough for an identifier (e.g. `{id:guid}` on
  retrieval): a malformed id matches no route.
- Validation is fail-closed: an input that is missing, malformed, out of range or of an
  unexpected type MUST be rejected; payment request values MUST NOT be silently coerced,
  truncated, trimmed or defaulted into validity. Numeric fields MUST be JSON numbers: a number
  sent as a string (`"amount": "1050"`) is Rejected.
- Payment requests MUST be validated against every rule in the assessment (card number,
  expiry month, expiry year, expiry month + year in the future, currency in the supported
  list, amount as an integer in minor units, CVV). A request failing any rule results in a
  **Rejected** outcome, does not call the acquiring bank and is not stored (Principle VIII).
- Validation MUST report all failing fields in a single response, using a consistent error
  shape (RFC 7807 `ProblemDetails` / `ValidationProblemDetails`), with messages that name the
  field and the rule but never echo the PAN or CVV.
- Rules live in `Domain/` as described in Principle II; each limit is a named constant, and every
  rule MUST have unit tests covering its valid and invalid boundaries (`[Theory]`).
- Data received through driven adapters (e.g. acquiring bank responses) and application
  configuration are also untrusted: bank responses MUST be checked for the expected shape before
  being mapped into the core, and options MUST be validated at startup (`ValidateOnStart`) so a
  misconfigured service fails fast.

**Rationale**: the gateway's first responsibility in the assessment is "validating requests";
rejecting bad input early protects the acquiring bank, the merchant and the shopper, and
consistent errors make integration predictable for merchants.

### X. Documentation

- **XML documentation comments are required only on**:
  - the public HTTP contract – controllers, actions and request/response DTOs – because they
    feed the OpenAPI document;
  - driven ports (e.g. `IAcquiringBank`, `IPaymentRepository`) – they are the contracts
    adapters implement.
  Elsewhere, comments are written only where intent is not obvious from names and types.
- `GenerateDocumentationFile` stays on (it feeds OpenAPI) with `CS1591` suppressed, so missing
  docs outside the required places do not fail the build.
- The OpenAPI document MUST describe every endpoint, request and response schema, required
  field, request header, field constraint and possible status code.
- The **README** is the primary documentation and MUST contain:
  1. Overview
  2. Run locally
  3. Run with Docker (`docker compose up`)
  4. Test commands, per level (unit, integration, E2E) and coverage collection
  5. Test strategy (Principle V)
  6. Architecture diagram (Mermaid) showing the hexagon and the request flow
  7. API usage with examples, plus a `.http` file in the repository
  8. Observability: what is logged and measured, and how to see it
  9. Design Decisions & Assumptions
  10. Production next steps (not built): merchant authentication, persistent storage, PCI DSS
      scope, circuit breaker, OpenTelemetry exporters
- Spec Kit artifacts (`specs/`, `.specify/`) are referenced from the README as "how this was
  built", not as the primary documentation.
- Documentation is part of the change: a behaviour change is not done until its documentation
  is updated.

**Rationale**: the assessment asks for documented design considerations; a reviewer must be able
to run, test and understand the service from the README alone, without reading every file.

### XI. Observability

- **Structured logging** with `ILogger`: JSON console formatter with scopes enabled.
  - One entry per payment outcome: `paymentId` (when one exists), `status`, `currency`,
    `amount`.
  - One entry per acquiring bank call with its duration: `BankCallCompleted` (outcome) or
    `BankCallFailed` (failure kind, HTTP status). A bank failure produces exactly one entry at
    `Warning` (unavailable, error) or `Error` (outcome unknown – the shopper may have been charged);
    the use case's own outcome entry for it is `Information`.
  - Never the PAN or CVV (Principle VIII). High-performance `LoggerMessage` definitions are
    preferred.
  - One entry per HTTP request with its method, matched route template (never the raw path) and
    status, via an `IHttpLoggingInterceptor`.
- **Correlation**: the request `traceId` appears in every log entry of the request (logging
  scope), in every `ProblemDetails` response and in an `X-Trace-Id` header on every response, and
  it is propagated to the acquiring bank as `traceparent`, so any merchant-reported payment can be
  found in the logs.
- **Health**: a liveness endpoint `GET /health` via `AddHealthChecks()`.
- **Metrics** via `System.Diagnostics.Metrics` and `IMeterFactory`, meter name `PaymentGateway`:
  - a counter `paymentgateway.payments.outcomes`, tagged `result` (`authorized`, `declined`,
    `rejected`, `bank_unavailable`, `bank_error`, `bank_outcome_unknown`); a replayed idempotent
    result is not counted again;
  - acquiring bank call duration comes from the built-in `http.client.request.duration`
    histogram – no custom duration instrument.
  Metrics are viewable with `dotnet-counters` (documented in the README).
- **No external observability stack** (OpenTelemetry exporters, Prometheus, Grafana): listed as a
  production next step.

**Rationale**: a payment gateway must be diagnosable in production; the platform's built-in
logging, metrics and health checks give that without new dependencies.

### XII. Packaging & Hosting

- **Dockerfile**: multi-stage (`mcr.microsoft.com/dotnet/sdk:8.0` build →
  `mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled` runtime), running as a **non-root** user, with
  nothing else installed in the runtime image (no shell, no `curl`). `GET /health` is for the
  orchestrator's HTTP probe; compose declares no healthcheck.
- **docker-compose.yml** runs the gateway and the bank simulator with one `docker compose up`.
  The existing simulator service and `imposters/` MUST NOT change; only a gateway service is
  added. The gateway's host port MUST NOT clash with the simulator's ports (8080, 2525); inside
  the compose network the gateway reaches the bank at `http://bank_simulator:8080`.
- **Configuration** only through Options bound from configuration and environment variables
  (e.g. `AcquiringBank__BaseUrl`); no hard-coded URLs.
- **No HTTPS redirection** in the container: TLS is terminated upstream (documented assumption).
- **Swagger** is enabled by a configuration flag (e.g. `Swagger__Enabled=true`), never by setting
  `ASPNETCORE_ENVIRONMENT=Development` in the container, because Development enables the
  developer exception page and would leak exception details.
- **CI**: a GitHub Actions workflow runs restore, build (warnings as errors),
  `dotnet format --verify-no-changes`, unit and integration tests with coverage collection,
  publishes the Cobertura coverage file as an artifact, starts the bank simulator with
  `docker compose` and runs the E2E tests against it, and runs `docker build`.

**Rationale**: reviewers must be able to run the whole system with one command, and every change
must be verified automatically; the container follows the platform's secure defaults.

## Technical Constraints

- **Runtime**: .NET 8 (`net8.0`), ASP.NET Core Web API (controllers), C# with nullable reference
  types enabled.
- **Testing stack**: xUnit; hand-written fakes; `Microsoft.AspNetCore.Mvc.Testing`
  (`WebApplicationFactory`); WireMock.Net; `Microsoft.Extensions.TimeProvider.Testing`
  (`FakeTimeProvider`); `Microsoft.Extensions.Diagnostics.Testing` (`FakeLogger`,
  `MetricCollector`); coverlet.collector for coverage (Cobertura XML, published as a CI
  artifact, no threshold).
- **Payment storage**: in-memory, thread-safe storage only, as permitted by the assessment. It
  is lost on restart (documented limitation).
- **Supported currencies**: `GBP`, `EUR`, `USD` (decided in the UC1 spec), defined once in
  `Domain/`.
- **Acquiring bank contract** (bank simulator, `POST {BaseUrl}/payments`):
  - Request: `card_number`, `expiry_date` (`MM/yyyy`), `currency`, `amount`, `cvv`.
  - Response: `authorized` (bool), `authorization_code` (string).
  - Behaviour: card ending in an odd digit returns authorized; an even digit returns
    unauthorized; `0` returns 503; missing fields return 400.
  - The base URL and timeout are configuration (Options pattern), never hard-coded.

### API Design

- Resource `/api/payments`:
  - `POST /api/payments` → `201 Created` with the payment, `status` = `Authorized` | `Declined`,
    and a `Location` header pointing at it (it is a new, retrievable resource).
  - `GET /api/payments/{id}` → `200 OK` with the payment, or `404 Not Found` when no payment has
    that id. A malformed id (not a GUID) matches no route (`{id:guid}`); a second, unconstrained
    fallback route catches it and returns the byte-for-byte same `404` body as a well-formed but
    unknown id, without echoing the value, because it names no payment either.
- **No idempotency key**: every `POST /api/payments` is a new payment. See the README's
  [Unknown outcomes and double charges](../../README.md#unknown-outcomes-and-double-charges) for why
  an in-memory, per-process key was rejected as not worth its complexity.
- Errors are `ProblemDetails` (with `traceId`):
  - `400` Rejected (`POST /api/payments`) – all invalid fields in `errors`, plus
    `paymentStatus: "Rejected"`;
  - `502` bank error (an error status); `503` bank unavailable (`503`, or a connection never
    established); `504` outcome unknown (timeout, connection lost after sending, or a `200` whose
    body cannot be read – the payment may have been authorized) – each with an `errorCode`;
  - `404` payment not found (unknown or malformed id); `500` unexpected error, with no internal
    details.
- `paymentStatus: "Rejected"` appears **only** on `400` responses of `POST /api/payments`
  (validation failures and unreadable bodies), never on any other response.
- **Every** error response of the service is a `ProblemDetails` with `traceId`, including
  routing errors produced by the framework (e.g. `404` on an unknown route, `405 Method Not
  Allowed`), via `UseStatusCodePages()` on top of `AddProblemDetails()`.
- JSON is camelCase; enums are strings; card number, CVV and last four digits are strings.
- `GET /health` is operational and not part of the merchant contract.

### Repository layout

```text
src/
  PaymentGateway.Api/
    Domain/           # role folders only (Principle II), e.g. Payments/, PaymentRequests/
    Application/      # e.g. Ports/, ProcessPayment/, RetrievePayment/
    Infrastructure/   # e.g. AcquiringBank/, Persistence/
    Http/             # e.g. Payments/
    Program.cs        # the only source file at the project root
    PaymentGateway.Api.http
test/
  PaymentGateway.Api.Tests/
    Unit/             # mirrors the production role folders
    Integration/
    EndToEnd/
docs/requirements/assessment.md
specs/<NNN>-<use-case>/
imposters/            # bank simulator configuration – MUST NOT be changed
.github/workflows/ci.yml
Dockerfile
docker-compose.yml    # simulator service unchanged; gateway service added
README.md
```

- **Language**: all code, specs, plans, tasks, documentation and commit messages are written in
  English.

## Development Workflow & Quality Gates

1. **Specify** – `/speckit-specify` once per use case; the spec describes merchant-facing
   behaviour and acceptance scenarios, not implementation.
2. **Clarify** – `/speckit-clarify` resolves every ambiguity before planning; decisions are
   recorded as assumptions.
3. **Plan** – `/speckit-plan` MUST pass the Constitution Check. Any deviation is recorded in
   "Complexity Tracking" with justification, or the plan is rejected.
4. **Tasks** – `/speckit-tasks` groups tasks by use case (`[UC<n>]`); test tasks precede the
   implementation tasks they drive; each task is small enough for one Red-Green-Refactor cycle.
5. **Analyze** – `/speckit-analyze` MUST report no constitution violations before
   implementation starts.
6. **Implement** – `/speckit-implement` follows TDD strictly. Small, focused commits using
   Conventional Commits (e.g. `test: ...`, `feat: ...`, `refactor: ...`).

**Definition of Done for a use case** (all MUST hold):

- Solution builds with zero warnings; `dotnet format --verify-no-changes` passes.
- All unit and integration tests pass; E2E tests pass against the simulator where applicable.
- Coverage is collected as Cobertura XML and published as a CI artifact (no threshold).
- No PAN/CVV in logs, responses, storage or `ToString()` output (verified by tests).
- Every user input of the use case is validated, with tests for each rule's boundaries.
- The use case emits its outcome log entries and metrics (Principle XI), verified by tests.
- The OpenAPI document describes every endpoint, field constraint and status code of the use
  case.
- The service runs with `docker compose up` together with the simulator.
- CI is green.
- README sections affected by the use case are updated (Principle X).

## Governance

- This constitution supersedes all other practices, templates and agent defaults in this
  repository. When a Spec Kit template conflicts with it, the constitution wins.
- Amendments are made only through `/speckit-constitution`, MUST state their rationale, and
  MUST bump the version using semantic versioning:
  - MAJOR for removed or redefined principles;
  - MINOR for new principles or materially expanded guidance;
  - PATCH for clarifications and wording.
- Every plan's Constitution Check and every review MUST verify compliance with all principles.
  Unjustified complexity is a blocking finding.
- Runtime development guidance for AI agents lives in the agent guidance file generated by
  Spec Kit and MUST NOT contradict this constitution.

**Version**: 1.4.2 | **Ratified**: 2026-09-26 | **Last Amended**: 2026-09-28

<!-- Amendment 1.4.2: removed the Idempotency-Key API contract — the in-memory, per-process
     implementation was cut as not worth its complexity (see the README's Unknown outcomes and
     double charges section). Clarification only; no principle added, removed or redefined. -->
