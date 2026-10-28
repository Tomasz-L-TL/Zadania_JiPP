#!/usr/bin/env python3
"""Powtarzalne kontrole strukturalne week05, bez kompilacji .NET.

To NIE zastępuje dotnet build ani testów HTTP/EF Core.
"""
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

week = Path(__file__).resolve().parent
root = week.parent
api = week / 'src/Week05.Api'
tests = week / 'tests/Week05.IntegrationTests'
checks = []


def check(name, condition):
    checks.append((name, bool(condition)))


def txt(path):
    return path.read_text(encoding='utf-8') if path.exists() else ''


def contains(path, *parts):
    value = txt(path)
    return bool(value) and all(part in value for part in parts)


paths = {
    'API project': api / 'Week05.Api.csproj',
    'API startup': api / 'Program.cs',
    'Entity': api / 'Domain/User.cs',
    'EF Core context': api / 'Data/UsersDbContext.cs',
    'Design-time factory': api / 'Data/UsersDbContextFactory.cs',
    'Migration Up/Down': api / 'Data/Migrations/202610260001_InitialCreate.cs',
    'Migration target model': api / 'Data/Migrations/202610260001_InitialCreate.Designer.cs',
    'Migration model snapshot': api / 'Data/Migrations/UsersDbContextModelSnapshot.cs',
    'Contracts': api / 'Contracts/UserDtos.cs',
    'Validation': api / 'Validation/UserValidator.cs',
    'Endpoints': api / 'Endpoints/UserEndpoints.cs',
    'Example requests': api / 'Week05.Api.http',
    'Base config': api / 'appsettings.json',
    'Dev config': api / 'appsettings.Development.json',
    'Production config': api / 'appsettings.Production.json',
    'Launch profile': api / 'Properties/launchSettings.json',
    'Test project': tests / 'Week05.IntegrationTests.csproj',
    'Integration tests': tests / 'Program.cs',
    'PowerShell runner': week / 'test.ps1',
    'Bash runner': week / 'test.sh',
    'Week05 README': week / 'README.md',
    'Week05 verification report': week / 'TEST_REPORT.md',
    'Repo README': root / 'README.md',
    'Git ignore': root / '.gitignore',
    'CI workflow': root / '.github/workflows/ci.yml',
    'dotnet tool manifest': week / '.config/dotnet-tools.json',
}
for title, path in paths.items():
    check(title + ' exists', path.is_file() and path.stat().st_size > 0)

for path in api.rglob('*.json'):
    try:
        json.loads(txt(path))
        check(path.name + ' parses as JSON', True)
    except json.JSONDecodeError:
        check(path.name + ' parses as JSON', False)
try:
    json.loads(txt(week / '.config/dotnet-tools.json'))
    check('dotnet tool manifest parses as JSON', True)
except json.JSONDecodeError:
    check('dotnet tool manifest parses as JSON', False)

for path in root.rglob('*.csproj'):
    try:
        parsed = ET.parse(path).getroot()
        check(path.name + ' valid XML', parsed.tag == 'Project')
        check(path.name + ' target net10.0', parsed.findtext('./PropertyGroup/TargetFramework') == 'net10.0')
    except ET.ParseError:
        check(path.name + ' valid XML', False)
        check(path.name + ' target net10.0', False)

endpoint = api / 'Endpoints/UserEndpoints.cs'
program = api / 'Program.cs'
entity = api / 'Domain/User.cs'
model = api / 'Data/UsersDbContext.cs'
contracts = api / 'Contracts/UserDtos.cs'
validator = api / 'Validation/UserValidator.cs'
migration = api / 'Data/Migrations/202610260001_InitialCreate.cs'
designer = api / 'Data/Migrations/202610260001_InitialCreate.Designer.cs'
snapshot = api / 'Data/Migrations/UsersDbContextModelSnapshot.cs'
runner = tests / 'Program.cs'

