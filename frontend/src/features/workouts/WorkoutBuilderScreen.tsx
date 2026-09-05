import { useState, type ReactNode } from "react";
import { useNavigate } from "react-router";
import { ChevronLeft, Plus, Play } from "lucide-react";
import { ApiError } from "../../lib/api";
import { cn } from "../../lib/cn";
import { Button, Card, CardKicker, Checkbox, Input, PageHeader } from "../../components/ui";
import { SortableList } from "./SortableList";
import { DayEditor } from "./DayEditor";
import { ProgramOverview } from "./ProgramOverview";
import { useConfirm } from "./useConfirm";
import {
  useArchivedPrograms,
  useCreateProgram,
  useMutateProgram,
  useProgram,
  usePrograms,
  useStartSession,
} from "./api";

/** Starts a session (day or ad-hoc) and goes to the active-workout screen either way —
 * a 409 means one's already running, and that screen shows whichever session is active. */
function useStartWorkout() {
  const navigate = useNavigate();
  const start = useStartSession();
  return (dayId: string | null) =>
    start.mutate(dayId, {
      onSuccess: () => navigate("/workouts/active"),
      onError: (e) => {
        if (e instanceof ApiError && e.status === 409) {
          navigate("/workouts/active");
        }
      },
    });
}

export function WorkoutBuilderScreen() {
  const [programId, setProgramId] = useState<string | null>(null);
  const [dayId, setDayId] = useState<string | null>(null);

  if (programId && dayId) {
    return (
      <>
        <PageHeader title="Edit day" actions={<BackButton onClick={() => setDayId(null)} label="Back to program" />} />
        <Card>
          <DayEditor programId={programId} dayId={dayId} onClose={() => setDayId(null)} />
        </Card>
      </>
    );
  }

  if (programId) {
    return <ProgramDetail programId={programId} onBack={() => setProgramId(null)} onEditDay={setDayId} />;
  }

  return <ProgramList onOpen={setProgramId} />;
}

/** One-line message from a failed mutation, for inline display. */
function errMsg(error: unknown): string | null {
  if (!error) return null;
  if (error instanceof ApiError) {
    return Object.values(error.errors ?? {})[0]?.[0] ?? error.detail ?? error.title;
  }
  return "Something went wrong. Please try again.";
}

function InlineError({ error }: { error: unknown }) {
  const message = errMsg(error);
  return message ? (
    <p role="alert" className="m-0 text-[13px] text-danger">
      {message}
    </p>
  ) : null;
}

function BackButton({ onClick, label }: { onClick: () => void; label: string }) {
  return (
    <Button variant="ghost" onClick={onClick}>
      <ChevronLeft size={15} aria-hidden />
      {label}
    </Button>
  );
}

function Tab({ active, onClick, children }: { active: boolean; onClick: () => void; children: ReactNode }) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={active}
      onClick={onClick}
      className={cn(
        "-mb-px border-b-2 px-3 py-2 text-sm font-semibold",
        active ? "border-primary text-foreground" : "border-transparent text-foreground-muted hover:text-foreground",
      )}
    >
      {children}
    </button>
  );
}

