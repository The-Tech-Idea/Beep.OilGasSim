import { useEffect, useMemo, useRef } from 'react';
import Phaser from 'phaser';
import type { BlockMapDto } from '../../api/types';
import {
  generateIsoWorld,
  type IsoFeature,
  type IsoTerrain,
  type IsoWorld,
  type IsoWorldTile,
  type IsoWorldType,
} from './isoWorldGenerator';

interface BasinMapCanvasProps {
  blocks: BlockMapDto[];
  playerCompanyId: string | null;
  selectedBlockId: string | null;
  companyColors: Map<string, string>;
  worldType: IsoWorldType;
  onSelectBlock: (blockId: string) => void;
}

const TILE_WIDTH = 132;
const TILE_HEIGHT = 66;
const TILE_SIDE_OFFSET = 14;
const ISO_ASSET_ROOT = '/assets/iso';

const TERRAIN_TEXTURES: Record<IsoTerrain, string> = {
  desert: 'terrain-desert',
  scrub: 'terrain-desert',
  grass: 'terrain-grass',
  rock: 'terrain-rock',
  mountain: 'terrain-rock',
  water: 'terrain-water',
  coast: 'terrain-coast',
  'salt-flat': 'terrain-desert',
};

const FEATURE_TEXTURES: Partial<Record<IsoFeature, string>> = {
  cactus: 'feature-cactus',
  palm: 'feature-palm',
  tree: 'feature-palm',
  rock: 'feature-rock',
};

const STAGE_COLORS: Record<string, number> = {
  Unlicensed: 0xb68b4d,
  Licensed: 0x60a5fa,
  Studied: 0x38bdf8,
  SeismicEvaluated: 0x818cf8,
  ExplorationDrilling: 0xa78bfa,
  DryHole: 0x8b8175,
  Discovery: 0xfacc15,
  Appraisal: 0xfbbf24,
  DevelopmentApproved: 0xfb923c,
  UnderConstruction: 0xf97316,
  Producing: 0x4ade80,
  LateLife: 0xeab308,
  Abandoned: 0x78716c,
};

export function BasinMapCanvas({
  blocks,
  playerCompanyId,
  selectedBlockId,
  companyColors,
  worldType,
  onSelectBlock,
}: BasinMapCanvasProps) {
  const containerRef = useRef<HTMLDivElement | null>(null);

  const worldSeed = useMemo(
    () =>
      blocks
        .map((block) => `${block.id}:${block.gridX}:${block.gridY}`)
        .sort()
        .join('|') || 'beep-oil-gas-empty-world',
    [blocks],
  );

  const world = useMemo(
    () => generateIsoWorld(blocks, { seed: worldSeed, worldType }),
    [blocks, worldSeed, worldType],
  );

  const colorLookup = useMemo(
    () => Object.fromEntries(companyColors.entries()),
    [companyColors],
  );

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const mapWindow = window as Window & {
      render_game_to_text?: () => string;
      advanceTime?: (ms: number) => string;
      __isoMapHookToken?: symbol;
    };
    const hookToken = Symbol('iso-map-hook');
    mapWindow.__isoMapHookToken = hookToken;
    mapWindow.render_game_to_text = () =>
      renderIsoWorldToText(world, selectedBlockId, null);
    mapWindow.advanceTime = () => renderIsoWorldToText(world, selectedBlockId, null);

    const scene = new IsoMapScene({
      world,
      selectedBlockId,
      playerCompanyId,
      companyColors: colorLookup,
      onSelectBlock,
    });
    const rect = container.getBoundingClientRect();
    const game = new Phaser.Game({
      type: Phaser.CANVAS,
      parent: container,
      width: Math.max(320, rect.width),
      height: Math.max(240, rect.height),
      backgroundColor: '#111827',
      scene,
      audio: {
        noAudio: true,
      },
      render: {
        antialias: true,
        pixelArt: false,
      },
      scale: {
        mode: Phaser.Scale.RESIZE,
        autoCenter: Phaser.Scale.NO_CENTER,
      },
    });
    mapWindow.render_game_to_text = () => scene.renderGameToText();
    mapWindow.advanceTime = (ms: number) => {
      scene.advance(ms);
      return scene.renderGameToText();
    };

    const resizeGame = (width = container.clientWidth, height = container.clientHeight) => {
      game.scale.resize(Math.max(320, width), Math.max(240, height));
      scene.centerCamera(false);
    };
    let resizeObserver: ResizeObserver | null = null;
    const resizeListener = () => resizeGame();
    if (typeof ResizeObserver !== 'undefined') {
      resizeObserver = new ResizeObserver((entries) => {
        const entry = entries[0];
        if (!entry) return;
        resizeGame(entry.contentRect.width, entry.contentRect.height);
      });
      resizeObserver.observe(container);
    } else {
      window.addEventListener('resize', resizeListener);
    }

    return () => {
      resizeObserver?.disconnect();
      window.removeEventListener('resize', resizeListener);
      if (mapWindow.__isoMapHookToken === hookToken) {
        delete mapWindow.render_game_to_text;
        delete mapWindow.advanceTime;
        delete mapWindow.__isoMapHookToken;
      }
      game.destroy(true);
    };
  }, [colorLookup, onSelectBlock, playerCompanyId, selectedBlockId, world]);

  return (
    <div className="map-viewport">
      <div
        ref={containerRef}
        className="basin-map-canvas"
        data-testid="iso-basin-map"
      />
    </div>
  );
}

