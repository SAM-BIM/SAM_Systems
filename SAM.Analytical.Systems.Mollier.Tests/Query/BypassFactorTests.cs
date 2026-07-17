// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (c) 2020-2026 Michal Dengusiak & Jakub Ziolkowski and contributors
using SAM.Analytical.Systems.Mollier;
using SAM.Core.Mollier;
using Xunit;
using Mollier = SAM.Analytical.Systems.Mollier;

namespace SAM.Analytical.Systems.Mollier.Tests.Query
{
    public class BypassFactorTests
    {
        private static readonly double Pressure = 101325.0;

        private static MollierPoint CreatePoint(double dryBulb, double humidityRatio = 0.008)
        {
            return new MollierPoint(dryBulb, humidityRatio, Pressure);
        }

        [Fact]
        public void BypassFactor_ReturnsValue_RangeZeroToOne()
        {
            MollierPoint start = CreatePoint(28.0);
            CoolingProcess process = start.CoolingProcess(12.0, 0.8);
            double bypassFactor = process.BypassFactor();

            Assert.False(double.IsNaN(bypassFactor));
            Assert.True(bypassFactor >= 0);
            Assert.True(bypassFactor <= 1.0);
        }

        [Fact]
        public void BypassFactor_ReturnsNaN_ForNullProcess()
        {
            double bypassFactor = Mollier.Query.BypassFactor(null);
            Assert.True(double.IsNaN(bypassFactor));
        }

        [Fact]
        public void BypassFactor_ClampedToZero()
        {
            MollierPoint start = CreatePoint(28.0);
            CoolingProcess process = start.CoolingProcess(26.0, 0.99);
            double bypassFactor = process.BypassFactor();

            Assert.False(double.IsNaN(bypassFactor));
            Assert.True(bypassFactor >= 0);
        }

        [Fact]
        public void BypassFactor_ClampedToOne()
        {
            MollierPoint start = CreatePoint(28.0);
            CoolingProcess process = start.CoolingProcess(12.0, 0.01);
            double bypassFactor = process.BypassFactor();

            Assert.False(double.IsNaN(bypassFactor));
            Assert.True(bypassFactor <= 1.0);
        }

        [Fact]
        public void BypassFactor_NotZero_ForPartialCooling()
        {
            MollierPoint start = CreatePoint(28.0);
            CoolingProcess process = start.CoolingProcess(14.0, 0.5);
            double bypassFactor = process.BypassFactor();

            Assert.False(double.IsNaN(bypassFactor));
            Assert.True(bypassFactor > 0);
            Assert.True(bypassFactor < 1.0);
        }
    }
}
