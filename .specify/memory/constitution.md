<!--
Sync Impact Report
==================
Version Change: 1.0.0 → 1.1.0 (Added Coding Standards section)
Created: 2025-11-25
Modified: 2025-11-25
Modified Principles: N/A
Added Sections: Coding Standards (with Testing Philosophy, Clean Architecture, Dependency Injection, Creative Problem-Solving)
Removed Sections: N/A

Template Update Status:
✅ plan-template.md - Reviewed, Constitution Check section aligns with new standards
✅ spec-template.md - Reviewed, requirements structure aligns
✅ tasks-template.md - Reviewed, task categorization aligns with testing requirements
✅ No command files exist yet in .specify/templates/commands/

Follow-up TODOs: None
-->

# TheSexy6BotWorker Constitution

## Core Principles

### I. Service-Oriented Architecture
The bot is structured as a .NET Worker Service with clear separation of concerns. Each functional component MUST be:
- Implemented as an injectable service with a defined interface
- Registered in dependency injection with appropriate lifetime (singleton for stateful services like Discord client and Semantic Kernel)
- Testable in isolation through integration tests

**Rationale**: Discord bots are long-running services requiring reliable state management and clean separation between Discord event handling, AI orchestration, and external API integrations.

### II. AI Model Flexibility
Multiple AI models MUST be supported through Microsoft Semantic Kernel's abstraction layer. For each AI provider:
- Register as a distinct chat completion service with unique service ID
- Support dynamic routing based on message prefix or user selection
- Maintain consistent conversation history handling across models
- Preserve provider-specific capabilities (e.g., system prompts, function calling)

**Rationale**: Different AI models offer different capabilities, cost profiles, and response styles. The architecture enables experimentation and graceful fallback without code restructuring.

### III. Function Calling via Semantic Kernel Plugins
All bot capabilities beyond chat completion (weather, search, etc.) MUST be implemented as Semantic Kernel plugins with [KernelFunction] attributes. Each plugin MUST:
- Expose functions with clear, descriptive names for AI discoverability
- Accept strongly-typed parameters with validation
- Return structured data (DTOs) not raw strings
- Handle errors gracefully and return meaningful failure messages
- Be independently testable with integration tests

**Rationale**: Semantic Kernel's function calling enables AI models to autonomously invoke tools. Plugins as first-class citizens ensure extensibility and testability.

### IV. Configuration Security & Environment Awareness
Secrets MUST NEVER be committed to source control. All sensitive configuration MUST use:
- .NET User Secrets for local development
- Environment variables for containerized deployments
- GuardClauses validation at service startup to fail fast on missing config

The bot MUST support LOCAL_DEV mode to prefix commands (e.g., `test-gemini` vs `gemini`) enabling production and development bots to coexist in the same Discord server.

**Rationale**: Security is non-negotiable. Clear development/production separation prevents accidental production deployments and enables safe testing.

### V. Observability & Conversation Context
The bot MUST provide visibility into its operations through:
- Structured logging for all AI interactions, plugin invocations, and errors
- Reply chain traversal to build conversation history (with depth limits to control token usage)
- Message chunking for long responses (max 1980 chars per Discord message)
- Graceful error handling with user-friendly messages (never expose internal exceptions to Discord users)

**Rationale**: Discord bots run continuously and process unpredictable user input. Observability ensures rapid debugging. Context-aware responses improve user experience.

## Architecture Requirements

### Technology Stack
- **Runtime**: .NET 9.0 with Worker Service template
- **Discord Library**: DSharpPlus 5.x with TextCommandProcessor
- **AI Orchestration**: Microsoft Semantic Kernel 1.65.0+
- **HTTP Clients**: Named HttpClient pattern via IHttpClientFactory
- **Testing**: xUnit with integration test categories
- **Containerization**: Docker with Linux target OS

### Mandatory Patterns
- **Dependency Injection**: All services registered in Program.cs with explicit lifetimes
- **Async/Await**: All I/O operations MUST be asynchronous
- **DTO Pattern**: External API responses mapped to strongly-typed DTOs
- **Guard Clauses**: Validate all configuration and external inputs at boundaries
- **Event Handlers**: Discord events handled through dedicated handler classes

