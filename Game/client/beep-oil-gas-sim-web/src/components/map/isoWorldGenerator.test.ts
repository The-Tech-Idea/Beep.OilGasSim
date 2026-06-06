import { describe, expect, test } from 'vitest';
import type { BlockMapDto } from '../../api/types';
import { generateIsoWorld } from './isoWorldGenerator';

const blocks: BlockMapDto[] = Array.from({ length: 20 }, (_, index) => ({
  id: `block-${index + 1}`,
  blockCode: `D-${String(index + 1).padStart(2, '0')}`,
  name: `Block D-${String(index + 1).padStart(2, '0')}`,
  gridX: index % 5,
  gridY: Math.floor(index / 5),
  ownerCompanyId: index === 4 ? 'player-1' : undefined,
  stage: index === 4 ? 'Producing' : index === 8 ? 'ExplorationDrilling' : 'Unlicensed',
  publicGeologyHint: 'Regional structural trend with moderate source potential.',
  publicRiskRating: index % 3 === 0 ? 'High' : 'Medium',
  estimatedChanceOfSuccess: index === 4 ? 0.41 : undefined,
}));

describe('generateIsoWorld', () => {
  test('preserves every license block as a selectable lease tile', () => {
    const world = generateIsoWorld(blocks, {
      seed: 'same-seed',
      worldType: 'desert-frontier',
    });

    const leaseTiles = world.tiles.filter((tile) => tile.block);

    expect(leaseTiles).toHaveLength(blocks.length);
    expect(leaseTiles.map((tile) => tile.block?.id).sort()).toEqual(
      blocks.map((block) => block.id).sort(),
    );
    expect(world.pointsOfInterest.some((poi) => poi.kind === 'export-terminal')).toBe(true);
  });

  test('is deterministic for a seed and world type', () => {
    const first = generateIsoWorld(blocks, {
      seed: 'stable-frontier',
      worldType: 'coastal-delta',
    });
    const second = generateIsoWorld(blocks, {
      seed: 'stable-frontier',
      worldType: 'coastal-delta',
    });

    expect(second).toEqual(first);
  });

  test('creates varied Civilization-style terrain instead of a flat block grid', () => {
    const world = generateIsoWorld(blocks, {
      seed: 'variety',
      worldType: 'desert-frontier',
    });

    const terrainKinds = new Set(world.tiles.map((tile) => tile.terrain));

    expect(world.width).toBeGreaterThan(5);
    expect(world.height).toBeGreaterThan(4);
    expect(terrainKinds.size).toBeGreaterThanOrEqual(5);
    expect(world.tiles.some((tile) => tile.feature !== 'none')).toBe(true);
  });

  test('uses world type to change terrain distribution', () => {
    const offshore = generateIsoWorld(blocks, {
      seed: 'distribution',
      worldType: 'offshore-shelf',
    });
    const mountain = generateIsoWorld(blocks, {
      seed: 'distribution',
      worldType: 'fold-belt',
    });

    const offshoreWater = offshore.tiles.filter((tile) => tile.terrain === 'water').length;
    const mountainRock = mountain.tiles.filter(
      (tile) => tile.terrain === 'mountain' || tile.terrain === 'rock',
    ).length;

    expect(offshoreWater).toBeGreaterThan(mountain.tiles.filter((tile) => tile.terrain === 'water').length);
    expect(mountainRock).toBeGreaterThan(
      offshore.tiles.filter((tile) => tile.terrain === 'mountain' || tile.terrain === 'rock').length,
    );
  });
});
