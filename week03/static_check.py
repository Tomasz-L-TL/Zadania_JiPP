#!/usr/bin/env python3
"""Kontrole strukturalne Week 03 — nie zastępują kompilacji ani testów .NET."""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent
PKG = ROOT.parent
SRC = ROOT / 'src/Week03.Api'
TEST = ROOT / 'tests/Week03.IntegrationTests'
checks: list[tuple[str, bool]] = []


def check(name: str, condition: bool) -> None:
    checks.append((name, bool(condition)))


def text(path: Path) -> str:
    return path.read_text(encoding='utf-8') if path.is_file() else ''


def has(path: Path, *tokens: str) -> bool:
    content = text(path)
    return bool(content) and all(token in content for token in tokens)


api_proj = SRC / 'Week03.Api.csproj'
test_proj = TEST / 'Week03.IntegrationTests.csproj'
program = SRC / 'Program.cs'
user_dto = SRC / 'Dto/CreateUserRequest.cs'
task_dto = SRC / 'Dto/CreateTaskRequest.cs'
domain = SRC / 'Domain/Entities.cs'
responses = SRC / 'Contracts/Responses.cs'
user_service = SRC / 'Services/UserRegistrationService.cs'
user_repo = SRC / 'Repositories/UserRepository.cs'
task_repo = SRC / 'Repositories/TaskRepository.cs'
validator_contract = SRC / 'Validation/IRequestValidator.cs'
validator = SRC / 'Validation/DataAnnotationsRequestValidator.cs'
filter_path = SRC / 'Validation/ValidationFilter.cs'
users = SRC / 'Endpoints/UserEndpoints.cs'
tasks = SRC / 'Endpoints/TaskEndpoints.cs'
http = SRC / 'Week03.Api.http'
harness = TEST / 'Program.cs'
ci = PKG / '.github/workflows/ci.yml'

for file in [api_proj, test_proj, program, user_dto, task_dto, domain, responses,
             user_service, user_repo, task_repo, validator_contract, validator,
             filter_path, users, tasks, http, harness, ci, ROOT/'README.md',
             ROOT/'TEST_REPORT.md', ROOT/'test.sh', ROOT/'test.ps1', PKG/'README.md']:
    check(f'Istnieje {file.relative_to(PKG)}', file.is_file() and file.stat().st_size > 0)

for path in [api_proj, test_proj]:
    try:
        project_xml = ET.parse(path).getroot()
        check(f'{path.name}: poprawny XML', project_xml.tag == 'Project')
        check(f'{path.name}: net10.0', project_xml.findtext('./PropertyGroup/TargetFramework') == 'net10.0')
        check(f'{path.name}: nullable', project_xml.findtext('./PropertyGroup/Nullable') == 'enable')
    except ET.ParseError:
        check(f'{path.name}: poprawny XML', False)
        check(f'{path.name}: net10.0', False)
        check(f'{path.name}: nullable', False)