### Prohibited Patterns
- **Tight Coupling**: No direct Discord client access outside of handlers
- **Blocking I/O**: No synchronous HTTP calls or database operations
- **Magic Strings**: Configuration keys MUST be constants or strongly-typed options
- **Silent Failures**: All errors MUST be logged; user-facing errors MUST be reported to Discord

## Coding Standards

### VI. Testing Philosophy (NON-NEGOTIABLE)
All code MUST be accompanied by appropriate tests. Testing is not optional; it is a core engineering practice.

**Unit Tests** - REQUIRED for:
- Business logic and algorithms
- Data transformations and mappers
- Utility functions and helpers
- Validation logic
- DTO serialization/deserialization

**Integration Tests** - REQUIRED for:
- All Semantic Kernel plugins (test actual API calls with real or mock endpoints)
- External service integrations (weather APIs, search APIs, AI models)
- Database operations (if added in future)
- End-to-end Discord command flows
- Configuration validation and dependency injection setup

**Test Organization**:
- Tests MUST be in separate project (TheSexy6BotWorker.Tests)
- Use xUnit framework with descriptive test method names
- Mark integration tests with `[Fact(Skip = "Integration")]` or `Category=Integration` for selective execution
- Each service MUST have a corresponding test file (e.g., `WeatherService.cs` → `WeatherServiceTests.cs`)

**Test Quality Standards**:
- Tests MUST be independent and isolated (no shared state between tests)
- Use Arrange-Act-Assert pattern for clarity
- Mock external dependencies appropriately (use interfaces for testability)
- Assert on specific outcomes, not implementation details
- Include edge cases and error scenarios

**Rationale**: Tests are executable documentation that prove correctness, enable safe refactoring, and catch regressions. Integration tests ensure external dependencies work as expected before deployment.

### VII. Clean Architecture (Pragmatic Application)
Apply clean architecture principles pragmatically without over-engineering. The goal is maintainability and testability, not architectural purity.

**Layer Separation** - MUST maintain:
- **Handlers** (Presentation): Discord event handling, message formatting, user interaction
- **Services** (Application): Business logic, orchestration, Semantic Kernel plugin implementations
- **DTOs** (Domain): Data contracts for external APIs, strongly-typed models
- **Infrastructure**: Configuration, logging, HTTP clients, external API clients

