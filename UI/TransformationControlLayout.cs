using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace HyperSpace.UI;

/// <summary>
/// Computes control bounds for each panel mode without owning input or graphics resources.
/// </summary>
internal sealed class TransformationControlLayout
{
    private const int PreferredWidth = TransformationControlPanel.PreferredWidth;
    private const int Padding = 10;
    private const int InnerPadding = 8;
    private const int ColumnGap = 6;
    private const int ButtonHeight = 23;

    private readonly Dictionary<TransformationCommand, UiButton> _buttonByCommand;
    private readonly IntegerInputField _bodyCountInput;
    private readonly IntegerInputField _seedInput;

    public TransformationControlLayout(
        Dictionary<TransformationCommand, UiButton> buttonByCommand,
        IntegerInputField bodyCountInput,
        IntegerInputField seedInput)
    {
        _buttonByCommand = buttonByCommand;
        _bodyCountInput = bodyCountInput;
        _seedInput = seedInput;
    }

    public Rectangle Bounds { get; private set; }

    public void Apply(
        int viewportWidth,
        int viewportHeight,
        bool isSpiral,
        bool isFractal,
        bool showPhysicsPanel,
        bool showGravityLab,
        bool showNBodyLab)
    {
        var width = Math.Min(PreferredWidth, Math.Max(1, viewportWidth));
        Bounds = new Rectangle(viewportWidth - width, 0, width, viewportHeight);
        var contentLeft = Bounds.X + Padding + InnerPadding;
        var contentWidth = Math.Max(2, width - (2 * (Padding + InnerPadding)));
        var columnWidth = Math.Max(1, (contentWidth - ColumnGap) / 2);
        var right = contentLeft + columnWidth + ColumnGap;
        var modeButtonBounds = new Rectangle(Bounds.Right - 68, 66, 50, 17);
        SetBounds(TransformationCommand.OpenPhysicsPanel, modeButtonBounds);
        SetBounds(TransformationCommand.ClosePhysicsPanel,
            new Rectangle(Bounds.Right - 68, 66, 50, 17));
        SetBounds(TransformationCommand.OpenGravityLabView,
            new Rectangle(Bounds.Right - 207, 66, 63, 17));
        SetBounds(TransformationCommand.OpenParticlePhysicsView,
            new Rectangle(Bounds.Right - 276, 66, 63, 17));
        SetBounds(TransformationCommand.OpenNBodyLabView,
            new Rectangle(Bounds.Right - 138, 66, 64, 17));

        if (showPhysicsPanel)
        {
            if (showNBodyLab)
            {
                LayoutNBodyLabControls(contentLeft, right, columnWidth, contentWidth);
            }
            else if (showGravityLab)
            {
                LayoutGravityLabControls(contentLeft, right, columnWidth, contentWidth);
            }
            else
            {
                LayoutPhysicsControls(contentLeft, right, columnWidth, contentWidth);
            }
            return;
        }

        SetTwoColumnRow(TransformationCommand.SelectTesseract, TransformationCommand.SelectHypersphere,
            contentLeft, right, columnWidth, 87);
        SetTwoColumnRow(TransformationCommand.SelectSimplex, TransformationCommand.SelectIrregular,
            contentLeft, right, columnWidth, 116);
        SetTwoColumnRow(TransformationCommand.SelectSpiral, TransformationCommand.SelectFractal,
            contentLeft, right, columnWidth, 145);

        if (isFractal)
        {
            LayoutFractalControls(contentLeft, right, columnWidth, contentWidth);
        }
        else if (isSpiral)
        {
            var adjustmentLeft = contentLeft + 145;
            const int adjustmentWidth = 62;
            var adjustmentRight = adjustmentLeft + adjustmentWidth + ColumnGap;
            SetTwoColumnRow(TransformationCommand.DecreaseSpiralR1, TransformationCommand.IncreaseSpiralR1,
                adjustmentLeft, adjustmentRight, adjustmentWidth, 238);
            SetTwoColumnRow(TransformationCommand.DecreaseSpiralR2, TransformationCommand.IncreaseSpiralR2,
                adjustmentLeft, adjustmentRight, adjustmentWidth, 267);
            SetTwoColumnRow(TransformationCommand.DecreaseSpiralK, TransformationCommand.IncreaseSpiralK,
                adjustmentLeft, adjustmentRight, adjustmentWidth, 296);
            SetTwoColumnRow(TransformationCommand.DecreaseSpiralSamples, TransformationCommand.IncreaseSpiralSamples,
                adjustmentLeft, adjustmentRight, adjustmentWidth, 325);
            SetBounds(TransformationCommand.RegenerateSpiral,
                new Rectangle(contentLeft, 354, contentWidth, ButtonHeight));
            SetTwoColumnRow(TransformationCommand.PlayCurve, TransformationCommand.ResetCurve,
                contentLeft, right, columnWidth, 383);

            LayoutCommonControls(contentLeft, right, columnWidth, contentWidth,
                rotationY: 462, transformY: 579, systemY: 750);
            SetTwoColumnRow(TransformationCommand.ToggleGrid, TransformationCommand.ToggleAxes,
                contentLeft, right, columnWidth, 814);
            SetTwoColumnRow(TransformationCommand.ToggleCurve, TransformationCommand.ToggleCurvePoints,
                contentLeft, right, columnWidth, 843);
            SetBounds(TransformationCommand.ToggleCurveDirection,
                new Rectangle(contentLeft, 872, contentWidth, ButtonHeight));
        }
        else
        {
            LayoutCommonControls(contentLeft, right, columnWidth, contentWidth,
                rotationY: 238, transformY: 355, systemY: 526);
            SetTwoColumnRow(TransformationCommand.ToggleGrid, TransformationCommand.ToggleAxes,
                contentLeft, right, columnWidth, 590);
            SetTwoColumnRow(TransformationCommand.ToggleCells, TransformationCommand.ToggleEdges,
                contentLeft, right, columnWidth, 619);
            SetBounds(TransformationCommand.ToggleVertices,
                new Rectangle(contentLeft, 648, contentWidth, ButtonHeight));
        }
    }

