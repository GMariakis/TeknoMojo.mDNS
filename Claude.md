# Development Environment

## Technology Stack

- **.NET Version**: .NET 10
- **C# Version**: C# 14

All development in this project uses .NET 10 and C# 14.

## Naming Conventions

### Namespace Structure

All namespaces in this project use the **TeknoMojo** prefix:

- **Root Namespace**: `TeknoMojo.mDNS.*`
- **Core Library**: `TeknoMojo.mDNS.Core.*`
  - `TeknoMojo.mDNS.Core.Protocol`
  - `TeknoMojo.mDNS.Core.Transport`
  - `TeknoMojo.mDNS.Core.ServiceDiscovery`
  - `TeknoMojo.mDNS.Core.Advertising`
  - `TeknoMojo.mDNS.Core.Configuration`
  - `TeknoMojo.mDNS.Core.Cache`
  - `TeknoMojo.mDNS.Core.Utilities`
- **Service Host**: `TeknoMojo.mDNS.ServiceHost`
- **Client Application**: `TeknoMojo.mDNS.Client`

Example:
```csharp
namespace TeknoMojo.mDNS.Core.ServiceDiscovery;

using TeknoMojo.mDNS.Core.Protocol;
using TeknoMojo.mDNS.Core.Configuration;
```

## Async/Await Patterns

All asynchronous code must follow these guidelines:

- **Use async/await**: Always use proper async/await patterns for asynchronous operations
- **CancellationToken**: Include CancellationToken parameters for all async methods that may run long or indefinitely
- **Task return types**: Async methods should return Task or Task<T>
- **ConfigureAwait**: Use ConfigureAwait(false) in library code to avoid deadlocks
- **Avoid async void**: Never use async void except for event handlers
- **Naming**: Async methods should have the "Async" suffix (e.g., StartAsync, StopAsync)

Example:
```csharp
public async Task ProcessAsync(CancellationToken cancellationToken = default)
{
    await SomeOperationAsync(cancellationToken).ConfigureAwait(false);
}
```
