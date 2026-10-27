#!/usr/bin/env python3
"""Offline structural checks; DOES NOT compile or run the .NET application."""
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent
repo = root.parent
api = root / 'src' / 'Week02.Api'
tests = root / 'tests' / 'Week02.IntegrationTests'

sources = {
    name: (api / 'Endpoints' / f'{name}Endpoints.cs').read_text(encoding='utf-8')
    for name in ('User', 'Task', 'Product')
}
contracts = (api / 'Contracts' / 'ApiContracts.cs').read_text(encoding='utf-8')
repos = (api / 'Repositories' / 'Catalogs.cs').read_text(encoding='utf-8')
program = (api / 'Program.cs').read_text(encoding='utf-8')
test_code = (tests / 'Program.cs').read_text(encoding='utf-8')
workflow = (repo / '.github' / 'workflows' / 'week02-ci.yml').read_text(encoding='utf-8')
checks = []

def check(label, condition):
    checks.append((label, bool(condition)))

necessary = [
    api / 'Week02.Api.csproj', api / 'Week02.Api.http', api / 'Program.cs',
    api / 'Contracts' / 'ApiContracts.cs', api / 'Domain' / 'Entities.cs',
    api / 'Repositories' / 'Catalogs.cs',
    *[api / 'Endpoints' / f'{name}Endpoints.cs' for name in sources],
    tests / 'Week02.IntegrationTests.csproj', tests / 'Program.cs',
    root / 'README.md', root / 'test.ps1', root / 'test.sh',
    repo / 'README.md', repo / '.gitignore', repo / '.github' / 'workflows' / 'week02-ci.yml'
]
check('All required source, CI and documentation files are present', all(p.is_file() for p in necessary))

projects = []
for file in (api / 'Week02.Api.csproj', tests / 'Week02.IntegrationTests.csproj'):
    try:
        projects.append(ET.parse(file).getroot())
    except ET.ParseError:
        pass
check('Both csproj files parse as XML', len(projects) == 2)
check('Both projects target net10.0', len(projects) == 2 and all(p.findtext('.//TargetFramework') == 'net10.0' for p in projects))
check('API uses Microsoft.NET.Sdk.Web', len(projects) == 2 and projects[0].attrib.get('Sdk') == 'Microsoft.NET.Sdk.Web')
check('Tests are a standalone executable', len(projects) == 2 and projects[1].findtext('.//OutputType') == 'Exe')
check('No external NuGet package references', all('<PackageReference' not in p.read_text() for p in (api / 'Week02.Api.csproj', tests / 'Week02.IntegrationTests.csproj')))
check('Endpoint groups are registered in Program.cs', all('app.Map'+name+'Endpoints()' in program for name in ('User', 'Task', 'Product')))
check('Seed repositories are registered in DI', all(x in program for x in ('AddSingleton<IUserDirectory', 'AddSingleton<ITaskCatalog', 'AddSingleton<IProductCatalog')))
check('GET /users/{id:int} is mapped', 'MapGroup("/users")' in sources['User'] and 'MapGet("/{id:int}"' in sources['User'])
check('User ID has 200/400/404 typed variants', all(x in sources['User'] for x in ('Results<Ok<UserDto>, BadRequest<ApiError>, NotFound<ApiError>>', 'TypedResults.Ok(', 'TypedResults.NotFound(', 'TypedResults.BadRequest(')))
check('GET /users query parameters have defaults', all(x in sources['User'] for x in ('string? search = null', 'int page = 1', 'int pageSize = 10')))
check('GET /users validates page and pageSize bounds', 'page is < 1 or > 10_000' in sources['User'] and 'pageSize is < 1 or > 50' in sources['User'])
check('GET /users validates search length', 'Length: > 100' in sources['User'])
check('GET /users searches name and email ignoring case', all(x in sources['User'] for x in ('user.Name.Contains(query, StringComparison.OrdinalIgnoreCase)', 'user.Email.Contains(query, StringComparison.OrdinalIgnoreCase)')))
check('GET /users uses deterministic pagination', all(x in sources['User'] for x in ('OrderBy(user => user.Id)', '.Skip((page - 1) * pageSize)', '.Take(pageSize)')))
check('GET /tasks is in its own group', 'MapGroup("/tasks")' in sources['Task'] and 'MapGet("/{id:int}"' in sources['Task'] and 'MapGet("", ListTasks)' in sources['Task'])
check('GET /tasks/{id} has 200/400/404 typed variants', 'Results<Ok<TaskDto>, BadRequest<ApiError>, NotFound<ApiError>>' in sources['Task'])
check('Products expose separate v1/v2 groups', all(x in sources['Product'] for x in ('MapGroup("/api/v1/products")', 'MapGroup("/api/v2/products")')))
check('v1 and v2 have independent DTO types', all(x in contracts for x in ('record ProductV1Dto', 'record ProductV2Dto', 'record MoneyV2Dto')))
check('v1 and v2 map different price contracts', 'new(product.Id, product.Name, product.Price, product.InStock)' in sources['Product'] and 'new MoneyV2Dto(product.Price, product.Currency)' in sources['Product'])
check('Product groups share one catalog service', 'IProductCatalog catalog' in sources['Product'] and 'interface IProductCatalog' in repos and 'SeededProductCatalog' in repos)
check('Product single-resource routes each have 200/400/404', all(x in sources['Product'] for x in ('Results<Ok<ProductV1Dto>, BadRequest<ApiError>, NotFound<ApiError>>', 'Results<Ok<ProductV2Dto>, BadRequest<ApiError>, NotFound<ApiError>>')))
check('API uses stable in-memory fixtures', all(x in repos for x in ('SeededUserDirectory', 'SeededTaskCatalog', 'SeededProductCatalog', 'Array.AsReadOnly')))
ids = re.findall(r'await Check\("(\d\d) ', test_code)
check('Exactly 42 sequential HTTP scenarios are included', ids == [f'{n:02}' for n in range(1, 43)])
check('Integration tests launch API and use actual HttpClient', 'Process.Start(info)' in test_code and 'new HttpClient' in test_code and 'StartApi(' in test_code)
check('Integration tests terminate the server', 'api.Kill(entireProcessTree: true)' in test_code and 'await api.WaitForExitAsync()' in test_code)
check('GitHub Actions targets .NET 10', "dotnet-version: '10.0.x'" in workflow and 'week02/' in workflow)
check('GitHub Actions compiles API and tests and invokes the tests', 'dotnet build week02/src/' in workflow and 'dotnet build week02/tests/' in workflow and 'dotnet run --project week02/tests/' in workflow)
check('PowerShell runner checks exit codes', '$LASTEXITCODE -ne 0' in (root / 'test.ps1').read_text())
check('Bash runner syntax passes bash -n', subprocess.run(['bash', '-n', str(root / 'test.sh')], capture_output=True).returncode == 0)
check('HTTP examples include both API versions', all(x in (api / 'Week02.Api.http').read_text() for x in ('/api/v1/products', '/api/v2/products')))
check('README documents the five exercises and version differences', all(x in (root / 'README.md').read_text() for x in ('Route parameter', 'Query parameters', 'Route groups', 'Typed results', 'Wersjonowanie kontraktu')))
check('No build artifacts are included', not any(p.name in ('obj', 'bin') for p in root.rglob('*')))

passed = sum(ok for _, ok in checks)
for label, ok in checks:
    print(f'{"PASS" if ok else "FAIL"} {label}')
print(f'\nStatic checks: {passed}/{len(checks)} PASS')
sys.exit(0 if passed == len(checks) else 1)