    private void LayoutCommonControls(
        int left,
        int right,
        int columnWidth,
        int contentWidth,
        int rotationY,
        int transformY,
        int systemY)
    {
        SetTwoColumnRow(TransformationCommand.RotateXY, TransformationCommand.RotateXZ,
            left, right, columnWidth, rotationY);
        SetTwoColumnRow(TransformationCommand.RotateXW, TransformationCommand.RotateYZ,
            left, right, columnWidth, rotationY + 29);
        SetTwoColumnRow(TransformationCommand.RotateYW, TransformationCommand.RotateZW,
            left, right, columnWidth, rotationY + 58);
        SetTwoColumnRow(TransformationCommand.ScaleUp, TransformationCommand.ScaleDown,
            left, right, columnWidth, transformY);
        SetTwoColumnRow(TransformationCommand.MovePositiveX, TransformationCommand.MoveNegativeX,
            left, right, columnWidth, transformY + 29);
        SetTwoColumnRow(TransformationCommand.MovePositiveY, TransformationCommand.MoveNegativeY,
            left, right, columnWidth, transformY + 58);
        SetTwoColumnRow(TransformationCommand.MovePositiveZ, TransformationCommand.MoveNegativeZ,
            left, right, columnWidth, transformY + 87);
        SetTwoColumnRow(TransformationCommand.MovePositiveW, TransformationCommand.MoveNegativeW,
            left, right, columnWidth, transformY + 116);
        SetTwoColumnRow(TransformationCommand.ResetObject, TransformationCommand.ResetCamera,
            left, right, columnWidth, systemY);
    }

