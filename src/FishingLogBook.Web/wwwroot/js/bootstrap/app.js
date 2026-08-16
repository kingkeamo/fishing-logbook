import { applyStoredCulture, installCulture } from '../browser/culture.js';
import { installNetwork } from '../browser/network.js';
import { installDiagnostics } from './diagnostics.js';
import { listenForServiceWorkerErrors } from './service-worker-registration.js';

installCulture(window);
installNetwork(window);
installDiagnostics(window);
listenForServiceWorkerErrors(window);
applyStoredCulture(window);
