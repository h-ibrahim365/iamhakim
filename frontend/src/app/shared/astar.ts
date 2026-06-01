/**
 * A* on a 4-connected grid, exposed as an incremental stepper so the UI can
 * animate the frontier expanding. No external deps - this is the real thing,
 * the same family of algorithm behind my M1 internship router.
 */

export interface Cell {
  x: number;
  y: number;
}

export type CellState = 'empty' | 'wall' | 'open' | 'closed' | 'path' | 'start' | 'goal';

interface Node {
  x: number;
  y: number;
  g: number;
  h: number;
  f: number;
  parent: Node | null;
}

export interface AstarStats {
  expanded: number;
  frontier: number;
  pathLength: number;
  status: 'idle' | 'running' | 'found' | 'no-path';
}

export class AstarGrid {
  readonly cols: number;
  readonly rows: number;

  walls: boolean[][];
  start: Cell;
  goal: Cell;

  private open: Node[] = [];
  private closedSet = new Set<string>();
  private openMap = new Map<string, Node>();
  private goalNode: Node | null = null;

  expanded = 0;
  status: AstarStats['status'] = 'idle';

  constructor(cols: number, rows: number) {
    this.cols = cols;
    this.rows = rows;
    this.walls = Array.from({ length: rows }, () => Array<boolean>(cols).fill(false));
    this.start = { x: 1, y: Math.floor(rows / 2) };
    this.goal = { x: cols - 2, y: Math.floor(rows / 2) };
  }

  private key(x: number, y: number): string {
    return `${x},${y}`;
  }

  /** Manhattan distance - admissible on a 4-connected grid. */
  private heuristic(x: number, y: number): number {
    return Math.abs(x - this.goal.x) + Math.abs(y - this.goal.y);
  }

  reset(): void {
    this.open = [];
    this.closedSet.clear();
    this.openMap.clear();
    this.goalNode = null;
    this.expanded = 0;
    this.status = 'idle';
  }

  begin(): void {
    this.reset();
    const startNode: Node = {
      x: this.start.x,
      y: this.start.y,
      g: 0,
      h: this.heuristic(this.start.x, this.start.y),
      f: 0,
      parent: null
    };
    startNode.f = startNode.g + startNode.h;
    this.open.push(startNode);
    this.openMap.set(this.key(startNode.x, startNode.y), startNode);
    this.status = 'running';
  }

  /** One expansion. Returns false when the search is finished. */
  step(): boolean {
    if (this.status !== 'running') return false;
    if (this.open.length === 0) {
      this.status = 'no-path';
      return false;
    }

    // pop lowest f (linear scan - grid is small, clarity over micro-perf)
    let bestIdx = 0;
    for (let i = 1; i < this.open.length; i++) {
      if (this.open[i].f < this.open[bestIdx].f) bestIdx = i;
    }
    const current = this.open.splice(bestIdx, 1)[0];
    this.openMap.delete(this.key(current.x, current.y));
    this.closedSet.add(this.key(current.x, current.y));
    this.expanded++;

    if (current.x === this.goal.x && current.y === this.goal.y) {
      this.goalNode = current;
      this.status = 'found';
      return false;
    }

    const dirs = [
      [1, 0],
      [-1, 0],
      [0, 1],
      [0, -1]
    ];
    for (const [dx, dy] of dirs) {
      const nx = current.x + dx;
      const ny = current.y + dy;
      if (nx < 0 || ny < 0 || nx >= this.cols || ny >= this.rows) continue;
      if (this.walls[ny][nx]) continue;
      const k = this.key(nx, ny);
      if (this.closedSet.has(k)) continue;

      const g = current.g + 1;
      const existing = this.openMap.get(k);
      if (existing && g >= existing.g) continue;

      const node: Node = {
        x: nx,
        y: ny,
        g,
        h: this.heuristic(nx, ny),
        f: 0,
        parent: current
      };
      node.f = node.g + node.h;

      if (existing) {
        existing.g = node.g;
        existing.f = node.f;
        existing.parent = current;
      } else {
        this.open.push(node);
        this.openMap.set(k, node);
      }
    }
    return true;
  }

  cellState(x: number, y: number): CellState {
    if (x === this.start.x && y === this.start.y) return 'start';
    if (x === this.goal.x && y === this.goal.y) return 'goal';
    if (this.walls[y][x]) return 'wall';
    const k = this.key(x, y);
    if (this.closedSet.has(k)) return 'closed';
    if (this.openMap.has(k)) return 'open';
    return 'empty';
  }

  path(): Cell[] {
    const out: Cell[] = [];
    let n = this.goalNode;
    while (n) {
      out.push({ x: n.x, y: n.y });
      n = n.parent;
    }
    return out.reverse();
  }

  stats(): AstarStats {
    return {
      expanded: this.expanded,
      frontier: this.open.length,
      pathLength: this.status === 'found' ? this.path().length : 0,
      status: this.status
    };
  }

  /** Procedural obstacle field - varied each call so the demo never looks scripted. */
  randomizeWalls(density = 0.26): void {
    for (let y = 0; y < this.rows; y++) {
      for (let x = 0; x < this.cols; x++) {
        const isEndpoint =
          (x === this.start.x && y === this.start.y) || (x === this.goal.x && y === this.goal.y);
        this.walls[y][x] = !isEndpoint && Math.random() < density;
      }
    }
  }
}