    private void LayoutFractalControls(
        int left,
        int right,
        int columnWidth,
        int contentWidth)
    {
        var adjustmentLeft = left + 180;
        const int adjustmentWidth = 50;
        var adjustmentRight = adjustmentLeft + adjustmentWidth + ColumnGap;
        SetTwoColumnRow(TransformationCommand.DecreaseJuliaA, TransformationCommand.IncreaseJuliaA,
            adjustmentLeft, adjustmentRight, adjustmentWidth, 238);
        SetTwoColumnRow(TransformationCommand.DecreaseJuliaB, TransformationCommand.IncreaseJuliaB,
            adjustmentLeft, adjustmentRight, adjustmentWidth, 263);
        SetTwoColumnRow(TransformationCommand.DecreaseJuliaC, TransformationCommand.IncreaseJuliaC,
            adjustmentLeft, adjustmentRight, adjustmentWidth, 288);
        SetTwoColumnRow(TransformationCommand.DecreaseJuliaD, TransformationCommand.IncreaseJuliaD,
            adjustmentLeft, adjustmentRight, adjustmentWidth, 313);
        SetTwoColumnRow(TransformationCommand.DecreaseJuliaIterations, TransformationCommand.IncreaseJuliaIterations,
            adjustmentLeft, adjustmentRight, adjustmentWidth, 338);
        SetTwoColumnRow(
            TransformationCommand.DecreaseJuliaEscapeRadius,
            TransformationCommand.IncreaseJuliaEscapeRadius,
            adjustmentLeft,
            adjustmentRight,
            adjustmentWidth,
            363);
        SetTwoColumnRow(
            TransformationCommand.DecreaseJuliaResolution,
            TransformationCommand.IncreaseJuliaResolution,
            adjustmentLeft,
            adjustmentRight,
            adjustmentWidth,
            388);
        SetThreeColumnRow(
            TransformationCommand.SelectJuliaPreset1,
            TransformationCommand.SelectJuliaPreset2,
            TransformationCommand.SelectJuliaPreset3,
            left,
            contentWidth,
            413);
        SetThreeColumnRow(
            TransformationCommand.GenerateFractal,
            TransformationCommand.CancelFractalGeneration,
            TransformationCommand.ResetFractal,
            left,
            contentWidth,
            442);

        SetTwoColumnRow(
            TransformationCommand.ColorFractalByW,
            TransformationCommand.ColorFractalByIterations,
            left,
            right,
            columnWidth,
            514);
        SetTwoColumnRow(
            TransformationCommand.ToggleFractalWSlice,
            TransformationCommand.ToggleVertices,
            left,
            right,
            columnWidth,
            543);
        SetTwoColumnRow(
            TransformationCommand.ToggleGrid,
            TransformationCommand.ToggleAxes,
            left,
            right,
            columnWidth,
            572);
        SetThreeColumnRow(
            TransformationCommand.DecreaseFractalSliceW,
            TransformationCommand.IncreaseFractalSliceW,
            TransformationCommand.CycleFractalPointSize,
            left,
            contentWidth,
            601);

        SetThreeColumnRow(
            TransformationCommand.RotateXY,
            TransformationCommand.RotateXZ,
            TransformationCommand.RotateXW,
            left,
            contentWidth,
            660);
        SetThreeColumnRow(
            TransformationCommand.RotateYZ,
            TransformationCommand.RotateYW,
            TransformationCommand.RotateZW,
            left,
            contentWidth,
            689);

        SetTwoColumnRow(
            TransformationCommand.ScaleUp,
            TransformationCommand.ScaleDown,
            left,
            right,
            columnWidth,
            748);
        SetFourColumnRow(
            TransformationCommand.MovePositiveX,
            TransformationCommand.MoveNegativeX,
            TransformationCommand.MovePositiveY,
            TransformationCommand.MoveNegativeY,
            left,
            contentWidth,
            777);
        SetFourColumnRow(
            TransformationCommand.MovePositiveZ,
            TransformationCommand.MoveNegativeZ,
            TransformationCommand.MovePositiveW,
            TransformationCommand.MoveNegativeW,
            left,
            contentWidth,
            806);
        SetTwoColumnRow(
            TransformationCommand.ResetObject,
            TransformationCommand.ResetCamera,
            left,
            right,
            columnWidth,
            860);
    }

