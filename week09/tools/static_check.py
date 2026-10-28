#!/usr/bin/env python3
"""Kontrola struktury Week 09. NIE kompiluje C# i NIE uruchamia testów."""
from __future__ import annotations
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT.parent
SRC = ROOT / 'src/Week09.Api'
TEST = ROOT / 'tests/Week09.IntegrationTests'
checks: list[tuple[str, bool]] = []


def check(description: str, success: bool) -> None:
    checks.append((description, bool(success)))


def body(rel: str) -> str:
    file = PACKAGE / rel
    return file.read_text(encoding='utf8') if file.is_file() else ''


def has(rel: str, *fragments: str) -> bool:
    content = body(rel)
    return all(frag in content for frag in fragments)


required = [
    'README.md', '.gitignore', '.github/workflows/ci.yml',
    'week09/README.md', 'week09/TEST_REPORT.md', 'week09/test.sh',
    'week09/test.ps1', 'week09/tools/static_check.py',
    'week09/src/Week09.Api/Week09.Api.csproj',
    'week09/src/Week09.Api/Program.cs',
    'week09/src/Week09.Api/Contracts/ApiDtos.cs',
    'week09/src/Week09.Api/Contracts/RequestValidation.cs',
    'week09/src/Week09.Api/Data/Week09DbContext.cs',
    'week09/src/Week09.Api/Domain/Entities.cs',
    'week09/src/Week09.Api/Endpoints/AuthEndpoints.cs',
    'week09/src/Week09.Api/Endpoints/NoteEndpoints.cs',
    'week09/src/Week09.Api/Middleware/CorrelationIdMiddleware.cs',
    'week09/src/Week09.Api/Security/JwtTokenService.cs',
    'week09/src/Week09.Api/Week09.Api.http',
    'week09/src/Week09.Api/Properties/launchSettings.json',
    'week09/src/Week09.Api/appsettings.json',
    'week09/src/Week09.Api/appsettings.Development.json',
    'week09/src/Week09.Api/appsettings.Production.json',
    'week09/tests/Week09.IntegrationTests/Week09.IntegrationTests.csproj',
    'week09/tests/Week09.IntegrationTests/TestApiFactory.cs',
    'week09/tests/Week09.IntegrationTests/ApiScenario.cs',
    'week09/tests/Week09.IntegrationTests/IntegrationTests.cs',
]
for rel in required:
    file = PACKAGE / rel
    check('Obecny plik: ' + rel, file.is_file() and file.stat().st_size > 0)

for rel in [r for r in required if r.endswith('.csproj')]:
    try:
        proj = ET.parse(PACKAGE / rel).getroot()
        check('Poprawny XML: ' + rel, proj.tag == 'Project')
        check('Framework net10.0: ' + rel, proj.findtext('./PropertyGroup/TargetFramework') == 'net10.0')
    except ET.ParseError:
        check('Poprawny XML: ' + rel, False)
        check('Framework net10.0: ' + rel, False)

for rel in [r for r in required if r.endswith('.json')]:
    try:
        result = json.loads(body(rel))
        check('Poprawny JSON: ' + rel, isinstance(result, dict))
    except (json.JSONDecodeError, OSError):
        check('Poprawny JSON: ' + rel, False)

api_proj = 'week09/src/Week09.Api/Week09.Api.csproj'
test_proj = 'week09/tests/Week09.IntegrationTests/Week09.IntegrationTests.csproj'
api_program = 'week09/src/Week09.Api/Program.cs'
factory = 'week09/tests/Week09.IntegrationTests/TestApiFactory.cs'
scenario = 'week09/tests/Week09.IntegrationTests/ApiScenario.cs'
tests_path = 'week09/tests/Week09.IntegrationTests/IntegrationTests.cs'
contract = 'week09/src/Week09.Api/Contracts/ApiDtos.cs'
validation = 'week09/src/Week09.Api/Contracts/RequestValidation.cs'
auth = 'week09/src/Week09.Api/Endpoints/AuthEndpoints.cs'
notes = 'week09/src/Week09.Api/Endpoints/NoteEndpoints.cs'
db = 'week09/src/Week09.Api/Data/Week09DbContext.cs'

