using System;

namespace Backend.Application.BackgroundJobs.IntakeBottleneck;

public interface IIntakeBottleneckCalculator
{
    IntakeBottleneckCalculationResult Calculate(IntakeBottleneckCalculationInput input);
}
