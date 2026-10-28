#!/usr/bin/env python3
"""Offline structural validation. Does not compile or execute .NET code."""
from __future__ import annotations
from pathlib import Path
from xml.etree import ElementTree as ET
import json
import re
import subprocess
import sys

root = Path(__file__).resolve().parent
repo = root.parent
src = root / 'src/Week07.Api'
checks: list[tuple[str, bool]] = []


def text(rel: str) -> str:
    file = repo / rel
    return file.read_text(encoding='utf-8') if file.is_file() else ''


def check(description: str, success: bool) -> None:
    checks.append((description, bool(success)))


def has(rel: str, *fragments: str) -> bool:
    content = text(rel)
    return bool(content) and all(s in content for s in fragments)


paths = [
    'README.md', '.gitignore', '.github/workflows/ci.yml',
    'week07/README.md', 'week07/TEST_REPORT.md', 'week07/test.sh', 'week07/test.ps1',
    'week07/src/Week07.Api/Week07.Api.csproj',
    'week07/src/Week07.Api/Program.cs',
    'week07/src/Week07.Api/Auth/JwtSettings.cs',
    'week07/src/Week07.Api/Auth/TokenService.cs',
    'week07/src/Week07.Api/Auth/Policies.cs',
    'week07/src/Week07.Api/Auth/AuthService.cs',
    'week07/src/Week07.Api/Users/StoredUser.cs',
    'week07/src/Week07.Api/Users/InMemoryUserStore.cs',
    'week07/src/Week07.Api/Contracts/AuthRequests.cs',
    'week07/src/Week07.Api/Endpoints/AuthEndpoints.cs',
    'week07/src/Week07.Api/Endpoints/UserEndpoints.cs',
    'week07/src/Week07.Api/appsettings.json',
    'week07/src/Week07.Api/Week07.Api.http',
    'week07/tests/Week07.IntegrationTests/Week07.IntegrationTests.csproj',
    'week07/tests/Week07.IntegrationTests/Program.cs',
]
for path in paths:
    check('Istnieje ' + path, (repo/path).is_file() and (repo/path).stat().st_size > 0)

for csproj in [repo/'week07/src/Week07.Api/Week07.Api.csproj', repo/'week07/tests/Week07.IntegrationTests/Week07.IntegrationTests.csproj']:
    try:
        project = ET.parse(csproj).getroot()
        check('csproj XML '+csproj.name, project.tag == 'Project')
        check('net10.0 '+csproj.name, project.findtext('./PropertyGroup/TargetFramework') == 'net10.0')
    except ET.ParseError:
        check('csproj XML '+csproj.name, False)
        check('net10.0 '+csproj.name, False)

project = 'week07/src/Week07.Api/Week07.Api.csproj'
program = 'week07/src/Week07.Api/Program.cs'
jwt = 'week07/src/Week07.Api/Auth/JwtSettings.cs'
token = 'week07/src/Week07.Api/Auth/TokenService.cs'
auth = 'week07/src/Week07.Api/Auth/AuthService.cs'
store = 'week07/src/Week07.Api/Users/InMemoryUserStore.cs'
contracts = 'week07/src/Week07.Api/Contracts/AuthRequests.cs'
authend = 'week07/src/Week07.Api/Endpoints/AuthEndpoints.cs'
userend = 'week07/src/Week07.Api/Endpoints/UserEndpoints.cs'
tests = 'week07/tests/Week07.IntegrationTests/Program.cs'
ci = '.github/workflows/ci.yml'

check('JwtBearer NuGet 10.0.0', has(project, 'Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0"'))
check('ASP.NET Core Identity hasher registered', has(program, 'IPasswordHasher<StoredUser>', 'PasswordHasher<StoredUser>'))
check('Hashed password only in domain model', has('week07/src/Week07.Api/Users/StoredUser.cs', 'PasswordHash'))
check('Public DTO omits hash', has('week07/src/Week07.Api/Users/StoredUser.cs', 'record PublicUser') and
      'PasswordHash' not in text('week07/src/Week07.Api/Users/StoredUser.cs').split('public sealed record PublicUser', 1)[1])
