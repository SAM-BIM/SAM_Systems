// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using SAM.Analytical.Systems.Mollier;
using SAM.Core.Mollier;
using Xunit;
using Mollier = SAM.Analytical.Systems.Mollier;

namespace SAM.Analytical.Systems.Mollier.Tests.Query
{
    /// <summary>
    /// Proves Query.HeatRecoveryEfficiencies exactly inverts the HeatRecoveryProcess_Supply/_Extract factory.
    /// The factory builds the supply End state by linear interpolation between the supply inlet and the extract
    /// inlet using eff/100 (sensible on dry-bulb temperature, latent on humidity ratio); the query recovers that
    /// same ratio, so round-tripping the factory's own design percentages returns them exactly (rho/cp-free,
    /// unlike the fan pressure convention - this is a plain linear ratio).
    /// </summary>
    public class HeatRecoveryEfficiencyTests
    {
        private const double Pressure = 101325.0;

        private static MollierPoint Outdoor() => SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(32, 40, Pressure);
        private static MollierPoint Room() => SAM.Core.Mollier.Create.MollierPoint_ByRelativeHumidity(24, 50, Pressure);

        [Fact]
        public void Efficiencies_Invert_FactoryPercentages()
        {
            MollierPoint outdoor = Outdoor();
            MollierPoint room = Room();

            HeatRecoveryProcess supply = outdoor.HeatRecoveryProcess_Supply(room, 75, 65);
            HeatRecoveryProcess extract = room.HeatRecoveryProcess_Extract(outdoor, 75, 65);

            supply.HeatRecoveryEfficiencies(extract, out double sensible, out double latent);

            Assert.Equal(0.75, sensible, 9);
            Assert.Equal(0.65, latent, 9);
        }

        [Fact]
        public void SensibleOnly_LeavesLatentNaN()
        {
            MollierPoint outdoor = Outdoor();
            MollierPoint room = Room();

            // 0% latent recovery: the factory leaves the supply humidity ratio bit-for-bit unchanged
            // (End.HumidityRatio == Start.HumidityRatio, verified by direct probe), so the Query's "does this
            // device transfer any moisture at all" gate (|supplyOut.w - supplyIn.w| > 1e-6) never opens and
            // latent stays NaN rather than being reported as 0.
            HeatRecoveryProcess supply = outdoor.HeatRecoveryProcess_Supply(room, 75, 0);
            HeatRecoveryProcess extract = room.HeatRecoveryProcess_Extract(outdoor, 75, 0);

            supply.HeatRecoveryEfficiencies(extract, out double sensible, out double latent);

            Assert.Equal(0.75, sensible, 9);
            Assert.True(double.IsNaN(latent), "Sensible-only recovery must leave latent effectiveness as NaN, not 0");
        }

        [Fact]
        public void NullProcess_LeavesBothNaN()
        {
            MollierPoint outdoor = Outdoor();
            MollierPoint room = Room();
            HeatRecoveryProcess supply = outdoor.HeatRecoveryProcess_Supply(room, 75, 65);

            Mollier.Query.HeatRecoveryEfficiencies(supply, null, out double sensible, out double latent);

            Assert.True(double.IsNaN(sensible));
            Assert.True(double.IsNaN(latent));
        }

        [Fact]
        public void Efficiencies_AreClampedTo_ZeroOneRange()
        {
            // The factory does not reject requests above 100%: verified by direct probe, outdoor (32 C) ->
            // room (24 C) at a requested 150% sensible recovery overshoots linearly to 20 C - a ratio of
            // (20-32)/(24-32) = 1.5, i.e. one-and-a-half times the 32->24 gap. Query.HeatRecoveryEfficiencies'
            // own Clamp() is what keeps the reported effectiveness physically meaningful at exactly 1.0.
            MollierPoint outdoor = Outdoor();
            MollierPoint room = Room();

            HeatRecoveryProcess supply = outdoor.HeatRecoveryProcess_Supply(room, 150, 65);
            HeatRecoveryProcess extract = room.HeatRecoveryProcess_Extract(outdoor, 150, 65);

            supply.HeatRecoveryEfficiencies(extract, out double sensible, out double latent);

            Assert.Equal(1.0, sensible, 9);
            Assert.True(sensible <= 1.0);
        }
    }
}
