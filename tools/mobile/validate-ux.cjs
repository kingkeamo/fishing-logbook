const assert = require('node:assert/strict');
const u = require('./emulator-helpers.cjs');
const phase = process.argv[2];
function englishLight() {
    u.launch();
    u.tap(u.nodes().some(n => n['content-desc'] === 'Settings') ? 'Settings' : 'Réglages');
    u.tap('LanguagePicker'); u.tap('English');
    u.tap('ThemePicker'); u.tap('Light');
    u.tap('Catches');
}
function showcase() { u.tap('Open navigation drawer'); u.tap('UX showcase · developer'); u.find('Shared interactions'); }
function stateButton(label) { u.scroll(); u.scroll(); u.tap(label === 'Restore sample journal' ? 'StateReady' : 'State' + label); u.back(); }
if (phase === 'navigation') {
    englishLight();
    u.find('Your time on the water'); u.capture('summary-en-light');
    u.tap('View European perch, Sunday, 14 June 2026 07:30');
    u.find('Catch details'); u.capture('detail-en-light');
    u.tap('OpenPhoto'); u.find('Photo preview'); u.capture('preview-en-light');
    u.back(); u.find('Catch details');
    u.tap('Navigate up'); u.find('Your time on the water');
    u.tap('Trips'); u.find('A little further downstream'); u.capture('trips-en-light');
    u.tap('Explore'); u.find('A little further downstream'); u.capture('explore-en-light');
    u.tap('Settings'); u.find('Make yourself at home'); u.capture('settings-en-light');
} else if (phase === 'preferences') {
    englishLight(); u.tap('Settings');
    u.tap('LanguagePicker'); u.tap('Français');
    u.tap('ThemePicker'); u.tap('Sombre');
    u.find('Apparence, Sombre'); u.capture('settings-fr-dark');
    u.tap('Prises'); u.find('Perche commune'); u.find('2,4 kg'); u.capture('summary-fr-dark');
    u.tap('Voir Perche commune, dimanche 14 juin 2026 07:30');
    u.find('dimanche 14 juin 2026 07:30'); u.capture('detail-fr-dark');
    u.tap('OpenPhoto'); u.tap('Réglages');
    u.tap('LanguagePicker'); u.tap('English'); u.tap('Catches');
    u.find('Photo preview'); u.find('European perch');
    u.tap('Settings'); u.tap('LanguagePicker'); u.tap('Français'); u.tap('Prises');
    u.find('Perche commune');
    u.launch(); u.find('Perche commune');
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
        if (label === 'Error' || label === 'Offline') { u.tap('StateRetry'); u.find('European perch'); }
    }
    showcase(); stateButton('Restore sample journal'); u.find('European perch');
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
        u.scroll(); u.tap('View European perch, Sunday, 14 June 2026 07:30');
        u.find('Catch details'); u.capture('detail-narrow-large-text');
        u.scroll(); u.tap('OpenPhoto'); u.find('Photo preview'); u.capture('preview-narrow-large-text');
        u.back(); u.find('Catch details'); u.back();
        u.tap('Settings'); u.find('Make yourself at home'); u.capture('settings-narrow-large-text');
        u.run('shell', 'settings', 'put', 'system', 'font_scale', font);
        u.run('shell', 'wm', 'size', override || 'reset');
        u.run('shell', 'settings', 'put', 'system', 'accelerometer_rotation', '0');
        u.run('shell', 'settings', 'put', 'system', 'user_rotation', '1');
        u.pause(1000); u.find('Make yourself at home'); u.capture('settings-landscape');
    } finally {
        u.run('shell', 'settings', 'put', 'system', 'font_scale', font);
        u.run('shell', 'wm', 'size', override || 'reset');
        u.run('shell', 'settings', 'put', 'system', 'user_rotation', rotation);
        u.run('shell', 'settings', 'put', 'system', 'accelerometer_rotation', automatic);
    }
} else {
    throw new Error('Choose navigation, preferences, interactions states or accessibility. Install a Debug APK first.');
}
console.log('PASS ' + phase);
