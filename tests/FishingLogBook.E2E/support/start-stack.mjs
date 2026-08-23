import { spawn, spawnSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const projectRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../../..');
const container = `fishing-logbook-e2e-${randomUUID().slice(0, 8)}`;
const port = process.env.E2E_POSTGRES_PORT ?? '55433';
const password = randomUUID();
const connection = `Host=localhost;Port=${port};Database=postgres;Username=postgres;Password=${password}`;
const children = [];

function run(command, args, environment = {}) {
    const child = spawn(command, args, {
        cwd: projectRoot,
        env: { ...process.env, ...environment },
        stdio: ['ignore', 'pipe', 'pipe']
    });
    child.stdout.on('data', data => process.stdout.write(data));
    child.stderr.on('data', data => process.stderr.write(data));
    children.push(child);
    return child;
}

async function waitFor(url, label, timeout = 120_000) {
    const deadline = Date.now() + timeout;
    while (Date.now() < deadline) {
        try {
            const response = await fetch(url, { redirect: 'manual' });
            if (response.status >= 200 && response.status < 400) return;
        } catch { /* readiness is expected to fail while starting */ }
        await new Promise(resolvePromise => setTimeout(resolvePromise, 500));
    }
    throw new Error(`${label} did not become ready within ${timeout}ms.`);
}

function cleanup() {
    for (const child of children.reverse()) child.kill();
    spawnSync('docker', ['rm', '--force', container], { stdio: 'ignore' });
}

process.once('SIGINT', () => { cleanup(); process.exit(130); });
process.once('SIGTERM', () => { cleanup(); process.exit(143); });
process.once('exit', cleanup);

const docker = spawnSync('docker', [
    'run', '--detach', '--rm', '--name', container,
    '-e', `POSTGRES_PASSWORD=${password}`, '-p', `${port}:5432`, 'postgres:18-alpine'
], { cwd: projectRoot, encoding: 'utf8' });
if (docker.status !== 0) throw new Error('Unable to start the disposable E2E PostgreSQL container.');

for (let attempt = 0; attempt < 120; attempt += 1) {
    const ready = spawnSync('docker', ['exec', container, 'pg_isready', '-U', 'postgres'], {
        stdio: 'ignore'
    });
    if (ready.status === 0) break;
    if (attempt === 119) throw new Error('Disposable E2E PostgreSQL did not become ready.');
    await new Promise(resolvePromise => setTimeout(resolvePromise, 500));
}

const migration = spawnSync('dotnet', ['run', '--', '--run'], {
    cwd: resolve(projectRoot, 'src/FishingLogBook.Db.Migrations.App'),
    env: { ...process.env, Db__ConnectionString: connection },
    encoding: 'utf8',
});
if (migration.status !== 0) throw new Error(`E2E migrations failed.\n${migration.stdout}\n${migration.stderr}`);

run('dotnet', ['run', '--project', 'src/FishingLogBook.Api', '--launch-profile', 'https'], {
    ASPNETCORE_ENVIRONMENT: 'Development', ConnectionStrings__Postgres: connection
});
await waitFor('http://localhost:5110/health', 'API');

run('dotnet', ['run', '--project', 'src/FishingLogBook.Web', '--launch-profile', 'http'], {
    ASPNETCORE_ENVIRONMENT: 'Development'
});
await waitFor('http://localhost:5019', 'Web');
await new Promise(() => {});
