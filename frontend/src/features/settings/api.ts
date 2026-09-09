import { useMutation } from "@tanstack/react-query";
import { API_BASE_URL, ApiError, apiFetch } from "../../lib/api";
import { useAuth } from "../auth/auth";

function useToken() {
  return useAuth().session?.accessToken;
}

type ExportFormat = "xlsx" | "json";

/** Download the account export as a file. Binary (xlsx) so it can't go through apiFetch's JSON parse. */
async function downloadExport(format: ExportFormat, accessToken?: string) {
  const query = format === "xlsx" ? "?format=xlsx" : "";
  const res = await fetch(`${API_BASE_URL}/api/v1/me/export${query}`, {
    headers: accessToken ? { Authorization: `Bearer ${accessToken}` } : {},
    credentials: "include",
  });
  if (!res.ok) throw new ApiError(res.status, "Export failed");

  const blob = await res.blob();
  const stamp = new Date().toISOString().slice(0, 10);
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `myself-export-${stamp}.${format}`;
  document.body.appendChild(a);
  a.click();
  a.remove();
  URL.revokeObjectURL(url);
}

export function useExportMyData() {
  const accessToken = useToken();
  return useMutation({
    mutationFn: (format: ExportFormat = "xlsx") => downloadExport(format, accessToken),
  });
}

/** Permanently delete the account, then clear the local session (RequireAuth redirects to /login). */
export function useDeleteAccount() {
  const accessToken = useToken();
  const { setSession } = useAuth();
  return useMutation({
    mutationFn: () => apiFetch<void>("/api/v1/me", { method: "DELETE", accessToken }),
    onSuccess: () => setSession(null),
  });
}