**Pragmatic Guidelines**:
- Interfaces required ONLY when multiple implementations exist or for testability
- No need for separate projects per layer (single project is acceptable for this bot's scale)
- Domain logic belongs in services, not handlers
- Keep handlers thin - delegate to services
- DTOs should be anemic (data only, no behavior)

**Dependency Flow**:
- Handlers depend on Services
- Services depend on DTOs and external clients
- No circular dependencies
- Dependencies injected via constructor, never instantiated directly

**When to Refactor**:
- Handler contains business logic → Extract to service
- Service directly accesses Discord client → Pass data through handler
- Duplicated code across services → Extract to shared utility
- Growing complexity in service → Consider splitting into focused services

**Rationale**: Clean architecture improves testability and maintainability. Pragmatic application means we don't create layers/abstractions until they're needed, but we respect boundaries to prevent coupling.

### VIII. Dependency Injection (MANDATORY)
All services, clients, and dependencies MUST be registered in the DI container and injected via constructor injection.

**Registration Rules**:
- Register in `Program.cs` using `IServiceCollection` extensions
- Choose appropriate lifetime:
  - **Singleton**: Discord client, Semantic Kernel, HttpClient factories, stateful services
  - **Scoped**: Request-specific data (currently not applicable for worker service)
  - **Transient**: Stateless services, factories that create new instances
- Use named HttpClients via `IHttpClientFactory` for external API calls
- Never use `new` for services - always inject

**Constructor Injection Pattern**:
```csharp
public class MyService
{
    private readonly ILogger<MyService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public MyService(ILogger<MyService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = Guard.Against.Null(logger);
        _httpClientFactory = Guard.Against.Null(httpClientFactory);
    }
}
```

**Validation**:
- All injected dependencies MUST be validated with GuardClauses
- Required configuration MUST be validated at startup (fail fast)
- Use `IOptions<T>` pattern for strongly-typed configuration

**Rationale**: DI enables testability (inject mocks), manages object lifetimes correctly (preventing memory leaks), and makes dependencies explicit. It's a core .NET pattern that simplifies testing and improves code quality.

### IX. Creative Problem-Solving & Innovation
While standards provide structure, engineers are encouraged to be creative and innovative in solving problems.

**Encouraged Creativity**:
- **Algorithm Design**: Find elegant, efficient solutions to complex problems
- **API Design**: Design intuitive Semantic Kernel plugin interfaces
- **Error Handling**: Create informative, user-friendly error messages
- **Performance Optimization**: Identify and resolve bottlenecks creatively
- **User Experience**: Improve Discord interaction patterns (threading, formatting, context handling)
- **AI Prompt Engineering**: Craft effective system prompts and function descriptions for models

**Innovation Within Boundaries**:
- Experiment with new Semantic Kernel features (embeddings, planners, etc.)
- Explore new AI models and compare their capabilities
- Propose architectural improvements backed by research and prototypes
- Introduce new tools/libraries if they solve real problems (justify in PR)

**When to Break Standards**:
Standards can be challenged if:
1. There's a demonstrably better approach for the specific use case
2. New technology or pattern emerges that solves existing pain points
3. Performance requirements demand a different solution

**Process for Standard Exceptions**:
1. Document the problem and why current standards are insufficient
2. Propose alternative with proof-of-concept or research
3. Discuss in code review or architecture discussion
4. If approved, update constitution and add migration plan

**Rationale**: Standards prevent chaos, but creativity drives progress. Engineers should feel empowered to solve problems elegantly while respecting architectural boundaries. The best solutions often come from questioning assumptions and exploring alternatives.

## Development Workflow

### Code Review Requirements
All changes MUST:
- Include integration tests for new Semantic Kernel plugins
- Update CLAUDE.md if adding new commands, services, or architectural patterns
- Validate configuration requirements (add new secrets to documentation)
- Pass `dotnet build` without warnings
- Run successfully in LOCAL_DEV mode before production deployment

### Testing Gates
- **Unit Tests**: Required for utility functions and data transformations
- **Integration Tests**: Required for all Semantic Kernel plugins and external API integrations (marked with [Fact(Skip = "Integration")] or Category=Integration)
- **Manual Testing**: Required for Discord message flows and command interactions

### Deployment Approval
Changes MUST be tested in LOCAL_DEV mode in a development Discord server before merging to main. The CI/CD pipeline automatically builds and pushes Docker images; production deployment requires manual container restart.

## Governance

### Constitution Authority
This constitution defines the architectural principles and non-negotiable requirements for TheSexy6BotWorker. All pull requests MUST be reviewed for compliance with these principles.

### Amendment Process
Constitution amendments require:
1. Documented rationale for the change (new requirements, lessons learned, technical evolution)
2. Review of impact on existing code and templates
3. Migration plan if changes affect current implementations
4. Version bump following semantic versioning (MAJOR for breaking principle changes, MINOR for new principles, PATCH for clarifications)

### Complexity Justification
Any violation of these principles (e.g., adding synchronous I/O, coupling handlers to services) MUST be explicitly justified in code review with:
- Why the principle doesn't apply to this specific case
- What simpler alternatives were considered and rejected
- Plan to refactor toward compliance if technical debt

### Versioning Policy
Version format: MAJOR.MINOR.PATCH
- **MAJOR**: Backward-incompatible governance changes (e.g., removing a principle, changing mandatory patterns)
- **MINOR**: New principles added or significant guidance expansions
- **PATCH**: Clarifications, wording improvements, typo fixes

### Compliance Review
The constitution MUST be reviewed:
- During onboarding of new contributors
- When adding major new features (new AI models, plugins, commands)
- Quarterly to assess if principles reflect current best practices

For runtime development guidance, refer to [CLAUDE.md](../../../CLAUDE.md) in the repository root.

**Version**: 1.1.0 | **Ratified**: 2025-11-25 | **Last Amended**: 2025-11-25
