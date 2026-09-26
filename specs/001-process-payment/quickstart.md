# Quickstart: Process a Payment (UC1)

Validation guide proving UC1 works end to end. Contract:
[contracts/payments-api.yaml](contracts/payments-api.yaml). Rules: [data-model.md](data-model.md).

## Prerequisites

- .NET SDK 8.0.x (`dotnet --version`)
- Docker with Compose v2 (`docker compose version`)
- Optional: `dotnet tool install -g dotnet-counters` (to watch metrics)

## 1. Quality gates (no Docker needed)

Run from the repository root.

```bash
dotnet build -c Release                     # zero warnings (TreatWarningsAsErrors)
dotnet format --verify-no-changes           # .editorconfig compliance
```

## 2. Tests per level

```bash
# Unit – domain rules and ProcessPaymentService with hand-written fakes
dotnet test --filter "FullyQualifiedName~PaymentGateway.Api.Tests.Unit"

# Integration – real adapters in-process, bank replaced by WireMock
dotnet test --filter "FullyQualifiedName~PaymentGateway.Api.Tests.Integration"

# Default run (unit + integration, E2E excluded) with coverage collection (no threshold)
dotnet test --filter "Category!=E2E" --collect:"XPlat Code Coverage" --results-directory ./coverage

# End-to-end – needs the real simulator
docker compose up -d bank_simulator
dotnet test --filter "Category=E2E"
```

**Expected**: all tests pass. Coverage is written as Cobertura XML under
`coverage/<run-id>/coverage.cobertura.xml`; it is reported, not enforced.

## 3. Run locally (gateway on the host, simulator in Docker)

```bash
docker compose up -d bank_simulator              # simulator on http://localhost:8080
dotnet run --project src/PaymentGateway.Api      # http://localhost:5067 (launch profile)
```

- Swagger UI: `http://localhost:5067/swagger` (enabled by `Swagger:Enabled=true` in
  `appsettings.Development.json`).
- Health: `curl http://localhost:5067/health` → `200 Healthy`.
- Logs: JSON lines on the console, one per payment outcome and one per bank call, each with a
  `TraceId` scope.

## 4. Run with Docker (gateway + simulator)

```bash
docker compose up --build
```

- Gateway: `http://localhost:8090` (container port 8080; the simulator keeps 8080 and 2525).
- Swagger UI: `http://localhost:8090/swagger` (enabled by `Swagger__Enabled=true` in compose,
  not by the Development environment).
- `docker compose ps` shows `payment_gateway` as `healthy` once `/health` answers.
- Check it runs as non-root: `docker compose exec payment_gateway id` → `uid=1654(app)`.

## 5. Manual scenarios

Use the requests in `src/PaymentGateway.Api/PaymentGateway.Api.http` (VS Code REST Client,
Rider or Visual Studio), or `curl`. `{{base}}` is `http://localhost:5067` (local) or
`http://localhost:8090` (compose). Expiry `12/2030` is in the future.

| # | Scenario | Request change (vs. valid body below) | Expected |
|---|---|---|---|
| 1 | Authorized (US1) | card `2222405343248877` (ends 7) | `200`, `status: "Authorized"`, `cardNumberLastFour: "8877"`, `id` in body |
| 2 | Declined (US1) | card `2222405343248878` (ends 8) | `200`, `status: "Declined"` |
| 3 | Leading zeros (US1) | card `2222405343240012` (ends 2) | `200`, `cardNumberLastFour: "0012"` |
| 4 | Rejected – several fields (US2) | card `1234`, currency `gbp`, amount `0` | `400`, `paymentStatus: "Rejected"`, errors for `cardNumber`, `currency`, `amount`, plus `traceId` |
| 5 | Rejected – expired (US2) | `expiryMonth` / `expiryYear` = the month before today (e.g. `8` / `2026` on 2026-09-26) | `400`, error on expiry |
| 6 | Rejected – wrong type (US2) | `"amount": "ten"` | `400`, same Rejected shape |
| 7 | Bank unavailable (US3) | card `2222405343248870` (ends 0) | `503`, `errorCode: "bank_unavailable"`, no `paymentStatus` |
| 8 | Bank unavailable (US3) | stop the simulator: `docker compose stop bank_simulator` | `503`, `errorCode: "bank_unavailable"` |
| 9 | Unknown route | `GET {{base}}/api/unknown` | `404` `ProblemDetails` with `traceId`, no `paymentStatus` |
| 10 | Wrong method | `PUT {{base}}/api/payments` (any body) | `405` `ProblemDetails` with `traceId`, no `paymentStatus` |
| 11 | Card number in a path | `GET {{base}}/api/payments/4111111111111111` | an error `ProblemDetails`; `4111111111111111` appears in **no** console log line |

Valid body:

```bash
curl -i -X POST "{{base}}/api/payments" -H "Content-Type: application/json" -d '{
  "cardNumber": "2222405343248877",
  "expiryMonth": 12,
  "expiryYear": 2030,
  "currency": "GBP",
  "amount": 1050,
  "cvv": "123"
}'
```

**Latency (SC-006, research R18 – manual check, no automated test)**:

```bash
# valid payment – expected < 2 s
curl -s -o /dev/null -w "%{http_code} %{time_total}s\n" -X POST "{{base}}/api/payments" \
  -H "Content-Type: application/json" \
  -d '{"cardNumber":"2222405343248877","expiryMonth":12,"expiryYear":2030,"currency":"GBP","amount":1050,"cvv":"123"}'
# Rejected – expected < 1 s
curl -s -o /dev/null -w "%{http_code} %{time_total}s\n" -X POST "{{base}}/api/payments" \
  -H "Content-Type: application/json" -d '{"cardNumber":"1234"}'
```

Percentiles for a running gateway: `http.server.request.duration` (section 6).

**Check in every response and in the console logs**: the full card number and the CVV never
appear (FR-020) – also for scenario 6 with the card number in the unreadable body, and for
scenario 11. There is no "Request starting …" line, because `Microsoft.AspNetCore` and
`System.Net.Http.HttpClient` stay at `Warning` and HTTP logging is not enabled. Scenario 6 writes
one `PaymentRequestUnreadable` entry (field paths only). The `traceId` of an error response matches the `TraceId` of its log entries.
**Bank error (502)** cannot be produced with the simulator for a valid request; it is covered by
the integration tests (WireMock stub returning `400` and an unreadable body).

## 6. Metrics

With the gateway running locally (section 3):

```bash
dotnet-counters monitor -n PaymentGateway.Api --counters PaymentGateway
```

Send the scenarios above and watch:

- `paymentgateway.payments.outcomes` – one increment per request, by `result`
  (`authorized`, `declined`, `rejected`, `bank_unavailable`, `bank_error`);
- `paymentgateway.bank.request.duration` – bank call durations, by `outcome`.

`rejected` counts only requests that reached the use case (scenarios 4 and 5). Scenario 6 (unreadable
body) does not increment it; watch it as a `400` on route `api/payments` instead:

```bash
dotnet-counters monitor -n PaymentGateway.Api --counters Microsoft.AspNetCore.Hosting
```

(`http.server.request.duration`, tags `http.route`, `http.response.status_code`).

## 7. Startup validation

Set `AcquiringBank__BaseUrl=not-a-url` (or blank it) and run the gateway.
**Expected**: the application fails at startup with an options validation error (Principle IX).

## 8. CI

Push a branch: `.github/workflows/ci.yml` runs restore, build, format verification, tests
(`Category!=E2E`) with coverage collection, uploads the coverage file and runs `docker build`.
**Expected**: the workflow is green.
