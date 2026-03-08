---
name: azure-function
description: Use this skill whenever the user wants to create an Azure Function in the TicketFlow project. This covers simple, self-contained functions (HTTP-triggered, timer, queue, etc.) where all logic is contained within a single function — as opposed to orchestrated or long-lived Durable Functions. Trigger whenever the user says things like "create a function", "add an endpoint", "new Azure Function", "add a [GET/POST/PUT/DELETE] for [resource]", or describes a new API operation to implement. By default, generate both the function and its integration test unless the user explicitly asks for just one.
---

# Azure Function Skill

Generates self-contained Azure Functions for the TicketFlow project, along with happy-path
integration tests. "Simple" means all logic is contained within one function — not orchestrated,
not long-lived, not Durable Functions.

---

## Project Conventions

### Namespaces

- Functions: `TicketFlow.Functions` (e.g. `TicketFlow.Functions.Http` for HTTP)
- Integration tests: `TicketFlow.Integration.Tests` (e.g. `TicketFlow.Integration.Tests.Http`)
- Sub-namespace matches trigger type: `Http`, `Timer`, `Queue`, etc.

### File paths

- Functions: `src/TicketFlow.Functions/<TriggerType>/<FunctionName>.cs`
- Tests: `tests/TicketFlow.Integration.Tests/<TriggerType>/<FunctionName>Tests.cs`

### Dependencies

- Primary DB dependency: `TicketFlowDbContext` (injected via constructor)
- Other services added as needed via constructor injection

---

## Function Template (HTTP)

```csharp
namespace TicketFlow.Functions.Http;

public sealed class {FunctionName}Function(TicketFlowDbContext dbContext)
{
    [Function("{FunctionName}")]
    public async Task<IResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "{method}", Route = "{route}")]
        Request {requestParamName}
    )
    {
        // implementation
    }

    public sealed record Request(
        // request properties
    );
}
```

**Key rules:**

- Class is `sealed`, named `{FunctionName}Function`
- Constructor injection only — no `[FromServices]` attributes
- Return type is `IResult` for HTTP functions
- `Request` is a nested `sealed record` inside the function class
- Route follows REST conventions: plural nouns, e.g. `events`, `events/{id}`, `tickets/{id}`
- Always ask the developer whether the function should use `AuthorizationLevel.Anonymous` or `AuthorizationLevel.Function` — never assume

### Common return patterns

```csharp
return Results.Ok(ResponseDto.FromEntity(entity));           // GET
return Results.Created($"/{route}/{entity.Id}", ResponseDto.FromEntity(entity)); // POST
return Results.NoContent();                                   // PUT/DELETE (no body)
return Results.NotFound();                                    // not found
```

---

## Integration Test Template (HTTP)

```csharp
namespace TicketFlow.Integration.Tests.Http;

[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public class {FunctionName}FunctionTests(CosmosDbContainerFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task Run_Should{ExpectedBehaviour}()
    {
        // Arrange
        await using var scope = Fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TicketFlowDbContext>();

        var function = new {FunctionName}Function(dbContext);

        var request = new {FunctionName}Function.Request(
            // request values
        );

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            Response =
            {
                Body = new MemoryStream()
            }
        };

        // Act
        var result = await function.Run(request);
        await result.ExecuteAsync(httpContext);

        // Assert
        httpContext.Response.StatusCode.ShouldBe(StatusCodes.Status{ExpectedCode});

        // If response has a body:
        var responseBody = httpContext.Response.Body;
        responseBody.Seek(0, SeekOrigin.Begin);
        var responseJson = await new StreamReader(responseBody).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<{ResponseType}>(responseJson, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        response.ShouldNotBeNull();
        // assert each field from request maps correctly to response
    }
}
```

**Key rules:**

- One `[Fact]` per file for the happy path
- Test method named `Run_Should{ExpectedBehaviour}` (e.g. `Run_ShouldCreateEventAndReturnCreated`)
- Always verify status code first, then deserialize and assert each response field
- Deserialize with `JsonNamingPolicy.CamelCase`
- Use `Shouldly` for assertions (`ShouldBe`, `ShouldNotBeNull`, `ShouldBeEquivalentTo`)
- Scope is created and disposed with `await using`

---

## DTOs

Response DTOs live in `src/TicketFlow.Functions/DTO/`.

**Before creating a new DTO:**

- Check the existing files in `src/TicketFlow.Functions/DTO/` to see if a matching or reusable DTO already exists
- Reuse an existing DTO if it fits; only create a new one if there is no suitable match

**DTO conventions:**

- Named `{Entity}Response` (e.g. `TicketEventResponse`, `TicketResponse`)
- Include a static `FromEntity` factory method (e.g. `TicketEventResponse.FromTicketEvent(entity)`)
- Namespace: `TicketFlow.Functions.DTO`

---

## Non-HTTP Triggers (future)

When adding support for triggers other than HTTP (Timer, Queue, Service Bus, Blob, etc.):

- Sub-namespace matches trigger type: e.g. `TicketFlow.Functions.Timer`
- Test namespace: `TicketFlow.Integration.Tests.Timer`
- The `Request` nested record pattern may not apply — use the appropriate trigger binding parameter instead
- Integration test setup will differ (no `DefaultHttpContext`) — adapt accordingly

---

## Step-by-step Workflow

When asked to create a function:

1. **Discuss the implementation first** — before writing any code, have a conversation with the developer to understand:
   - The use case and business requirement (what problem does this solve?)
   - The entity/resource being operated on and what state changes are involved
   - Edge cases, validation rules, or constraints to be aware of
   - Any specific implementation details or preferences
   - Don't skip this step — even for seemingly simple functions, a brief discussion prevents wrong assumptions

2. **Clarify specifics** (if not already resolved in step 1):
   - What HTTP method and route? (or what trigger type?)
   - What fields does the request carry?
   - What should the response look like?
   - Should this function use `AuthorizationLevel.Anonymous` or `AuthorizationLevel.Function`? **Always ask.**

3. **Check existing DTOs** in `src/TicketFlow.Functions/DTO/` before creating a new one — reuse if a matching one exists.

4. **Generate the function file** following the template above.

5. **Generate the integration test file** (unless user asked for function only), following the test template. The happy-path test should:
   - Arrange a realistic request with sensible test values
   - Assert status code
   - Assert every response field maps correctly from the request

6. **Present both files** to the user with a brief summary of what was created.
