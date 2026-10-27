#!/usr/bin/env python3
"""Strukturalna kontrola Week10. Nie kompiluje projektu C# i nie uruchamia xUnit."""
from pathlib import Path
from xml.etree import ElementTree as ET
import json
import re
import subprocess
import sys

root = Path(__file__).resolve().parent
repo = root.parent
api = root / 'src/Week10.Api'
test = root / 'tests/Week10.IntegrationTests'
checks = []


def check(description, ok):
    checks.append((description, bool(ok)))


def contents(relative):
    file = root / relative
    return file.read_text(encoding='utf8') if file.is_file() else ''


required = [
    'README.md', 'TEST_REPORT.md', 'test.sh', 'test.ps1', 'Week10.slnx',
    '.config/dotnet-tools.json',
    'src/Week10.Api/Week10.Api.csproj', 'src/Week10.Api/Program.cs',
    'src/Week10.Api/Domain/Entities.cs', 'src/Week10.Api/Contracts/Contracts.cs',
    'src/Week10.Api/Data/AppDbContext.cs', 'src/Week10.Api/Data/AppDbContextFactory.cs',
    'src/Week10.Api/Data/Migrations/202610260001_InitialCreate.cs',
    'src/Week10.Api/Data/Migrations/202610260001_InitialCreate.Designer.cs',
    'src/Week10.Api/Data/Migrations/AppDbContextModelSnapshot.cs',
    'src/Week10.Api/Endpoints/AuthEndpoints.cs', 'src/Week10.Api/Endpoints/UserEndpoints.cs',
    'src/Week10.Api/Endpoints/TaskEndpoints.cs', 'src/Week10.Api/Endpoints/TagEndpoints.cs',
    'src/Week10.Api/Endpoints/ReportEndpoints.cs', 'src/Week10.Api/Services/ApiHelpers.cs',
    'src/Week10.Api/Services/DevSeeder.cs', 'src/Week10.Api/Security/JwtService.cs',
    'src/Week10.Api/appsettings.json', 'src/Week10.Api/appsettings.Development.json',
    'src/Week10.Api/appsettings.Production.json', 'src/Week10.Api/Properties/launchSettings.json',
    'src/Week10.Api/Week10.Api.http', 'tests/Week10.IntegrationTests/ApiTests.cs',
    'tests/Week10.IntegrationTests/ApiFactory.cs',
    'tests/Week10.IntegrationTests/Week10.IntegrationTests.csproj',
]
for file in required:
    check('Required file: ' + file, (root / file).is_file())

for project in sorted(root.rglob('*.csproj')):
    try:
        tree = ET.parse(project).getroot()
        check('Valid .csproj XML: ' + project.name, tree.tag == 'Project')
        check('net10.0: ' + project.name, tree.findtext('.//TargetFramework') == 'net10.0')
    except ET.ParseError:
        check('Valid .csproj XML: ' + project.name, False)
        check('net10.0: ' + project.name, False)

for file in sorted(root.rglob('*.json')):
    try:
        json.loads(file.read_text(encoding='utf8'))
        check('Valid JSON: ' + file.name, True)
    except ValueError:
        check('Valid JSON: ' + file.name, False)

try:
    ET.parse(root / 'Week10.slnx')
    check('Solution .slnx is XML', True)
except ET.ParseError:
    check('Solution .slnx is XML', False)

sources = {p.name: p.read_text(encoding='utf8') for p in api.rglob('*.cs')}
program = sources['Program.cs']
entities = sources['Entities.cs']
model = sources['AppDbContext.cs']
contracts = sources['Contracts.cs']
auth = sources['AuthEndpoints.cs']
users = sources['UserEndpoints.cs']
tasks = sources['TaskEndpoints.cs']
tags = sources['TagEndpoints.cs']
reports = sources['ReportEndpoints.cs']
seed = sources['DevSeeder.cs']
migration = sources['202610260001_InitialCreate.cs']
designer = sources['202610260001_InitialCreate.Designer.cs']
snapshot = sources['AppDbContextModelSnapshot.cs']
xunit = (test / 'ApiTests.cs').read_text(encoding='utf8')
factory = (test / 'ApiFactory.cs').read_text(encoding='utf8')

