import { beforeEach, describe, expect, it } from 'vitest';
import { buildCognitoLogoutUrl, clearOidcUser } from './authentication.js';

describe('authentication', () => {
    beforeEach(() => {
        sessionStorage.clear();
        localStorage.clear();
    });

    it('builds the Cognito-supported logout request', () => {
        const result = new URL(buildCognitoLogoutUrl(
            'client-id',
            'https://example.auth.eu-west-2.amazoncognito.com',
            'https://example.test/'));

        expect(result.pathname).toBe('/logout');
        expect(result.searchParams.get('client_id')).toBe('client-id');
        expect(result.searchParams.get('logout_uri')).toBe('https://example.test/');
        expect(result.searchParams.has('post_logout_redirect_uri')).toBe(false);
        expect(result.searchParams.has('id_token_hint')).toBe(false);
    });

    it('clears only the current OIDC user session', () => {
        const authority = 'https://issuer.example.test/pool';
        const key = `oidc.user:${authority}:client-id`;
        sessionStorage.setItem(key, 'session-token');
        localStorage.setItem(key, 'local-token');
        sessionStorage.setItem('unrelated', 'keep');

        clearOidcUser(window, authority, 'client-id');

        expect(sessionStorage.getItem(key)).toBeNull();
        expect(localStorage.getItem(key)).toBeNull();
        expect(sessionStorage.getItem('unrelated')).toBe('keep');
    });
});
