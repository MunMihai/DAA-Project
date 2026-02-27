export type StoredAuth = {
    accessToken: string;
    expiresAt: string; // ISO
    email: string;
    roles: string[];
};

const KEY = "auth";

/** Decode JWT payload without validation (client-side only) */
function decodeJwtPayload(token: string): Record<string, unknown> {
    try {
        const parts = token.split(".");
        if (parts.length !== 3) return {};
        const payload = parts[1].replace(/-/g, "+").replace(/_/g, "/");
        return JSON.parse(atob(payload));
    } catch {
        return {};
    }
}

export function extractRoles(token: string): string[] {
    const payload = decodeJwtPayload(token);
    // ASP.NET Identity emits roles under this claim
    const roleClaim =
        payload["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] ??
        payload["role"] ??
        payload["roles"];
    if (!roleClaim) return [];
    if (Array.isArray(roleClaim)) return roleClaim.map(String);
    return [String(roleClaim)];
}

export function readAuth(): StoredAuth | null {
    try {
        const raw = localStorage.getItem(KEY);
        if (!raw) return null;
        const parsed = JSON.parse(raw) as StoredAuth;
        if (!parsed?.accessToken || !parsed?.expiresAt) return null;
        // backfill roles if missing (old sessions)
        if (!parsed.roles) {
            parsed.roles = extractRoles(parsed.accessToken);
        }
        return parsed;
    } catch {
        return null;
    }
}

export function writeAuth(auth: StoredAuth) {
    localStorage.setItem(KEY, JSON.stringify(auth));
}

export function clearAuth() {
    localStorage.removeItem(KEY);
}

export function isExpired(expiresAtIso: string) {
    const exp = new Date(expiresAtIso).getTime();
    return Date.now() > exp - 15_000;
}