import type { BlockMapDto } from '../../api/types';

export type IsoWorldType =
  | 'desert-frontier'
  | 'coastal-delta'
  | 'fold-belt'
  | 'offshore-shelf';

export type IsoTerrain =
  | 'desert'
  | 'scrub'
  | 'grass'
  | 'rock'
  | 'mountain'
  | 'water'
  | 'coast'
  | 'salt-flat';

export type IsoFeature =
  | 'none'
  | 'cactus'
  | 'palm'
  | 'tree'
  | 'rock'
  | 'oil-seep'
  | 'gas-flare';

export interface IsoWorldOptions {
  seed: string;
  worldType: IsoWorldType;
}

export interface IsoWorldTile {
  id: string;
  x: number;
  y: number;
  terrain: IsoTerrain;
  elevation: number;
  moisture: number;
  prospectivity: number;
  feature: IsoFeature;
  block?: BlockMapDto;
}

export interface IsoPointOfInterest {
  id: string;
  kind: 'export-terminal' | 'service-base' | 'port';
  x: number;
  y: number;
  label: string;
}

export interface IsoRoute {
  id: string;
  kind: 'pipeline' | 'road';
  points: Array<{ x: number; y: number }>;
}

export interface IsoWorld {
  seed: string;
  worldType: IsoWorldType;
  width: number;
  height: number;
  blockOffsetX: number;
  blockOffsetY: number;
  tiles: IsoWorldTile[];
  pointsOfInterest: IsoPointOfInterest[];
  routes: IsoRoute[];
}

const BLOCK_MARGIN = 3;
const PIPELINE_STAGES = new Set([
  'Producing',
  'LateLife',
  'UnderConstruction',
  'DevelopmentApproved',
]);

export function generateIsoWorld(
  blocks: BlockMapDto[],
  options: IsoWorldOptions,
): IsoWorld {
  const maxBlockX = blocks.length ? Math.max(...blocks.map((block) => block.gridX)) : 4;
  const maxBlockY = blocks.length ? Math.max(...blocks.map((block) => block.gridY)) : 3;
  const width = Math.max(10, maxBlockX + 1 + BLOCK_MARGIN * 2);
  const height = Math.max(9, maxBlockY + 1 + BLOCK_MARGIN * 2);
  const blockByCoord = new Map<string, BlockMapDto>();

  for (const block of blocks) {
    blockByCoord.set(`${block.gridX + BLOCK_MARGIN},${block.gridY + BLOCK_MARGIN}`, block);
  }

  const tiles: IsoWorldTile[] = [];
  for (let y = 0; y < height; y += 1) {
    for (let x = 0; x < width; x += 1) {
      const base = sampleWorld(options.seed, options.worldType, x, y, width, height);
      const block = blockByCoord.get(`${x},${y}`);
      const prospectivity = block?.estimatedChanceOfSuccess ?? base.prospectivity;

      tiles.push({
        id: `${x}:${y}`,
        x,
        y,
        terrain: base.terrain,
        elevation: round2(base.elevation),
        moisture: round2(base.moisture),
        prospectivity: round2(prospectivity),
        feature: block ? featureForBlock(block, base.feature) : base.feature,
        ...(block ? { block } : {}),
      });
    }
  }

  const exportTerminal = findExportTerminal(options.worldType, width, height, tiles);
  const routes = buildRoutes(blocks, exportTerminal);

  return {
    seed: options.seed,
    worldType: options.worldType,
    width,
    height,
    blockOffsetX: BLOCK_MARGIN,
    blockOffsetY: BLOCK_MARGIN,
    tiles,
    pointsOfInterest: [exportTerminal],
    routes,
  };
}

interface SampledTile {
  terrain: IsoTerrain;
  elevation: number;
  moisture: number;
  prospectivity: number;
  feature: IsoFeature;
}