check('API SDK Web', has(api_proj, 'Microsoft.NET.Sdk.Web'))
check('EF Core Sqlite', has(api_proj, 'Microsoft.EntityFrameworkCore.Sqlite'))
check('JWT Bearer', has(api_proj, 'Microsoft.AspNetCore.Authentication.JwtBearer'))
check('WebApplicationFactory dependency', has(test_proj, 'Microsoft.AspNetCore.Mvc.Testing'))
check('xUnit package', has(test_proj, 'Include="xunit"'))
check('xUnit runner', has(test_proj, 'xunit.runner.visualstudio'))
check('Test SDK', has(test_proj, 'Microsoft.NET.Test.Sdk'))
check('Test project reference', has(test_proj, '../../src/Week09.Api/Week09.Api.csproj'))
check('Public Program for test host', has(api_program, 'public partial class Program'))
check('Health API', has(api_program, 'MapGet("/health"', '"healthy"'))
check('JWT signing key externally configured', has(api_program, 'Jwt:SigningKey', 'GetByteCount(signingKey) < 32'))
check('No hard-coded JWT signing key in API Program', 'test-only-key' not in body(api_program))
check('JWT token validity', has(api_program, 'ValidateIssuerSigningKey = true', 'ValidateLifetime = true'))
check('Authorization enabled', has(api_program, 'UseAuthentication()', 'UseAuthorization()'))
check('Dedicated exception handler', has(api_program, 'UseExceptionHandler(', 'Results.Problem(statusCode: 500'))
check('Dedicated correlation middleware', has(api_program, 'UseMiddleware<CorrelationIdMiddleware>'))
check('Test error endpoint only in Testing', has(api_program, 'IsEnvironment("Testing")', '/__test/throw'))
check('DbContext EF Core', has(db, 'class Week09DbContext', 'DbSet<AppUser>', 'DbSet<Note>'))
check('Uniqueness email enforced by database', has(db, 'HasIndex(user => user.Email).IsUnique()'))
check('Relation owner->notes', has(db, 'HasForeignKey(note => note.OwnerId)'))
check('HTTP auth registration and login', has(auth, 'MapPost("/register"', 'MapPost("/login"'))
check('Password hashing and verification', has(auth, 'hasher.HashPassword', 'hasher.VerifyHashedPassword'))
check('Registration returns 201 and conflict 409', has(auth, 'Results.Created(', 'statusCode: 409'))
check('User input validated', has(auth, 'RequestValidation.Check(request)'))
check('Validation returns errors dictionary', has(validation, 'Validator.TryValidateObject', 'JsonNamingPolicy.CamelCase'))
check('Data annotations for register', has(contract, '[Required, EmailAddress', '[Required, MinLength(8)'))
check('Data annotations for notes', has(contract, 'public sealed class NoteRequest', 'StringLength(100', 'StringLength(2000)'))
check('Notes CRUD methods mapped', has(notes, 'MapGet(""', 'MapGet("/{id:guid}"', 'MapPost(""', 'MapPut("/{id:guid}"', 'MapDelete("/{id:guid}"'))
check('Notes require authorization', has(notes, 'RequireAuthorization()'))
check('Read filters notes by owner', has(notes, 'Where(note => note.OwnerId == ownerId)'))
check('Single-record filter includes owner', has(notes, 'item.Id == id && item.OwnerId == ownerId'))
check('No-tracking reads', has(notes, 'AsNoTracking()'))
check('Admin endpoint guarded by role', has(notes, 'RequireRole("Admin")'))
check('HTTP 201, 204 and 404 routes implemented', has(notes, 'Results.Created(', 'Results.NoContent()', 'Results.NotFound()'))
check('Factory inherits WebApplicationFactory', has(factory, 'WebApplicationFactory<Program>'))
check('Factory creates independent in-memory SQLite', has(factory, 'new SqliteConnection("Data Source=:memory:")'))
check('Factory opens connection', has(factory, '_connection.Open()'))
check('Factory overrides services', has(factory, 'ConfigureTestServices(', 'AddDbContext<Week09DbContext>'))
check('Factory removes previous configuration', has(factory, 'RemoveAll<DbContextOptions<Week09DbContext>>', 'RemoveAll<IDbContextOptionsConfiguration<Week09DbContext>>'))
check('Factory cleans up connection', has(factory, '_connection?.Dispose()'))
check('Factory overrides environment', has(factory, 'UseEnvironment("Testing")'))
check('Factory supplies test-only JWT key', has(factory, 'Jwt:SigningKey', 'TestJwtKey'))
check('Scenario creates new factory + client', has(scenario, 'TestApiFactory Factory { get; } = new()', 'Factory.CreateClient()'))
check('Scenario authenticates via /auth/login', has(scenario, 'LoginAsync(', 'AuthenticationHeaderValue("Bearer", token)'))
check('Scenario can promote test-only user', has(scenario, 'PromoteToAdminAsync(', 'user.Role = "Admin"'))
check('Scenario cleans resources', has(scenario, 'Factory.Dispose()', 'Client.Dispose()'))

