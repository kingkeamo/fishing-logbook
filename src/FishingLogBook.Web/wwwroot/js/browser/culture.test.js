import { afterEach, describe, expect, it, vi } from 'vitest';
import { applyStoredCulture, createCultureApi, installCulture } from './culture.js';

function createTargetWindow({ language = 'en-IE', storageThrows = false } = {}) {
    const storage = {};
    return {
        localStorage: {
            getItem(key) {
                if (storageThrows) {
                    throw new Error('blocked');
                }

                return Object.prototype.hasOwnProperty.call(storage, key) ? storage[key] : null;
            },
            setItem(key, value) {
                if (storageThrows) {
                    throw new Error('blocked');
                }

                storage[key] = value;
            }
        },
        document: {
            documentElement: {
                lang: 'en'
            }
        },
        navigator: {
            language
        },
        location: {
            origin: 'https://example.test',
            pathname: '/log',
            replace: vi.fn()
        }
    };
}

describe('culture', () => {
    afterEach(() => {
        document.documentElement.lang = 'en';
        localStorage.clear();
    });

    it('stores culture and updates the document language', () => {
        const targetWindow = createTargetWindow();
        const api = createCultureApi(targetWindow);

        api.set('ga');

        expect(api.get()).toBe('ga');
        expect(targetWindow.document.documentElement.lang).toBe('ga');
    });

    it('returns null and ignores writes when localStorage is blocked', () => {
        const targetWindow = createTargetWindow({ storageThrows: true });
        const api = createCultureApi(targetWindow);

        expect(api.get()).toBeNull();
        api.set('ga');
        expect(targetWindow.document.documentElement.lang).toBe('ga');
        expect(api.get()).toBeNull();
    });

    it('falls back to en-GB when the browser language is missing', () => {
        const targetWindow = createTargetWindow({ language: '' });

        expect(createCultureApi(targetWindow).browser()).toBe('en-GB');
    });

    it('uses the browser language when present', () => {
        const targetWindow = createTargetWindow({ language: 'ga' });

        expect(createCultureApi(targetWindow).browser()).toBe('ga');
    });

    it('reloads the current origin and path', () => {
        const targetWindow = createTargetWindow();

        createCultureApi(targetWindow).reload();

        expect(targetWindow.location.replace).toHaveBeenCalledWith('https://example.test/log');
    });

    it('reloads the site root when the path is empty', () => {
        const targetWindow = createTargetWindow();
        targetWindow.location.pathname = '';

        createCultureApi(targetWindow).reload();

        expect(targetWindow.location.replace).toHaveBeenCalledWith('https://example.test/');
    });

    it('installs the culture API on the window', () => {
        const targetWindow = createTargetWindow();

        installCulture(targetWindow);

        expect(targetWindow.fishingLogBookCulture.get()).toBeNull();
        targetWindow.fishingLogBookCulture.set('ga');
        expect(targetWindow.document.documentElement.lang).toBe('ga');
    });

    it('applies a stored culture to the document', () => {
        const targetWindow = createTargetWindow();
        installCulture(targetWindow);
        targetWindow.fishingLogBookCulture.set('ga');
        targetWindow.document.documentElement.lang = 'en';

        applyStoredCulture(targetWindow);

        expect(targetWindow.document.documentElement.lang).toBe('ga');
    });

    it('leaves the document language unchanged when no culture is stored', () => {
        const targetWindow = createTargetWindow();
        installCulture(targetWindow);

        applyStoredCulture(targetWindow);

        expect(targetWindow.document.documentElement.lang).toBe('en');
    });
});