check('Secure hash used on registration', has(auth, 'hasher.HashPassword(', 'TryCreate('))
check('VerifyHashedPassword used on login', has(auth, 'VerifyHashedPassword(', 'PasswordVerificationResult.Success'))
check('New users registered only as User', has(auth, 'TryCreate(normalizedEmail, hash, "User")'))
check('Admin bootstrap allowed only in Development', has(program, 'app.Environment.IsDevelopment()', 'BootstrapAdmin('))
check('Admin email/password required in pair', has(program, 'string.IsNullOrWhiteSpace(adminEmail)', 'string.IsNullOrWhiteSpace(adminPassword)'))
check('Bootstrap admin password hashed', has(auth, '"Admin"', 'HashPassword('))
check('Atomic user add', has(store, 'lock (_sync)', '_byEmail.ContainsKey(email)', '_byId.Add(stored.Id, stored)'))
check('Email keys use OrdinalIgnoreCase', has(store, 'StringComparer.OrdinalIgnoreCase'))
check('Atomic delete removes both indexes', has(store, '_byId.Remove(', '_byEmail.Remove('))
check('Registration DataAnnotations', has(contracts, '[Required(', '[EmailAddress(', '[MaxLength(', '[StringLength('))
check('Password min length 12, max 128', has(contracts, 'MinimumLength = 12', 'StringLength(128'))
check('ValidationProblem for invalid registration', has(authend, 'Results.ValidationProblem(errors)'))
check('Register returns Created', has(authend, 'Results.Created('))
check('Conflict is 409', has(authend, 'statusCode: 409'))
check('Login 401 generic', has(authend, 'statusCode: 401', 'Nieprawidłowe dane logowania.'))
check('JWT created with signing credentials', has(token, 'JsonWebTokenHandler', 'SecurityTokenDescriptor', 'SigningCredentials'))
check('JWT is HS256', has(token, 'SecurityAlgorithms.HmacSha256'))
for field in ['"sub"', '"email"', '"role"', '"jti"']:
    check('JWT claim '+field, field in text(token))
check('JWT includes issuer/audience/expiry', has(token, 'Issuer = settings.Issuer', 'Audience = settings.Audience', 'Expires = expiry.UtcDateTime'))
check('JWT returns Bearer and expiry', has(token, 'IssuedToken', '"Bearer", expiry'))
check('JWT secret required at startup', has(program, 'JwtSettings.Load(builder.Configuration)') and has(jwt, 'Brak {setting}'))
check('JWT parses only Base64 secret', has(jwt, 'Convert.FromBase64String(base64)'))
check('JWT demands >=32 secret bytes', has(jwt, 'bytes.Length < 32'))
check('Signing key kid fingerprinted', has(jwt, 'SHA256.HashData(bytes)', 'KeyId = fingerprint'))
check('JWT issuer validation', has(program, 'ValidateIssuer = true', 'ValidIssuer = jwt.Issuer'))
check('JWT audience validation', has(program, 'ValidateAudience = true', 'ValidAudience = jwt.Audience'))
check('JWT lifetime validation', has(program, 'ValidateLifetime = true', 'RequireExpirationTime = true'))
check('JWT signature validation', has(program, 'RequireSignedTokens = true', 'ValidateIssuerSigningKey = true'))
check('JWT alg whitelist', has(program, 'ValidAlgorithms = [SecurityAlgorithms.HmacSha256]'))
check('JWT expiry bounded', has(jwt, 'minutes is < 5 or > 60'))
check('JWT clock skew limited', has(program, 'ClockSkew = TimeSpan.FromSeconds(30)'))
check('JWT previous keys support rotation', has(jwt, 'Jwt:PreviousSigningKeys', 'IssuerSigningKeys') or
      (has(jwt, 'Jwt:PreviousSigningKeys', 'ValidationKeys') and has(program, 'IssuerSigningKeys = jwt.ValidationKeys')))