methods = re.findall(r'\[Fact\]\s*public async Task (\w+)\(', body(tests_path))
check('Exactly 39 xUnit Fact tests', len(methods) == 39)
check('Test methods have unique names', len(methods) == len(set(methods)))
for fragment, name in [
    ('Health_returns_200_and_expected_JSON_body', 'Health: 200 + JSON'),
    ('Missing_note_returns_404_independent_of_data', 'Resource not found: 404'),
    ('Successful_POST_creates_a_note_with_201_and_Location', 'POST: 201 + Location'),
    ('Invalid_POST_returns_400_with_validation_errors', 'POST: 400 + validation'),
    ('Two_factories_have_completely_isolated_databases', 'Isolated DB factories'),
    ('Users_cannot_read_other_users_notes', 'Auth: ownership read'),
    ('Users_cannot_modify_other_users_notes', 'Auth: ownership update'),
    ('Users_cannot_delete_other_users_notes', 'Auth: ownership delete'),
    ('Admin_role_can_access_statistics', 'Auth: admin'),
    ('Exception_middleware_returns_500_without_internal_error_details', 'Middleware: exception'),
    ('Invalid_correlation_id_is_replaced', 'Middleware: correlation'),
    ('PUT_changes_title_and_body', 'CRUD update'),
    ('DELETE_existing_note_returns_204', 'CRUD delete'),
]:
    check('Test: ' + name, fragment in methods)

check('No real database credentials in production JSON', 'ConnectionStrings' not in body('week09/src/Week09.Api/appsettings.Production.json'))
check('No JWT secret in any API config file', all('SigningKey' not in body(r) for r in required if r.startswith('week09/src') and r.endswith('.json')))
check('CI restores, builds and runs dotnet test', has('.github/workflows/ci.yml', 'bash \"week${{ matrix.week }}/test.sh\"') and has('week09/test.sh', 'dotnet restore', 'dotnet test'))
check('CI uploads TRX result', has('.github/workflows/ci.yml', 'actions/upload-artifact@v4', 'week09.trx'))
check('Bash test script calls dotnet test', has('week09/test.sh', 'dotnet test'))
check('PowerShell test script calls dotnet test', has('week09/test.ps1', 'dotnet test'))
check('No compiled artifacts committed', not any(p.name in ('bin', 'obj', '.env') for p in ROOT.rglob('*')))

# Parse request bodies from the hand-authored .http file, but skip sections without JSON.
http = body('week09/src/Week09.Api/Week09.Api.http')
examples = re.split(r'(?m)^###', http)
valid_json = 0
broken_json = []
for part in examples:
    if 'Content-Type: application/json' not in part:
        continue
    try:
        _, example_json = part.split('\n\n', 1)
        json.loads(example_json.strip())
        valid_json += 1
    except (ValueError, json.JSONDecodeError):
        broken_json.append(part[:40])
check('All four JSON HTTP examples parse', valid_json == 4 and not broken_json)

for name, success in checks:
    print(f'{"PASS" if success else "FAIL"}  {name}')
passed = sum(success for _, success in checks)
print(f'\nSTATIC CHECKS: {passed}/{len(checks)} passed.')
if passed != len(checks):
    sys.exit(1)
