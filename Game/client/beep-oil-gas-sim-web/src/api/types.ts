export interface HealthResponse {
  status: string;
  service: string;
}

export interface GameMode {
  id: string;
  name: string;
  description: string;
  totalTurns: number;
  actionSlotsPerTurn: number;
  startingCash: number;
  maxDebt: number;
  modeType: string;
  uiComplexityLevel: string;
  enableHedging: boolean;
  enableAdvancedFinance: boolean;
}

export interface ModeProfileDto {
  uiComplexityLevel: string;
  aiAssistanceLevel: string;
  enableHedging: boolean;
  enableAdvancedFinance: boolean;
  startingCash: number;
  maxDebt: number;
}

export interface CompanyDto {
  id: string;
  name: string;
  colorHex: string;
  cash: number;
  debt: number;
  companyValue: number;
  rank: number;
  productionBoePerDay: number;
  turnCommitted?: boolean;
}

export interface LobbyPlayerDto {
  companyId: string;
  playerId: string;
  companyName: string;
  displayName: string;
  colorHex: string;
  isHost: boolean;
  isReady: boolean;
  turnCommitted?: boolean;
}

export interface ChatMessageDto {
  id: string;
  companyId?: string;
  senderName: string;
  channel: string;
  text: string;
  sentAtUtc: string;
}

export interface DiscoveryDto {
  id: string;
  blockId: string;
  companyId: string;
  name: string;
  sizeClass: string;
  estimatedMidVolumeMmboe: number;
  stage: string;
  confidence: number;
}

export interface ProducingFieldDto {
  id: string;
  blockId: string;
  companyId: string;
  name: string;
  stage: string;
  currentProductionBoePerDay: number;
  remainingRecoverableMmboe: number;
  productionPhase: string;
}

export interface PendingActionDto {
  id: string;
  companyId?: string;
  actionType: string;
  targetBlockId?: string;
  targetAssetId?: string;
  estimatedCost: number;
  status: string;
}

export interface GameSession {
  id: string;
  name: string;
  scenarioId: string;
  gameplayMode: string;
  state: string;
  currentTurnNumber: number;
  totalTurns: number;
  oilPrice: number;
  actionSlotsPerTurn: number;
  isMultiplayer?: boolean;
  joinCode?: string;
  maxPlayers?: number;
  minPlayers?: number;
  hostCompanyId?: string;
  modeProfile: ModeProfileDto;
  companies: CompanyDto[];
  lobbyPlayers: LobbyPlayerDto[];
  discoveries: DiscoveryDto[];
  producingFields: ProducingFieldDto[];
  pendingActions: PendingActionDto[];
  chatMessages?: ChatMessageDto[];
}

export interface BlockMapDto {
  id: string;
  blockCode: string;
  name: string;
  gridX: number;
  gridY: number;
  ownerCompanyId?: string;
  stage: string;
  publicGeologyHint: string;
  publicRiskRating: string;
  estimatedChanceOfSuccess?: number;
}

export interface MapResponse {
  blocks: BlockMapDto[];
}

export interface SubmitActionRequest {
  companyId: string;
  actionType: string;
  targetBlockId?: string;
  targetAssetId?: string;
  bidAmount?: number;
  parametersJson?: string;
}

export interface TurnActionResponse {
  id: string;
  actionType: string;
  targetBlockId?: string;
  targetAssetId?: string;
  estimatedCost: number;
  status: string;
}

export interface TurnEventDto {
  category: string;
  headline: string;
  detail: string;
  isPublic: boolean;
}

export interface CompanyTurnSummaryDto {
  companyId: string;
  endingCash: number;
  capex: number;
  companyValue: number;
  rank: number;
  revenue?: number;
}

export interface TurnResultResponse {
  turnNumber: number;
  events: TurnEventDto[];
  companySummaries: CompanyTurnSummaryDto[];
}

export interface LeaderboardEntryDto {
  companyId: string;
  name: string;
  rank: number;
  companyValue: number;
  cash: number;
  debt: number;
  productionBoePerDay: number;
}

export interface CreateGameSessionRequest {
  scenarioId: string;
  gameplayModeProfileId: string;
  companyName: string;
  playerDisplayName: string;
  isMultiplayer?: boolean;
}

export interface JoinGameSessionRequest {
  sessionId?: string;
  joinCode: string;
  companyName: string;
  playerDisplayName: string;
}

export interface JoinSessionResponse {
  sessionId: string;
  companyId: string;
  playerId: string;
  session: GameSession;
}

export interface SendChatRequest {
  companyId?: string;
  senderName: string;
  channel: string;
  text: string;
}

export interface HistoryPointDto {
  turnNumber: number;
  value: number;
}

export interface SessionHistoryResponse {
  oilPrice: HistoryPointDto[];
  productionBoePerDay: HistoryPointDto[];
  cash: HistoryPointDto[];
  companyValue: HistoryPointDto[];
}

export type SidebarView = 'map' | 'company' | 'assets' | 'finance' | 'leaderboard' | 'command';

export interface AiAdvisorResponse {
  advisorType: string;
  message: string;
  recommendationType: string;
  suggestedActions: string[];
  risks: string[];
}

export interface AiTurnReport {
  turnNumber: number;
  summary: string;
  highlights: string[];
  recommendations: string[];
}

export interface AiAskRequest {
  companyId: string;
  advisorType: string;
  message: string;
  selectedBlockId?: string;
  selectedAssetId?: string;
}

export interface QueuedAction {
  id: string;
  actionType: string;
  label: string;
  estimatedCost: number;
  targetBlockId?: string;
  targetAssetId?: string;
}