interface IsoMapSceneOptions {
  world: IsoWorld;
  selectedBlockId: string | null;
  playerCompanyId: string | null;
  companyColors: Record<string, string>;
  onSelectBlock: (blockId: string) => void;
}

class IsoMapScene extends Phaser.Scene {
  private readonly world: IsoWorld;
  private readonly selectedBlockId: string | null;
  private readonly playerCompanyId: string | null;
  private readonly companyColors: Record<string, string>;
  private readonly onSelectBlock: (blockId: string) => void;
  private cursors?: Phaser.Types.Input.Keyboard.CursorKeys;
  private keys?: Record<'W' | 'A' | 'S' | 'D', Phaser.Input.Keyboard.Key>;
  private bounds:
    | {
        minX: number;
        maxX: number;
        minY: number;
        maxY: number;
      }
    | null = null;

  constructor(options: IsoMapSceneOptions) {
    super('iso-map');
    this.world = options.world;
    this.selectedBlockId = options.selectedBlockId;
    this.playerCompanyId = options.playerCompanyId;
    this.companyColors = options.companyColors;
    this.onSelectBlock = options.onSelectBlock;
  }

  preload() {
    this.load.image('terrain-desert', `${ISO_ASSET_ROOT}/terrain-desert.png`);
    this.load.image('terrain-grass', `${ISO_ASSET_ROOT}/terrain-grass.png`);
    this.load.image('terrain-water', `${ISO_ASSET_ROOT}/terrain-water.png`);
    this.load.image('terrain-coast', `${ISO_ASSET_ROOT}/terrain-coast.png`);
    this.load.image('terrain-rock', `${ISO_ASSET_ROOT}/terrain-rock.png`);
    this.load.image('facility-terminal', `${ISO_ASSET_ROOT}/facility-terminal.png`);
    this.load.image('facility-service', `${ISO_ASSET_ROOT}/facility-service.png`);
    this.load.image('facility-production', `${ISO_ASSET_ROOT}/facility-production.png`);
    this.load.image('facility-drilling-rig', `${ISO_ASSET_ROOT}/facility-drilling-rig.png`);
    this.load.image('vehicle-truck', `${ISO_ASSET_ROOT}/vehicle-truck.png`);
    this.load.image('feature-rock', `${ISO_ASSET_ROOT}/feature-rock.png`);
    this.load.image('feature-palm', `${ISO_ASSET_ROOT}/feature-palm.png`);
    this.load.image('feature-cactus', `${ISO_ASSET_ROOT}/feature-cactus.png`);
  }

  create() {
    this.cursors = this.input.keyboard?.createCursorKeys();
    this.keys = this.input.keyboard?.addKeys('W,A,S,D') as
      | Record<'W' | 'A' | 'S' | 'D', Phaser.Input.Keyboard.Key>
      | undefined;

    this.add
      .rectangle(0, 0, 6000, 4000, 0x1f2937, 1)
      .setOrigin(0.5)
      .setDepth(-2000);

    this.drawTerrain();
    this.drawRoutes();
    this.drawPointsOfInterest();
    this.drawCameraInteractions();
    this.centerCamera(true);
  }