    private void LayoutPhysicsControls(
        int left,
        int right,
        int columnWidth,
        int contentWidth)
    {
        SetThreeColumnRow(
            TransformationCommand.TogglePhysicsEnabled,
            TransformationCommand.PlayPhysics,
            TransformationCommand.PausePhysics,
            left,
            contentWidth,
            120);
        SetThreeColumnRow(
            TransformationCommand.DecreaseTimeScale,
            TransformationCommand.IncreaseTimeScale,
            TransformationCommand.StepPhysics,
            left,
            contentWidth,
            149);

        var adjustmentLeft = left + 180;
        const int adjustmentWidth = 50;
        var adjustmentRight = adjustmentLeft + adjustmentWidth + ColumnGap;
        SetTwoColumnRow(TransformationCommand.DecreaseGravityX, TransformationCommand.IncreaseGravityX,
            adjustmentLeft, adjustmentRight, adjustmentWidth, 238);
        SetTwoColumnRow(TransformationCommand.DecreaseGravityY, TransformationCommand.IncreaseGravityY,
            adjustmentLeft, adjustmentRight, adjustmentWidth, 267);
        SetTwoColumnRow(TransformationCommand.DecreaseGravityZ, TransformationCommand.IncreaseGravityZ,
            adjustmentLeft, adjustmentRight, adjustmentWidth, 296);
        SetTwoColumnRow(TransformationCommand.DecreaseGravityW, TransformationCommand.IncreaseGravityW,
            adjustmentLeft, adjustmentRight, adjustmentWidth, 325);
        SetTwoColumnRow(TransformationCommand.SetZeroGravity, TransformationCommand.SetYGravity,
            left, right, columnWidth, 354);
        SetTwoColumnRow(TransformationCommand.SetWGravity, TransformationCommand.SetYWGravity,
            left, right, columnWidth, 383);

        SetTwoColumnRow(
            TransformationCommand.DecreaseInitialVelocityX,
            TransformationCommand.IncreaseInitialVelocityX,
            adjustmentLeft,
            adjustmentRight,
            adjustmentWidth,
            447);
        SetTwoColumnRow(
            TransformationCommand.DecreaseInitialVelocityY,
            TransformationCommand.IncreaseInitialVelocityY,
            adjustmentLeft,
            adjustmentRight,
            adjustmentWidth,
            476);
        SetTwoColumnRow(
            TransformationCommand.DecreaseInitialVelocityZ,
            TransformationCommand.IncreaseInitialVelocityZ,
            adjustmentLeft,
            adjustmentRight,
            adjustmentWidth,
            505);
        SetTwoColumnRow(
            TransformationCommand.DecreaseInitialVelocityW,
            TransformationCommand.IncreaseInitialVelocityW,
            adjustmentLeft,
            adjustmentRight,
            adjustmentWidth,
            534);

        SetThreeColumnRow(
            TransformationCommand.SpawnParticle,
            TransformationCommand.SpawnTenParticles,
            TransformationCommand.ClearParticles,
            left,
            contentWidth,
            598);
        SetTwoColumnRow(
            TransformationCommand.DecreaseRestitution,
            TransformationCommand.IncreaseRestitution,
            adjustmentLeft,
            adjustmentRight,
            adjustmentWidth,
            686);
        SetTwoColumnRow(
            TransformationCommand.TogglePhysicsCollisions,
            TransformationCommand.TogglePhysicsPlane,
            left,
            right,
            columnWidth,
            715);
    }

