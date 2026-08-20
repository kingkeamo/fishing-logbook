export function buildCognitoLogoutUrl(clientId, hostedUiDomain, logoutUri) {
    const endpoint = new URL('/logout', hostedUiDomain);
    endpoint.searchParams.set('client_id', clientId);
    endpoint.searchParams.set('logout_uri', logoutUri);
    return endpoint.toString();
}

export function clearOidcUser(target, authority, clientId) {
    const key = `oidc.user:${authority}:${clientId}`;
    target.sessionStorage.removeItem(key);
    target.localStorage.removeItem(key);
}

export function installAuthentication(target) {
    target.fishingLogBookAuthentication = {
        logout(authority, clientId, hostedUiDomain, logoutUri) {
            clearOidcUser(target, authority, clientId);
            target.location.assign(buildCognitoLogoutUrl(clientId, hostedUiDomain, logoutUri));
        }
    };
}
