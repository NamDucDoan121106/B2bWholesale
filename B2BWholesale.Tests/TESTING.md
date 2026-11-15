# B2BWholesale – Unit Testing

## Testing Framework
- Framework: xUnit
- Target Framework: .NET 8
- Development Environment: Visual Studio 2026

## Test Coverage
The project includes 25 unit tests covering core business logic:

- Order validation and normalization
- Stock availability validation
- Order amount calculations
- Order status transitions
- Authentication and authorization
- Account lockout validation
- Password hashing and verification

## How to Run Tests

Open a terminal at the solution root and execute:

```powershell
dotnet test .\B2BWholesale.Tests\B2BWholesale.Tests.csproj
```

## Test Results

- Total: 25
- Passed: 25
- Failed: 0
- Skipped: 0

All 25 tests passed during local execution on November 15, 2026.

## Limitations

These unit tests do not replace database integration testing. Concurrent ordering, SQL transaction rollback, and actual database stock updates require additional integration tests.
