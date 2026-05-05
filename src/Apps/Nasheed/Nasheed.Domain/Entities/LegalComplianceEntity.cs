namespace Nasheed.Domain.Entities;

public class LegalComplianceEntity
{
    public const string RiskLevelLow = "low";
    public const string RiskLevelMedium = "medium";
    public const string RiskLevelHigh = "high";

    public const string SafetyFlagSafe = "safe";
    public const string SafetyFlagFlagged = "flagged";

    public string CopyrightRiskLevel { get; private set; } = RiskLevelLow;
    public string ContentSafetyFlag { get; private set; } = SafetyFlagSafe;
    public string? RiskReason { get; private set; }

    private LegalComplianceEntity() { }

    public static bool IsValidRiskLevel(string? value)
    {
        return value == RiskLevelLow || value == RiskLevelMedium || value == RiskLevelHigh;
    }

    public static bool IsValidSafetyFlag(string? value)
    {
        return value == SafetyFlagSafe || value == SafetyFlagFlagged;
    }

    public static LegalComplianceEntity Create(string copyrightRiskLevel, string contentSafetyFlag, string? riskReason)
    {
        return new LegalComplianceEntity
        {
            CopyrightRiskLevel = copyrightRiskLevel,
            ContentSafetyFlag = contentSafetyFlag,
            RiskReason = riskReason
        };
    }

    public void Update(string copyrightRiskLevel, string contentSafetyFlag, string? riskReason)
    {
        CopyrightRiskLevel = copyrightRiskLevel;
        ContentSafetyFlag = contentSafetyFlag;
        RiskReason = riskReason;
    }
}