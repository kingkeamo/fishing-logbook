const assert = require('node:assert/strict');
const u = require('./emulator-helpers.cjs');
const phase = process.argv[2];
function englishLight() {
    u.launch();
    for (let depth = 0; depth < 4 && u.nodes().some(n => n['content-desc'] === 'Navigate up'); depth++) u.tap('Navigate up');
    u.tap(u.nodes().some(n => n['content-desc'] === 'Settings') ? 'Settings' : 'Réglages');
    u.tap('LanguagePicker'); u.pause(1200); u.tap('English'); u.pause(1200);
    u.tap('ThemePicker'); u.pause(1200); u.tap(u.nodes().some(n => n.text === 'Light') ? 'Light' : 'Clair'); u.pause(1200);
    u.tap(u.nodes().some(n => n['content-desc'] === 'Catches') ? 'Catches' : 'Prises');
    for (let depth = 0; depth < 4 && u.nodes().some(n => n['content-desc'] === 'Navigate up'); depth++) u.tap('Navigate up');
}
function showcase() { u.tap('Open navigation drawer'); u.tap('UX showcase · developer'); u.find('Shared interactions'); }
function stateButton(label) { u.scroll(); u.scroll(); u.tap(label === 'Restore sample journal' ? 'StateReady' : 'State' + label); u.back(); }
if (phase === 'navigation') {
    englishLight();
    u.find('Recent catches'); u.capture('summary-en-light');
    u.tap('OpenCatch');
    u.find('Catch details'); u.capture('detail-en-light');
    u.tap('OpenPhoto'); u.find('Photo preview'); u.capture('preview-en-light');
    u.back(); u.find('Catch details');
    u.tap('Navigate up'); u.find('Recent catches');
    u.tap('Trips'); u.find('A little further downstream'); u.capture('trips-en-light');
    u.tap('Explore'); u.find('A little further downstream'); u.capture('explore-en-light');
    u.tap('Settings'); u.find('Preferences'); u.capture('settings-en-light');
} else if (phase === 'preferences') {
    englishLight(); u.tap('Settings');
    u.tap('LanguagePicker'); u.tap('Français');
    u.tap('ThemePicker'); u.tap('Sombre');
    u.find('Apparence, Sombre'); u.capture('settings-fr-dark');
    u.tap('Prises'); u.pause(1200); u.find('Truite fario'); u.find('2,4 kg · 48 cm'); u.capture('summary-fr-dark');
    u.tap('OpenCatch');
    u.find('dimanche 14 juin 2026 07:30'); u.capture('detail-fr-dark');
    u.tap('OpenPhoto'); u.find('Aperçu de la photo'); u.find('Truite fario');
    u.back(); u.find('Détails de la prise'); u.tap('Réglages');
    u.tap('LanguagePicker'); u.tap('English'); u.tap('Catches'); u.pause(1200);
    u.tap('Navigate up'); u.find('Recent catches'); u.tap('OpenCatch'); u.tap('OpenPhoto');
    u.find('Photo preview'); u.find('Brown trout');
    u.back(); u.tap('Navigate up'); u.tap('Settings'); u.tap('LanguagePicker'); u.tap('Français'); u.tap('Prises'); u.pause(1200);
    u.find('Truite fario');
    u.launch(); u.find('Truite fario');
    u.tap('Réglages'); u.find('Apparence, Sombre'); u.find('Langue, Français');
    u.tap('Open navigation drawer');
    assert(!u.nodes().some(n => n.clickable === 'true' && ['Prises', 'Sorties', 'Explorer', 'Réglages'].includes(n.text)), 'Primary destinations duplicated in flyout');
    u.capture('flyout-fr-dark'); u.back();
} else if (phase === 'interactions') {
    englishLight(); showcase();
    for (const kind of ['Success', 'Info', 'Warning', 'Error']) {
        u.tap('Notify' + kind); u.pause(1000); u.capture('notification-' + kind.toLowerCase());
    }
    u.tap('ConfirmDemo'); u.find('Reset this demo?'); u.capture('confirmation-en-light');
    u.tap('DialogCancel'); u.find('Shared interactions');
    u.tap('ConfirmDemo'); u.tap('DialogAccept'); u.find('Shared interactions');
    u.tap('ModalDemo'); u.find('A moment by the water'); u.capture('modal-en-light');
    u.back(); u.find('Shared interactions');
    u.tap('ModalDemo'); u.tap('DialogCancel'); u.find('Shared interactions');
    u.tap('ModalDemo'); u.tap('DialogAccept'); u.find('Shared interactions');
    u.scroll(); u.tap('CancelModalDemo'); u.pause(3800); u.find('Content states');
} else if (phase === 'states') {
    englishLight();
    for (const [label, title] of [['Loading', 'Getting things ready'], ['Empty', 'No catches to display'], ['Error', 'Something went wrong'], ['Offline', 'You’re offline']]) {
        showcase(); stateButton(label); u.find(title); u.capture('state-' + label.toLowerCase());
        if (label === 'Error' || label === 'Offline') { u.tap('StateRetry'); u.find('Brown trout'); }
    }
    showcase(); stateButton('Restore sample journal'); u.find('Brown trout');
} else if (phase === 'filters') {
    englishLight();
    u.find('Recent catches');
    u.tap('FilterFly'); u.find('Brown trout');
    assert(!u.nodes().some(n => n.text === 'Atlantic salmon'), 'Fly filter still includes the spinning salmon');
    u.capture('filter-fly-en-light');
    u.tap('ClearFilters'); u.find('Atlantic salmon');
} else if (phase === 'accessibility') {
    englishLight();
    const font = u.run('shell', 'settings', 'get', 'system', 'font_scale').trim();
    const rotation = u.run('shell', 'settings', 'get', 'system', 'user_rotation').trim();
    const automatic = u.run('shell', 'settings', 'get', 'system', 'accelerometer_rotation').trim();
    const override = u.run('shell', 'wm', 'size').match(/Override size: (\d+x\d+)/)?.[1];
    try {
        u.run('shell', 'settings', 'put', 'system', 'font_scale', '2.0');
        u.run('shell', 'wm', 'size', '840x1800');
        u.pause(1000); u.capture('summary-narrow-large-text');
        u.tap('Brown trout');
        u.find('Catch details'); u.capture('detail-narrow-large-text');
        for (let attempt = 0; attempt < 4 && !u.nodes().some(n => n['resource-id'].endsWith(':id/OpenPhoto')); attempt++) u.scroll();
        u.tap('OpenPhoto'); u.find('Photo preview'); u.capture('preview-narrow-large-text');
        u.back(); u.find('Catch details'); u.back();
        u.tap('Settings'); u.find('Preferences'); u.capture('settings-narrow-large-text');
        u.run('shell', 'settings', 'put', 'system', 'font_scale', font);
        u.run('shell', 'wm', 'size', override || 'reset');
        u.run('shell', 'settings', 'put', 'system', 'accelerometer_rotation', '0');
        u.run('shell', 'settings', 'put', 'system', 'user_rotation', '1');
        u.pause(1000); u.find('Preferences'); u.capture('settings-landscape');
    } finally {
        u.run('shell', 'settings', 'put', 'system', 'font_scale', font);
        u.run('shell', 'wm', 'size', override || 'reset');
        u.run('shell', 'settings', 'put', 'system', 'user_rotation', rotation);
        u.run('shell', 'settings', 'put', 'system', 'accelerometer_rotation', automatic);
    }
} else {
    throw new Error('Choose navigation, preferences, interactions, states, filters or accessibility. Install a Debug APK first.');
}
console.log('PASS ' + phase);
