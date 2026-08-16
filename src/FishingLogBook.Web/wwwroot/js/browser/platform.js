export function getPlatform(navigatorLike) {
    const platform = navigatorLike.userAgentData?.platform || navigatorLike.platform || '';
    const browser = navigatorLike.userAgent || '';
    return `${platform} ${browser}`.trim().slice(0, 120);
}
