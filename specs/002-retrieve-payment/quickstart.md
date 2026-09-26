# Quickstart: Retrieve a Payment's Details (UC2)

Validation guide proving UC2 works end to end. Contract:
[contracts/payments-api.yaml](contracts/payments-api.yaml). Design: [data-model.md](data-model.md).
Build, run and Docker steps are the same as UC1 – see
[UC1 quickstart](../001-process-payment/quickstart.md) §1, §3 and §4.

## Prerequisites

- UC1 implemented (processing records the payments UC2 retrieves).
- .NET SDK 8.0.x, Docker with Compose v2.

## 1. Tests per level

```bash
# Unit – RetrievePaymentService (fake repository) and InMemoryPaymentRepository add-then-get
dotnet test --filter "FullyQualifiedName~PaymentGateway.Api.Tests.Unit"

# Integration – GET through the real pipeline; bank replaced by WireMock (used only by POST)
dotnet test --filter "FullyQualifiedName~PaymentGateway.Api.Tests.Integration"

# End-to-end – process then retrieve against the real simulator
docker compose up -d bank_simulator
dotnet test --filter "Category=E2E"
```

**Expected**: all tests pass, including UC1's (the processing `400` still carries
`paymentStatus: "Rejected"`).

## 2. Manual scenarios

Start the gateway (UC1 quickstart §3 or §4). `{{base}}` is `http://localhost:5067` (local) or
`http://localhost:8090` (compose). The `.http` file contains the same requests.

First process a payment and keep its id:

```bash
curl -s -X POST "{{base}}/api/payments" -H "Content-Type: application/json" -d '{
  "cardNumber": "2222405343240017", "expiryMonth": 12, "expiryYear": 2030,
  "currency": "GBP", "amount": 1050, "cvv": "123"
}'
# → 200 {"id":"<ID>","status":"Authorized","cardNumberLastFour":"0017",...}
```

| # | Scenario | Request | Expected |
|---|---|---|---|
| 1 | Found (US1) | `GET /api/payments/<ID>` | `200`, body **identical** to the `POST` response |
| 2 | Declined (US1) | process card `2222405343248878`, then `GET` its id | `200`, `status: "Declined"` |
| 3 | Leading zeros (US1) | scenario 1 (card ends `0017`) | `cardNumberLastFour: "0017"` |
| 4 | Other notations (US1) | `<ID>` in uppercase; without hyphens; as `{<ID>}` (URL-encoded `%7B…%7D`) | `200`, same payment, `id` returned in lowercase canonical form |
| 5 | Repeatable (US1) | scenario 1 three times | three identical `200` bodies |
| 6 | Not found (US2) | `GET /api/payments/00000000-0000-0000-0000-000000000000` | `404`, `title: "Payment not found"`, `traceId`, no payment fields |
| 7 | Not found after restart (US2) | restart the gateway, repeat scenario 1 | `404` |
| 8 | Invalid id (US3) | `GET /api/payments/abc` | `400`, `title: "Invalid payment id"`, `errors.id`, `traceId`, **no** `paymentStatus` |
| 9 | Invalid id, card-like (US3) | `GET /api/payments/4111111111111111` | `400`; `4111111111111111` appears neither in the body nor in the console logs |
| 10 | Empty id (Edge) | `GET /api/payments/` | `405` `ProblemDetails` with `traceId` |
| 11 | Bank down (Edge) | `docker compose stop bank_simulator`, repeat scenario 1 | `200` – retrieval never calls the bank |

**Check in the console logs**: one entry per retrieval – `PaymentRetrieved` (3000),
`PaymentNotFound` (3001) or `PaymentIdInvalid` (3002) – each with the `TraceId` shown in the
error response; no full card number, CVV or raw invalid id anywhere.

## 3. Latency (SC-004)

```bash
curl -s -o /dev/null -w "%{http_code} %{time_total}s\n" "{{base}}/api/payments/<ID>"
```

**Expected**: well under 0.5 s (an in-memory lookup; no bank call).

## 4. API documentation

Open Swagger UI (`{{base}}/swagger`): `GET /api/payments/{id}` lists the `id` parameter with its
accepted forms and the `200`, `400`, `404` and `500` responses with their schemas. The README's
API usage, Design Decisions, Observability and Production next steps sections cover retrieval.