  update(_time: number, delta: number) {
    if (!this.cameras?.main) return;
    const camera = this.cameras.main;
    const speed = (delta / 16.67) * 11 / camera.zoom;
    if (this.cursors?.left.isDown || this.keys?.A.isDown) camera.scrollX -= speed;
    if (this.cursors?.right.isDown || this.keys?.D.isDown) camera.scrollX += speed;
    if (this.cursors?.up.isDown || this.keys?.W.isDown) camera.scrollY -= speed;
    if (this.cursors?.down.isDown || this.keys?.S.isDown) camera.scrollY += speed;
  }

  centerCamera(animated: boolean) {
    if (!this.cameras?.main || !this.bounds) return;
    const camera = this.cameras.main;
    const boundsWidth = this.bounds.maxX - this.bounds.minX + TILE_WIDTH * 2;
    const boundsHeight = this.bounds.maxY - this.bounds.minY + TILE_HEIGHT * 3;
    const zoom = Math.min(
      1.05,
      Math.max(0.62, Math.min(camera.width / boundsWidth, camera.height / boundsHeight) * 0.92),
    );
    camera.setZoom(zoom);
    const centerX = (this.bounds.minX + this.bounds.maxX) / 2;
    const centerY = (this.bounds.minY + this.bounds.maxY) / 2;
    if (animated) {
      camera.pan(centerX, centerY, 150, 'Sine.easeOut');
    } else {
      camera.centerOn(centerX, centerY);
    }
  }

  advance(ms: number) {
    this.update(this.time?.now ?? 0, ms);
  }

  renderGameToText = () => {
    const selected = this.world.tiles.find((tile) => tile.block?.id === this.selectedBlockId);
    const terrainCounts = this.world.tiles.reduce<Record<string, number>>((acc, tile) => {
      acc[tile.terrain] = (acc[tile.terrain] ?? 0) + 1;
      return acc;
    }, {});
    const camera = this.cameras?.main;

    return JSON.stringify({
      coordinateSystem:
        'Isometric grid. Tile origin is northwest corner, x increases east, y increases south.',
      worldType: this.world.worldType,
      mapSize: { width: this.world.width, height: this.world.height },
      terrainCounts,
      selectedBlock: selected?.block
        ? {
            id: selected.block.id,
            code: selected.block.blockCode,
            stage: selected.block.stage,
            terrain: selected.terrain,
            x: selected.x,
            y: selected.y,
          }
        : null,
      camera: camera
        ? {
            scrollX: Math.round(camera.scrollX),
            scrollY: Math.round(camera.scrollY),
            zoom: Math.round(camera.zoom * 100) / 100,
          }
        : null,
      selectableBlocks: this.world.tiles.filter((tile) => tile.block).length,
      pointsOfInterest: this.world.pointsOfInterest,
    });
  };

  private drawTerrain() {
    const sortedTiles = [...this.world.tiles].sort((a, b) => a.x + a.y - (b.x + b.y));
    const screenPoints = sortedTiles.map((tile) => tileToScreen(tile.x, tile.y));
    this.bounds = {
      minX: Math.min(...screenPoints.map((point) => point.x)),
      maxX: Math.max(...screenPoints.map((point) => point.x)),
      minY: Math.min(...screenPoints.map((point) => point.y)),
      maxY: Math.max(...screenPoints.map((point) => point.y)),
    };

    for (const tile of sortedTiles) {
      const { x, y } = tileToScreen(tile.x, tile.y);
      const texture = TERRAIN_TEXTURES[tile.terrain];
      const baseDepth = y;
      const terrain = this.add.image(x, y, texture).setOrigin(0.5, 0.58).setDepth(baseDepth);
      terrain.setScale(TILE_WIDTH / terrain.width);
      const tint = terrainTint(tile);
      if (tint) terrain.setTint(tint);

      if (tile.block) {
        terrain.setInteractive({ cursor: 'pointer' });
        terrain.on('pointerup', (pointer: Phaser.Input.Pointer) => {
          if (pointer.getDistance() < 9 && tile.block) {
            this.onSelectBlock(tile.block.id);
          }
        });
        this.drawLeaseOverlay(tile, x, y);
        this.drawStageProp(tile, x, y);
        this.drawBlockLabel(tile, x, y);
      } else {
        this.drawFeature(tile, x, y);
      }
    }
  }

