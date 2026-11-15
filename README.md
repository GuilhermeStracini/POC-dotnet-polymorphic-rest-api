# PoC .NET - Polymorphic REST API

[![wakatime](https://wakatime.com/badge/github/GuilhermeStracini/POC-dotnet-polymorphic-rest-api.svg)](https://wakatime.com/badge/github/GuilhermeStracini/POC-dotnet-polymorphic-rest-api)
[![Maintainability](https://api.codeclimate.com/v1/badges/e5fbd3f04924d0e61c83/maintainability)](https://codeclimate.com/github/GuilhermeStracini/POC-dotnet-polymorphic-rest-api/maintainability)
[![Test Coverage](https://api.codeclimate.com/v1/badges/e5fbd3f04924d0e61c83/test_coverage)](https://codeclimate.com/github/GuilhermeStracini/POC-dotnet-polymorphic-rest-api/test_coverage)
[![CodeFactor](https://www.codefactor.io/repository/github/GuilhermeStracini/POC-dotnet-polymorphic-rest-api/badge)](https://www.codefactor.io/repository/github/GuilhermeStracini/POC-dotnet-polymorphic-rest-api)
[![GitHub license](https://img.shields.io/github/license/GuilhermeStracini/POC-dotnet-polymorphic-rest-api)](https://github.com/GuilhermeStracini/POC-dotnet-polymorphic-rest-api)
[![GitHub last commit](https://img.shields.io/github/last-commit/GuilhermeStracini/POC-dotnet-polymorphic-rest-api)](https://github.com/GuilhermeStracini/POC-dotnet-polymorphic-rest-api)

🔬 Proof of Concept of a **Polymorphic REST API** with **.NET 8** and ASP.NET
Core.

A polymorphic REST API exposes a single endpoint that accepts and returns
objects of a **base type**, while the actual payload can be any of several
**derived types**. A **type discriminator** in the JSON (e.g. `"$type": "card"`)
tells the serializer which concrete class to bind to, so one contract can serve
many shapes of data.

---

## Table of contents

- [Project status](#project-status)
- [Tech stack](#tech-stack)
- [Repository layout](#repository-layout)
- [How it works today](#how-it-works-today)
  - [Request pipeline](#request-pipeline)
  - [Request lifecycle](#request-lifecycle)
  - [Domain model](#domain-model)
  - [Temperature conversion graph](#temperature-conversion-graph)
- [Target design: polymorphism](#target-design-polymorphism)
  - [Type hierarchy](#type-hierarchy)
  - [Polymorphic request flow](#polymorphic-request-flow)
  - [Deserialization decision](#deserialization-decision)
  - [Example payloads](#example-payloads)
- [Getting started](#getting-started)
- [Testing](#testing)
- [Quality gates & CI](#quality-gates--ci)
- [Contributing](#contributing)
- [Links](#links)

---

## Project status

> [!NOTE]
> The repository currently contains the **ASP.NET Core Web API scaffold** (the
> `WeatherForecast` sample). It already has hosting, Swagger, Docker, tests
> and CI in place.
> The polymorphic endpoints described in [Target design:
> polymorphism](#target-design-polymorphism) are the **planned** next step and
> are **not implemented yet**.

```mermaid
flowchart LR
    A[✅ Web API scaffold] --> B[✅ Swagger / OpenAPI]
    B --> C[✅ Docker support]
    C --> D[✅ Unit tests + coverage]
    D --> E[✅ CI quality gates]
    E --> F[🚧 Polymorphic models<br/>JsonPolymorphic / JsonDerivedType]
    F --> G[🚧 Polymorphic endpoints<br/>GET / POST base type]
    G --> H[🚧 Swagger oneOf + discriminator]

    classDef done fill:#d4edda,stroke:#28a745,color:#155724
    classDef todo fill:#fff3cd,stroke:#ffc107,color:#856404
    class A,B,C,D,E done
    class F,G,H todo
```

---

## Tech stack

| Concern         | Technology                                          |
| --------------- | --------------------------------------------------- |
| Runtime         | .NET 8 (`net8.0`), ASP.NET Core Web API             |
| API docs        | Swashbuckle.AspNetCore (Swagger UI in Development)  |
| Serialization   | `System.Text.Json`                                  |
| Containers      | Multi-stage `Dockerfile`, ports `8080` / `8081`     |
| Tests           | xUnit, FluentAssertions, NSubstitute, AutoFixture   |
| Coverage        | Coverlet                                            |
| Formatting      | CSharpier (local tool, pre-commit hook, CI linter)  |
| Static analysis | SonarCloud, DeepSource, CodeFactor, Code Climate    |

---

## Repository layout

```text
POC-dotnet-polymorphic-rest-api/
├── POCPolymorphicRestApi.sln
├── Src/
│   └── POCPolymorphicRestApi/
│       ├── Controllers/
│       │   └── WeatherForecastController.cs   # GET /WeatherForecast
│       ├── Properties/launchSettings.json     # Launch profiles
│       ├── Program.cs                         # Host + middleware
│       ├── WeatherForecast.cs                 # Response model
│       ├── POCPolymorphicRestApi.http         # Sample request
│       └── Dockerfile
├── Tests/
│   ├── Coverlet.runsettings
│   └── POCPolymorphicRestApi.Tests/
│       └── Controllers/WeatherForecastControllerTests.cs
├── .githooks/pre-commit                       # Branch name + format
├── .config/dotnet-tools.json                  # CSharpier local tool
└── .github/workflows/                         # CI pipelines
```

```mermaid
flowchart TB
    subgraph Solution["POCPolymorphicRestApi.sln"]
        API["📦 POCPolymorphicRestApi<br/><i>Microsoft.NET.Sdk.Web</i>"]
        TESTS["🧪 POCPolymorphicRestApi.Tests<br/><i>xUnit</i>"]
    end
    TESTS -- ProjectReference --> API
    API -- NuGet --> SW[Swashbuckle.AspNetCore]
    API -- NuGet --> CT[VS Containers Tools]
    X[xUnit · FluentAssertions<br/>NSubstitute · AutoFixture<br/>Coverlet]
    TESTS -- NuGet --> X
```

---

## How it works today

### Request pipeline

`Program.cs` builds a minimal ASP.NET Core host. Every request passes through
the middleware below, in this order:

```mermaid
flowchart LR
    Client([HTTP client]) --> HTTPS[UseHttpsRedirection]
    HTTPS --> Dev{Environment is<br/>Development?}
    Dev -- yes --> Swagger[UseSwagger +<br/>UseSwaggerUI]
    Dev -- no --> Auth
    Swagger --> Auth[UseAuthorization]
    Auth --> Routing[MapControllers<br/>attribute routing]
    Routing --> Ctrl[WeatherForecastController]
    Ctrl --> JSON[System.Text.Json<br/>serializer]
    JSON --> Client
```

Services registered in the DI container:

| Registration                | Purpose                                     |
| --------------------------- | ------------------------------------------- |
| `AddControllers()`          | Controllers, model binding, JSON formatter  |
| `AddEndpointsApiExplorer()` | Endpoint metadata for OpenAPI               |
| `AddSwaggerGen()`           | Generates the OpenAPI document              |

### Request lifecycle

What happens for `GET /WeatherForecast`:

```mermaid
sequenceDiagram
    autonumber
    actor C as Client
    participant K as Kestrel
    participant P as Middleware pipeline
    participant R as Router
    participant WC as WeatherForecastController
    participant L as ILogger
    participant S as System.Text.Json

    C->>K: GET /WeatherForecast
    K->>P: HttpContext
    P->>R: Match route [controller]
    R->>WC: Get()
    WC->>L: LogInformation("GetWeatherForecast called")
    loop 5 times (index 1..5)
        WC->>WC: Date = UtcNow + index days
        WC->>WC: TemperatureC = Random(-20, 55)
        WC->>WC: Summary = random pick of 10 labels
    end
    WC-->>S: WeatherForecast[5]
    S-->>C: 200 OK · application/json
```

Example response:

```json
[
  {
    "date": "2026-11-16",
    "temperatureC": 21,
    "temperatureF": 69,
    "summary": "Mild"
  }
]
```

### Domain model

```mermaid
classDiagram
    direction LR
    class ControllerBase {
        <<ASP.NET Core>>
    }
    class WeatherForecastController {
        -string[] Summaries$
        -ILogger~WeatherForecastController~ logger
        +Get() IEnumerable~WeatherForecast~
    }
    class WeatherForecast {
        +DateOnly Date
        +int TemperatureC
        +int TemperatureF
        +string Summary
    }
    ControllerBase <|-- WeatherForecastController
    WeatherForecastController ..> WeatherForecast : creates 5
```

`TemperatureF` is a computed property: `32 + (int)(TemperatureC / 0.5556)`.

### Temperature conversion graph

`TemperatureC` is drawn from `Random.Shared.Next(-20, 55)`, so it ranges from
**-20 °C to 54 °C**. The graph shows what the API returns for `TemperatureF`
across that range, next to the exact value (`C × 9/5 + 32`):

```mermaid
xychart-beta
    title "TemperatureF returned by the API vs exact conversion"
    x-axis "TemperatureC (°C)" [-20, -10, 0, 10, 20, 30, 40, 50, 54]
    y-axis "°F" -10 --> 135
    line [-3, 15, 32, 49, 67, 85, 103, 121, 129]
    line [-4, 14, 32, 50, 68, 86, 104, 122, 129.2]
```

| °C   | API `TemperatureF` | Exact °F |
| ---- | ------------------ | -------- |
| -20  | -3                 | -4       |
| -10  | 15                 | 14       |
| 0    | 32                 | 32       |
| 10   | 49                 | 50       |
| 20   | 67                 | 68       |
| 30   | 85                 | 86       |
| 40   | 103                | 104      |
| 50   | 121                | 122      |
| 54   | 129                | 129.2    |

> [!TIP]
> The `(int)` cast truncates toward zero, so the result is often **1 °F off**.
> It is too low for positive temperatures and too high for negative ones. This
> comes from the stock template and is harmless for a PoC.

---

## Target design: polymorphism

> [!IMPORTANT]
> This section describes the **planned** implementation. It uses the built-in
> `System.Text.Json` polymorphism support (available since .NET 7), so no
> extra library is needed. The `Payment` domain below is only an example.

### Type hierarchy

The base type is annotated with `[JsonPolymorphic]` and lists every derived type
with `[JsonDerivedType]` and its discriminator value:

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(CardPayment), "card")]
[JsonDerivedType(typeof(PixPayment), "pix")]
[JsonDerivedType(typeof(BankTransferPayment), "bankTransfer")]
public abstract class Payment
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
}
```

```mermaid
classDiagram
    direction TB
    class Payment {
        <<abstract>>
        +Guid Id
        +decimal Amount
        +string Currency
    }
    class CardPayment {
        +string CardHolder
        +string Last4Digits
        +int Installments
    }
    class PixPayment {
        +string PixKey
    }
    class BankTransferPayment {
        +string Iban
        +string Bic
    }
    Payment <|-- CardPayment : $type = card
    Payment <|-- PixPayment : $type = pix
    Payment <|-- BankTransferPayment : $type = bankTransfer

    class PaymentsController {
        +Get() IEnumerable~Payment~
        +GetById(Guid id) Payment
        +Post(Payment payment) Payment
    }
    PaymentsController ..> Payment : accepts / returns
```

### Polymorphic request flow

One endpoint accepts the base type, and the runtime type decides how the payload
is handled:

```mermaid
sequenceDiagram
    autonumber
    actor C as Client
    participant MB as Model binding
    participant S as System.Text.Json
    participant PC as PaymentsController
    participant H as Handler for the concrete type

    C->>MB: POST /payments {"$type":"pix", ...}
    MB->>S: Deserialize as Payment
    S->>S: Read "$type" = "pix"
    S-->>MB: PixPayment instance
    MB->>PC: Post(Payment payment)
    PC->>H: switch on payment (pattern matching)
    H-->>PC: processed PixPayment
    PC-->>S: Serialize as Payment
    S-->>C: 201 Created {"$type":"pix", ...}
```

### Deserialization decision

```mermaid
flowchart TD
    In[/Incoming JSON body/] --> Has{Has $type<br/>property?}
    Has -- no --> Abs{Is the base type<br/>abstract?}
    Abs -- yes --> E1[400 Bad Request<br/>cannot create abstract type]
    Abs -- no --> Base[Bind as base type]
    Has -- yes --> Known{Is the value registered<br/>with JsonDerivedType?}
    Known -- no --> E2[400 Bad Request<br/>unknown type discriminator]
    Known -- yes --> Map{Discriminator}
    Map -- card --> Card[CardPayment]
    Map -- pix --> Pix[PixPayment]
    Map -- bankTransfer --> Bank[BankTransferPayment]
    Card & Pix & Bank --> Ctrl[Controller action receives Payment]
    Base --> Ctrl
```

> [!WARNING]
> `System.Text.Json` expects the `$type` discriminator to be the **first
> property** in the JSON object by default. Starting with .NET 9 you can turn
> on `AllowOutOfOrderMetadataProperties` to accept it anywhere.

### Example payloads

#### Card payment

```json
{
  "$type": "card",
  "id": "8f7a2c4e-1b3d-4e5f-9a8b-7c6d5e4f3a2b",
  "amount": 199.9,
  "currency": "EUR",
  "cardHolder": "JANE DOE",
  "last4Digits": "4242",
  "installments": 3
}
```

#### Pix payment

```json
{
  "$type": "pix",
  "id": "1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d",
  "amount": 50,
  "currency": "BRL",
  "pixKey": "jane@example.com"
}
```

#### Bank transfer

```json
{
  "$type": "bankTransfer",
  "id": "9e8d7c6b-5a4f-3e2d-1c0b-a9f8e7d6c5b4",
  "amount": 1200,
  "currency": "EUR",
  "iban": "IE29AIBK93115212345678",
  "bic": "AIBKIE2D"
}
```

---

## Getting started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Optional: [Docker](https://www.docker.com/)

### Run locally

```bash
git clone https://github.com/GuilhermeStracini/POC-dotnet-polymorphic-rest-api.git
cd POC-dotnet-polymorphic-rest-api
dotnet run --project Src/POCPolymorphicRestApi --launch-profile https
```

| Profile       | URLs                                              |
| ------------- | ------------------------------------------------- |
| `http`        | `http://localhost:5021`                           |
| `https`       | `https://localhost:7258`, `http://localhost:5021` |
| `IIS Express` | `http://localhost:7324` (SSL port `44374`)        |

Swagger UI is served at `/swagger` on any of these URLs.

Try it with the bundled [`.http` file][http-file], or with curl:

```bash
curl http://localhost:5021/weatherforecast
```

### Run with Docker

The `Dockerfile` copies paths relative to `Src/`, so build from that folder:

```bash
cd Src
docker build -f POCPolymorphicRestApi/Dockerfile -t poc-polymorphic-rest-api .
docker run --rm -p 8080:8080 poc-polymorphic-rest-api
curl http://localhost:8080/weatherforecast
```

```mermaid
flowchart LR
    subgraph build["Stage: build (sdk:8.0)"]
        R[dotnet restore] --> B[dotnet build]
    end
    subgraph publish["Stage: publish"]
        P[dotnet publish<br/>UseAppHost=false]
    end
    subgraph final["Stage: final (aspnet:8.0)"]
        F[/app · user: app<br/>EXPOSE 8080, 8081/]
    end
    B --> P --> F
    F --> E((dotnet<br/>POCPolymorphicRestApi.dll))
```

> Swagger UI is only enabled when `ASPNETCORE_ENVIRONMENT=Development`. Add
> `-e ASPNETCORE_ENVIRONMENT=Development` to `docker run` to get it inside the
> container.

---

## Testing

```bash
dotnet test
```

With coverage (Coverlet, OpenCover format):

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

The tests use **xUnit** for the runner, **NSubstitute** to mock `ILogger<T>`,
and **FluentAssertions** for the assertions. `Program` is marked
`[ExcludeFromCodeCoverage]`, so coverage counts only the controller and model
code.

---

## Quality gates & CI

```mermaid
flowchart TB
    Dev([Developer]) --> Hook{{pre-commit hook}}
    Hook -->|valid branch<br/>feature/, fix/ ...| CS[csharpier --check]
    Hook -->|invalid branch name| Reject[❌ commit rejected]
    CS --> Push[git push / open PR]

    Push --> Sonar[SonarCloud Analysis<br/><i>push + PR · windows-latest</i>]
    Push --> Deep[DeepSource<br/><i>push + PR</i>]
    Push --> PR{Pull request?}
    PR -- yes --> Lint[CSharpier linter]
    PR -- yes --> Secrets[Infisical secrets check]
    PR -- yes --> Size[PR size label]
    Sonar -. check_run .-> Pixee[Pixeebot<br/>auto-fix Sonar issues]

    Lint & Secrets & Size & Sonar & Deep --> Merge([Ready to merge])
```

| Workflow                      | Trigger         | What it does               |
| ----------------------------- | --------------- | -------------------------- |
| `sonar-cloud.yml`             | push, PR        | Build, test, SonarCloud    |
| `deep-source.yml`             | push, PR        | Coverage to DeepSource     |
| `linter.yml`                  | PR, manual      | CSharpier format check     |
| `infisical-secrets-check.yml` | PR, manual      | Scans for leaked secrets   |
| `size-label.yml`              | PR              | Labels the PR by diff size |
| `sonarcloud-pixeebot.yml`     | Sonar check run | Pixeebot auto-fixes        |

---

## Contributing

1. Enable the repository hooks once: `git config core.hooksPath .githooks`
2. Restore local tools: `dotnet tool restore`
3. Name your branch with a conventional prefix: `feature/`, `fix/`, `docs/`,
   `style/`, `refactor/`, `perf/`, `hotfix/`, `test/`, `chore/` or `create/`
4. Format before committing: `dotnet csharpier .`
5. Open a pull request against `main`

---

## Links

- [References](https://blog.guilhermebranco.com.br/)
- [Polymorphic serialization with System.Text.Json][stj-polymorphism]
- [Swashbuckle.AspNetCore][swashbuckle]

## License

Distributed under the terms of the [LICENSE](LICENSE) file.

[http-file]: Src/POCPolymorphicRestApi/POCPolymorphicRestApi.http
[swashbuckle]: https://github.com/domaindrivendev/Swashbuckle.AspNetCore
[stj-polymorphism]:
https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/polymorphism
