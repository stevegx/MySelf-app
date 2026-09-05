import { useState } from "react";
import { useNavigate } from "react-router";
import { ChevronLeft, Plus, Play } from "lucide-react";
import { ApiError } from "../../lib/api";
import { Button, Card, CardKicker, Input, PageHeader } from "../../components/ui";
import { SortableList } from "./SortableList";
import { DayEditor } from "./DayEditor";
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

function BackButton({ onClick, label }: { onClick: () => void; label: string }) {
  return (
    <Button variant="ghost" onClick={onClick}>
      <ChevronLeft size={15} aria-hidden />
      {label}
    </Button>
  );
}

function ProgramList({ onOpen }: { onOpen: (id: string) => void }) {
  const navigate = useNavigate();
  const { data: programs, isLoading } = usePrograms();
  const create = useCreateProgram();
  const startWorkout = useStartWorkout();
  const [name, setName] = useState("");
  const [splitLabel, setSplitLabel] = useState("");

  async function onCreate() {
    if (!name.trim()) return;
    const created = await create.mutateAsync({ name: name.trim(), splitLabel: splitLabel.trim() || undefined });
    setName("");
    setSplitLabel("");
    onOpen(created.id);
  }

  return (
    <>
      <PageHeader
        title="Workout programs"
        subtitle="Build the workouts you train from. No fixed days."
        actions={
          <>
            <Button variant="ghost" onClick={() => navigate("/workouts/history")}>
              History
            </Button>
            <Button variant="secondary" onClick={() => startWorkout(null)}>
              <Play size={14} aria-hidden />
              Start ad-hoc workout
            </Button>
          </>
        }
      />

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

      {isLoading ? (
        <p className="text-sm text-foreground-muted">Loading…</p>
      ) : programs && programs.length > 0 ? (
        <div className="flex flex-col gap-2">
          {programs.map((p) => (
            <button
              key={p.id}
              onClick={() => onOpen(p.id)}
              className="flex items-center justify-between rounded-card border border-border bg-surface px-4 py-3 text-left hover:border-border-strong"
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

  return (
    <div className="mt-6">
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        className="text-sm font-semibold text-foreground-muted hover:text-foreground"
      >
        {open ? "▾" : "▸"} Archived programs
      </button>
      {open && (
        <div className="mt-2 flex flex-col gap-2">
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
                <Button
                  variant="secondary"
                  size="sm"
                  disabled={m.restore.isPending}
                  onClick={() => m.restore.mutate(p.id)}
                >
                  Restore
                </Button>
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
              variant="danger"
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
          </>
        }
      />

      <div className="flex flex-col gap-3">
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