  private drawLeaseOverlay(tile: IsoWorldTile, x: number, y: number) {
    if (!tile.block) return;
    const stageColor = STAGE_COLORS[tile.block.stage] ?? STAGE_COLORS.Unlicensed;
    const ownerColor = tile.block.ownerCompanyId
      ? parseHexColor(this.companyColors[tile.block.ownerCompanyId])
      : null;
    const isMine = tile.block.ownerCompanyId === this.playerCompanyId;
    const selected = tile.block.id === this.selectedBlockId;
    const fill = ownerColor && isMine ? ownerColor : stageColor;
    const polygon = this.add
      .polygon(
        x,
        y - TILE_SIDE_OFFSET,
        [0, -TILE_HEIGHT / 2, TILE_WIDTH / 2, 0, 0, TILE_HEIGHT / 2, -TILE_WIDTH / 2, 0],
        fill,
        selected ? 0.42 : 0.24,
      )
      .setDepth(y + 3);

    polygon.setStrokeStyle(selected ? 4 : 2, selected ? 0xffffff : fill, selected ? 0.96 : 0.72);

    if (ownerColor) {
      this.add
        .rectangle(x - 34, y - 48, 26, 8, ownerColor, 0.95)
        .setStrokeStyle(1, 0x111827, 0.55)
        .setDepth(y + 42);
    }
  }

  private drawStageProp(tile: IsoWorldTile, x: number, y: number) {
    const block = tile.block;
    if (!block) return;
    const texture = facilityTextureForStage(block.stage);
    if (!texture) return;

    const sprite = this.add
      .image(x, y - 34, texture)
      .setOrigin(0.5, 0.82)
      .setDepth(y + 34);
    sprite.setScale(stagePropScale(texture));
  }

  private drawBlockLabel(tile: IsoWorldTile, x: number, y: number) {
    if (!tile.block) return;
    const selected = tile.block.id === this.selectedBlockId;
    this.add
      .text(x, y - 70, tile.block.blockCode, {
        fontFamily: 'Inter, Segoe UI, sans-serif',
        fontSize: selected ? '15px' : '13px',
        fontStyle: '700',
        color: '#ffffff',
        backgroundColor: selected ? 'rgba(14, 165, 233, 0.85)' : 'rgba(17, 24, 39, 0.68)',
        padding: { left: 6, right: 6, top: 2, bottom: 2 },
      })
      .setOrigin(0.5)
      .setDepth(y + 70);
  }

  private drawFeature(tile: IsoWorldTile, x: number, y: number) {
    const texture = FEATURE_TEXTURES[tile.feature];
    if (!texture) return;
    const sprite = this.add
      .image(x + 4, y - 30, texture)
      .setOrigin(0.5, 0.88)
      .setDepth(y + 28);
    sprite.setScale(featureScale(tile.feature));
    if (tile.feature === 'tree') sprite.setTint(0x8fcf73);
  }

  private drawRoutes() {
    const graphics = this.add.graphics().setDepth(3000);
    for (const route of this.world.routes) {
      graphics.lineStyle(route.kind === 'pipeline' ? 7 : 5, 0x243746, 0.75);
      route.points.forEach((point, index) => {
        const screen = tileToScreen(point.x, point.y);
        if (index === 0) graphics.beginPath().moveTo(screen.x, screen.y - 22);
        else graphics.lineTo(screen.x, screen.y - 22);
      });
      graphics.strokePath();
      graphics.lineStyle(route.kind === 'pipeline' ? 3 : 2, 0x93c5fd, 0.85);
      route.points.forEach((point, index) => {
        const screen = tileToScreen(point.x, point.y);
        if (index === 0) graphics.beginPath().moveTo(screen.x, screen.y - 22);
        else graphics.lineTo(screen.x, screen.y - 22);
      });
      graphics.strokePath();
    }
  }