check('JWT previous key count limited', has(jwt, 'previousStrings.Length > 3'))
check('JWT duplicate keys rejected', has(jwt, 'Distinct(StringComparer.Ordinal)'))
check('RoleClaimType matched', has(program, 'RoleClaimType = "role"'))
check('Inbound claims unmapped', has(program, 'MapInboundClaims = false'))
check('Deleted accounts invalidate JWT', has(program, 'OnTokenValidated =', 'store.FindById(', 'context.Fail('))
check('Authentication middleware precedes authorization', text(program).find('app.UseAuthentication();') < text(program).find('app.UseAuthorization();'))
check('Public health endpoint', has(program, 'MapGet("/health"', 'AllowAnonymous()'))
check('JWT protected users group', has(userend, 'MapGroup("/api/users")', 'RequireAuthorization()'))
check('AdminOnly policy is configured', has(program, 'Policies.AdminOnly', 'RequireRole("Admin")'))
check('GET users guarded AdminOnly', has(userend, 'users.MapGet("", List).RequireAuthorization(Policies.AdminOnly)'))
check('GET user/id guarded AdminOnly', has(userend, 'users.MapGet("/{id:int}", GetById).RequireAuthorization(Policies.AdminOnly)'))
check('DELETE guarded AdminOnly', has(userend, 'users.MapDelete("/{id:int}", Delete).RequireAuthorization(Policies.AdminOnly)'))
check('Admin cannot delete self', has(userend, 'selfId == id', 'statusCode: 409'))
check('No token cache on login', has(authend, 'CacheControl = "no-store"'))
check('No JWT secret in appsettings', 'SigningKey' not in text('week07/src/Week07.Api/appsettings.json'))
check('Jwt issuer audience and expiry in appsettings', has('week07/src/Week07.Api/appsettings.json', 'Issuer', 'Audience', 'ExpiryMinutes'))
check('No sample admin creds in production configs', not list(src.rglob('appsettings.Production.json')))

json_paths = list(src.rglob('*.json'))
for path in json_paths:
    try:
        json.loads(path.read_text(encoding='utf-8'))
        check('JSON valid: '+path.name, True)
    except json.JSONDecodeError:
        check('JSON valid: '+path.name, False)

checks_num = re.findall(r'await Check\("(\d{2}) ', text(tests))
check('60 sequential HTTP test scenarios', checks_num == [f'{i:02d}' for i in range(1, 61)])
check('Tests issue real HTTP', has(tests, 'HttpClient', 'PostAsJsonAsync(', 'SendAsync('))
check('Tests start and terminate API subprocess', has(tests, 'Process.Start(info)', 'Kill(entireProcessTree: true)'))
check('Tests generate runtime passwords and secrets', has(tests, 'RandomNumberGenerator.GetBytes(', 'Guid.NewGuid()'))
check('Tests check token expiry', has(tests, 'expiry', 'token wygasły') or has(tests, 'expiresInSeconds: -90'))
check('Tests cover 401 and 403', has(tests, '401', '403'))
check('Tests check rotation', has(tests, 'oldKey', 'newerKey', 'previous = null'))
check('Tests forge tokens for bad issuer/audience/signature', has(tests, 'Forge(', 'evil-issuer', 'evil-audience'))
check('Tests check absence of password hash', has(tests, 'Password', 'ExpectKeys(', 'hash'))
check('PowerShell runs .NET tests', has('week07/test.ps1', 'dotnet run --project'))
check('Bash runs .NET tests', has('week07/test.sh', 'dotnet run --project'))
check('No compiled artifacts in ZIP source', not any(p.name in {'bin','obj'} for p in root.rglob('*')))
check('Workflow includes install .NET 10', has(ci, '10.0.x', 'actions/setup-dotnet@v4'))
check('Workflow builds and runs tests', has(ci, 'bash \"week${{ matrix.week }}/test.sh\"') and has('week07/test.sh', 'dotnet build week07/src/', 'dotnet build week07/tests/', 'dotnet run --project week07/tests/'))

if Path('/bin/bash').exists():
    check('Bash script syntax', subprocess.run(['bash', '-n', str(root/'test.sh')], capture_output=True).returncode == 0)

try:
    import yaml
    parsed = yaml.safe_load((repo/ci).read_text(encoding='utf-8'))
    check('GitHub Actions YAML parses', isinstance(parsed, dict) and 'jobs' in parsed)
except ImportError:
    print('INFO YAML check skipped: PyYAML not installed')
except Exception:
    check('GitHub Actions YAML parses', False)

passed = sum(ok for _, ok in checks)
for name, ok in checks:
    print(('PASS ' if ok else 'FAIL ') + name)
print(f'\nKontrole statyczne: {passed}/{len(checks)} zaliczone.')
if passed != len(checks):
    sys.exit(1)
