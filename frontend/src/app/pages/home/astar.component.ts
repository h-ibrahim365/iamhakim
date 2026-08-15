import {
  AfterViewInit, Component, ElementRef, NgZone, OnDestroy, PLATFORM_ID,
  computed, effect, inject, output, signal, viewChild
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { AstarGrid, AstarStats } from '../../shared/astar';
import { TranslatePipe } from '../../i18n/translate.pipe';
import { ThemeService } from '../../core/theme.service';

type Mode = 'demo' | 'maze';

interface Achievement { id: string; icon: string; labelKey: string; hintKey: string; unlocked: boolean; }

@Component({
  selector: 'app-astar',
  imports: [TranslatePipe],
  templateUrl: './astar.component.html',
  styleUrl: './astar.component.scss'
})
export class AstarComponent implements AfterViewInit, OnDestroy {
  private readonly platformId = inject(PLATFORM_ID);
  private readonly zone = inject(NgZone);
  private readonly themeService = inject(ThemeService);
  private readonly canvasRef = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');

  /** Redraws the grid whenever the light/dark theme flips - the canvas paints
   * with plain colors, so it can't react to CSS custom properties on its own. */
  private readonly redrawOnThemeChange = effect(() => {
    this.themeService.theme();
    this.draw(this.grid?.status === 'found');
  });

  readonly runFinished = output<{ outcome: 'found' | 'no-path'; expanded: number; mode: Mode; score: number }>();

  protected readonly mode = signal<Mode>('demo');
  protected readonly playState = signal<'idle' | 'running' | 'paused' | 'done'>('idle');
  protected readonly stats = signal<AstarStats>({ expanded: 0, frontier: 0, pathLength: 0, status: 'idle' });
  protected readonly speed = signal<number>(3);

  protected readonly wallBudget = signal<number>(60);
  protected readonly wallsUsed = signal<number>(0);
  protected readonly wallsLeft = computed(() => Math.max(0, this.wallBudget() - this.wallsUsed()));
  protected readonly score = signal<number>(0);
  protected readonly bestScore = signal<number>(0);
  protected readonly isNewBest = signal<boolean>(false);

  protected readonly scoreRank = computed(() => {
    const s = this.score();
    if (s === 0) return '-';
    if (s < 60) return 'astar.rank.apprentice';
    if (s < 120) return 'astar.rank.tactician';
    if (s < 220) return 'astar.rank.architect';
    if (s < 360) return 'astar.rank.lord';
    return 'astar.rank.nemesis';
  });

  protected readonly buttonLabel = computed(() => {
    if (this.mode() === 'maze') {
      switch (this.playState()) {
        case 'running': return 'astar.btn.solving';
        case 'done': return 'astar.btn.playagain';
        default: return 'astar.btn.run';
      }
    }
    switch (this.playState()) {
      case 'running': return 'astar.btn.pause';
      case 'paused': return 'astar.btn.resume';
      case 'done': return 'astar.btn.again';
      default: return 'astar.btn.start';
    }
  });

  protected readonly statusLabel = computed(() => {
    const s = this.stats().status;
    if (this.playState() === 'paused') return 'astar.status.paused';
    if (s === 'running') return 'astar.status.searching';
    if (s === 'found') return 'astar.status.found';
    if (s === 'no-path') return 'astar.status.blocked';
    return 'astar.status.ready';
  });

  protected readonly achievements = signal<Achievement[]>([
    { id: 'architect', icon: '◳', labelKey: 'astar.ach.architect', hintKey: 'astar.ach.architect.hint', unlocked: false },
    { id: 'deadend', icon: '⊘', labelKey: 'astar.ach.deadend', hintKey: 'astar.ach.deadend.hint', unlocked: false },
    { id: 'speedrun', icon: '⚡', labelKey: 'astar.ach.speedrun', hintKey: 'astar.ach.speedrun.hint', unlocked: false },
    { id: 'maze', icon: '▦', labelKey: 'astar.ach.maze', hintKey: 'astar.ach.maze.hint', unlocked: false }
  ]);
  protected readonly toast = signal<Achievement | null>(null);
  protected readonly unlockedCount = computed(() => this.achievements().filter((a) => a.unlocked).length);

  private grid!: AstarGrid;
  private ctx: CanvasRenderingContext2D | null = null;
  private rafId: number | null = null;
  private cell = 16;
  private readonly cols = 41;
  private readonly rows = 21;
  private dragging = false;
  private dragValue = true;
  private finishedEmitted = false;
  private runToken = 0;
  private toastTimer: number | null = null;

  ngAfterViewInit(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    const canvas = this.canvasRef().nativeElement;
    this.ctx = canvas.getContext('2d');
    this.setupCanvas();
    this.grid = new AstarGrid(this.cols, this.rows);
    this.grid.randomizeWalls(0.24);
    this.draw();
    this.loadProgress();
    window.addEventListener('resize', this.onResize);
  }

  ngOnDestroy(): void {
    if (this.rafId !== null) cancelAnimationFrame(this.rafId);
    if (isPlatformBrowser(this.platformId)) window.removeEventListener('resize', this.onResize);
  }

  private onResize = (): void => {
    const wasRunning = this.playState() === 'running';
    if (this.rafId !== null) cancelAnimationFrame(this.rafId);
    this.setupCanvas();
    this.draw(this.grid.status === 'found');
    if (wasRunning) this.loop(this.runToken);
  };

  private setupCanvas(): void {
    const canvas = this.canvasRef().nativeElement;
    const dpr = window.devicePixelRatio || 1;
    const wrapW = canvas.parentElement?.clientWidth ?? 660;
    const cssWidth = Math.max(1, Math.floor(wrapW));
    const cssHeight = (cssWidth / this.cols) * this.rows;

    this.cell = cssWidth / this.cols;
    canvas.width = Math.ceil(cssWidth * dpr);
    canvas.height = Math.ceil(cssHeight * dpr);
    canvas.style.width = '100%';
    canvas.style.height = `${cssHeight}px`;
    canvas.parentElement?.style.setProperty('--astar-cell-size', `${this.cell}px`);
    this.ctx?.setTransform(dpr, 0, 0, dpr, 0, 0);
  }

  setMode(m: Mode): void {
    if (m === this.mode()) return;
    this.runToken++;
    if (this.rafId !== null) cancelAnimationFrame(this.rafId);
    this.rafId = null;
    this.mode.set(m);
    this.playState.set('idle');
    this.finishedEmitted = false;
    this.score.set(0);
    this.isNewBest.set(false);
    this.grid.reset();
    if (m === 'demo') {
      this.grid.randomizeWalls(0.24);
    } else {
      this.clearWalls();
      this.wallsUsed.set(0);
    }
    this.draw(false);
    this.stats.set(this.grid.stats());
  }

  toggle(): void {
    const state = this.playState();
    if (this.mode() === 'maze') {
      if (state === 'running') return;
      if (state === 'done') { this.newMazeRound(); return; }
      this.startSearch();
      return;
    }
    if (state === 'running') {
      if (this.rafId !== null) cancelAnimationFrame(this.rafId);
      this.playState.set('paused');
      return;
    }
    if (state === 'paused') {
      this.playState.set('running');
      this.zone.runOutsideAngular(() => this.loop(this.runToken));
      return;
    }
    this.startSearch();
  }

  private startSearch(): void {
    this.grid.begin();
    this.finishedEmitted = false;
    this.playState.set('running');
    this.stats.set(this.grid.stats());
    this.zone.runOutsideAngular(() => this.loop(this.runToken));
  }

  newMap(): void {
    this.runToken++;
    if (this.rafId !== null) cancelAnimationFrame(this.rafId);
    this.rafId = null;
    this.grid.reset();
    this.grid.randomizeWalls(0.24);
    this.playState.set('idle');
    this.finishedEmitted = false;
    this.draw(false);
    this.stats.set(this.grid.stats());
  }

  private newMazeRound(): void {
    this.runToken++;
    if (this.rafId !== null) cancelAnimationFrame(this.rafId);
    this.rafId = null;
    this.grid.reset();
    this.clearWalls();
    this.wallsUsed.set(0);
    this.score.set(0);
    this.isNewBest.set(false);
    this.playState.set('idle');
    this.finishedEmitted = false;
    this.draw(false);
    this.stats.set(this.grid.stats());
  }

  cycleSpeed(): void {
    this.speed.set(this.speed() >= 6 ? 1 : this.speed() + 1);
  }

  private loop(token: number): void {
    if (token !== this.runToken) return;
    let alive = true;
    for (let i = 0; i < this.speed() && alive; i++) alive = this.grid.step();
    this.draw();
    const s = this.grid.stats();
    this.zone.run(() => this.stats.set(s));
    if (alive) {
      this.rafId = requestAnimationFrame(() => this.loop(token));
      return;
    }
    this.draw(true);
    this.zone.run(() => this.settle(s));
  }

  private settle(s: AstarStats): void {
    this.playState.set('done');
    this.stats.set(s);
    if (this.finishedEmitted) return;
    this.finishedEmitted = true;

    let score = 0;
    if (this.mode() === 'maze') {
      score = s.status === 'found' ? s.expanded : 0;
      this.score.set(score);
      if (score > this.bestScore()) {
        this.bestScore.set(score);
        this.isNewBest.set(true);
        this.saveProgress();
      }
      if (score >= 200) this.unlock('maze');
    }

    if (s.expanded > 300) this.unlock('architect');
    if (s.status === 'no-path') this.unlock('deadend');
    if (s.status === 'found' && s.expanded < 50) this.unlock('speedrun');

    if (s.status === 'found' || s.status === 'no-path') {
      this.runFinished.emit({ outcome: s.status, expanded: s.expanded, mode: this.mode(), score });
    }
  }

  onPointerDown(ev: PointerEvent): void {
    if (this.playState() === 'running') return;
    const cell = this.pointToCell(ev);
    if (!cell) return;
    this.dragging = true;
    this.dragValue = !this.grid.walls[cell.y][cell.x];
    this.paint(cell.x, cell.y);
  }

  onPointerMove(ev: PointerEvent): void {
    if (!this.dragging) return;
    const cell = this.pointToCell(ev);
    if (cell) this.paint(cell.x, cell.y);
  }

  onPointerUp(): void { this.dragging = false; }

  private paint(x: number, y: number): void {
    const isEndpoint =
      (x === this.grid.start.x && y === this.grid.start.y) ||
      (x === this.grid.goal.x && y === this.grid.goal.y);
    if (isEndpoint) return;
    const currentlyWall = this.grid.walls[y][x];
    if (this.mode() === 'maze') {
      if (this.dragValue && !currentlyWall) {
        if (this.wallsLeft() <= 0) return;
        this.grid.walls[y][x] = true;
        this.wallsUsed.update((n) => n + 1);
      } else if (!this.dragValue && currentlyWall) {
        this.grid.walls[y][x] = false;
        this.wallsUsed.update((n) => Math.max(0, n - 1));
      }
    } else {
      this.grid.walls[y][x] = this.dragValue;
    }
    if (this.playState() !== 'running') this.draw();
  }

  private pointToCell(ev: PointerEvent): { x: number; y: number } | null {
    const rect = this.canvasRef().nativeElement.getBoundingClientRect();
    if (rect.width <= 0 || rect.height <= 0) return null;

    const x = Math.floor(((ev.clientX - rect.left) / rect.width) * this.cols);
    const y = Math.floor(((ev.clientY - rect.top) / rect.height) * this.rows);
    if (x < 0 || y < 0 || x >= this.cols || y >= this.rows) return null;
    return { x, y };
  }

  private clearWalls(): void {
    for (let y = 0; y < this.rows; y++) for (let x = 0; x < this.cols; x++) this.grid.walls[y][x] = false;
  }

  private unlock(id: string): void {
    const list = this.achievements();
    const target = list.find((a) => a.id === id);
    if (!target || target.unlocked) return;
    this.achievements.set(list.map((a) => (a.id === id ? { ...a, unlocked: true } : a)));
    this.saveProgress();
    this.toast.set(target);
    if (this.toastTimer !== null) window.clearTimeout(this.toastTimer);
    this.toastTimer = window.setTimeout(() => this.toast.set(null), 3200);
  }

  private loadProgress(): void {
    if (typeof window === 'undefined') return;
    try {
      const best = Number(sessionStorage.getItem('iamhakim.astar.best') ?? '0');
      if (!Number.isNaN(best)) this.bestScore.set(best);
      const raw = sessionStorage.getItem('iamhakim.astar.achievements');
      if (raw) {
        const ids = new Set<string>(JSON.parse(raw));
        this.achievements.update((list) => list.map((a) => ({ ...a, unlocked: ids.has(a.id) })));
      }
    } catch { /* ignore */ }
  }

  private saveProgress(): void {
    if (typeof window === 'undefined') return;
    try {
      sessionStorage.setItem('iamhakim.astar.best', String(this.bestScore()));
      const ids = this.achievements().filter((a) => a.unlocked).map((a) => a.id);
      sessionStorage.setItem('iamhakim.astar.achievements', JSON.stringify(ids));
    } catch { /* ignore */ }
  }

  /** Reads all --maze-* custom properties once per draw so the canvas follows
   * the active theme (light/dark palettes are defined once, in styles.scss)
   * without paying for a getComputedStyle() call per grid cell. */
  private mazePalette(): Record<string, string> {
    if (!isPlatformBrowser(this.platformId)) {
      return { wall: 'transparent', closed: 'transparent', open: 'transparent', start: 'transparent', goal: 'transparent', path: 'transparent' };
    }
    const styles = getComputedStyle(this.canvasRef().nativeElement);
    const read = (name: string) => styles.getPropertyValue(name).trim();
    return {
      wall: read('--maze-wall'),
      closed: read('--maze-closed'),
      open: read('--maze-open'),
      start: read('--maze-start'),
      goal: read('--maze-goal'),
      path: read('--maze-path')
    };
  }

  private draw(withPath = false): void {
    const ctx = this.ctx;
    if (!ctx) return;
    const c = this.cell;
    const palette = this.mazePalette();
    const colorFor = (state: string): string => {
      switch (state) {
        case 'wall': return palette['wall'];
        case 'closed': return palette['closed'];
        case 'open': return palette['open'];
        case 'start': return palette['start'];
        case 'goal': return palette['goal'];
        default: return 'transparent';
      }
    };
    ctx.clearRect(0, 0, c * this.cols, c * this.rows);
    for (let y = 0; y < this.rows; y++) {
      for (let x = 0; x < this.cols; x++) {
        const st = this.grid.cellState(x, y);
        const fill = colorFor(st);
        if (fill === 'transparent') continue;
        const px = x * c, py = y * c;
        const inset = Math.max(1, c * 0.08);
        ctx.fillStyle = fill;
        this.roundRect(ctx, px + inset, py + inset, c - (inset * 2), c - (inset * 2), Math.max(1, c * 0.08));
        ctx.fill();
      }
    }
    if (withPath && this.grid.status === 'found') {
      const path = this.grid.path();
      ctx.strokeStyle = palette['path'];
      ctx.lineWidth = Math.max(2, c * 0.18);
      ctx.lineJoin = 'round'; ctx.lineCap = 'round';
      ctx.beginPath();
      path.forEach((p, i) => {
        const px = p.x * c + c / 2, py = p.y * c + c / 2;
        if (i === 0) ctx.moveTo(px, py); else ctx.lineTo(px, py);
      });
      ctx.stroke();
    }
  }

  private roundRect(ctx: CanvasRenderingContext2D, x: number, y: number, w: number, h: number, r: number): void {
    ctx.beginPath();
    ctx.moveTo(x + r, y);
    ctx.arcTo(x + w, y, x + w, y + h, r);
    ctx.arcTo(x + w, y + h, x, y + h, r);
    ctx.arcTo(x, y + h, x, y, r);
    ctx.arcTo(x, y, x + w, y, r);
    ctx.closePath();
  }
}
