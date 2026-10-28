#!/usr/bin/env python3
"""Reproducible offline structural checks; not a substitute for dotnet build or runtime tests."""
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET
import json

root = Path(__file__).resolve().parent
package = root.parent
src = root / 'src/Week06.Api'
tests = root / 'tests/Week06.IntegrationTests'
results = []

def check(name, good):
    results.append((name, bool(good)))

def get(relative):
    return (root / relative).read_text(encoding='utf-8')

files = [
    'README.md', 'SQL_COMPARISON.md', 'TEST_REPORT.md',
    'test.sh', 'test.ps1', '.config/dotnet-tools.json',
    'src/Week06.Api/Week06.Api.csproj',
    'src/Week06.Api/Program.cs',
    'src/Week06.Api/Domain/Entities.cs',
    'src/Week06.Api/Data/AppDbContext.cs',
    'src/Week06.Api/Data/AppDbContextFactory.cs',
    'src/Week06.Api/Data/Configurations/RelationshipConfiguration.cs',
    'src/Week06.Api/Data/Migrations/202610260001_InitialRelations.cs',
    'src/Week06.Api/Data/Migrations/202610260001_InitialRelations.Designer.cs',
    'src/Week06.Api/Data/Migrations/InitialSchemaModel.cs',
    'src/Week06.Api/Data/Migrations/AppDbContextModelSnapshot.cs',
    'src/Week06.Api/Contracts/Responses.cs',
    'src/Week06.Api/Endpoints/RelationshipEndpoints.cs',
    'src/Week06.Api/Queries/UserQueries.cs',
    'src/Week06.Api/Seeding/DevelopmentSeeder.cs',
    'src/Week06.Api/Week06.Api.http',
    'src/Week06.Api/appsettings.json',
    'src/Week06.Api/appsettings.Development.json',
    'src/Week06.Api/appsettings.Production.json',
    'src/Week06.Api/Properties/launchSettings.json',
    'tests/Week06.IntegrationTests/Week06.IntegrationTests.csproj',
    'tests/Week06.IntegrationTests/Program.cs',
]
for f in files:
    check('File exists: ' + f, (root / f).is_file() and (root / f).stat().st_size > 0)
for f in ['README.md', '.gitignore', '.github/workflows/ci.yml']:
    check('Top-level file: ' + f, (package / f).is_file())

for f in sorted(package.rglob('*.json')):
    try:
        json.loads(f.read_text(encoding='utf-8'))
        ok = True
    except (json.JSONDecodeError, UnicodeDecodeError):
        ok = False
    check('Valid JSON: ' + str(f.relative_to(package)), ok)

for f in sorted(package.rglob('*.csproj')):
    try:
        tree=ET.parse(f).getroot()
        ok=tree.tag == 'Project'
        net10=tree.findtext('./PropertyGroup/TargetFramework') == 'net10.0'
    except ET.ParseError:
        ok=net10=False
    check('Project XML: '+str(f.relative_to(package)),ok)
    check('.NET 10 target: '+str(f.relative_to(package)),net10)

entities=get('src/Week06.Api/Domain/Entities.cs')
config=get('src/Week06.Api/Data/Configurations/RelationshipConfiguration.cs')
context=get('src/Week06.Api/Data/AppDbContext.cs')
migration=get('src/Week06.Api/Data/Migrations/202610260001_InitialRelations.cs')
schema=get('src/Week06.Api/Data/Migrations/InitialSchemaModel.cs')
queries=get('src/Week06.Api/Queries/UserQueries.cs')
endpoints=get('src/Week06.Api/Endpoints/RelationshipEndpoints.cs')
seed=get('src/Week06.Api/Seeding/DevelopmentSeeder.cs')
program=get('src/Week06.Api/Program.cs')
test_code=get('tests/Week06.IntegrationTests/Program.cs')
csproj=get('src/Week06.Api/Week06.Api.csproj')

def has_all(content, items): return all(s in content for s in items)

