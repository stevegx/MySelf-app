import { useState } from "react";
import { ChevronLeft, Plus } from "lucide-react";
import { Button, Card, CardKicker, Input, PageHeader } from "../../components/ui";
import { VariantEditor } from "./VariantEditor";
import { useCreateProgram, useMutateProgram, useProgram, usePrograms } from "./api";

export function WorkoutBuilderScreen() {
  const [programId, setProgramId] = useState<string | null>(null);
  const [variantId, setVariantId] = useState<string | null>(null);

  if (programId && variantId) {
    return (
      <>
        <PageHeader title="Edit variant" actions={<BackButton onClick={() => setVariantId(null)} label="Back to program" />} />
        <Card>
          <VariantEditor programId={programId} variantId={variantId} onClose={() => setVariantId(null)} />
        </Card>
      </>
    );
  }

  if (programId) {
    return <ProgramDetail programId={programId} onBack={() => setProgramId(null)} onEditVariant={setVariantId} />;
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
  const { data: programs, isLoading } = usePrograms();
  const create = useCreateProgram();
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
      <PageHeader title="Workout programs" subtitle="Build the workouts you train from. No fixed days." />

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
                  {p.groupCount} groups · {p.variantCount} variants
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
    </>
  );
}

function ProgramDetail({
  programId,
  onBack,
  onEditVariant,
}: {
  programId: string;
  onBack: () => void;
  onEditVariant: (id: string) => void;
}) {
  const { data: program, isLoading } = useProgram(programId);
  const m = useMutateProgram(programId);
  const [groupName, setGroupName] = useState("");
  const [variantNameByGroup, setVariantNameByGroup] = useState<Record<string, string>>({});

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
              onClick={() => {
                if (confirm("Archive this program?")) m.archive.mutate(program.id, { onSuccess: onBack });
              }}
            >
              Archive
            </Button>
          </>
        }
      />

      <div className="flex flex-col gap-3">
        {program.groups.map((g) => (
          <Card key={g.id} className="gap-2">
            <div className="flex items-center justify-between">
              <span className="text-sm font-bold uppercase tracking-[0.06em] text-primary-pressed">{g.name}</span>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => {
                  if (confirm(`Delete group "${g.name}" and its variants?`)) m.deleteGroup.mutate(g.id);
                }}
              >
                Delete group
              </Button>
            </div>

            {g.variants.map((v) => (
              <div key={v.id} className="flex items-center justify-between rounded-control border border-border px-3 py-2">
                <span className="text-[13px]">
                  <span className="font-semibold">{v.name}</span>
                  <span className="ml-2 text-xs text-foreground-muted">{v.exerciseCount} exercises</span>
                </span>
                <div className="flex gap-1">
                  <Button variant="secondary" size="sm" onClick={() => onEditVariant(v.id)}>
                    Edit
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => {
                      if (confirm(`Delete variant "${v.name}"?`)) m.deleteVariant.mutate(v.id);
                    }}
                  >
                    Delete
                  </Button>
                </div>
              </div>
            ))}

            <div className="flex gap-2">
              <Input
                placeholder="New variant name (e.g. Legs #1)"
                value={variantNameByGroup[g.id] ?? ""}
                onChange={(e) => setVariantNameByGroup((s) => ({ ...s, [g.id]: e.target.value }))}
                className="max-w-[220px]"
              />
              <Button
                variant="secondary"
                size="sm"
                disabled={!(variantNameByGroup[g.id] ?? "").trim()}
                onClick={() => {
                  const nm = (variantNameByGroup[g.id] ?? "").trim();
                  if (nm) {
                    m.addVariant.mutate({ groupId: g.id, name: nm });
                    setVariantNameByGroup((s) => ({ ...s, [g.id]: "" }));
                  }
                }}
              >
                <Plus size={14} aria-hidden />
                Add variant
              </Button>
            </div>
          </Card>
        ))}

        <Card className="gap-2">
          <CardKicker>Add workout group</CardKicker>
          <div className="flex gap-2">
            <Input
              placeholder="Group name (e.g. Push)"
              value={groupName}
              onChange={(e) => setGroupName(e.target.value)}
              className="max-w-[220px]"
            />
            <Button
              variant="secondary"
              disabled={!groupName.trim()}
              onClick={() => {
                m.addGroup.mutate({ name: groupName.trim() });
                setGroupName("");
              }}
            >
              Add group
            </Button>
          </div>
        </Card>
      </div>
    </>
  );
}
