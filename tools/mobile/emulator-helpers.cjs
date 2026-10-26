const fs = require('node:fs');
const { spawnSync } = require('node:child_process');
const path = require('node:path');
const adb = process.env.ADB_PATH || path.join(process.env.LOCALAPPDATA, 'Android/Sdk/platform-tools/adb.exe');
const serial = process.env.ANDROID_SERIAL || 'emulator-5554';
const output = path.resolve('artifacts/mobile-249-validation');
fs.mkdirSync(output, { recursive: true });
function run(...args) {
    for (let attempt = 0; attempt < 3; attempt++) {
        const result = spawnSync(adb, ['-s', serial, ...args], { encoding: 'utf8', timeout: 30000 });
        if (result.status === 0) return result.stdout;
        const error = result.stderr || result.error || result.stdout;
        if (attempt === 2 || !String(error).includes('cannot connect to daemon')) throw new Error(error);
        console.warn('ADB transport unavailable before command execution; retrying.');
        pause(1500);
    }
}
function pause(ms = 400) { Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms); }
function nodes() {
    run('shell', 'uiautomator', 'dump', '/sdcard/flb-249.xml');
    const xml = run('shell', 'cat', '/sdcard/flb-249.xml');
    fs.writeFileSync(path.join(output, 'last-window.xml'), xml);
    return [...xml.matchAll(/<node\b([^>]*)>/g)].map(match => Object.fromEntries([...match[1].matchAll(/([\w-]+)="([^"]*)"/g)].map(attr => [attr[1], attr[2].replaceAll('&amp;', '&').replaceAll('&quot;', '"').replaceAll('&apos;', "'")])));
}
function find(value) {
    const matches = nodes().filter(n => n.text === value || n['content-desc'] === value || n['resource-id'] === value || n['resource-id'].endsWith(':id/' + value));
    const node = matches.find(n => n.clickable === 'true') || matches[0];
    if (!node) throw new Error('UI element not found: ' + value);
    return node;
}
function tap(value) {
    const node = find(value);
    const b = node.bounds.match(/\d+/g).map(Number);
    run('shell', 'input', 'tap', String(Math.round((b[0] + b[2]) / 2)), String(Math.round((b[1] + b[3]) / 2)));
    pause();
}
function capture(name) {
    run('shell', 'screencap', '-p', '/sdcard/flb-249.png');
    run('pull', '/sdcard/flb-249.png', path.join(output, name + '.png'));
    console.log('Captured ' + name);
}
function back() { run('shell', 'input', 'keyevent', '4'); pause(); }
function scroll() {
    const bounds = nodes()[0].bounds.match(/\d+/g).map(Number);
    const x = String(Math.round(bounds[2] / 2));
    run('shell', 'input', 'swipe', x, String(Math.round(bounds[3] * 0.8)), x, String(Math.round(bounds[3] * 0.35)), '400');
    pause();
}
function launch() { run('shell', 'am', 'force-stop', 'com.fishinglogbook.mobile.demo'); console.log(run('shell', 'am', 'start', '-W', '-n', 'com.fishinglogbook.mobile.demo/crc6421edb0463bbcc4fc.MainActivity')); pause(); }
module.exports = { run, pause, nodes, find, tap, capture, back, scroll, launch };
if (require.main === module) console.log(nodes().filter(n => n.text || n['content-desc']).map(n => ({ text: n.text, description: n['content-desc'], id: n['resource-id'], bounds: n.bounds })));