check('Project uses EF Core 10 SQLite and Design', has_all(csproj,
      ['Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.0"',
       'Microsoft.EntityFrameworkCore.Design" Version="10.0.0"']))
check('User has Tasks navigation', has_all(entities,['class User', 'ICollection<TaskItem> Tasks']))
check('TaskItem has User FK and navigation', has_all(entities,['int UserId','User User']))
check('TaskItem has Comments navigation', 'ICollection<Comment> Comments' in entities)
check('Comment has TaskItem FK and navigation', has_all(entities,['int TaskItemId', 'TaskItem TaskItem']))
check('TaskItem and Tag have M:N navigations', has_all(entities,['ICollection<Tag> Tags','ICollection<TaskItem> Tasks']))
check('DbContext has 4 DbSets', all('DbSet<'+name+'>' in context for name in ('User','TaskItem','Comment','Tag')))
check('DbContext uses Fluent API configurations', 'ApplyConfigurationsFromAssembly' in context)
check('User->TaskItem required FK mapping', has_all(config, ['HasForeignKey(x => x.UserId).IsRequired()','HasMany(x => x.Tasks).WithOne(x => x.User)']))
check('TaskItem->Comment required FK mapping', has_all(config, ['HasForeignKey(x => x.TaskItemId).IsRequired()','HasMany(x => x.Comments).WithOne(x => x.TaskItem)']))
check('M:N UsingEntity configured', 'UsingEntity<Dictionary<string, object>>' in config)
check('M:N join table explicitly named', 'join.ToTable("TaskTags")' in config)
check('Join relation unique composite PK', 'join.HasKey("TaskItemId", "TagId")' in config)
check('M:N navigations configured', 'HasMany(x => x.Tags).WithMany(x => x.Tasks)' in config)
check('Index on Tag.Name is unique', 'HasIndex(x => x.Name).IsUnique()' in config)
check('Migration class has correct ID and attributes', has_all(migration,['[Migration("202610260001_InitialRelations")]','[DbContext(typeof(AppDbContext))]']))
check('Migration supports Up and Down', has_all(migration,['void Up(MigrationBuilder','void Down(MigrationBuilder']))
for table in ['Users','Tasks','Comments','Tags','TaskTags']:
    check('Migration creates table '+table,'name: "'+table+'"' in migration)
for key in ['FK_Tasks_Users_UserId','FK_Comments_Tasks_TaskItemId','FK_TaskTags_Tasks_TaskItemId','FK_TaskTags_Tags_TagId']:
    check('Migration contains FK '+key, key in migration)
check('Migration contains composite PK', 'table.PrimaryKey("PK_TaskTags", x => new { x.TaskItemId, x.TagId })' in migration)
check('Migration contains unique tag index', '"IX_Tags_Name", "Tags", "Name", unique: true' in migration)
check('Migration has frozen schema model', 'static class InitialSchemaModel' in schema)
check('Migration designer uses frozen schema', 'InitialSchemaModel.Build(modelBuilder)' in get('src/Week06.Api/Data/Migrations/202610260001_InitialRelations.Designer.cs'))
check('Migration snapshot uses frozen schema', 'InitialSchemaModel.Build(modelBuilder)' in get('src/Week06.Api/Data/Migrations/AppDbContextModelSnapshot.cs'))
check('Queries: Include', '.Include(user => user.Tasks)' in queries)
check('Queries: ThenInclude Comments', '.ThenInclude(task => task.Comments)' in queries)
check('Queries: ThenInclude Tags', '.ThenInclude(task => task.Tags)' in queries)
check('Queries: AsNoTracking', queries.count('.AsNoTracking()') >= 4)
check('Queries: AsSplitQuery', queries.count('.AsSplitQuery()') >= 3)
check('Queries: Select projection', '.Select(user => new UserSummaryResponse(' in queries)
check('Queries: SQL translated counts', has_all(queries, ['user.Tasks.Count','task.Comments.Count','task.Tags.Count']))
for route in ['/users/{id:int}/tasks','/users/{id:int}/details','/users/{id:int}/graph','/users/{id:int}/summary','/tasks/{taskId:int}/tags/{tagId:int}']:
    check('Endpoint mapped '+route, route in endpoints)
