namespace Beep.OilGasSim.Domain.Common;

public enum AssetStage
{
    Unlicensed,
    Licensed,
    Studied,
    SeismicEvaluated,
    ExplorationDrilling,
    DryHole,
    Discovery,
    Appraisal,
    CommercialDiscovery,
    DevelopmentPlanning,
    DevelopmentApproved,
    UnderConstruction,
    Producing,
    LateLife,
    Decommissioning,
    Abandoned,
    Sold
}

public enum FluidType
{
    Unknown,
    Dry,
    Oil,
    Gas,
    Condensate,
    OilAndGas
}

public enum GameSessionState
{
    Lobby,
    Preparing,
    Planning,
    Committing,
    Resolving,
    Results,
    Completed,
    Cancelled
}

public enum TurnActionType
{
    BidForLicense,
    RelinquishLicense,
    GeologicalStudy,
    Acquire2DSeismic,
    Acquire3DSeismic,
    DrillExplorationWell,
    DrillAppraisalWell,
    ApproveDevelopment,
    OptimizeField,
    HedgeProduction,
    TakeDebt,
    RepayDebt,
    SellAsset,
    AbandonField
}

public enum TurnActionStatus
{
    Pending,
    Committed,
    Resolved,
    Failed,
    Cancelled
}

public enum GameplayModeType
{
    Fun,
    Balanced,
    Realistic,
    MissionChallenge,
    Training,
    Sandbox
}

public enum AiAssistanceLevel
{
    Off,
    BasicHints,
    Guided,
    FullAdvisor,
    TrainingCoach
}

public enum UiComplexityLevel
{
    Simple,
    Standard,
    Advanced,
    Expert
}

public enum KnowledgeLevel
{
    None,
    PublicHint,
    GeologicalStudy,
    TwoDSeismic,
    ThreeDSeismic,
    ExplorationWell,
    AppraisalWell,
    ProductionHistory
}

public enum PublicRiskRating
{
    Unknown,
    Low,
    Moderate,
    High,
    VeryHigh
}

public enum MarketTrend
{
    Stable,
    Bullish,
    Bearish,
    Volatile,
    Crash,
    Boom
}

public enum ProductionPhase
{
    RampUp,
    Plateau,
    Decline,
    LateLife,
    ShutIn,
    Abandoned
}

public enum DevelopmentConceptType
{
    Small,
    Standard,
    Large
}

public enum DiscoverySizeClass
{
    NonCommercial,
    Commercial,
    Major
}
