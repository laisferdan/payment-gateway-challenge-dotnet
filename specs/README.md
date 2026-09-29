# Specs – how this was built

Each folder is one use case, specified, planned and implemented with Spec Kit, test first:

- [`001-process-payment`](001-process-payment/) – UC1, process a payment
- [`002-retrieve-payment`](002-retrieve-payment/) – UC2, retrieve a payment

They record the design **as planned**. The code, the [README](../README.md) and the
[constitution](../.specify/memory/constitution.md) are current; where they differ from a spec, the
spec is superseded. The bank failure classification below is also corrected in the specs
themselves, because it decides whether a merchant may retry a payment.

## Superseded since the plan

| Planned | Now | Why |
|---|---|---|
| A bank timeout is *unavailable* (`503`) | A timeout, a connection lost after sending, or a `200` the gateway cannot read is *outcome unknown* (`504` `bank_outcome_unknown`) | The bank may have authorized it; only a `503` or a connection never made invites a retry |
| An unreadable `200` from the bank is a *bank error* (`502`) | *Outcome unknown* (`504`) | A `200` means the bank processed it |
| No framework request logging | One entry per request with method, status and duration – never the path, query, headers or body | On-call needs request outcomes and latency; the path may hold a pasted card number |
| A `paymentgateway.payments.outcomes` counter and a `paymentgateway.bank.request.duration` histogram | The counter (tag `result`, now including `bank_outcome_unknown`); bank latency from the built-in `http.client.request.duration` and the `BankCallCompleted`/`BankCallFailed` logs | The built-in histogram already measures the bank call |
| The bank's authorization code is discarded | Stored on the payment, not returned | Reconciliation and disputes need it; it is not card data |
| `GET /api/payments/{id}` with a malformed id → `400` | `404`, from the `{id:guid}` route constraint | A malformed id names no payment; the response is identical for every unknown id |
| Expiry year up to 9999 | At most 20 years ahead | Cards are issued for a few years; a later year is a typing error |
| `PaymentResponse`; the body bound directly to `ProcessPaymentCommand` | `PaymentResponseDto` and `ProcessPaymentRequest`, both in `Http/Payments/`, mapped to the use case's `ProcessPaymentCommand` | The wire contract (with its Swagger docs) and the use case's own input are kept as separate types even though their shape matches, so a rename on one side cannot silently rename the other; the response DTO also masks/omits card data |
| `UseHttpsRedirection` | None; TLS is terminated upstream | Redirecting after a card number was sent in clear protects nothing |
| `POST /api/payments` → `200 OK` | `201 Created` with a `Location` header | A processed payment is a new, retrievable resource |
| `{id:guid}` alone for retrieval | Still `{id:guid}` alone | A second, undocumented `{id}` route once mapped a malformed id to the same "not found" body as an unknown id; it was removed as unnecessary – a malformed id now gets the framework's own `404` for an unmatched route |