check('Endpoint outputs DTO models; summary projected in query',has_all(endpoints,['UserTasksResponse','UserDetailsResponse','UserGraphResponse']) and 'UserQueries.Summary(db, id)' in endpoints and 'UserSummaryResponse' in queries)
check('Endpoint handles 404', endpoints.count('Results.NotFound()') >= 5)
check('Endpoint handles 400', endpoints.count('Results.BadRequest()') >= 5)
check('Endpoint assignment is idempotent', has_all(endpoints,['task.Tags.Any(t => t.Id == tagId)','Results.NoContent()']))
check('Endpoint persists relation', has_all(endpoints,['task.Tags.Add(tag)','SaveChangesAsync']))
check('Endpoint checks SQLite constraint for race', has_all(endpoints,['SqliteErrorCode: 19','TaskTags WHERE']))
check('Seeder guarded to Development',has_all(seed,['environment.IsDevelopment()','InvalidOperationException']))
check('Seeder checks existing data', 'db.Users.AnyAsync(ct)' in seed)
check('Seeder has 3 demo tasks', all(x in seed for x in ['Implement relation mapping','Write documentation','Review tests']))
check('Seed has 3 demo comments', all(x in seed for x in ['Check foreign keys','Review migration','Explain queries']))
check('Application checks connection string', 'ConnectionStrings:Week06Db' in program)
check('Application guards migrations by Development', 'app.Environment.IsDevelopment()' in program)
check('Application applies migrations', 'db.Database.MigrateAsync()' in program)
check('Application registers endpoints', 'app.MapRelationshipEndpoints()' in program)
check('Tests use HTTP client', 'new HttpClient' in test_code)
check('Tests start and stop API', has_all(test_code,['StartApi(FindProject()','Kill(entireProcessTree: true)']))
check('Tests use EF database context', has_all(test_code,['DbContextOptionsBuilder<AppDbContext>','UseSqlite(']))
check('Tests compare SQL', test_code.count('.ToQueryString()') >= 2)
check('Tests check no tracking', 'db.ChangeTracker.Entries()' in test_code)
check('Tests check database constraints', 'ExecuteSqlRawAsync' in test_code)
check('48 numbered integration tests', re.findall(r'Run\("(\d{2}) ',test_code) == [f'{n:02d}' for n in range(1,49)])
check('Test process returns failure code if not all pass', 'testsPassed == 48 ? 0 : 1' in test_code)
check('Production has no credentials', 'ConnectionStrings' not in get('src/Week06.Api/appsettings.Production.json'))
check('Production disables seeding and migrations', not any(json.loads(get('src/Week06.Api/appsettings.Production.json'))['Database'].values()))
check('Development enables seeding and migrations', all(json.loads(get('src/Week06.Api/appsettings.Development.json'))['Database'].values()))
check('SQL analysis covers split-query limitations', has_all(get('SQL_COMPARISON.md'),['AsSplitQuery','ToQueryString','Count','AsNoTracking']))
check('GitHub workflow invokes .NET build and tests', has_all((package/'.github/workflows/ci.yml').read_text(), ['dotnet-version: \'10.0.x\'', 'bash \"week${{ matrix.week }}/test.sh\"']) and has_all(get('test.sh'), ['dotnet build', 'dotnet run']))
check('No compiled binaries packaged', not any(p.name in ('bin','obj') for p in root.rglob('*')))

passed=sum(ok for _,ok in results)
for name,ok in results: print(('PASS' if ok else 'FAIL')+' '+name)
print(f'\nStatic checks: {passed}/{len(results)} PASS')
sys.exit(0 if passed==len(results) else 1)