check('EF Core Sqlite dependency 10.0.0', 'Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.0"' in txt(paths['API project']))
check('EF Core Design dependency 10.0.0', 'Microsoft.EntityFrameworkCore.Design" Version="10.0.0"' in txt(paths['API project']))
check('DbContext exposes DbSet<User>', contains(model, 'DbSet<User> Users', 'class UsersDbContext'))
check('Entity includes six public data fields', all(token in txt(entity) for token in ('public int Id', 'public string Name', 'public string Email', 'public int Age', 'public DateTime CreatedAtUtc', 'public DateTime UpdatedAtUtc')))
check('Entity has server-only IsAdmin field', contains(entity, 'public bool IsAdmin { get; private set; }'))
check('DTO requests cannot set ID or IsAdmin', 'int Id' not in txt(contracts).split('public sealed record UserResponse')[0] and 'IsAdmin' not in txt(contracts))
check('Separate POST PUT PATCH request DTOs', all(x in txt(contracts) for x in ('CreateUserRequest', 'UpdateUserRequest', 'PatchUserRequest')))
check('Response DTO does not expose EF entity', 'UserResponse' in txt(contracts) and 'IsAdmin' not in txt(contracts))
check('Unique normalized email index', contains(model, 'HasIndex(x => x.NormalizedEmail).IsUnique()'))
check('EF context registered in DI', contains(program, 'AddDbContext<UsersDbContext>', 'UseSqlite'))
check('Async migration guarded for Development', contains(program, 'app.Environment.IsDevelopment()', 'db.Database.MigrateAsync()'))
check('POST uses SaveChangesAsync', contains(endpoint, 'users.MapPost("", Create)', 'db.Users.Add(user)', 'await db.SaveChangesAsync(ct)'))
check('201 CreatedAtRoute points to named GET', contains(endpoint, 'Results.CreatedAtRoute(', '"GetUserById"', 'users.MapGet("/{id:int}", GetById)'))
check('Both GET operations use AsNoTracking', txt(endpoint).count('AsNoTracking()') >= 2)
check('PUT and PATCH included', contains(endpoint, 'users.MapPut(', 'users.MapPatch(', 'UpdateProfile('))
check('DELETE uses Remove + SaveChangesAsync', contains(endpoint, 'users.MapDelete(', 'db.Users.Remove(user)', 'await db.SaveChangesAsync(ct)'))
check('Duplicate email conflict returns 409', contains(endpoint, 'Results.Conflict(', 'duplicate_email', 'SqliteErrorCode: 19'))
check('Paging and filtering are performed by database', contains(endpoint, 'CountAsync(ct)', '.Skip((page - 1) * pageSize)', '.Take(pageSize)', 'query.Where('))
check('Sort fields are explicitly whitelisted', all(x in txt(endpoint) for x in ('"name"', '"age"', '"email"', '"createdat"', '"id"')))
check('Pagination metadata is returned', contains(contracts, 'PagedResponse<T>', 'TotalItems', 'TotalPages', 'PageSize'))
check('Input validation checks email, name, age', contains(validator, 'EmailAddressAttribute', 'name', 'email', 'age'))
check('Validation returns field errors', contains(endpoint, 'Results.ValidationProblem(errors)'))
check('No raw EF entities returned', 'Results.Ok(user)' not in txt(endpoint) and 'Results.Ok(query)' not in txt(endpoint))
check('Migration creates Users table', contains(migration, 'migrationBuilder.CreateTable(', 'name: "Users"'))
check('Migration creates unique email index', contains(migration, 'migrationBuilder.CreateIndex(', 'IX_Users_NormalizedEmail', 'unique: true'))
check('Migration has rollback', contains(migration, 'void Down(MigrationBuilder', 'DropTable(name: "Users")'))
check('Migration designer includes BuildTargetModel', contains(designer, 'override void BuildTargetModel(', 'NormalizedEmail'))
check('Model snapshot includes unique index', contains(snapshot, 'ModelSnapshot', 'HasIndex("NormalizedEmail").IsUnique()'))
check('Factory supports environment variables', contains(paths['Design-time factory'], '.AddEnvironmentVariables()', 'IDesignTimeDbContextFactory'))
check('Create, Update, Delete all call EF SaveChangesAsync', txt(endpoint).count('await db.SaveChangesAsync(ct)') >= 4)
check('Unit test runner starts actual dotnet server', contains(runner, 'StartServer(', 'HttpClient', 'ASPNETCORE_ENVIRONMENT'))
check('Runner cleans up server and SQLite file', contains(runner, 'Kill(entireProcessTree: true)', 'File.Delete(sqlitePath + suffix)'))
check('Runner directly interrogates EF SQLite DB', contains(runner, 'GetAppliedMigrationsAsync', 'OpenDirectDatabase()'))
numbers = re.findall(r'await Test\("(\d{2}) ', txt(runner))
check('Exactly 60 sequential integration scenarios', numbers == [f'{n:02d}' for n in range(1, 61)])
check('Runner returns nonzero if a test fails', 'passed == 60 && failed == 0 ? 0 : 1' in txt(runner))
check('Development configuration includes local connection string', 'UsersDb' in txt(paths['Dev config']))
prod = json.loads(txt(paths['Production config']))
check('Production config does not contain connection string', 'ConnectionStrings' not in prod)
check('Production startup migrations disabled', prod.get('Database', {}).get('ApplyMigrationsOnStartup') is False)
check('CI invokes .NET build and tests', contains(paths['CI workflow'], "10.0.x", 'bash "week${{ matrix.week }}/test.sh"') and contains(week/'test.sh', 'dotnet build', 'dotnet run', 'Week05.IntegrationTests'))
check('HTTP file contains CRUD requests', all(token in txt(paths['Example requests']) for token in ('POST {{api}}/users', 'GET {{api}}/users', 'PUT {{api}}/users', 'PATCH {{api}}/users', 'DELETE {{api}}/users')))
check('Archive source does not include bin/obj or databases', not any(p.name in ('bin', 'obj') or p.suffix == '.db' for p in root.rglob('*')))

failed = [name for name, ok in checks if not ok]
for name, ok in checks:
    print(('PASS ' if ok else 'FAIL ') + name)
print(f'\nStatic verification: {len(checks) - len(failed)}/{len(checks)} checks passed.')
sys.exit(1 if failed else 0)
