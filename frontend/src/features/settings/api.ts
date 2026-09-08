import { useMutation } from "@tanstack/react-query";
import { apiFetch } from "../../lib/api";
import { useAuth } from "../auth/auth";

function useToken() {
  return useAuth().session?.accessToken;
}

/** Fetch the full account export and hand the browser a JSON file to save. */
export function useExportMyData() {
  const accessToken = useToken();
  return useMutation({
    mutationFn: async () => {
      const data = await apiFetch<unknown>("/api/v1/me/export", { accessToken });
      const blob = new Blob([JSON.stringify(data, null, 2)], { type: "application/json" });
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `myself-export-${new Date().toISOString().slice(0, 10)}.json`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(url);
    },
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
