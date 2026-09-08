import { useState } from "react";
import { useNavigate } from "react-router";
import { Button, Card, CardTitle, Checkbox, Field, Input, Segmented } from "../../components/ui";
import { useTheme } from "../../app/theme";
import type { ThemeMode } from "../../app/theme";
import { useAuth } from "../auth/auth";
import { useLogout } from "../auth/useLogout";
import { useMe } from "../auth/useMe";
import { useUpdatePreferences } from "../workouts/api";
import { useDeleteAccount, useExportMyData } from "./api";

type Units = "metric" | "imperial";

export function SettingsScreen() {
  const navigate = useNavigate();
  const { mode, setMode } = useTheme();
  const [units, setUnits] = useState<Units>("metric");
  const { session } = useAuth();
  const { data: me } = useMe();
  const user = me?.user ?? session?.user;
  const logoutMutation = useLogout();
  const updatePrefs = useUpdatePreferences();
  const warnOffFocus = me?.profile?.warnOffFocusExercises ?? true;

  const exportData = useExportMyData();
  const deleteAccount = useDeleteAccount();
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [confirmText, setConfirmText] = useState("");

  return (
    <>
      <h1 className="mb-5 text-[26px]">Settings</h1>

      <div className="flex max-w-[560px] flex-col gap-4">
        <Card>
          <CardTitle>Profile</CardTitle>
          <Field label="Name" htmlFor="settings-name">
            {/* key forces a remount once the real username arrives (useMe resolves after
                mount) — defaultValue only sets the initial value of an uncontrolled input. */}
            <Input key={user?.username ?? "loading"} id="settings-name" defaultValue={user?.username ?? ""} />
          </Field>
          <Field label="Email" htmlFor="settings-email">
            <Input
              key={user?.email ?? "loading"}
              id="settings-email"
              type="email"
              defaultValue={user?.email ?? ""}
            />
          </Field>
          <Field label="Units">
            <Segmented<Units>
              aria-label="Units"
              value={units}
              onChange={setUnits}
              options={[
                { value: "metric", label: "Metric" },
                { value: "imperial", label: "Imperial" },
              ]}
            />
          </Field>
          <Button variant="primary" className="self-start">
            Save changes
          </Button>
        </Card>

        <Card>
          <CardTitle>Preferences</CardTitle>
          <Field label="Theme">
            <Segmented<ThemeMode>
              aria-label="Theme"
              value={mode}
              onChange={setMode}
              options={[
                { value: "light", label: "Light" },
                { value: "dark", label: "Dark" },
                { value: "system", label: "System" },
              ]}
            />
          </Field>
          <Field label="Language" htmlFor="settings-language">
            <Input id="settings-language" defaultValue="English" disabled />
          </Field>
          <Field label="Workouts">
            <Checkbox
              label="Warn when an exercise is outside a day's focus"
              checked={warnOffFocus}
              disabled={updatePrefs.isPending || me?.profile == null}
              onChange={(e) => updatePrefs.mutate({ warnOffFocusExercises: e.target.checked })}
            />
          </Field>
        </Card>

        <Card>
          <CardTitle>Security</CardTitle>
          <div className="flex flex-col">
            <div className="flex items-center justify-between border-b border-border py-2.5">
              <span className="text-sm">Password</span>
              <Button variant="ghost" onClick={() => navigate("/forgot-password")}>
                Change
              </Button>
            </div>
            <div className="flex items-center justify-between py-2.5">
              <span className="text-sm">Active sessions</span>
              <Button variant="ghost">Manage</Button>
            </div>
          </div>
          <Button
            variant="danger"
            className="self-start"
            disabled={logoutMutation.isPending}
            onClick={() => logoutMutation.mutate()}
          >
            {logoutMutation.isPending ? "Logging out…" : "Log out"}
          </Button>
        </Card>

        <Card>
          <CardTitle>Your data</CardTitle>
          <div className="flex items-center justify-between border-b border-border py-2.5">
            <div>
              <div className="text-sm">Export my data</div>
              <div className="text-xs text-foreground-muted">
                A formatted Excel workbook — a check-in overview plus nutrition and training sheets.{" "}
                <button
                  type="button"
                  className="text-primary underline disabled:opacity-50"
                  disabled={exportData.isPending}
                  onClick={() => exportData.mutate("json")}
                >
                  raw JSON
                </button>
              </div>
            </div>
            <Button
              variant="ghost"
              disabled={exportData.isPending}
              onClick={() => exportData.mutate("xlsx")}
            >
              {exportData.isPending ? "Preparing…" : "Export to Excel"}
            </Button>
          </div>
          {exportData.isError && (
            <p className="m-0 text-[13px] text-danger">Couldn’t prepare the export. Try again.</p>
          )}

          <div className="py-2.5">
            <div className="text-sm">Delete account</div>
            <div className="text-xs text-foreground-muted">
              Permanently removes your account and all of its data. This can’t be undone.
            </div>

            {!confirmingDelete ? (
              <Button
                variant="danger"
                className="mt-2 self-start"
                onClick={() => setConfirmingDelete(true)}
              >
                Delete account
              </Button>
            ) : (
              <div className="mt-3 flex flex-col gap-2">
                <Field label="Type DELETE to confirm" htmlFor="confirm-delete">
                  <Input
                    id="confirm-delete"
                    autoFocus
                    autoComplete="off"
                    value={confirmText}
                    onChange={(e) => setConfirmText(e.target.value)}
                  />
                </Field>
                <div className="flex gap-2">
                  <Button
                    variant="danger"
                    disabled={confirmText !== "DELETE" || deleteAccount.isPending}
                    onClick={() => deleteAccount.mutate()}
                  >
                    {deleteAccount.isPending ? "Deleting…" : "Delete my account"}
                  </Button>
                  <Button
                    variant="ghost"
                    onClick={() => {
                      setConfirmingDelete(false);
                      setConfirmText("");
                    }}
                  >
                    Cancel
                  </Button>
                </div>
                {deleteAccount.isError && (
                  <p className="m-0 text-[13px] text-danger">Couldn’t delete the account. Try again.</p>
                )}
              </div>
            )}
          </div>
        </Card>
      </div>
    </>
  );
}