function ProgramList({ onOpen }: { onOpen: (id: string) => void }) {
  const { data: programs, isLoading } = usePrograms();
  const create = useCreateProgram();
  const startWorkout = useStartWorkout();
  const m = useMutateProgram(null);
  const { confirm, dialog } = useConfirm();
  const [name, setName] = useState("");
  const [splitLabel, setSplitLabel] = useState("");
  const [selecting, setSelecting] = useState(false);
  const [selected, setSelected] = useState<Set<string>>(new Set());

  const toggle = (id: string) =>
    setSelected((s) => {
      const next = new Set(s);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  const exitSelect = () => {
    setSelecting(false);
    setSelected(new Set());
  };

  async function onCreate() {
    if (!name.trim()) return;
    const created = await create.mutateAsync({ name: name.trim(), splitLabel: splitLabel.trim() || undefined });
    setName("");
    setSplitLabel("");
    onOpen(created.id);
  }

  async function deleteSelected() {
    const ids = [...selected];
    if (
      await confirm({
        title: `Delete ${ids.length} program${ids.length > 1 ? "s" : ""}?`,
        message: "This can't be undone. Workouts you already logged from them are kept.",
        confirmLabel: `Delete ${ids.length}`,
      })
    ) {
      await Promise.all(ids.map((id) => m.remove.mutateAsync(id).catch(() => {})));
      exitSelect();
    }
  }

  async function duplicateSelected() {
    const ids = [...selected];
    await Promise.all(ids.map((id) => m.clone.mutateAsync(id).catch(() => {})));
    exitSelect();
  }

  const busy = m.remove.isPending || m.clone.isPending;

  return (
    <>
      {dialog}
      <PageHeader
        title="Workout programs"
        subtitle="Build the workouts you train from. No fixed days."
        actions={
          <>
            {programs && programs.length > 0 && (
              <Button variant="ghost" onClick={() => (selecting ? exitSelect() : setSelecting(true))}>
                {selecting ? "Cancel" : "Select"}
              </Button>
            )}
            <Button variant="secondary" onClick={() => startWorkout(null)}>
              <Play size={14} aria-hidden />
              Start ad-hoc workout
            </Button>
          </>
        }
      />

      {selecting && selected.size > 0 && (
        <div className="mb-3 flex flex-wrap items-center gap-2 rounded-control border border-border bg-surface-subtle px-3 py-2 text-[13px]">
          <span className="font-semibold">{selected.size} selected</span>
          <Button variant="secondary" size="sm" onClick={duplicateSelected} disabled={busy}>
            Duplicate
          </Button>
          <Button variant="danger" size="sm" onClick={deleteSelected} disabled={busy}>
            Delete
          </Button>
        </div>
      )}

      {!selecting && (
        <Card className="mb-4 gap-2">
          <CardKicker>New program</CardKicker>
          <div className="flex flex-wrap gap-2">
            <Input placeholder="Program name (e.g. PPL)" value={name} onChange={(e) => setName(e.target.value)} className="max-w-[220px]" />
            <Input placeholder="Split label (optional)" value={splitLabel} onChange={(e) => setSplitLabel(e.target.value)} className="max-w-[220px]" />
            <Button variant="primary" onClick={onCreate} disabled={create.isPending || !name.trim()}>
              Create
            </Button>
          </div>
        </Card>
      )}

      <InlineError error={m.remove.error ?? m.clone.error} />

      {isLoading ? (
        <p className="text-sm text-foreground-muted">Loading…</p>
      ) : programs && programs.length > 0 ? (
        <div className="flex flex-col gap-2">
          {programs.map((p) => (
            <div
              key={p.id}
              className={cn(
                "flex items-center gap-3 rounded-card border bg-surface px-4 py-3",
                selecting && selected.has(p.id) ? "border-primary bg-primary-soft" : "border-border",
              )}
            >
              {selecting && (
                <Checkbox
                  label=""
                  checked={selected.has(p.id)}
                  onChange={() => toggle(p.id)}
                  aria-label={`Select ${p.name}`}
                />
              )}
              <button
                onClick={() => (selecting ? toggle(p.id) : onOpen(p.id))}
                className="flex flex-1 items-center justify-between text-left"
              >
                <span>
                  <span className="font-bold">{p.name}</span>
                  {p.splitLabel ? <span className="ml-2 text-xs text-foreground-muted">{p.splitLabel}</span> : null}
                  <span className="ml-2 text-xs text-foreground-muted">
                    {p.dayCount} days · {p.exerciseCount} exercises
                  </span>
                </span>
                {p.isActive ? (
                  <span className="rounded-full bg-success-soft px-2 py-0.5 text-xs font-semibold text-success">Active</span>
                ) : (
                  <span className="text-xs text-foreground-muted">Draft</span>
                )}
              </button>
              {!selecting && (
                <div className="flex gap-1">
                  <Button variant="ghost" size="sm" onClick={() => m.clone.mutate(p.id)} disabled={busy}>
                    Duplicate
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    disabled={busy}
                    onClick={async () => {
                      if (
                        await confirm({
                          title: `Delete "${p.name}"?`,
                          message: "This can't be undone. Workouts you already logged from it are kept.",
                          confirmLabel: "Delete",
                        })
                      ) {
                        m.remove.mutate(p.id);
                      }
                    }}
                  >
                    Delete
                  </Button>
                </div>
              )}
            </div>
          ))}
        </div>
      ) : (
        <p className="text-sm text-foreground-muted">No programs yet. Create one above.</p>
      )}

      <ArchivedPrograms />
    </>
  );
}

function ArchivedPrograms() {
  const [open, setOpen] = useState(false);
  const { data: archived, isLoading } = useArchivedPrograms(open);
  const m = useMutateProgram(null);
  const { confirm, dialog } = useConfirm();

  return (
    <div className="mt-6">
      {dialog}
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        className="text-sm font-semibold text-foreground-muted hover:text-foreground"
      >
        {open ? "▾" : "▸"} Archived programs
      </button>
      {open && (
        <div className="mt-2 flex flex-col gap-2">
          <InlineError error={m.remove.error ?? m.restore.error} />
          {isLoading ? (
            <p className="text-sm text-foreground-muted">Loading…</p>
          ) : archived && archived.length > 0 ? (
            archived.map((p) => (
              <div
                key={p.id}
                className="flex items-center justify-between rounded-card border border-border bg-surface-subtle px-4 py-3"
              >
                <span className="text-[13px]">
                  <span className="font-semibold">{p.name}</span>
                  <span className="ml-2 text-xs text-foreground-muted">
                    {p.dayCount} days · {p.exerciseCount} exercises
                  </span>
                </span>
                <div className="flex gap-1.5">
                  <Button
                    variant="secondary"
                    size="sm"
                    disabled={m.restore.isPending}
                    onClick={() => m.restore.mutate(p.id)}
                  >
                    Restore
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    disabled={m.remove.isPending}
                    onClick={async () => {
                      if (
                        await confirm({
                          title: `Permanently delete "${p.name}"?`,
                          message: "This can't be undone. Workouts you already logged from it are kept.",
                          confirmLabel: "Delete",
                        })
                      ) {
                        m.remove.mutate(p.id);
                      }
                    }}
                  >
                    Delete
                  </Button>
                </div>
              </div>
            ))
          ) : (
            <p className="text-sm text-foreground-muted">Nothing archived.</p>
          )}
        </div>
      )}
    </div>
  );
}

function ProgramDetail({
  programId,
  onBack,
  onEditDay,
}: {
  programId: string;
  onBack: () => void;
  onEditDay: (id: string) => void;
}) {
  const { data: program, isLoading } = useProgram(programId);
  const m = useMutateProgram(programId);
  const startWorkout = useStartWorkout();
  const { confirm, dialog } = useConfirm();
  const [dayName, setDayName] = useState("");
  const [tab, setTab] = useState<"overview" | "days">("overview");

  if (isLoading || !program) {
    return (
      <>
        <PageHeader title="Program" actions={<BackButton onClick={onBack} label="All programs" />} />
        <p className="text-sm text-foreground-muted">Loading…</p>
      </>
    );
  }

  return (
    <>
      {dialog}
      <PageHeader
        title={program.name}
        subtitle={program.isActive ? "Active program" : "Draft"}
        actions={
          <>
            <BackButton onClick={onBack} label="All programs" />
            {!program.isActive && (
              <Button variant="primary" onClick={() => m.activate.mutate(program.id)} disabled={m.activate.isPending}>
                Activate
              </Button>
            )}
            <Button variant="secondary" onClick={() => m.clone.mutate(program.id)} disabled={m.clone.isPending}>
              {m.clone.isPending ? "Duplicating…" : "Duplicate"}
            </Button>
            <Button
              variant="ghost"
              onClick={async () => {
                if (
                  await confirm({
                    title: "Archive this program?",
                    message: "It leaves your active list. You can restore it later from Archived programs.",
                    confirmLabel: "Archive",
                  })
                ) {
                  m.archive.mutate(program.id, { onSuccess: onBack });
                }
              }}
            >
              Archive
            </Button>
            <Button
              variant="danger"
              onClick={async () => {
                if (
                  await confirm({
                    title: `Delete "${program.name}"?`,
                    message:
                      "This permanently removes the program and its days. Workouts you already logged from it are kept.",
                    confirmLabel: "Delete",
                  })
                ) {
                  m.remove.mutate(program.id, { onSuccess: onBack });
                }
              }}
            >
              Delete program
            </Button>
          </>
        }
      />

      <InlineError error={m.remove.error ?? m.archive.error ?? m.activate.error ?? m.clone.error} />

      <div role="tablist" className="mb-4 flex gap-1 border-b border-border">
        <Tab active={tab === "overview"} onClick={() => setTab("overview")}>
          Overview
        </Tab>
        <Tab active={tab === "days"} onClick={() => setTab("days")}>
          Days
        </Tab>
      </div>

      {tab === "overview" && <ProgramOverview programId={program.id} />}

      <div className={cn("flex flex-col gap-3", tab === "days" ? "" : "hidden")}>
        <SortableList
          items={program.days}
          getId={(d) => d.id}
          onReorder={(dayOrder) => m.updateProgram.mutate({ dayOrder, rowVersion: program.rowVersion })}
        >
          {(d, dayHandle) => (
            <div className="flex items-center justify-between rounded-card border border-border bg-surface px-4 py-3">
              <span className="flex items-center gap-2 text-[13px]">
                {dayHandle}
                <span className="font-semibold">{d.name}</span>
                <span className="ml-1 text-xs text-foreground-muted">{d.exerciseCount} exercises</span>
              </span>
              <div className="flex gap-1">
                <Button variant="primary" size="sm" onClick={() => startWorkout(d.id)}>
                  <Play size={13} aria-hidden />
                  Start
                </Button>
                <Button variant="secondary" size="sm" onClick={() => onEditDay(d.id)}>
                  Edit
                </Button>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={async () => {
                    if (
                      await confirm({
                        title: `Delete day "${d.name}"?`,
                        message: "Its exercises and set targets go with it. This can't be undone.",
                        confirmLabel: "Delete day",
                      })
                    ) {
                      m.deleteDay.mutate(d.id);
                    }
                  }}
                >
                  Delete
                </Button>
              </div>
            </div>
          )}
        </SortableList>

        <Card className="gap-2">
          <CardKicker>New day</CardKicker>
          <div className="flex gap-2">
            <Input
              placeholder="Day name (e.g. Push)"
              value={dayName}
              onChange={(e) => setDayName(e.target.value)}
              className="max-w-[220px]"
            />
            <Button
              variant="secondary"
              disabled={!dayName.trim()}
              onClick={() => {
                m.addDay.mutate({ name: dayName.trim() });
                setDayName("");
              }}
            >
              <Plus size={14} aria-hidden />
              Add day
            </Button>
          </div>
        </Card>
      </div>
    </>
  );
}