    private void LayoutGravityLabControls(
        int left,
        int right,
        int columnWidth,
        int contentWidth)
    {
        SetThreeColumnRow(
            TransformationCommand.TogglePhysicsEnabled,
            TransformationCommand.PlayPhysics,
            TransformationCommand.PausePhysics,
            left,
            contentWidth,
            120);
        SetThreeColumnRow(
            TransformationCommand.DecreaseTimeScale,
            TransformationCommand.IncreaseTimeScale,
            TransformationCommand.StepPhysics,
            left,
            contentWidth,
            149);

        var adjustmentLeft = left + 180;
        const int adjustmentWidth = 50;
        var adjustmentRight = adjustmentLeft + adjustmentWidth + ColumnGap;
        SetTwoColumnRow(
            TransformationCommand.DecreaseGravitationalConstant,
            TransformationCommand.IncreaseGravitationalConstant,
            adjustmentLeft,
            adjustmentRight,
            adjustmentWidth,
            209);
        SetTwoColumnRow(
            TransformationCommand.DecreaseGravitySoftening,
            TransformationCommand.IncreaseGravitySoftening,
            adjustmentLeft,
            adjustmentRight,
            adjustmentWidth,
            238);
        SetBounds(TransformationCommand.ToggleMutualGravity,
            new Rectangle(left, 267, contentWidth, ButtonHeight));

        SetTwoColumnRow(
            TransformationCommand.DecreaseCentralMass,
            TransformationCommand.IncreaseCentralMass,
            adjustmentLeft,
            adjustmentRight,
            adjustmentWidth,
            325);
        SetTwoColumnRow(
            TransformationCommand.DecreaseOrbiterPositionX,
            TransformationCommand.IncreaseOrbiterPositionX,
            adjustmentLeft,
            adjustmentRight,
            adjustmentWidth,
            383);
        SetTwoColumnRow(
            TransformationCommand.DecreaseOrbiterPositionY,
            TransformationCommand.IncreaseOrbiterPositionY,
            adjustmentLeft,
            adjustmentRight,
            adjustmentWidth,
            412);
        SetTwoColumnRow(
            TransformationCommand.DecreaseOrbiterPositionZ,
            TransformationCommand.IncreaseOrbiterPositionZ,
            adjustmentLeft,
            adjustmentRight,
            adjustmentWidth,
            441);
        SetTwoColumnRow(
            TransformationCommand.DecreaseOrbiterPositionW,
            TransformationCommand.IncreaseOrbiterPositionW,
            adjustmentLeft,
            adjustmentRight,
            adjustmentWidth,
            470);

        SetTwoColumnRow(
            TransformationCommand.DecreaseOrbiterVelocityX,
            TransformationCommand.IncreaseOrbiterVelocityX,
            adjustmentLeft,
            adjustmentRight,
            adjustmentWidth,
            539);
        SetTwoColumnRow(
            TransformationCommand.DecreaseOrbiterVelocityY,
            TransformationCommand.IncreaseOrbiterVelocityY,
            adjustmentLeft,
            adjustmentRight,
            adjustmentWidth,
            568);
        SetTwoColumnRow(
            TransformationCommand.DecreaseOrbiterVelocityZ,
            TransformationCommand.IncreaseOrbiterVelocityZ,
            adjustmentLeft,
            adjustmentRight,
            adjustmentWidth,
            597);
        SetTwoColumnRow(
            TransformationCommand.DecreaseOrbiterVelocityW,
            TransformationCommand.IncreaseOrbiterVelocityW,
            adjustmentLeft,
            adjustmentRight,
            adjustmentWidth,
            626);
        SetThreeColumnRow(
            TransformationCommand.SelectLowOrbiterVelocity,
            TransformationCommand.SelectMediumOrbiterVelocity,
            TransformationCommand.SelectHighOrbiterVelocity,
            left,
            contentWidth,
            655);
        SetTwoColumnRow(
            TransformationCommand.SetOrbiterXYVelocity,
            TransformationCommand.SetOrbiterXYWVelocity,
            left,
            right,
            columnWidth,
            684);

        SetTwoColumnRow(
            TransformationCommand.ToggleGravityTrail,
            TransformationCommand.ToggleGravityField,
            left,
            right,
            columnWidth,
            742);
        SetTwoColumnRow(
            TransformationCommand.ClearGravityTrail,
            TransformationCommand.ResetGravityExperiment,
            left,
            right,
            columnWidth,
            771);
        SetTwoColumnRow(
            TransformationCommand.DecreaseGravityTrailLength,
            TransformationCommand.IncreaseGravityTrailLength,
            adjustmentLeft,
            adjustmentRight,
            adjustmentWidth,
            800);
    }

