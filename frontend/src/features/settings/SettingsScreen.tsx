import { useState } from "react";
import { Button, Card, CardTitle, Field, Input, Segmented } from "../../components/ui";
import { useTheme } from "../../app/theme";
import type { ThemeMode } from "../../app/theme";

type Units = "metric" | "imperial";

export function SettingsScreen() {
  const { mode, setMode } = useTheme();
  const [units, setUnits] = useState<Units>("metric");

  return (
    <>
      <h2 className="mb-5">Settings</h2>

      <div className="flex max-w-[560px] flex-col gap-4">
        <Card>
          <CardTitle>Profile</CardTitle>
          <Field label="Name" htmlFor="settings-name">
            <Input id="settings-name" defaultValue="Alex Papadopoulos" />
          </Field>
          <Field label="Email" htmlFor="settings-email">
            <Input id="settings-email" type="email" defaultValue="alex@example.com" />
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
              <Button variant="ghost">Change</Button>
            </div>
            <div className="flex items-center justify-between py-2.5">
              <span className="text-sm">Active sessions</span>
              <Button variant="ghost">Manage</Button>
            </div>
          </div>
          <Button variant="danger" className="self-start">
            Log out
          </Button>
        </Card>
      </div>
    </>
  );
}
