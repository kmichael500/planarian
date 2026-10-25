# System overview

This page is the high-level runtime map of Planarian. It deliberately avoids class-level detail.

## Runtime containers

**Notation:** rectangles are runtime containers/components, cylinders are datastores, and arrows are labeled with the interaction they represent.

~~~mermaid
flowchart LR
    User["Planarian user"]

    subgraph Planarian["Planarian"]
        Web["React + TypeScript SPA"]
        API["ASP.NET Core API"]
        DB[("PostgreSQL + PostGIS")]
        Storage["Object storage"]
    end

    User -->|"uses in browser"| Web
    Web -->|"HTTPS JSON / file requests"| API
    API -->|"EF Core / Npgsql"| DB
    API -->|"File/object operations"| Storage
~~~

### React SPA

Primary source: Planarian.Web/.

Responsibilities include:

- routing and pages;
- Cave forms and authoring state;
- proposal/review UI;
- maps and File viewers;
- API request construction;
- client-side presentation of revision diffs.

The browser does not own authoritative publication semantics. A form may describe desired Files or line plots, but the API validates ownership, concurrency, and normalized result before publication.

### ASP.NET Core API

Primary source: Planarian/Planarian/.

Responsibilities include:

- authentication and authorization;
- application/workflow orchestration;
- Cave publication and proposal review;
- import execution;
- persistence;
- file streaming;
- external storage coordination.

The API is arranged mainly by feature modules rather than a single global service layer.

### PostgreSQL/PostGIS

Primary EF model: Planarian/Planarian.Model/.

PostgreSQL is authoritative for:

- current Cave relational state;
- account/permission/reference data;
- Cave revision history;
- proposal/change-request workflow;
- File metadata and storage locators;
- import history;
- spatial Entrance data.

PostGIS/NetTopologySuite support geospatial persistence and queries. Cave line plots currently remain GeoJSON stored in PostgreSQL jsonb; this branch intentionally does not redesign them into PostGIS features.

### Object storage

The current production adapter is Azure Blob Storage.

For the Cave/File publication lifecycle, application code depends on:

- IObjectStorage;
- StorageObjectAddress;
- AzureBlobObjectStorage as the infrastructure adapter.

Some older unrelated storage workflows still use Azure-specific BlobService or Azure SDK objects directly. The provider-neutral Cave/File boundary is therefore **not yet a repository-wide storage abstraction**.

## Backend zoom-in

~~~mermaid
flowchart TB
    Controllers["Controllers"]

    subgraph Application["Application / workflow layer"]
        CaveService["CaveService"]
        ChangeService["CaveChangeRequestService"]
        RevisionService["CaveRevisionService"]
        FileService["FileService"]
        ImportService["ImportService"]
        AccountService["AccountService"]
    end

    subgraph DomainPersistence["Domain + persistence capabilities"]
        Mutation["CaveMutationCoordinator"]
        CaveRepo["CaveRepository"]
        FileRepo["FileRepository"]
        AccountRepo["AccountRepository"]
        ChangeRepo["CaveChangeRequestRepository"]
        SnapshotRepo["CavePublishedSnapshotRepository"]
        RevisionRepo["CaveRevisionQueryRepository"]
        BulkRepo["CaveBulkRevisionRepository"]
        ImportPlanning["Import planning repositories + pure planners"]
        ImportExecution["Import execution repositories"]
    end

    DB[("PostgreSQL / PostGIS")]
    Objects["IObjectStorage"]

    Controllers -->|"invoke use cases"| Application

    CaveService -->|"publish one Cave"| Mutation
    CaveService -->|"load/update aggregate"| CaveRepo
    ChangeService -->|"request workflow"| ChangeRepo
    ChangeService -->|"approval publication"| CaveService
    RevisionService -->|"history queries"| RevisionRepo
    FileService -->|"File metadata / staging"| FileRepo
    FileService -->|"Cave access / published mutation support"| CaveRepo
    FileService -->|"revision-aware direct File import"| Mutation
    AccountService -->|"account reset / archive projections"| AccountRepo
    AccountService -->|"external File/container cleanup"| FileService
    ImportService -->|"read-only planning"| ImportPlanning
    ImportService -->|"apply immutable plan"| ImportExecution

    Mutation -->|"transactional publication"| DB
    Mutation -->|"canonical snapshot reads"| SnapshotRepo
    CaveRepo -->|"EF / SQL"| DB
    FileRepo -->|"EF / SQL"| DB
    AccountRepo -->|"EF / SQL"| DB
    ChangeRepo -->|"EF / SQL"| DB
    SnapshotRepo -->|"bounded projections"| DB
    RevisionRepo -->|"history projections"| DB
    ImportExecution -->|"set-oriented writes"| DB
    ImportExecution -->|"before/after snapshots"| SnapshotRepo
    ImportExecution -->|"bulk revisions"| BulkRepo
    BulkRepo -->|"revisions + pointers"| DB

    FileService -->|"bytes"| Objects
~~~

This is a responsibility map, not a complete dependency graph. Small support repositories and policies are intentionally omitted.

## Request boundaries

### Ordinary reads

Ordinary Cave detail/search/map/history reads and authoring-context reads are read-only. Existing Caves receive their first revision during the controlled revision-history rollout before the API serves requests; request handling never creates a late rollout baseline.

### Interactive writes

Interactive Cave-owned fields, tags, Entrances, Files, and line plots are intended to publish as one aggregate mutation.

### Bulk writes

Cave/Entrance CSV imports and selected administration workflows use set-oriented persistence algorithms and `CaveBulkRevisionRepository`. They do not call CaveService once per Cave.

File import is different: it publishes one target Cave at a time through FileService and the single-Cave mutation boundary.

## Deployment-specific concerns

This page intentionally avoids depicting environment-specific Azure deployment topology, load balancers, scaling, or CI/CD. Those are deployment concerns rather than the logical runtime architecture shown here.

For local development configuration, use the root README.md and Planarian.Web/README.md.
