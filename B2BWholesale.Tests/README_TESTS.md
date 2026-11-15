# B2BWholesale.Tests — xUnit (.NET 8)

## Run in Visual Studio 2026

1. Open `B2BWholesale.sln`.
2. Build > Rebuild Solution.
3. Test > Test Explorer > Run All Tests.

Or from a terminal in the solution directory:

```powershell
dotnet restore
dotnet test B2BWholesale.Tests/B2BWholesale.Tests.csproj --configuration Debug
```

The suite tests the existing Core business logic: order line validation, merging, stock availability checks, decimal totals, order-status workflow, password hashing, role access, and 5-attempt user lockout.

**Important scope:** Tests use pure functions and EF Core's InMemory provider (for authentication). They do **not** verify SQL Server transactions, atomic stock deductions, transaction rollback, or concurrent orders. Those require separate integration tests against an isolated SQL Server test database. Do not run destructive tests on your real database. These test cases have NOT been executed in this package's authoring environment (no .NET SDK available here). Run `dotnet test` locally and retain the Test Explorer screenshot for your report.
