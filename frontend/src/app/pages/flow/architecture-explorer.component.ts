import { Component, computed, effect, inject, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { TranslatePipe } from '../../i18n/translate.pipe';
import { TranslationService } from '../../i18n/translation.service';

export type ArchId = 'layered' | 'onion' | 'hexagonal' | 'cqrs';
type CqrsMode = 'command' | 'query';
type StepStatus = 'idle' | 'running' | 'done';
type ScenarioId = 'booking' | 'profile' | 'deletion';

interface StepDef { id: string; index: string; titleKey: string; descKey: string; exampleKey: string; }
interface RuntimeStep extends StepDef { status: StepStatus; }
interface ScenarioDef {
  id: ScenarioId;
  tabKey: string;
  entityKey: string;
  actionKey: string;
  /** Bare infinitive (create/update/delete) - used in quoted operation names like "create Booking". */
  verbKey: string;
  /** Present-tense form (creates/updates/deletes) - used after an implicit "it ___s the entity". */
  verbActionKey: string;
  /** Capitalized code-identifier form (Create/Update/Delete) - always English, used where the text represents a class/folder name regardless of UI language. */
  verbCapitalizedKey: string;
  /** Noun form (creation/update/deletion) - used in "handles the {entity} {verbNoun}" style phrasing, chosen specifically to sidestep French/Dutch past-participle gender/agreement issues that a passive "is {verb}ed" construction would hit. */
  verbNounKey: string;
}

const SCENARIOS: ScenarioDef[] = [
  {
    id: 'booking', tabKey: 'flow.arch.scenario.booking.tab', entityKey: 'flow.arch.scenario.booking.entity', actionKey: 'flow.arch.scenario.booking.action',
    verbKey: 'flow.arch.scenario.booking.verb', verbActionKey: 'flow.arch.scenario.booking.verbAction', verbCapitalizedKey: 'flow.arch.scenario.booking.verbCapitalized', verbNounKey: 'flow.arch.scenario.booking.verbNoun'
  },
  {
    id: 'profile', tabKey: 'flow.arch.scenario.profile.tab', entityKey: 'flow.arch.scenario.profile.entity', actionKey: 'flow.arch.scenario.profile.action',
    verbKey: 'flow.arch.scenario.profile.verb', verbActionKey: 'flow.arch.scenario.profile.verbAction', verbCapitalizedKey: 'flow.arch.scenario.profile.verbCapitalized', verbNounKey: 'flow.arch.scenario.profile.verbNoun'
  },
  {
    id: 'deletion', tabKey: 'flow.arch.scenario.deletion.tab', entityKey: 'flow.arch.scenario.deletion.entity', actionKey: 'flow.arch.scenario.deletion.action',
    verbKey: 'flow.arch.scenario.deletion.verb', verbActionKey: 'flow.arch.scenario.deletion.verbAction', verbCapitalizedKey: 'flow.arch.scenario.deletion.verbCapitalized', verbNounKey: 'flow.arch.scenario.deletion.verbNoun'
  }
];

// ---- per-architecture step definitions - content (titles/descriptions/examples)
// is shared with the previous linear version; only the spatial ARRANGEMENT is new
// (built by hand per architecture in the template, not derived from this list). ----

const LAYERED_STEPS: StepDef[] = [
  { id: 'ui', index: '01', titleKey: 'flow.arch.layered.step1.title', descKey: 'flow.arch.layered.step1.desc', exampleKey: 'flow.arch.layered.step1.example' },
  { id: 'controller', index: '02', titleKey: 'flow.arch.layered.step2.title', descKey: 'flow.arch.layered.step2.desc', exampleKey: 'flow.arch.layered.step2.example' },
  { id: 'service', index: '03', titleKey: 'flow.arch.layered.step3.title', descKey: 'flow.arch.layered.step3.desc', exampleKey: 'flow.arch.layered.step3.example' },
  { id: 'repository', index: '04', titleKey: 'flow.arch.layered.step4.title', descKey: 'flow.arch.layered.step4.desc', exampleKey: 'flow.arch.layered.step4.example' },
  { id: 'database', index: '05', titleKey: 'flow.arch.layered.step5.title', descKey: 'flow.arch.layered.step5.desc', exampleKey: 'flow.arch.layered.step5.example' }
];

const ONION_STEPS: StepDef[] = [
  { id: 'presentation', index: '01', titleKey: 'flow.arch.onion.step1.title', descKey: 'flow.arch.onion.step1.desc', exampleKey: 'flow.arch.onion.step1.example' },
  { id: 'application', index: '02', titleKey: 'flow.arch.onion.step2.title', descKey: 'flow.arch.onion.step2.desc', exampleKey: 'flow.arch.onion.step2.example' },
  { id: 'domain', index: '03', titleKey: 'flow.arch.onion.step3.title', descKey: 'flow.arch.onion.step3.desc', exampleKey: 'flow.arch.onion.step3.example' },
  { id: 'repo-abstraction', index: '04', titleKey: 'flow.arch.onion.step4.title', descKey: 'flow.arch.onion.step4.desc', exampleKey: 'flow.arch.onion.step4.example' },
  { id: 'infrastructure', index: '05', titleKey: 'flow.arch.onion.step5.title', descKey: 'flow.arch.onion.step5.desc', exampleKey: 'flow.arch.onion.step5.example' },
  { id: 'database', index: '06', titleKey: 'flow.arch.onion.step6.title', descKey: 'flow.arch.onion.step6.desc', exampleKey: 'flow.arch.onion.step6.example' }
];

const HEXAGONAL_STEPS: StepDef[] = [
  { id: 'inbound-adapter', index: '01', titleKey: 'flow.arch.hexagonal.step1.title', descKey: 'flow.arch.hexagonal.step1.desc', exampleKey: 'flow.arch.hexagonal.step1.example' },
  { id: 'inbound-port', index: '02', titleKey: 'flow.arch.hexagonal.step2.title', descKey: 'flow.arch.hexagonal.step2.desc', exampleKey: 'flow.arch.hexagonal.step2.example' },
  { id: 'use-case', index: '03', titleKey: 'flow.arch.hexagonal.step3.title', descKey: 'flow.arch.hexagonal.step3.desc', exampleKey: 'flow.arch.hexagonal.step3.example' },
  { id: 'domain', index: '04', titleKey: 'flow.arch.hexagonal.step4.title', descKey: 'flow.arch.hexagonal.step4.desc', exampleKey: 'flow.arch.hexagonal.step4.example' },
  { id: 'outbound-port', index: '05', titleKey: 'flow.arch.hexagonal.step5.title', descKey: 'flow.arch.hexagonal.step5.desc', exampleKey: 'flow.arch.hexagonal.step5.example' },
  { id: 'outbound-adapter', index: '06', titleKey: 'flow.arch.hexagonal.step6.title', descKey: 'flow.arch.hexagonal.step6.desc', exampleKey: 'flow.arch.hexagonal.step6.example' },
  { id: 'infrastructure', index: '07', titleKey: 'flow.arch.hexagonal.step7.title', descKey: 'flow.arch.hexagonal.step7.desc', exampleKey: 'flow.arch.hexagonal.step7.example' }
];

// CQRS: "request" and "dispatcher" are shared (rendered once, above the split);
// the rest are branch-specific and rendered twice (once per slice), with only
// the selected branch actually animating - the other stays visibly idle.
const CQRS_SHARED_STEPS: StepDef[] = [
  { id: 'request', index: '01', titleKey: 'flow.arch.cqrs.request.title', descKey: 'flow.arch.cqrs.request.desc', exampleKey: 'flow.arch.cqrs.request.example' },
  { id: 'dispatcher', index: '02', titleKey: 'flow.arch.cqrs.dispatcher.title', descKey: 'flow.arch.cqrs.dispatcher.desc', exampleKey: 'flow.arch.cqrs.dispatcher.example' }
];
const CQRS_COMMAND_STEPS: StepDef[] = [
  { id: 'command', index: '03', titleKey: 'flow.arch.cqrs.cmd3.title', descKey: 'flow.arch.cqrs.cmd3.desc', exampleKey: 'flow.arch.cqrs.cmd3.example' },
  { id: 'cmd-handler', index: '04', titleKey: 'flow.arch.cqrs.cmd4.title', descKey: 'flow.arch.cqrs.cmd4.desc', exampleKey: 'flow.arch.cqrs.cmd4.example' },
  { id: 'domain-port', index: '05', titleKey: 'flow.arch.cqrs.cmd5.title', descKey: 'flow.arch.cqrs.cmd5.desc', exampleKey: 'flow.arch.cqrs.cmd5.example' },
  { id: 'cmd-adapter', index: '06', titleKey: 'flow.arch.cqrs.cmd6.title', descKey: 'flow.arch.cqrs.cmd6.desc', exampleKey: 'flow.arch.cqrs.cmd6.example' },
  { id: 'cmd-infrastructure', index: '07', titleKey: 'flow.arch.cqrs.cmd7.title', descKey: 'flow.arch.cqrs.cmd7.desc', exampleKey: 'flow.arch.cqrs.cmd7.example' }
];
const CQRS_QUERY_STEPS: StepDef[] = [
  { id: 'query', index: '03', titleKey: 'flow.arch.cqrs.qry3.title', descKey: 'flow.arch.cqrs.qry3.desc', exampleKey: 'flow.arch.cqrs.qry3.example' },
  { id: 'qry-handler', index: '04', titleKey: 'flow.arch.cqrs.qry4.title', descKey: 'flow.arch.cqrs.qry4.desc', exampleKey: 'flow.arch.cqrs.qry4.example' },
  { id: 'read-abstraction', index: '05', titleKey: 'flow.arch.cqrs.qry5.title', descKey: 'flow.arch.cqrs.qry5.desc', exampleKey: 'flow.arch.cqrs.qry5.example' },
  { id: 'qry-adapter', index: '06', titleKey: 'flow.arch.cqrs.qry6.title', descKey: 'flow.arch.cqrs.qry6.desc', exampleKey: 'flow.arch.cqrs.qry6.example' },
  { id: 'result', index: '07', titleKey: 'flow.arch.cqrs.qry7.title', descKey: 'flow.arch.cqrs.qry7.desc', exampleKey: 'flow.arch.cqrs.qry7.example' }
];

/** Every architecture explains itself the same way: what it is (KEY IDEA),
 *  when it's worth it (USE IT WHEN), and its main trade-off (WATCH OUT) -
 *  one consistent structure instead of four different ad-hoc metadata sets
 *  (focus / secondary focus / fit / slice), which read as duplicated or
 *  redundant once KEY IDEA already made the point. noteKey stays for the one
 *  short, architecture-specific aside (Onion's runtime-vs-dependency
 *  distinction, CQRS's "this composes with Onion/Hexagonal" clarification) -
 *  it renders after the three main blocks, visually secondary. */
interface FlowMeta {
  id: ArchId;
  tabKey: string;
  keyIdeaKey: string;
  useItWhenKey: string;
  watchOutKey: string;
  noteKey?: string;
}

const FLOW_META: FlowMeta[] = [
  {
    id: 'layered',
    tabKey: 'flow.arch.tab.layered',
    keyIdeaKey: 'flow.arch.layered.keyIdea',
    useItWhenKey: 'flow.arch.layered.useItWhen',
    watchOutKey: 'flow.arch.layered.watchOut'
  },
  {
    id: 'onion',
    tabKey: 'flow.arch.tab.onion',
    keyIdeaKey: 'flow.arch.onion.keyIdea',
    useItWhenKey: 'flow.arch.onion.useItWhen',
    watchOutKey: 'flow.arch.onion.watchOut',
    noteKey: 'flow.arch.onion.note'
  },
  {
    id: 'hexagonal',
    tabKey: 'flow.arch.tab.hexagonal',
    keyIdeaKey: 'flow.arch.hexagonal.keyIdea',
    useItWhenKey: 'flow.arch.hexagonal.useItWhen',
    watchOutKey: 'flow.arch.hexagonal.watchOut'
  },
  {
    id: 'cqrs',
    tabKey: 'flow.arch.tab.cqrs',
    keyIdeaKey: 'flow.arch.cqrs.keyIdea',
    useItWhenKey: 'flow.arch.cqrs.useItWhen',
    watchOutKey: 'flow.arch.cqrs.watchOut',
    noteKey: 'flow.arch.cqrs.note'
  }
];

/**
 * ALL steps that need to exist (and render) for the given architecture -
 * for CQRS this is deliberately BOTH branches at once (shared + command +
 * query), never just the selected one. Both slices must stay visible and
 * inspectable regardless of which mode is currently animating; only
 * `animationOrder()` below decides which subset actually moves.
 */
function allStepsForArch(arch: ArchId): StepDef[] {
  switch (arch) {
    case 'layered': return LAYERED_STEPS;
    case 'onion': return ONION_STEPS;
    case 'hexagonal': return HEXAGONAL_STEPS;
    case 'cqrs': return [...CQRS_SHARED_STEPS, ...CQRS_COMMAND_STEPS, ...CQRS_QUERY_STEPS];
  }
}

/** Step ids in the order the run() animation should visit them for the current architecture/CQRS mode. */
function animationOrder(arch: ArchId, cqrsMode: CqrsMode): string[] {
  const steps = arch === 'cqrs'
    ? [...CQRS_SHARED_STEPS, ...(cqrsMode === 'command' ? CQRS_COMMAND_STEPS : CQRS_QUERY_STEPS)]
    : allStepsForArch(arch);
  return steps.map((s) => s.id);
}

@Component({
  selector: 'app-architecture-explorer',
  imports: [TranslatePipe, NgTemplateOutlet],
  templateUrl: './architecture-explorer.component.html',
  styleUrl: './architecture-explorer.component.scss'
})
export class ArchitectureExplorerComponent {
  private readonly i18n = inject(TranslationService);

  protected readonly architectures = FLOW_META;
  protected readonly scenarios = SCENARIOS;
  protected readonly cqrsCommandSteps = CQRS_COMMAND_STEPS;
  protected readonly cqrsQuerySteps = CQRS_QUERY_STEPS;

  protected readonly selectedArch = signal<ArchId>('layered');
  protected readonly cqrsMode = signal<CqrsMode>('command');
  protected readonly selectedScenario = signal<ScenarioId>('booking');
  protected readonly speed = signal<1 | 2>(1);
  protected readonly running = signal(false);
  protected readonly hasRun = signal(false);
  private readonly runtimeSteps = signal<RuntimeStep[]>([]);
  private runToken = 0;

  protected readonly currentFlow = computed(() => this.architectures.find((a) => a.id === this.selectedArch())!);
  protected readonly currentScenario = computed(() => this.scenarios.find((s) => s.id === this.selectedScenario())!);
  protected readonly entityText = computed(() => this.i18n.t(this.currentScenario().entityKey));
  protected readonly actionText = computed(() => this.i18n.t(this.currentScenario().actionKey));
  protected readonly verbText = computed(() => this.i18n.t(this.currentScenario().verbKey));
  protected readonly verbActionText = computed(() => this.i18n.t(this.currentScenario().verbActionKey));
  protected readonly verbCapitalizedText = computed(() => this.i18n.t(this.currentScenario().verbCapitalizedKey));
  protected readonly verbNounText = computed(() => this.i18n.t(this.currentScenario().verbNounKey));

  /** O(1) lookup used by the bespoke per-architecture templates to place a
   *  given step's live (status-bearing) data at its spatial position. For
   *  CQRS this map always has BOTH branches, so the non-selected slice still
   *  renders (just permanently idle) instead of disappearing. */
  protected readonly stepsById = computed(() => new Map(this.runtimeSteps().map((s) => [s.id, s] as const)));

  /**
   * True while the flow is anywhere inside the "core" (Use Case → Domain in
   * Hexagonal, Application → Domain in Onion) - not just during Domain's own
   * single step. Domain isn't a one-off hop conceptually; the core it belongs
   * to stays visually engaged for the whole window business logic could be
   * consulted, not only its brief individual turn.
   *
   * It must also turn OFF again once execution has moved past the core's own
   * boundary (the outbound port). A plain "any core step !== idle" check
   * never resets to false once those steps go 'done', so the core would stay
   * highlighted even while the adapter/infrastructure steps are running -
   * visually implying the request was still inside the core when it's long
   * gone. entered / left keeps it a true "currently inside" flag instead.
   */
  protected readonly coreEngaged = computed(() => {
    const arch = this.selectedArch();
    const byId = this.stepsById();
    if (arch === 'hexagonal') {
      const useCase = byId.get('use-case');
      const domain = byId.get('domain');
      const outboundPort = byId.get('outbound-port');
      const entered = useCase?.status !== 'idle' || domain?.status !== 'idle';
      const left = outboundPort?.status !== 'idle';
      return entered && !left;
    }
    if (arch === 'onion') {
      const application = byId.get('application');
      const domain = byId.get('domain');
      const repoAbstraction = byId.get('repo-abstraction');
      const entered = application?.status !== 'idle' || domain?.status !== 'idle';
      const left = repoAbstraction?.status !== 'idle';
      return entered && !left;
    }
    return false;
  });

  /** The ids that actually animate for the current architecture/mode - used
   *  for progress counts and by run(), kept separate from the full render set. */
  private readonly currentAnimationOrder = computed(() => animationOrder(this.selectedArch(), this.cqrsMode()));

  protected readonly doneCount = computed(() => {
    const ids = new Set(this.currentAnimationOrder());
    return this.runtimeSteps().filter((s) => ids.has(s.id) && s.status === 'done').length;
  });
  protected readonly totalSteps = computed(() => this.currentAnimationOrder().length);

  constructor() {
    effect(() => {
      const arch = this.selectedArch();
      this.cqrsMode(); // switching Command/Query also resets execution, per spec
      this.runToken++;
      this.running.set(false);
      this.hasRun.set(false);
      this.runtimeSteps.set(allStepsForArch(arch).map((s) => ({ ...s, status: 'idle' as StepStatus })));
    });
  }

  selectArch(id: ArchId): void {
    if (this.selectedArch() === id) return;
    this.selectedArch.set(id);
  }

  selectCqrsMode(mode: CqrsMode): void {
    if (this.cqrsMode() === mode) return;
    this.cqrsMode.set(mode);
  }

  selectScenario(id: ScenarioId): void {
    this.selectedScenario.set(id);
  }

  setSpeed(value: 1 | 2): void {
    this.speed.set(value);
  }

  async run(): Promise<void> {
    if (this.running()) return;
    const token = ++this.runToken;
    this.running.set(true);
    this.hasRun.set(true);
    // Reset only the ids that are about to animate - the other CQRS branch
    // (not in this order) is left exactly as it was, still fully rendered.
    const order = this.currentAnimationOrder();
    const orderSet = new Set(order);
    this.runtimeSteps.update((steps) => steps.map((s) => (orderSet.has(s.id) ? { ...s, status: 'idle' as StepStatus } : s)));

    const stepMs = this.speed() === 2 ? 320 : 650;
    const gapMs = this.speed() === 2 ? 90 : 170;

    for (const id of order) {
      if (token !== this.runToken) return; // architecture/mode changed mid-run
      this.setStatusById(id, 'running');
      await this.delay(stepMs);
      if (token !== this.runToken) return;
      this.setStatusById(id, 'done');
      await this.delay(gapMs);
    }
    // Deliberately no auto-reset at the end: completed state stays visible
    // until Run again / architecture switch / Command-Query switch.
    if (token === this.runToken) this.running.set(false);
  }

  private setStatusById(id: string, status: StepStatus): void {
    this.runtimeSteps.update((steps) => steps.map((s) => (s.id === id ? { ...s, status } : s)));
  }

  private delay(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }
}