    private void LayoutNBodyLabControls(
        int left,
        int right,
        int columnWidth,
        int contentWidth)
    {
        SetThreeColumnRow(
            TransformationCommand.PlayPhysics,
            TransformationCommand.PausePhysics,
            TransformationCommand.StepPhysics,
            left,
            contentWidth,
            120);
        SetTwoColumnRow(
            TransformationCommand.DecreaseTimeScale,
            TransformationCommand.IncreaseTimeScale,
            left,
            right,
            columnWidth,
            149);

        _bodyCountInput.SetBounds(new Rectangle(left + 72, 209, 108, ButtonHeight));
        SetBounds(TransformationCommand.ApplyNBodyCount,
            new Rectangle(left + 186, 209, contentWidth - 186, ButtonHeight));
        _seedInput.SetBounds(new Rectangle(left + 48, 238, 94, ButtonHeight));
        SetBounds(TransformationCommand.ApplyNBodySeed,
            new Rectangle(left + 148, 238, 68, ButtonHeight));
        SetBounds(TransformationCommand.RandomizeNBodySeed,
            new Rectangle(left + 222, 238, contentWidth - 222, ButtonHeight));
        SetTwoColumnRow(
            TransformationCommand.GenerateNBodySystem,
            TransformationCommand.ResetNBodySystem,
            left,
            right,
            columnWidth,
            267);

        var adjustmentLeft = left + 180;
        const int adjustmentWidth = 50;
        var adjustmentRight = adjustmentLeft + adjustmentWidth + ColumnGap;
        SetTwoColumnRow(TransformationCommand.DecreaseNBodyRangeX, TransformationCommand.IncreaseNBodyRangeX,
            adjustmentLeft, adjustmentRight, adjustmentWidth, 330);
        SetTwoColumnRow(TransformationCommand.DecreaseNBodyRangeY, TransformationCommand.IncreaseNBodyRangeY,
            adjustmentLeft, adjustmentRight, adjustmentWidth, 359);
        SetTwoColumnRow(TransformationCommand.DecreaseNBodyRangeZ, TransformationCommand.IncreaseNBodyRangeZ,
            adjustmentLeft, adjustmentRight, adjustmentWidth, 388);
        SetTwoColumnRow(TransformationCommand.DecreaseNBodyRangeW, TransformationCommand.IncreaseNBodyRangeW,
            adjustmentLeft, adjustmentRight, adjustmentWidth, 417);

        SetTwoColumnRow(TransformationCommand.DecreaseNBodyMinimumSpeed, TransformationCommand.IncreaseNBodyMinimumSpeed,
            adjustmentLeft, adjustmentRight, adjustmentWidth, 478);
        SetTwoColumnRow(TransformationCommand.DecreaseNBodyMaximumSpeed, TransformationCommand.IncreaseNBodyMaximumSpeed,
            adjustmentLeft, adjustmentRight, adjustmentWidth, 507);
        SetTwoColumnRow(TransformationCommand.DecreaseNBodyMinimumMass, TransformationCommand.IncreaseNBodyMinimumMass,
            adjustmentLeft, adjustmentRight, adjustmentWidth, 536);
        SetTwoColumnRow(TransformationCommand.DecreaseNBodyMaximumMass, TransformationCommand.IncreaseNBodyMaximumMass,
            adjustmentLeft, adjustmentRight, adjustmentWidth, 565);
        SetBounds(TransformationCommand.DecreaseNBodyRadiusScale,
            new Rectangle(left + 101, 594, 28, ButtonHeight));
        SetBounds(TransformationCommand.IncreaseNBodyRadiusScale,
            new Rectangle(left + 135, 594, 28, ButtonHeight));
        SetBounds(TransformationCommand.DecreaseNBodyPointScale,
            new Rectangle(left + 242, 594, 28, ButtonHeight));
        SetBounds(TransformationCommand.IncreaseNBodyPointScale,
            new Rectangle(left + 276, 594, 28, ButtonHeight));

        SetTwoColumnRow(
            TransformationCommand.DecreaseGravitationalConstant,
            TransformationCommand.IncreaseGravitationalConstant,
            adjustmentLeft,
            adjustmentRight,
            adjustmentWidth,
            653);
        SetTwoColumnRow(
            TransformationCommand.DecreaseGravitySoftening,
            TransformationCommand.IncreaseGravitySoftening,
            adjustmentLeft,
            adjustmentRight,
            adjustmentWidth,
            682);
        SetTwoColumnRow(
            TransformationCommand.ToggleNBodyGravity,
            TransformationCommand.ToggleNBodyAggregation,
            left,
            right,
            columnWidth,
            711);

        SetTwoColumnRow(
            TransformationCommand.SelectNBodyExactGravity,
            TransformationCommand.SelectNBodyApproximateGravity,
            left,
            right,
            columnWidth,
            776);
        SetFourColumnRow(
            TransformationCommand.ColorNBodyByW,
            TransformationCommand.ColorNBodyByMass,
            TransformationCommand.ColorNBodyByAcceleration,
            TransformationCommand.ColorNBodyBySpeed,
            left,
            contentWidth,
            805);
        SetTwoColumnRow(
            TransformationCommand.DisableNBodyTrail,
            TransformationCommand.EnableSelectedNBodyTrail,
            left,
            right,
            columnWidth,
            834);
    }

