// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using SAM.Analytical.Systems.Mollier;
using SAM.Core.Mollier;
using Xunit;
using Mollier = SAM.Analytical.Systems.Mollier;

namespace SAM.Analytical.Systems.Mollier.Tests.Query
{
    /// <summary>
    /// Proves the fan pressure recovery equation dP = eta * rho * cp * dT - the exact inverse of
    /// SAM.Core.Mollier's SFP convention (PickupTemperature: dT = SFP / (rho * cp), SFP = dP / eta).
    /// </summary>
    public class FanPressureTests
    {
        private const double Pressure = 101325.0;
        private const double FanEfficiency = 0.7;

        [Fact]
        public void FanPressureRise_IsExactInverse_OfPickupTemperature()
        {
            // SAM.Core.Mollier's fan convention is dT = SFP / (rho * cp) with SFP = dP / eta, so recovering the
            // pressure from a temperature rise and converting it back to a specific fan power must return the
            // original rise exactly - rho and cp cancel because both evaluate at the same inlet state.
            MollierPoint start = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(20, 50, Pressure);
            const double deltaT = 1.0;
            FanProcess process = start.FanProcess_ByDryBulbTemperature(start.DryBulbTemperature + deltaT);

            double pressureRise = process.FanPressureRise(FanEfficiency);
            Assert.False(double.IsNaN(pressureRise));

            double specificFanPower = pressureRise / FanEfficiency / 1000.0; // dP [Pa] -> SFP [W/(l/s)]
            Assert.Equal(deltaT, start.PickupTemperature(specificFanPower), 9);
        }

        [Fact]
        public void FanPressureRise_Equals_EfficiencyTimesSpecificFanPower()
        {
            // The SFP invariant: for a rise built from a specific fan power, dP == eta * sfp * 1000 [Pa]
            // regardless of the inlet state, because rho and cp cancel. 0.7 * 0.8 * 1000 == 560 Pa.
            const double specificFanPower = 0.8;

            MollierPoint cool = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(16, 50, Pressure);
            MollierPoint warm = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(30, 50, Pressure);

            foreach (MollierPoint start in new MollierPoint[] { cool, warm })
            {
                FanProcess process = start.FanProcess_ByDryBulbTemperature(
                    start.DryBulbTemperature + start.PickupTemperature(specificFanPower));

                Assert.Equal(FanEfficiency * specificFanPower * 1000.0, process.FanPressureRise(FanEfficiency), 6);
            }
        }

        [Fact]
        public void FanPressureRise_MatchesHandCalculation_EfficiencyMultiplies()
        {
            // dT = 1 K at 20 C / 50 %RH: rho ~ 1.199 kg/m3, cp ~ 1013 J/kgK
            //   correct   eta * rho * cp * dT ~ 0.7 * 1.199 * 1013 * 1 ~  850 Pa
            //   inverted  rho * cp * dT / eta ~ 1.199 * 1013 / 0.7    ~ 1735 Pa
            // The [700, 1000] band admits the physical equation and rejects the inverted one.
            MollierPoint start = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(20, 50, Pressure);
            FanProcess process = start.FanProcess_ByDryBulbTemperature(21.0);

            double pressureRise = process.FanPressureRise(FanEfficiency);

            double expected = FanEfficiency * start.Density() * (1000.0 * start.SpecificHeatCapacity_Air()) * 1.0;
            Assert.Equal(expected, pressureRise, 6);
            Assert.InRange(pressureRise, 700.0, 1000.0);
        }

        [Fact]
        public void FanPressureRise_ScalesLinearly_WithEfficiency()
        {
            // eta multiplies: half the efficiency must give half the recovered pressure, not double.
            MollierPoint start = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(20, 50, Pressure);
            FanProcess process = start.FanProcess_ByDryBulbTemperature(21.0);

            double atFull = process.FanPressureRise(0.8);
            double atHalf = process.FanPressureRise(0.4);

            Assert.Equal(atFull / 2.0, atHalf, 6);
        }

        [Fact]
        public void FanPressureRise_NaN_ForInvalidInputs()
        {
            MollierPoint start = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(20, 50, Pressure);
            FanProcess process = start.FanProcess_ByDryBulbTemperature(21.0);

            Assert.True(double.IsNaN(((FanProcess)null).FanPressureRise()));
            Assert.True(double.IsNaN(process.FanPressureRise(double.NaN)));
            Assert.True(double.IsNaN(process.FanPressureRise(0.0)));
            Assert.True(double.IsNaN(process.FanPressureRise(-0.5)));
        }

        [Fact]
        public void FanPressureRise_NaN_ForNonPositiveTemperatureRise()
        {
            // A fan always heats the air, so a zero rise carries no recoverable pressure.
            // (FanProcess_ByDryBulbTemperature heats by the ABSOLUTE difference, so it cannot express a
            // cooling fan at all - asking for 19 C from a 20 C inlet yields a 21 C outlet.)
            MollierPoint start = SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(20, 50, Pressure);
            FanProcess process = start.FanProcess_ByDryBulbTemperature(start.DryBulbTemperature);

            Assert.Equal(0.0, process.End.DryBulbTemperature - process.Start.DryBulbTemperature, 9);
            Assert.True(double.IsNaN(process.FanPressureRise(FanEfficiency)));
        }
    }
}
