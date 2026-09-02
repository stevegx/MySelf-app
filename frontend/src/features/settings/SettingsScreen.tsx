import { useState } from "react";
import { useNavigate } from "react-router";
import { Button, Card, CardTitle, Field, Input, Segmented } from "../../components/ui";
import { useTheme } from "../../app/theme";
import type { ThemeMode } from "../../app/theme";
import { useAuth } from "../auth/auth";
import { useLogout } from "../auth/useLogout";
import { useMe } from "../auth/useMe";

type Units = "metric" | "imperial";

export function SettingsScreen() {
  const navigate = useNavigate();
  const { mode, setMode } = useTheme();
  const [units, setUnits] = useState<Units>("metric");
  const { session } = useAuth();
  const { data: me } = useMe();
  const user = me?.user ?? session?.user;
  const logoutMutation = useLogout();

  return (
    <>
      <h2 className="mb-5">Settings</h2>

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
      </div>
    </>
  );
}