    private void SetTwoColumnRow(
        TransformationCommand leftCommand,
        TransformationCommand rightCommand,
        int left,
        int right,
        int width,
        int y)
    {
        SetBounds(leftCommand, new Rectangle(left, y, width, ButtonHeight));
        SetBounds(rightCommand, new Rectangle(right, y, width, ButtonHeight));
    }

    private void SetThreeColumnRow(
        TransformationCommand first,
        TransformationCommand second,
        TransformationCommand third,
        int left,
        int contentWidth,
        int y)
    {
        var width = Math.Max(1, (contentWidth - (2 * ColumnGap)) / 3);
        SetBounds(first, new Rectangle(left, y, width, ButtonHeight));
        SetBounds(second, new Rectangle(left + width + ColumnGap, y, width, ButtonHeight));
        SetBounds(third, new Rectangle(left + (2 * (width + ColumnGap)), y, width, ButtonHeight));
    }

    private void SetFourColumnRow(
        TransformationCommand first,
        TransformationCommand second,
        TransformationCommand third,
        TransformationCommand fourth,
        int left,
        int contentWidth,
        int y)
    {
        var width = Math.Max(1, (contentWidth - (3 * ColumnGap)) / 4);
        SetBounds(first, new Rectangle(left, y, width, ButtonHeight));
        SetBounds(second, new Rectangle(left + width + ColumnGap, y, width, ButtonHeight));
        SetBounds(third, new Rectangle(left + (2 * (width + ColumnGap)), y, width, ButtonHeight));
        SetBounds(fourth, new Rectangle(left + (3 * (width + ColumnGap)), y, width, ButtonHeight));
    }

    private void SetBounds(TransformationCommand command, Rectangle bounds) =>
        _buttonByCommand[command].SetBounds(bounds);

}
