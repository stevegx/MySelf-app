import { useState, type ReactNode } from "react";
import { useNavigate } from "react-router";
import { ChevronLeft, Plus, Play, Settings2, Trash2, TriangleAlert } from "lucide-react";
import { ApiError } from "../../lib/api";
import { cn } from "../../lib/cn";
import { Button, Card, CardKicker, Checkbox, Input, PageHeader, Skeleton } from "../../components/ui";
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
  useProgramStats,
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
  // "home" = the training view (active program's days, Start buttons); "programs" = the
  // manage/build surface. A selected programId opens its detail regardless of view.
  const [view, setView] = useState<"home" | "programs">("home");
  const [programId, setProgramId] = useState<string | null>(null);

  if (programId) {
    return <ProgramDetail programId={programId} onBack={() => setProgramId(null)} />;
  }

  if (view === "programs") {
    return <ProgramList onOpen={setProgramId} onBack={() => setView("home")} />;
  }

  return <WorkoutsHome onManage={() => setView("programs")} onOpenProgram={setProgramId} />;
}

// ---------------------------------------------------------------------------
// Workouts home — the training view. Leads with the active program's days so
// "where do I start" is answered by the first thing on screen.
// ---------------------------------------------------------------------------

function relativeDay(iso: string | null): { label: string; stale: boolean } {
  if (!iso) return { label: "Not done yet", stale: true };
  const then = new Date(`${iso}T00:00:00`);
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const days = Math.round((today.getTime() - then.getTime()) / 86_400_000);
  if (days <= 0) return { label: "Done today", stale: false };
  if (days === 1) return { label: "Yesterday", stale: false };
  if (days < 7) return { label: `${days} days ago`, stale: false };
  if (days < 14) return { label: "Last week", stale: true };
  return { label: then.toLocaleDateString(undefined, { day: "numeric", month: "short" }), stale: true };
}

function DayCard({
  name,
  exerciseCount,
  sessions,
  lastPerformedOn,
  upNext,
  onStart,
  onEdit,
}: {
  name: string;
  exerciseCount: number;
  sessions: number;
  lastPerformedOn: string | null;
  /** This day is next in the program's rotation — the one to train today. */
  upNext: boolean;
  onStart: () => void;
  onEdit: () => void;
}) {
  const last = relativeDay(lastPerformedOn);
  return (
    <Card className={cn("gap-2", upNext && "border-primary ring-1 ring-primary/30")}>
      <div className="flex items-start justify-between gap-3">
        <div>
          <div className="flex items-center gap-2">
            <span className="font-bold">{name}</span>
            {upNext && (
              <span className="rounded-full bg-primary-soft px-2 py-0.5 text-[11px] font-semibold text-primary-pressed">
                Up next
              </span>
            )}
          </div>
          <div className="text-xs text-foreground-muted">
            {exerciseCount} {exerciseCount === 1 ? "exercise" : "exercises"}
          </div>
        </div>
        <Button variant="primary" size="sm" onClick={onStart} disabled={exerciseCount === 0}>
          <Play size={13} aria-hidden />
          Start
        </Button>
      </div>
      <div className="flex items-center gap-2 text-[12px] text-foreground-muted">
        <span className={cn("inline-flex items-center gap-1", last.stale && "text-warning")}>
          {last.stale && <TriangleAlert size={12} aria-hidden />}
          {last.label}
        </span>
        <span aria-hidden>·</span>
        <span>
          done {sessions}
          {"×"} this block
        </span>
        <button
          type="button"
          onClick={onEdit}
          className="ml-auto text-primary underline underline-offset-2 hover:no-underline"
        >
          Edit
        </button>
      </div>
    </Card>
  );
}

