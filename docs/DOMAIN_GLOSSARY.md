# Domain Glossary

| Term | Meaning |
|---|---|
| **Tenant** | A carrier company using ParcelFlow. All data is partitioned by `TenantId`. |
| **Parcel** | The physical package to deliver. Has a tenant-unique `Reference` shown to customers. |
| **Delivery task** | The unit of work: deliver one parcel. Owns the lifecycle state machine and audit history. |
| **Attempt** | One try at delivering. Failed attempts increment `AttemptCount` and put the task in `AttemptFailed`. |
| **Driver** | A courier employed by the tenant. Has a task `Capacity`. |
| **Shift** | A driver's working window. Only drivers with an **open shift** (no `EndedUtc`) are assignable. |
| **POD** | Proof of delivery — the note captured when a parcel is handed over. |
| **COD** | Cash on delivery — amount the driver collects from the recipient. |
| **Hub** | The tenant's depot where drivers collect parcels. Not modelled as an entity yet. |
| **Ops team** | The tenant-side operations staff who monitor alerts and reports. |
| **Sweep** | One pass of a background worker over all tenants. |
