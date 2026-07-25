using System;

namespace Backend.Application.BackgroundJobs.IntakeBottleneck;

public class IntakeBottleneckCalculator : IIntakeBottleneckCalculator
{
    public IntakeBottleneckCalculationResult Calculate(IntakeBottleneckCalculationInput input)
    {
        var result = new IntakeBottleneckCalculationResult
        {
            ExpectedIntakeKg = input.ExpectedIntakeKg,
            FreeStorageCapacityKg = input.FreeStorageCapacityKg,
            IntakeLabourCapacityKg = input.IntakeLabourCapacityKg,
            WarningRatio = input.WarningRatio,
            CriticalRatio = input.CriticalRatio,
            WindowStart = input.WindowStart,
            WindowEnd = input.WindowEnd,
            EffectiveCapacityKg = Math.Min(input.FreeStorageCapacityKg, input.IntakeLabourCapacityKg)
        };

        // 1. Validation cấu hình
        if (input.WarningRatio <= 0)
        {
            result.HasConfigError = true;
            result.ConfigErrorDetail = "Warning threshold must be greater than 0.";
            result.Classification = IntakeBottleneckConstants.Classification.Normal;
            result.LimitingResource = IntakeBottleneckConstants.LimitingResource.None;
            return result;
        }

        if (input.CriticalRatio <= input.WarningRatio)
        {
            result.HasConfigError = true;
            result.ConfigErrorDetail = "Critical threshold must be greater than Warning threshold.";
            result.Classification = IntakeBottleneckConstants.Classification.Normal;
            result.LimitingResource = IntakeBottleneckConstants.LimitingResource.None;
            return result;
        }

        if (input.IntakeLabourCapacityKg <= 0)
        {
            result.HasConfigError = true;
            result.ConfigErrorDetail = "Labour capacity must be greater than 0.";
            result.Classification = IntakeBottleneckConstants.Classification.Normal;
            result.LimitingResource = IntakeBottleneckConstants.LimitingResource.None;
            return result;
        }

        // 2. ExpectedIntake = 0 trả Normal
        if (input.ExpectedIntakeKg <= 0)
        {
            result.Classification = IntakeBottleneckConstants.Classification.Normal;
            result.LimitingResource = IntakeBottleneckConstants.LimitingResource.None;
            result.StorageLoadRatio = 0;
            result.LabourLoadRatio = 0;
            result.BottleneckRatio = 0;
            return result;
        }

        // 3. Tính ratios
        if (input.FreeStorageCapacityKg <= 0)
        {
            result.StorageLoadRatio = decimal.MaxValue;
        }
        else
        {
            result.StorageLoadRatio = input.ExpectedIntakeKg / input.FreeStorageCapacityKg;
        }

        result.LabourLoadRatio = input.ExpectedIntakeKg / input.IntakeLabourCapacityKg;
        result.BottleneckRatio = Math.Max(result.StorageLoadRatio, result.LabourLoadRatio);

        // 4. Limiting Resource
        if (result.StorageLoadRatio > result.LabourLoadRatio)
        {
            result.LimitingResource = IntakeBottleneckConstants.LimitingResource.Storage;
        }
        else if (result.LabourLoadRatio > result.StorageLoadRatio)
        {
            result.LimitingResource = IntakeBottleneckConstants.LimitingResource.Labour;
        }
        else
        {
            result.LimitingResource = IntakeBottleneckConstants.LimitingResource.Both;
        }

        // Nếu cả hai đều vượt critical
        if (result.StorageLoadRatio >= input.CriticalRatio && result.LabourLoadRatio >= input.CriticalRatio)
        {
            result.LimitingResource = IntakeBottleneckConstants.LimitingResource.Both;
        }

        // 5. Phân loại
        if (result.BottleneckRatio >= input.CriticalRatio)
        {
            result.Classification = IntakeBottleneckConstants.Classification.Critical;
        }
        else if (result.BottleneckRatio >= input.WarningRatio)
        {
            result.Classification = IntakeBottleneckConstants.Classification.Warning;
        }
        else
        {
            result.Classification = IntakeBottleneckConstants.Classification.Normal;
        }

        return result;
    }
}
