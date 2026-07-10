using System.Collections.Generic;
using SAM.Analytical.Systems.Mollier;
using Xunit;
using Xunit.Abstractions;

namespace SAM.Analytical.Systems.Mollier.Tests.Integration
{
    public class TwinWheelVerifyTests
    {
        private readonly ITestOutputHelper _output;

        public TwinWheelVerifyTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TwinWheelExample_Verify_ReturnsTrue_AllPassLines()
        {
            bool passed = TwinWheelExample.Verify(out List<string> messages);

            if (messages != null)
            {
                foreach (string msg in messages)
                {
                    _output.WriteLine(msg);
                }
            }

            Assert.True(passed, "TwinWheelExample.Verify() returned false. See output for details.");
        }
    }
}