function WorkoutsHome({
  onManage,
  onOpenProgram,
}: {
  onManage: () => void;
  onOpenProgram: (id: string) => void;
}) {
  const { data: programs, isLoading } = usePrograms();
  const startWorkout = useStartWorkout();
  const active = programs?.find((p) => p.isActive) ?? null;
  const { data: detail } = useProgram(active?.id ?? null);
  const { data: stats } = useProgramStats(active?.id ?? null);

  const statByDay = new Map((stats?.perDay ?? []).map((d) => [d.dayId, d]));

  // Rotation "up next": the day that follows whichever was trained most recently,
  // wrapping around; before anything's logged, the first day. Reuses the day
  // SortOrder the builder already maintains — no fixed weekday schedule.
  const orderedDays = [...(detail?.days ?? [])].sort((a, b) => a.sortOrder - b.sortOrder);
  const upNextDayId = (() => {
    if (orderedDays.length === 0) return null;
    let latest: { id: string; on: string } | null = null;
    for (const d of orderedDays) {
      const on = statByDay.get(d.id)?.lastPerformedOn;
      if (on && (!latest || on > latest.on)) latest = { id: d.id, on };
    }
    if (!latest) return orderedDays[0].id;
    const i = orderedDays.findIndex((d) => d.id === latest!.id);
    return orderedDays[(i + 1) % orderedDays.length].id;
  })();
  const upNextName = orderedDays.find((d) => d.id === upNextDayId)?.name ?? null;

  const header = (
    <PageHeader
      title="Train"
      subtitle={
        active
          ? `${active.name} · active program${upNextName ? ` · up next: ${upNextName}` : ""}`
          : "No active program yet"
      }
      actions={
        <>
          <Button variant="secondary" onClick={() => startWorkout(null)}>
            <Play size={14} aria-hidden />
            Ad-hoc workout
          </Button>
          <Button variant="ghost" onClick={onManage}>
            <Settings2 size={15} aria-hidden />
            Manage programs
          </Button>
        </>
      }
    />
  );

  if (isLoading) {
    return (
      <>
        {header}
        <div className="flex flex-col gap-2">
          {[0, 1, 2].map((i) => (
            <div key={i} className="rounded-card border border-border bg-surface px-4 py-4">
              <Skeleton className="mb-2 h-4 w-32" />
              <Skeleton className="h-3 w-48" />
            </div>
          ))}
        </div>
      </>
    );
  }

  if (!active) {
    return (
      <>
        {header}
        <Card className="gap-3">
          <p className="m-0 text-sm text-foreground-muted">
            {programs && programs.length > 0
              ? "You have programs but none is active. Activate one to train from it."
              : "Create a program to give your workouts some structure — or just start an ad-hoc workout."}
          </p>
          <Button variant="primary" className="self-start" onClick={onManage}>
            {programs && programs.length > 0 ? "Choose active program" : "Create a program"}
          </Button>
        </Card>
      </>
    );
  }

  return (
    <>
      {header}

      {orderedDays.length === 0 ? (
        <Card className="gap-3">
          <p className="m-0 text-sm text-foreground-muted">
            <strong>{active.name}</strong> has no days yet. Add one to start training from it.
          </p>
          <Button variant="primary" className="self-start" onClick={() => onOpenProgram(active.id)}>
            Add a day
          </Button>
        </Card>
      ) : (
        <div className="flex flex-col gap-2">
          {orderedDays.map((d) => {
            const st = statByDay.get(d.id);
            return (
              <DayCard
                key={d.id}
                name={d.name}
                exerciseCount={d.exerciseCount}
                sessions={st?.sessions ?? 0}
                lastPerformedOn={st?.lastPerformedOn ?? null}
                upNext={d.id === upNextDayId}
                onStart={() => startWorkout(d.id)}
                onEdit={() => onOpenProgram(active.id)}
              />
            );
          })}
        </div>
      )}

      {programs && programs.length > 1 && (
        <button
          type="button"
          onClick={onManage}
          className="mt-4 text-[13px] text-foreground-muted underline underline-offset-2 hover:text-foreground"
        >
          {programs.length - 1} other {programs.length - 1 === 1 ? "program" : "programs"} · manage
        </button>
      )}
    </>
  );
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

function ProgramList({ onOpen, onBack }: { onOpen: (id: string) => void; onBack?: () => void }) {
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
            {onBack && <BackButton onClick={onBack} label="Back to Train" />}
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
        <div className="flex flex-col gap-2">
          {Array.from({ length: 3 }, (_, i) => (
            <div key={i} className="flex items-center justify-between rounded-card border border-border bg-surface px-4 py-3">
              <Skeleton className="h-4 w-40" />
              <Skeleton className="h-4 w-12" />
            </div>
          ))}
        </div>
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
}: {
  programId: string;
  onBack: () => void;
}) {
  const { data: program, isLoading } = useProgram(programId);
  const m = useMutateProgram(programId);
  const { confirm, dialog } = useConfirm();
  const [dayName, setDayName] = useState("");
  const [tab, setTab] = useState<"overview" | "days">("days");
  const [selectedDayId, setSelectedDayId] = useState<string | null>(null);

  // Default to the first day once the program loads; keep a valid selection as days change.
  const days = program?.days ?? [];
  const selectedDay =
    days.find((d) => d.id === selectedDayId) ?? (tab === "days" ? days[0] : undefined);

  if (isLoading || !program) {
    return (
      <>
        <PageHeader title="Program" actions={<BackButton onClick={onBack} label="All programs" />} />
        <div className="flex flex-col gap-3">
          <Skeleton className="h-9 w-56" />
          <Skeleton className="h-16" />
          <Skeleton className="h-16" />
        </div>
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

      {tab === "days" && (
        <div className="flex flex-col gap-4 lg:flex-row lg:items-start">
          {/* Left: the day list. */}
          <div className="flex shrink-0 flex-col gap-2 lg:w-[220px]">
            <SortableList
              items={program.days}
              getId={(d) => d.id}
              onReorder={(dayOrder) => m.updateProgram.mutate({ dayOrder, rowVersion: program.rowVersion })}
            >
              {(d, dayHandle) => (
                <div
                  className={cn(
                    "flex items-center gap-2 rounded-card border px-3 py-2.5 text-[13px]",
                    selectedDay?.id === d.id ? "border-primary bg-primary-soft" : "border-border bg-surface",
                  )}
                >
                  {dayHandle}
                  <button
                    type="button"
                    onClick={() => setSelectedDayId(d.id)}
                    className="flex min-w-0 flex-1 flex-col items-start text-left"
                  >
                    <span className={cn("truncate font-semibold", selectedDay?.id === d.id && "text-primary-pressed")}>
                      {d.name}
                    </span>
                    <span className="text-xs text-foreground-muted">{d.exerciseCount} exercises</span>
                  </button>
                  <Button
                    variant="ghost"
                    size="sm"
                    iconOnly
                    aria-label={`Delete day ${d.name}`}
                    onClick={async () => {
                      if (
                        await confirm({
                          title: `Delete day "${d.name}"?`,
                          message: "Its exercises and set targets go with it. This can't be undone.",
                          confirmLabel: "Delete day",
                        })
                      ) {
                        if (selectedDayId === d.id) setSelectedDayId(null);
                        m.deleteDay.mutate(d.id);
                      }
                    }}
                  >
                    <Trash2 size={14} aria-hidden />
                  </Button>
                </div>
              )}
            </SortableList>

            <div className="flex gap-1.5">
              <Input
                placeholder="New day…"
                value={dayName}
                onChange={(e) => setDayName(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter" && dayName.trim()) {
                    m.addDay.mutate({ name: dayName.trim() }, { onSuccess: (d) => setSelectedDayId(d.id) });
                    setDayName("");
                  }
                }}
              />
              <Button
                variant="secondary"
                iconOnly
                aria-label="Add day"
                disabled={!dayName.trim()}
                onClick={() => {
                  m.addDay.mutate({ name: dayName.trim() }, { onSuccess: (d) => setSelectedDayId(d.id) });
                  setDayName("");
                }}
              >
                <Plus size={15} aria-hidden />
              </Button>
            </div>
          </div>

          {/* Right: the selected day. */}
          <div className="min-w-0 flex-1">
            {selectedDay ? (
              <Card key={selectedDay.id}>
                <DayEditor
                  programId={program.id}
                  dayId={selectedDay.id}
                  onSaved={() => {}}
                  onClose={() => setSelectedDayId(null)}
                />
              </Card>
            ) : (
              <Card>
                <p className="m-0 text-sm text-foreground-muted">
                  {days.length === 0
                    ? "Add your first day on the left — Push, Pull, Legs, whatever you like."
                    : "Pick a day on the left to edit its exercises."}
                </p>
              </Card>
            )}
          </div>
        </div>
      )}
    </>
  );
}