  private drawPointsOfInterest() {
    for (const poi of this.world.pointsOfInterest) {
      const { x, y } = tileToScreen(poi.x, poi.y);
      const sprite = this.add
        .image(x, y - 38, 'facility-terminal')
        .setOrigin(0.5, 0.82)
        .setDepth(y + 52)
        .setScale(0.9);
      if (poi.kind === 'port') sprite.setTint(0xbfe7ff);

      this.add
        .text(x, y - 94, poi.label, {
          fontFamily: 'Inter, Segoe UI, sans-serif',
          fontSize: '13px',
          fontStyle: '700',
          color: '#eff6ff',
          backgroundColor: 'rgba(15, 23, 42, 0.72)',
          padding: { left: 7, right: 7, top: 3, bottom: 3 },
        })
        .setOrigin(0.5)
        .setDepth(y + 90);
    }
  }

  private drawCameraInteractions() {
    this.input.on('pointermove', (pointer: Phaser.Input.Pointer) => {
      if (!pointer.isDown || pointer.getDistance() < 2) return;
      const camera = this.cameras.main;
      camera.scrollX -= (pointer.x - pointer.prevPosition.x) / camera.zoom;
      camera.scrollY -= (pointer.y - pointer.prevPosition.y) / camera.zoom;
    });

    this.input.on(
      'wheel',
      (
        _pointer: Phaser.Input.Pointer,
        _objects: Phaser.GameObjects.GameObject[],
        _dx: number,
        dy: number,
      ) => {
        const camera = this.cameras.main;
        camera.setZoom(clamp(camera.zoom * (dy > 0 ? 0.9 : 1.1), 0.5, 1.65));
      },
    );
  }
}

function tileToScreen(tileX: number, tileY: number) {
  return {
    x: (tileX - tileY) * (TILE_WIDTH / 2),
    y: (tileX + tileY) * (TILE_HEIGHT / 2),
  };
}

function renderIsoWorldToText(
  world: IsoWorld,
  selectedBlockId: string | null,
  camera: { scrollX: number; scrollY: number; zoom: number } | null,
): string {
  const selected = world.tiles.find((tile) => tile.block?.id === selectedBlockId);
  const terrainCounts = world.tiles.reduce<Record<string, number>>((acc, tile) => {
    acc[tile.terrain] = (acc[tile.terrain] ?? 0) + 1;
    return acc;
  }, {});

  return JSON.stringify({
    coordinateSystem:
      'Isometric grid. Tile origin is northwest corner, x increases east, y increases south.',
    worldType: world.worldType,
    mapSize: { width: world.width, height: world.height },
    terrainCounts,
    selectedBlock: selected?.block
      ? {
          id: selected.block.id,
          code: selected.block.blockCode,
          stage: selected.block.stage,
          terrain: selected.terrain,
          x: selected.x,
          y: selected.y,
        }
      : null,
    camera,
    selectableBlocks: world.tiles.filter((tile) => tile.block).length,
    pointsOfInterest: world.pointsOfInterest,
  });
}

function terrainTint(tile: IsoWorldTile): number | null {
  if (tile.terrain === 'scrub') return 0xc6b66b;
  if (tile.terrain === 'salt-flat') return 0xf7f3dd;
  if (tile.terrain === 'mountain') return 0x9a8974;
  if (tile.terrain === 'rock') return 0xb49778;
  if (tile.terrain === 'coast') return 0xd4b178;
  if (tile.terrain === 'water') return 0x7cc7dd;
  return null;
}

function facilityTextureForStage(stage: string): string | null {
  if (stage === 'Producing' || stage === 'LateLife') return 'facility-production';
  if (stage === 'ExplorationDrilling') return 'facility-drilling-rig';
  if (stage === 'Discovery' || stage === 'Appraisal') return 'facility-service';
  if (stage === 'DevelopmentApproved' || stage === 'UnderConstruction') {
    return 'facility-terminal';
  }
  if (stage === 'Licensed' || stage === 'Studied' || stage === 'SeismicEvaluated') {
    return 'vehicle-truck';
  }
  return null;
}

function stagePropScale(texture: string): number {
  if (texture === 'vehicle-truck') return 1.4;
  if (texture === 'facility-drilling-rig') return 0.86;
  return 0.78;
}

function featureScale(feature: IsoFeature): number {
  if (feature === 'cactus') return 0.34;
  if (feature === 'palm' || feature === 'tree') return 0.3;
  if (feature === 'rock') return 0.28;
  return 1;
}

function parseHexColor(value: string | undefined): number | null {
  if (!value) return null;
  const parsed = Number.parseInt(value.replace('#', ''), 16);
  return Number.isFinite(parsed) ? parsed : null;
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}
