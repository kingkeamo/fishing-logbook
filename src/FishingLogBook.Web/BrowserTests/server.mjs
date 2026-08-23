import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../');
const publishedRoot = path.join(root, 'artifacts', 'browser-tests', 'published-web');
const port = Number(process.env.PORT || 4173);

const publish = spawnSync(
    'dotnet',
    ['publish', 'src/FishingLogBook.Web/FishingLogBook.Web.csproj', '-c', 'Debug', '-o', publishedRoot, '--no-restore'],
    { cwd: root, stdio: 'inherit', shell: process.platform === 'win32' });
if (publish.status !== 0) process.exit(publish.status ?? 1);
const publishedWebRoot = path.join(publishedRoot, 'wwwroot');

const mimeTypes = {
    '.html': 'text/html; charset=utf-8',
    '.js': 'text/javascript; charset=utf-8',
    '.mjs': 'text/javascript; charset=utf-8',
    '.css': 'text/css; charset=utf-8',
    '.json': 'application/json; charset=utf-8',
    '.png': 'image/png',
    '.svg': 'image/svg+xml'
};

http.createServer((request, response) => {
    const url = new URL(request.url ?? '/', `http://127.0.0.1:${port}`);
    const decodedPath = decodeURIComponent(url.pathname);
    const contentRoot = decodedPath.startsWith('/src/') ? root : publishedWebRoot;
    let filePath = path.normalize(path.join(contentRoot, decodedPath));
    if (!filePath.startsWith(contentRoot)) {
        response.writeHead(403);
        response.end();
        return;
    }

    if (fs.existsSync(filePath) && fs.statSync(filePath).isDirectory()) {
        filePath = path.join(filePath, 'index.html');
    }

    if (!fs.existsSync(filePath) && contentRoot === publishedWebRoot && !path.extname(filePath)) {
        filePath = path.join(publishedWebRoot, 'index.html');
    }

    fs.readFile(filePath, (error, data) => {
        if (error) {
            response.writeHead(404, { 'Content-Type': 'text/plain; charset=utf-8' });
            response.end('not found');
            return;
        }

        const headers = {
            'Content-Type': mimeTypes[path.extname(filePath)] ?? 'application/octet-stream',
            'Service-Worker-Allowed': '/',
            'Cache-Control': 'no-store'
        };
        if (contentRoot === publishedWebRoot) {
            headers['Blazor-Environment'] = 'Development';
        }

        response.writeHead(200, headers);
        response.end(data);
    });
}).listen(port, '127.0.0.1', () => {
    process.stdout.write(`playwright harness listening on http://127.0.0.1:${port}\n`);
});
