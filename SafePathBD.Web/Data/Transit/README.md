# SafePath BD Synthetic Development Transit Network

These CSV files are **SYNTHETIC_DEVELOPMENT** data created only for the Chunk 7 multimodal routing prototype.

They are not official bus stops, schedules, operators, fares, or route geometry. They provide deterministic transfer/frequency data so the multimodal graph, bus waiting-time logic, transfer limits, ETA calculation, and UI can be implemented without changing the existing MySQL schema.

A production deployment must replace these files with an authoritative transit feed (for example GTFS or a verified local dataset) while keeping the same `ITransitNetworkService` abstraction.
