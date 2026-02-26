export type StoredAuth = {
    accessToken: string;
    expiresAt: string; // ISO
    email: string;
};

const KEY = "auth";

export function readAuth(): StoredAuth | null {
    try {
        const raw = localStorage.getItem(KEY);
        if (!raw) return null;
        const parsed = JSON.parse(raw) as StoredAuth;
        if (!parsed?.accessToken || !parsed?.expiresAt) return null;
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
    // mic buffer ca să nu expirăm fix în request
    return Date.now() > exp - 15_000;
}