check('DTO CreateUserRequest oddzielone od domeny', has(user_dto, 'class CreateUserRequest') and has(domain, 'record User('))
check('DTO CreateTaskRequest oddzielone od domeny', has(task_dto, 'class CreateTaskRequest') and has(domain, 'record TaskItem('))
check('DTO użytkownika nie ma Id', ' Id {' not in text(user_dto))
check('DTO użytkownika nie ma właściwości CreatedAtUtc', re.search(r'\bDateTimeOffset\??\s+CreatedAtUtc\s*\{', text(user_dto)) is None)
check('DTO user: Required', text(user_dto).count('[Required(') >= 4)
check('DTO user: StringLength', text(user_dto).count('[StringLength(') >= 3)
check('DTO user: EmailAddress', '[EmailAddress(' in text(user_dto))
check('DTO user: Range', '[Range(18, 120' in text(user_dto))
check('DTO user: RegularExpression', '[RegularExpression(' in text(user_dto))
check('DTO task: Required', text(task_dto).count('[Required(') >= 2)
check('DTO task: Range', '[Range(1, 5' in text(task_dto))
check('DTO task: StringLength', '[StringLength(' in text(task_dto))
check('Adnotacje mają komunikaty', text(user_dto).count('ErrorMessage =') >= 9)
check('UserResponse mapuje domenę', has(responses, 'UserResponse', 'FromDomain(User'))
check('TaskResponse mapuje domenę', has(responses, 'TaskResponse', 'FromDomain(TaskItem'))
check('Walidator używa TryValidateObject', has(validator, 'Validator.TryValidateObject(', 'validateAllProperties: true'))
check('Walidator zwraca błędy wielu pól', has(validator, 'MemberNames', 'ToDictionary('))
check('Klucze błędów są camelCase', has(validator, 'JsonNamingPolicy.CamelCase'))
check('Generyczny filtr IEndpointFilter', has(filter_path, 'ValidationFilter<TRequest>', 'IEndpointFilter'))
check('Filtr wywołuje walidator', has(filter_path, 'validator.Validate(input)'))
check('Filtr zwraca ValidationProblem', has(filter_path, 'Results.ValidationProblem('))
check('Filtr odrzuca błędy przed next(context)', text(filter_path).index('Results.ValidationProblem(') < text(filter_path).index('next(context)'))
check('Filtr na users', has(users, '.AddEndpointFilter<ValidationFilter<CreateUserRequest>>()'))
check('Filtr na tasks', has(tasks, '.AddEndpointFilter<ValidationFilter<CreateTaskRequest>>()'))
check('Osobny serwis logiki biznesowej', has(user_service, 'class UserRegistrationService', 'RegistrationResult'))
check('Serwis mapuje DTO -> domena', has(user_service, 'new User(', 'request.Login!.Trim()'))
check('Repozytorium chroni unikalność loginu', has(user_repo, 'TryAdd(', 'StringComparison.OrdinalIgnoreCase'))
check('Repozytorium zapisuje atomowo', has(user_repo, 'lock (_gate)', '_users.Add(saved)'))
check('Repozytorium tasks thread-safe', has(task_repo, 'lock (_gate)', '_tasks.Add(saved)'))
check('Duplikat dostaje 409', has(users, 'Status409Conflict', 'duplicate_login'))
check('Duplikat dostaje ProblemDetails', has(users, 'Results.Problem('))
check('Users obsługuje POST, GET i GET/id', all(route in text(users) for route in ['MapPost(""', 'MapGet(""', 'MapGet("/{id:int}"']))
check('Tasks obsługuje POST, GET i GET/id', all(route in text(tasks) for route in ['MapPost(""', 'MapGet(""', 'MapGet("/{id:int}"']))
check('DI rejestruje walidator', has(program, 'AddSingleton<IRequestValidator, DataAnnotationsRequestValidator>'))
check('DI rejestruje repozytoria', has(program, 'IUserRepository', 'ITaskRepository'))
check('DI rejestruje serwis biznesowy', has(program, 'AddSingleton<UserRegistrationService>()'))
check('Mapa endpoints users, tasks', has(program, 'MapUserEndpoints()', 'MapTaskEndpoints()'))
check('AddProblemDetails w kontenerze', has(program, 'AddProblemDetails()'))
check('Test runner uruchamia serwer', has(harness, 'StartServer(', 'WaitForReady()'))
check('Test runner kończy serwer', has(harness, 'Kill(entireProcessTree: true)'))
check('60 scenariuszy HTTP', text(harness).count('await Check(') == 60)
check('Test unikalności pod współbieżnym ruchem', has(harness, 'Task.WhenAll(concurrent)', 'StatusCode == HttpStatusCode.Conflict'))
check('Test walidacji wielu pól', has(harness, 'Cztery błędne pola', 'Wiele błędów w zadaniu'))
check('Testy zweryfikują application/problem+json', has(harness, 'application/problem+json'))
check('GitHub Actions instaluje .NET 10', has(ci, '10.0.x', 'actions/setup-dotnet@v4'))
check('GitHub Actions uruchamia testy', has(ci, 'bash \"week${{ matrix.week }}/test.sh\"') and has(ROOT/'test.sh', 'Week03.IntegrationTests', 'dotnet run --project'))
check('Skrypty testowe istnieją dla obu systemów', has(ROOT/'test.ps1','dotnet run') and has(ROOT/'test.sh','dotnet run'))

http_segments = re.split(r'(?m)^###', text(http))
payloads = [segment.split('\n\n', 1)[1].strip() for segment in http_segments
            if 'Content-Type: application/json' in segment and '\n\n' in segment]
# Każdy przykład .http parsujemy zwykłym json.loads, bez dodatkowych pakietów.
valid_payloads = True
for payload in payloads:
    try:
        json.loads(payload)
    except json.JSONDecodeError:
        valid_payloads = False
check('5 przykładów HTTP zawiera poprawny JSON', len(payloads) == 5 and valid_payloads)

failed = [(name, result) for name, result in checks if not result]
for name, result in checks:
    print(f'{"OK" if result else "FAIL":>4} {name}')
print(f'\nKontrole statyczne: {len(checks) - len(failed)}/{len(checks)} zaliczonych.')
if failed:
    print('BŁĘDY:', ', '.join(name for name, _ in failed), file=sys.stderr)
    sys.exit(1)
