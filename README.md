# ume-nuget

Shared NuGet libraries developed by **Team Turkos** at Umeå kommun. The packages are open source but primarily built for Team Turkos' own services. This repository produces three packages:

| Package         | NuGet ID              | Description                                                               |
| --------------- | --------------------- | ------------------------------------------------------------------------- |
| **Toolkit**     | `Umea.se.Toolkit`     | Shared runtime building blocks for ASP.NET Core APIs and Azure Functions. |
| **TestToolkit** | `Umea.se.TestToolkit` | Integration-test harness with pre-wired mocks and test base classes.      |
| **Templates**   | `Umea.se.Templates`   | `dotnet new` project templates for scaffolding new services.              |

All packages target **.NET 10** and are published to an Azure Artifacts feed (`turkos.umea.se`) and, for stable releases, to [NuGet.org](https://www.nuget.org/profiles/umeakommun).

---

## CI/CD & Deployment

The repository uses **Azure DevOps Pipelines** with three pipeline definitions:

### 1. Release Validation (`release-validation.yml`)

- **Trigger:** PR build validation on `main`
- **Purpose:** Builds and tests changed packages to gate merges
- Detects which packages changed and only builds those
- Toolkit and TestToolkit: runs restore, build, and tests
- Templates: runs the validation script that tests all parameter combinations

### 2. Release Orchestrator (`release-orchestrator.yml`)

- **Trigger:** Merged pull request to `main`
- **Stages:** Validate → Get PR info → Run publish pipeline → Finalize (tag commit, tag build, update work items)
- Prevents manual runs from `main` — releases happen only via merged pull requests

### 3. Publish Packages (`publish-packages.yml`)

- **Trigger:** Called by the release orchestrator, or manually for pre-releases
- **Stages:** Validate → Check changes → Pack → Publish (per package)
- Generates a date-based version, runs tests, packs `.nupkg`, and pushes to:
  - **Azure Artifacts** (`turkos.umea.se`) — all releases
  - [NuGet.org](https://www.nuget.org/profiles/umeakommun) — stable releases only

## Versioning

Package versions follow a **date-based** scheme:

| Release Type    | Format                                                | Example                       |
| --------------- | ----------------------------------------------------- | ----------------------------- |
| **Stable**      | `YYYY.M.D.<commit-count>`                             | `2026.3.9.147`                |
| **Pre-release** | `YYYY.M.D.<baseline-count>-dev.<branch-hash>.<delta>` | `2026.3.9.147-dev.a1b2c3d4.3` |

- The date component uses **Stockholm local time**
- The commit count is scoped to the package path for independent versioning
- `baseline-count` is the `main` commit count at the branch's last sync point, indicating which stable release the pre-release builds on
- `delta` is the number of commits on the branch ahead of `main`
- `branch-hash` is a truncated SHA-256 of the branch name for uniqueness
- Version tags are written as `<package>/<version>` (e.g., `toolkit/2026.3.9.147`)

---

## Templates Package

The `Umea.se.Templates` package provides `dotnet new` templates for scaffolding new services following Team Turkos conventions.

### Install

Install the latest stable version:

```bash
dotnet new install Umea.se.Templates
```

Install the latest pre-release version:

```bash
dotnet new install Umea.se.Templates::*-*
```

Install a specific version:

```bash
dotnet new install Umea.se.Templates::2026.5.15.42
```

Update to latest (if already installed):

```bash
dotnet new update
```

### Available Templates

#### `ume-template-api` — Turkos Backend API

An ASP.NET Core API service with Umea.se.Toolkit integration and xUnit tests. Supports full layered architecture or minimal setup, cloud or on-premises deployment, and optional EF Core database integration.

```bash
dotnet new ume-template-api -n MyNewService
```

All occurrences of `TemplateService` in file/folder names, namespaces, and content are replaced with the name you provide.

**Options:**

| Option            | Values             | Default   | Description                                                                                                                                                |
| ----------------- | ------------------ | --------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `--size`          | `Default`, `Small` | `Default` | **Default** — Full layered architecture (API, Logic, ServiceAccess, Shared, Tests). **Small** — Minimal setup with API and Tests only.                     |
| `--api-type`      | `Cloud`, `OnPrem`  | `Cloud`   | **Cloud** — Azure cloud with Key Vault, Application Insights, and per-environment config files. **OnPrem** — On-premises without Azure cloud dependencies. |
| `--database-name` | any string         | _(empty)_ | When set, includes a Database project with EF Core and SQL Server. The value is used for the connection string name and file naming.                       |

**Examples:**

```bash
# Full cloud API (default)
dotnet new ume-template-api -n OrderService

# Minimal on-premises API
dotnet new ume-template-api -n StatusService --size Small --api-type OnPrem

# Full cloud API with a database
dotnet new ume-template-api -n CustomerService --database-name CustomerDatabase
```

**Pipeline Snippets:**

Cloud templates include a `.pipeline-snippets/` folder with ready-to-copy Azure DevOps pipeline stages.

All auto-generated names (stage names, project references) use the service name you provided. Values that can't be auto-generated (resource group, app service name, etc.) are marked with `TODO` comments. The snippets are excluded from OnPrem templates.

### Local Development

The template source files under `src/ume-nuget-templates/templates/` contain [dotnet template engine](https://github.com/dotnet/templating) conditionals (`#if`, `#endif`) and are **not meant to build directly**. They are processed by the template engine during `dotnet new`.

To validate template changes locally, use the validation script which packs the package, installs it, instantiates all parameter combinations, and verifies each one builds and passes tests:

```bash
pwsh src/ume-nuget-templates/templates/ume-template-api/validate-package.ps1
```

To manually test a single combination:

```bash
# Pack and install locally
dotnet pack src/ume-nuget-templates --configuration Release --output ./artifacts
dotnet new install ./artifacts/Umea.se.Templates.*.nupkg

# Generate and build
dotnet new ume-template-api -n TestService --size Default --api-type Cloud
cd TestService
dotnet build
dotnet test
```

Uninstall when done:

```bash
dotnet new uninstall Umea.se.Templates
```

---

## License

This project is licensed under the **GNU Affero General Public License v3.0 (AGPL-3.0)**. See the [LICENSE](LICENSE) file for full terms.