function sampleWorld(
  seed: string,
  worldType: IsoWorldType,
  x: number,
  y: number,
  width: number,
  height: number,
): SampledTile {
  const nx = width <= 1 ? 0 : x / (width - 1);
  const ny = height <= 1 ? 0 : y / (height - 1);
  const landNoise = fbm(seed, `${worldType}:land`, x, y);
  const detailNoise = fbm(seed, `${worldType}:detail`, x + 19, y - 11);
  const wetNoise = fbm(seed, `${worldType}:wet`, x - 7, y + 23);
  const ridge = Math.abs(ny - (0.18 + nx * 0.42 + (detailNoise - 0.5) * 0.2));
  const shore = nx + ny * 0.32 + (landNoise - 0.5) * 0.22;

  let terrain: IsoTerrain;
  let elevation = detailNoise;
  let moisture = wetNoise;

  if (worldType === 'offshore-shelf') {
    terrain = shore > 0.58 ? 'water' : shore > 0.5 ? 'coast' : 'desert';
    if (terrain === 'desert' && ridge < 0.12) terrain = 'rock';
    if (terrain === 'desert' && wetNoise > 0.72) terrain = 'scrub';
    elevation = terrain === 'water' ? 0.05 + detailNoise * 0.12 : detailNoise * 0.45;
    moisture = Math.max(wetNoise, terrain === 'water' ? 1 : terrain === 'coast' ? 0.78 : 0.3);
  } else if (worldType === 'coastal-delta') {
    terrain = shore > 0.73 ? 'water' : shore > 0.63 ? 'coast' : 'grass';
    if (terrain === 'grass' && wetNoise < 0.38) terrain = 'scrub';
    if (terrain === 'grass' && detailNoise > 0.72) terrain = 'rock';
    if (ridge < 0.06 && shore < 0.55) terrain = 'mountain';
    moisture = Math.max(wetNoise, terrain === 'water' ? 1 : terrain === 'coast' ? 0.84 : 0.48);
  } else if (worldType === 'fold-belt') {
    terrain = ridge < 0.08 ? 'mountain' : ridge < 0.18 ? 'rock' : 'scrub';
    if (wetNoise > 0.74 && ridge > 0.18) terrain = 'grass';
    if (shore > 0.92) terrain = 'water';
    if (shore > 0.84 && shore <= 0.92) terrain = 'coast';
    elevation = Math.max(detailNoise, 1 - Math.min(1, ridge * 4));
    moisture = wetNoise * 0.7;
  } else {
    terrain = 'desert';
    if (ridge < 0.08) terrain = 'mountain';
    else if (ridge < 0.18 || detailNoise > 0.78) terrain = 'rock';
    else if (wetNoise > 0.74) terrain = 'scrub';
    else if (landNoise < 0.16) terrain = 'salt-flat';
    if (shore > 0.96) terrain = 'water';
    if (shore > 0.88 && shore <= 0.96) terrain = 'coast';
    moisture = wetNoise * 0.45;
  }

  return {
    terrain,
    elevation,
    moisture,
    prospectivity: prospectivityFor(seed, worldType, x, y, terrain),
    feature: featureFor(seed, worldType, x, y, terrain),
  };
}

function featureForBlock(block: BlockMapDto, fallback: IsoFeature): IsoFeature {
  if (block.stage === 'ExplorationDrilling') return 'gas-flare';
  if (block.stage === 'Discovery' || block.stage === 'Appraisal') return 'oil-seep';
  if (PIPELINE_STAGES.has(block.stage)) return 'gas-flare';
  return fallback;
}

function prospectivityFor(
  seed: string,
  worldType: IsoWorldType,
  x: number,
  y: number,
  terrain: IsoTerrain,
): number {
  const basinTrend = fbm(seed, `${worldType}:basin`, x + 101, y - 37);
  const terrainBonus =
    terrain === 'water' ? 0.1 : terrain === 'coast' ? 0.16 : terrain === 'rock' ? 0.2 : 0.12;
  return clamp(0.18 + basinTrend * 0.58 + terrainBonus, 0.05, 0.92);
}

