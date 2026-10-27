<#
.SYNOPSIS
    Regenerates the EF Core entities and PcPartsContext from the database (Database-First).

.DESCRIPTION
    Run after changing Database/schema.sql and re-running it against your database.
    Reads the connection string "ConnectionStrings:PcParts" from PCM.API configuration
    (user-secrets locally, environment variables in CI), so no secret is ever typed here
    or written into the generated code (--no-onconfiguring).

    Generated files (PCM.DomainEntities/Context/PcPartsContext.cs, DBEntities/*.cs) must
    never be edited by hand. Custom code goes in partial classes (Context/PcPartsContext.Partial.cs,
    Partials/). CI runs this same script and fails if the committed entities differ from the schema.

    Identity tables (AspNet*) are intentionally excluded: they are mapped by AuthDbContext.

.EXAMPLE
    pwsh ./scripts/scaffold.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

# Keep in sync with the non-Identity tables in Database/schema.sql.
$tables = @(
    'Address', 'Brand', 'Cart', 'CartItem', 'Category',
    'Offer', 'Order', 'OrderItem', 'Product', 'ProductImage',
    'ProductSpecValue', 'RefreshToken', 'SellerOrder', 'SellerProfile',
    'SpecAttribute', 'SpecOption', 'StockMovement'
)
$tableArgs = $tables | ForEach-Object { '--table'; $_ }

Push-Location $root
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed.' }

    # dotnet ef reads project metadata before building, so packages must be restored first.
    dotnet restore PcPartsMarketplace.slnx
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

    dotnet ef dbcontext scaffold 'Name=ConnectionStrings:PcParts' Microsoft.EntityFrameworkCore.SqlServer `
        --project PCM.DomainEntities `
        --startup-project PCM.API `
        --context PcPartsContext `
        --context-dir Context `
        --output-dir DBEntities `
        --namespace PCM.DomainEntities.DBEntities `
        --context-namespace PCM.DomainEntities.Context `
        --no-onconfiguring `
        --force `
        @tableArgs
    if ($LASTEXITCODE -ne 0) { throw 'Scaffolding failed.' }

    Write-Host 'Scaffolding complete. Review the changes with: git diff PCM.DomainEntities' -ForegroundColor Green
}
finally {
    Pop-Location
}
