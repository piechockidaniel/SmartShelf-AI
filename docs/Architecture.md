# SmartShelf architecture

## Domain and CQRS boundaries

`Shelf` is the configuration aggregate and the consistency boundary for a shelf's location,
enabled state, version, and typed resource bindings. Products, devices, and evaluation rules
are independent catalog aggregates referenced by identifier. Command handlers load and save
aggregates with an expected version; query handlers build views that combine configuration,
catalog labels, the latest observation, and active alerts. CQRS is in-process and uses the
same physical provider—there is no event sourcing or eventually consistent read database.

The application layer exposes persistence-agnostic command and query ports. The API selects
one adapter using `Persistence:Provider`:

* `sqlite` uses Dapper, short-lived `Microsoft.Data.Sqlite` connections, WAL, embedded SQL
  migrations, optimistic versions, and transactions.
* `inmemory` implements the same contract with thread-safe snapshots for demos and tests.

The generic `SmartShelf.Infrastructure` project contains hardware and external integrations;
database code lives only in `SmartShelf.Infrastructure.Sqlite` or
`SmartShelf.Infrastructure.InMemory`.

## Shelf configuration flow

1. The React configuration UI loads one shelf and the resource schema.
2. The operator connects the shelf to categorized hardware, products, and evaluation rules.
3. The UI saves the complete binding set with `expectedVersion`.
4. The command validates resource existence and cardinality, then persists the aggregate and
   bindings atomically. A stale version returns HTTP 409.
5. Operational dashboards consume composed shelf-overview queries.

Controller, camera, and LED-output bindings are single-valued. Sensors, products, and rules
are multi-valued. Observation recording evaluates connected rules; if none are connected,
the built-in edge policy remains the default. The observation and resulting alert transition
commit together before the LED side effect is invoked.
