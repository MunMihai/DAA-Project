import { toast } from "react-toastify";

export function toastApiError(err: any, fallback = "A apărut o eroare.") {
    const msg =
        err?.response?.data?.message ??
        (Array.isArray(err?.response?.data?.errors) ? err.response.data.errors.join(", ") : null) ??
        err?.message ??
        fallback;

    toast.error(msg);
}