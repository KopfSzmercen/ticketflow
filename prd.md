# Event Ticketing Platform — Business Scope

## The Idea

A lightweight platform where **organizers** publish events and **attendees** buy tickets. The interesting part isn't the CRUD — it's what happens _after_ someone clicks "Buy".

---

## Actors

- **Organizer** — creates events, sets ticket capacity and price, monitors sales
- **Attendee** — browses events, purchases tickets, receives confirmation
- **System** — handles the async, time-sensitive, failure-prone stuff in between

---

## Features

### 1. Event Management _(organizer)_

Organizer creates an event with a name, date, venue, ticket price, and capacity. That's it. No categories, no seating maps — just enough to have something meaningful in Cosmos DB.

### 2. Ticket Purchase _(the core flow)_

Attendee picks an event and submits their name/email + payment intent. This triggers the **orchestrated saga**:

```
Reserve seat (lock capacity)
    → Simulate payment processing       ← fan-out: fraud check runs in parallel
    → On success: generate QR ticket PDF and upload to Blob
    → Send confirmation via Service Bus → email worker picks it up
    → On failure/timeout: release the reserved seat
```

This is where 80% of the Azure work lives.

### 3. Reservation Expiry _(timer-driven)_

If payment isn't completed within **10 minutes**, the orchestrator wakes up via a durable timer and automatically releases the seat back to the pool. Classic "hold then expire" pattern you see in real ticketing systems.

### 4. Waitlist _(human interaction pattern)_

When an event is sold out, attendees can join a waitlist. When a seat is released (expiry or cancellation), the orchestrator uses Durable Functions' **external event / human interaction pattern** to notify the next person on the waitlist and give them a **15-minute window** to claim the ticket before moving to the next person.

### 5. Ticket Cancellation _(attendee)_

Attendee cancels before a cutoff (e.g. 24h before event). Triggers a compensation flow — refund simulation, seat released, waitlist notified if applicable.

### 6. Organizer Sales Dashboard _(read model)_

Organizer sees: tickets sold, revenue, reservations in progress, waitlist count. This is a simple read against Cosmos DB but gives you a reason to think about **partition design and read efficiency**.

---

## What's Intentionally Left Out

- Real payments (Stripe etc.) — simulated with a random success/failure
- Auth/login — a passed-in email is enough; no identity provider needed
- Rich UI — minimal React frontend, just enough to trigger the flows

---

## Why Each Feature Exists _(Azure perspective)_

| Feature                   | Why it's there                                      |
| ------------------------- | --------------------------------------------------- |
| Purchase saga             | Durable Functions orchestrator + activity functions |
| Parallel fraud check      | Durable Functions fan-out/fan-in                    |
| Reservation expiry        | Durable timer trigger                               |
| Waitlist claim window     | External event / human interaction pattern          |
| Confirmation dispatch     | Service Bus topic → multiple subscribers            |
| QR PDF generation         | Blob Storage output binding                         |
| Sales dashboard           | Cosmos DB query across partitions, RU awareness     |
| Cancellation compensation | Orchestrator rollback / compensation pattern        |

Every feature earns its place by exercising a specific Azure pattern — nothing is there just to have more endpoints.