function featureFor(
  seed: string,
  worldType: IsoWorldType,
  x: number,
  y: number,
  terrain: IsoTerrain,
): IsoFeature {
  const roll = hash01(`${seed}:${worldType}:feature:${x}:${y}`);
  if (terrain === 'water' || terrain === 'coast') return roll > 0.82 ? 'palm' : 'none';
  if (terrain === 'mountain' || terrain === 'rock') return roll > 0.55 ? 'rock' : 'none';
  if (terrain === 'grass') return roll > 0.7 ? 'tree' : 'none';
  if (terrain === 'scrub') return roll > 0.68 ? 'cactus' : 'none';
  if (terrain === 'desert') {
    if (roll > 0.91) return 'oil-seep';
    if (roll > 0.72) return 'cactus';
  }
  return 'none';
}

function findExportTerminal(
  worldType: IsoWorldType,
  width: number,
  height: number,
  tiles: IsoWorldTile[],
): IsoPointOfInterest {
  const preferred = worldType === 'offshore-shelf' || worldType === 'coastal-delta'
    ? ['coast', 'water']
    : ['desert', 'scrub', 'coast'];
  const candidates = tiles
    .filter((tile) => tile.x >= width - 3 && preferred.includes(tile.terrain))
    .sort((a, b) => Math.abs(a.y - height * 0.56) - Math.abs(b.y - height * 0.56));
  const tile = candidates[0] ?? tiles.find((candidate) => candidate.x === width - 2)
    ?? tiles[tiles.length - 1]!;

  return {
    id: 'export-terminal',
    kind: worldType === 'offshore-shelf' ? 'port' : 'export-terminal',
    x: tile.x,
    y: tile.y,
    label: worldType === 'offshore-shelf' ? 'Supply Port' : 'Export Terminal',
  };
}

function buildRoutes(blocks: BlockMapDto[], terminal: IsoPointOfInterest): IsoRoute[] {
  return blocks
    .filter((block) => PIPELINE_STAGES.has(block.stage))
    .map((block) => {
      const start = {
        x: block.gridX + BLOCK_MARGIN,
        y: block.gridY + BLOCK_MARGIN,
      };
      const mid = {
        x: Math.round((start.x + terminal.x) / 2),
        y: start.y,
      };

      return {
        id: `pipeline:${block.id}`,
        kind: 'pipeline' as const,
        points: [start, mid, { x: terminal.x, y: terminal.y }],
      };
    });
}

function fbm(seed: string, salt: string, x: number, y: number): number {
  const coarse = valueNoise(seed, salt, x, y, 4);
  const medium = valueNoise(seed, salt, x, y, 2);
  const fine = hash01(`${seed}:${salt}:${x}:${y}`);
  return coarse * 0.52 + medium * 0.32 + fine * 0.16;
}

function valueNoise(seed: string, salt: string, x: number, y: number, scale: number): number {
  const x0 = Math.floor(x / scale);
  const y0 = Math.floor(y / scale);
  const tx = (x % scale) / scale;
  const ty = (y % scale) / scale;
  const a = hash01(`${seed}:${salt}:${x0}:${y0}`);
  const b = hash01(`${seed}:${salt}:${x0 + 1}:${y0}`);
  const c = hash01(`${seed}:${salt}:${x0}:${y0 + 1}`);
  const d = hash01(`${seed}:${salt}:${x0 + 1}:${y0 + 1}`);
  return lerp(lerp(a, b, smooth(tx)), lerp(c, d, smooth(tx)), smooth(ty));
}

function hash01(text: string): number {
  let hash = 2166136261;
  for (let i = 0; i < text.length; i += 1) {
    hash ^= text.charCodeAt(i);
    hash = Math.imul(hash, 16777619);
  }
  return (hash >>> 0) / 4294967295;
}

function lerp(a: number, b: number, t: number): number {
  return a + (b - a) * t;
}

function smooth(value: number): number {
  return value * value * (3 - 2 * value);
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}

function round2(value: number): number {
  return Math.round(value * 100) / 100;
}