check('EF Core Sqlite dependency', 'Microsoft.EntityFrameworkCore.Sqlite' in contents('src/Week10.Api/Week10.Api.csproj'))
check('JWT bearer dependency', 'Microsoft.AspNetCore.Authentication.JwtBearer' in contents('src/Week10.Api/Week10.Api.csproj'))
check('xunit dependencies', all(x in contents('tests/Week10.IntegrationTests/Week10.IntegrationTests.csproj') for x in ['xunit', 'Microsoft.NET.Test.Sdk', 'Microsoft.AspNetCore.Mvc.Testing']))
check('3 domain models', all('class ' + name in entities for name in ['User', 'TaskItem', 'Tag']))
check('Join entity for many-to-many', 'class TaskTag' in entities and 'TaskItemId' in entities and 'TagId' in entities)
check('Data context has DbSet entities', all(x in model for x in ['DbSet<User>', 'DbSet<TaskItem>', 'DbSet<Tag>', 'DbSet<TaskTag>']))
check('One to many FK', 'HasForeignKey(x => x.OwnerId)' in model)
check('Many to many composite key', 'new { x.TaskItemId, x.TagId }' in model)
check('Unique user and tag indexes', model.count('.IsUnique()') >= 3)
check('Foreign keys and cascade in migration', migration.count('ForeignKey(') >= 3 and migration.count('ReferentialAction.Cascade') >= 3)
check('Migration includes all four tables', all('CreateTable("' + name + '"' in migration for name in ('Users', 'Tasks', 'Tags', 'TaskTags')))
check('Migration has Up and Down', 'void Up(' in migration and 'void Down(' in migration)
check('Migration target model', 'BuildTargetModel' in designer)
check('EF model snapshot', 'ModelSnapshot' in snapshot and 'BuildModel' in snapshot)
check('Enum string conversion in snapshot', 'HasConversion<string>()' in snapshot)
check('Request DTOs have validation', contracts.count('[Required') >= 5 and 'EmailAddress' in contracts)
check('Separate response DTOs', all(x in contracts for x in ['record UserDto', 'record TaskDto', 'record TagDto', 'record PagedDto']))
check('Registration and login endpoints', 'MapPost("/register"' in auth and 'MapPost("/login"' in auth)
check('User create alias /users', 'MapPost("/users"' in auth)
check('PasswordHasher is used', 'hasher.HashPassword' in auth and 'hasher.VerifyHashedPassword' in auth)
check('JWT token issuance', 'JwtSecurityToken' in sources['JwtService.cs'])
check('JWT configured with key length guard', 'GetByteCount(jwtKey) < 32' in program)
check('JWT validation parameters', all(x in program for x in ['ValidateIssuer = true', 'ValidateAudience = true', 'ValidateLifetime = true', 'ValidateIssuerSigningKey = true']))
check('Role-based admin policy', 'AddPolicy("AdminOnly"' in program)
check('401/403 through bearer/policies', 'UseAuthentication()' in program and 'UseAuthorization()' in program)
check('User CRUD endpoints', all(x in users for x in ['MapGet(""', 'MapGet("/{id:int}"', 'MapPut("/{id:int}"', 'MapDelete("/{id:int}"']))
check('Task CRUD endpoints', all(x in tasks for x in ['MapGet(""', 'MapGet("/{id:int}"', 'MapPost(""', 'MapPut("/{id:int}"', 'MapDelete("/{id:int}"']))
check('Ownership authorization', 'ApiHelpers.CurrentId(principal)' in tasks and 'Visible(db, principal)' in tasks and 'Results.Forbid()' in tasks)
check('Task owner not changeable', 'Owner cannot be changed' in tasks)
check('Pagination in CRUD', 'Skip((page - 1) * pageSize).Take(pageSize)' in users and 'Skip((page - 1) * pageSize).Take(pageSize)' in tasks)
check('SQL filtered read', 'AsNoTracking()' in users and 'AsNoTracking()' in tasks)
check('Validates task status', 'Enum.TryParse<WorkStatus>' in tasks)
check('Tag create limited to admin', 'RequireAuthorization("AdminOnly")' in tags)
check('Tag change computes add and delete instead of replacing all', 'desired.Where(x => !current.Contains(x))' in tasks and 'db.TaskTags.Remove(prior)' in tasks)
check('Report by status', '"/tasks/by-status"' in reports)
check('Report by user', '"/tasks/by-user"' in reports)
check('Report top tags', '"/tags/top"' in reports)
check('Report overdue', '"/tasks/overdue"' in reports)
check('Reports admin-only', '.RequireAuthorization("AdminOnly")' in reports)
check('Report LINQ aggregates', 'GroupBy(' in reports and '.Count' in reports)
check('Config for error middleware', 'UseExceptionHandler()' in program and 'AddProblemDetails()' in program)
check('Structured logging templates', 'LogInformation(' in program and '{Method}' in program and '{StatusCode}' in program)
check('No password logged by request middleware', 'Request.Body' not in program and 'context.Request.Headers' not in program and 'context.Request.Body' not in program)
check('Development-only DB migration', 'app.Environment.IsDevelopment()' in program and 'db.Database.MigrateAsync()' in program)
check('Development-only demo seed', 'env.IsDevelopment()' in seed and 'SeedAsync' in seed)
check('Demo admin needs external credentials', 'DevelopmentSeed:AdminPassword' in seed and 'password!.Length < 12' in seed and 'hasher.HashPassword(' in seed)
check('No password returned in DTO', 'PasswordHash' not in contracts)
check('49 or more xUnit Fact test cases', xunit.count('[Fact]') >= 49)
check('WebApplicationFactory is used', 'WebApplicationFactory<Program>' in factory)
check('SQLite memory database per factory', 'Data Source=:memory:' in factory and 'SqliteConnection' in factory)
check('Factory overrides DB service', 'ConfigureTestServices(' in factory and 'UseSqlite(_connection)' in factory)
check('Test JWT access control', 'Unauthorized' in xunit and 'Forbidden' in xunit)
check('Test four reports', all(x in xunit for x in ['/reports/tasks/by-status', '/reports/tasks/by-user', '/reports/tags/top', '/reports/tasks/overdue']))
check('Test migration applied', 'GetAppliedMigrationsAsync' in xunit)
check('Test DB isolation', 'Every_factory_uses_isolated_sqlite_database' in xunit)

