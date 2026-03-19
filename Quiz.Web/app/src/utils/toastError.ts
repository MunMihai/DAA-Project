import { toast } from "react-toastify";

export function extractErrorMessage(err: any, fallback = "A apărut o eroare.") {
    const msg =
        err?.response?.data?.message ??
        (Array.isArray(err?.response?.data?.errors) ? err.response.data.errors.join(", ") : null) ??
        err?.message ??
        fallback;

    if (typeof msg !== "string") {
        return fallback;
    }

    return msg
        .replace(/^Failed to invoke '[^']+' due to an error on the server\.?\s*/i, "")
        .replace(/^An unexpected error occurred invoking '[^']+' on the server\.?\s*/i, "")
        .replace(/^HubException:\s*/i, "")
        .replace(/^Error:\s*/i, "")
        .trim() || fallback;
}

export function toastErrorMessage(message: string, toastId?: string) {
    toast.error(message, toastId ? { toastId } : undefined);
}

export function toastApiError(err: any, fallback = "A apărut o eroare.") {
    toastErrorMessage(extractErrorMessage(err, fallback));
}
