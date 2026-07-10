using SAM.Analytical.Systems.Mollier;
using SAM.Core.Mollier;
using Xunit;
using Mollier = SAM.Analytical.Systems.Mollier;

namespace SAM.Analytical.Systems.Mollier.Tests.Query
{
    public class DutyTests
    {
        private static readonly double Pressure = 101325.0;

        private static MollierPoint CreatePoint(double dryBulb, double humidityRatio = 0.008)
        {
            return new MollierPoint(dryBulb, humidityRatio, Pressure);
        }

        [Fact]
        public void Duty_ReturnsPositiveValue_ForHeating()
        {
            MollierPoint start = CreatePoint(20.0);
            HeatingProcess process = start.HeatingProcess(35.0);
            double duty = process.Duty(2.0);

            Assert.False(double.IsNaN(duty));
            Assert.True(duty > 0);
        }

        [Fact]
        public void Duty_ReturnsPositiveValue_ForCooling()
        {
            MollierPoint start = CreatePoint(28.0);
            CoolingProcess process = start.CoolingProcess(12.0, 0.8);
            double duty = process.Duty(2.0);

            Assert.False(double.IsNaN(duty));
            Assert.True(duty > 0);
        }

        [Fact]
        public void Duty_ScalesWith_Airflow()
        {
            MollierPoint start = CreatePoint(20.0);
            HeatingProcess process = start.HeatingProcess(35.0);
            double duty1 = process.Duty(1.0);
            double duty2 = process.Duty(2.0);

            Assert.False(double.IsNaN(duty1));
            Assert.False(double.IsNaN(duty2));
            double ratio = duty2 / duty1;
            Assert.True(ratio > 1.9 && ratio < 2.1);
        }

        [Fact]
        public void Duty_ReturnsNaN_ForNaNAirflow()
        {
            MollierPoint start = CreatePoint(20.0);
            HeatingProcess process = start.HeatingProcess(35.0);
            double duty = process.Duty(double.NaN);

            Assert.True(double.IsNaN(duty));
        }

        [Fact]
        public void Duty_ReturnsNaN_ForNullProcess()
        {
            double duty = Mollier.Query.Duty(null, 2.0);
            Assert.True(double.IsNaN(duty));
        }

        [Fact]
        public void MassFlow_ReturnsValue_ForValidInput()
        {
            MollierPoint start = CreatePoint(20.0);
            HeatingProcess process = start.HeatingProcess(35.0);
            double massFlow = Mollier.Query.MassFlow(process, 2.0);

            Assert.False(double.IsNaN(massFlow));
            Assert.True(massFlow > 0);
        }

        [Fact]
        public void MassFlow_ReturnsNaN_ForNaNAirflow()
        {
            MollierPoint start = CreatePoint(20.0);
            HeatingProcess process = start.HeatingProcess(35.0);
            double massFlow = Mollier.Query.MassFlow(process, double.NaN);

            Assert.True(double.IsNaN(massFlow));
        }
    }
}
