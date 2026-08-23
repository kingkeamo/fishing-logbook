import { spawnSync } from 'node:child_process';
import { readFile, rm } from 'node:fs/promises';
import { resolve } from 'node:path';

export default async function teardown() {
    const containerFile = resolve('.runtime/container-name');
    let container;
    try {
        container = (await readFile(containerFile, 'utf8')).trim();
    } catch {
        return;
    }

    if (/^fishing-logbook-e2e-[a-f0-9]{8}$/.test(container)) {
        spawnSync('docker', ['rm', '--force', container], { stdio: 'ignore' });
    }
    await rm(resolve('.runtime'), { recursive: true, force: true });
}