try:
    development = json.loads(contents('src/Week10.Api/appsettings.Development.json'))
    production = json.loads(contents('src/Week10.Api/appsettings.Production.json'))
    default = json.loads(contents('src/Week10.Api/appsettings.json'))
    check('Production has no connection string', 'ConnectionStrings' not in production)
    check('Production has no signing key', all('SigningKey' not in json.dumps(j) for j in [production, development, default]))
    check('Production disables migration and seed', not production['Database']['ApplyMigrationsOnStartup'] and not production['Database']['SeedDemoData'])
    check('Development applies migration and seed', development['Database']['ApplyMigrationsOnStartup'] and development['Database']['SeedDemoData'])
except (ValueError, KeyError) as error:
    check('Appsettings configuration is valid', False)

check('No SQLite or binary files included', not any(p.is_file() and p.suffix.lower() in ('.db', '.sqlite', '.dll', '.exe') for p in repo.rglob('*')))
check('CI workflow exists', (repo/'.github/workflows/week10-ci.yml').is_file())
check('CI runs build and xUnit', all(x in (repo/'.github/workflows/week10-ci.yml').read_text() for x in ('dotnet build', 'dotnet test', '10.0.x')))
check('bash test script syntax', subprocess.run(['bash', '-n', str(root/'test.sh')], capture_output=True).returncode == 0)

for description, ok in checks:
    print(f'{"PASS" if ok else "FAIL"} {description}')

passed = sum(ok for _, ok in checks)
print(f'\nStatic checks: {passed}/{len(checks)} passed')
sys.exit(0 if passed == len(checks) else 1)
