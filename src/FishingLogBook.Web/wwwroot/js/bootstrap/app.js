import { captureInstallEvents } from '../browser/install.js';
import { applyStoredCulture, installCulture } from '../browser/culture.js';
import { installNetwork } from '../browser/network.js';
import { installDiagnostics } from './diagnostics.js';
import { installAuthentication } from '../browser/authentication.js';
import { listenForServiceWorkerErrors } from './service-worker-registration.js';

captureInstallEvents(window);
installCulture(window);
installNetwork(window);
installDiagnostics(window);
installAuthentication(window);
listenForServiceWorkerErrors(window);
applyStoredCulture(window);
