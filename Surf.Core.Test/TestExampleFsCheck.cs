using System;
using Xunit;
using FsCheck;
using FsCheck.Xunit;

namespace Surf.Core.Test
{
    /// <summary>
    /// Check out https://www.codit.eu/blog/property-based-testing-with-c
    /// </summary>
    public class TestExampleFsCHeck
    {

        public Property TestProp(int x)
        {
            int y = Arb.Generate<int>().Sample(1, 1).Head;

            return (y * 2 == Add(y, y)).ToProperty();
        }

        [Property]
        public Property TestProp2(int x)
        {
            return (Add(1, Add(1, x)) == Add(x, 2)).ToProperty();
        }

        [Property]
        public Property TestProp3(int x, int y)
        {
            return (Add(x, y) == Add(y, x)).ToProperty();
        }

        private int Add(int x, int y)
        {
            return x + y;
        }

        [Property]
        public Property TestProp4(int x, int y)
        {
            Func<bool> property = () => Divide(x * y, y) == x;
            return property.When(y != 0);
        }

        [Property]
        public Property TestProp5(int x)
        {
            return Prop.Throws<DivideByZeroException, int>(new Lazy<int>(() => Divide(x, 0)));
        }

        [Property]
        public Property TestProp6(int x)
        {
            return (x * 2 == Add(x, x)).Trivial(x < 0);
        }

        [Property]
        public Property TestProp7(int x)
        {
            Func<bool> lazy = () => (Add(1, Add(1, x)) == Add(x, 2));
            return lazy.Classify(x > 10, "Bigger than '10'")
                .Classify(x < 1000, "Smaller than '1000'");
        }

        [Property]
        public Property TestProp8(int x, int y)
        {
            return (Add(x, y) == Add(y, x)).Collect("Values together: " + (x + y));
        }

        [Property]
        public Property TestProp9(NonNegativeInt x, NonNegativeInt y)
        {
            int result = Add(x.Get, y.Get);
            return
              (result >= x.Get).Label("Result is bigger than 'x'").And(
                result >= y.Get).Label("Result is bigger than 'y'");
        }

        private int Divide(int x, int y)
        {
            return x / y;
        }
    }
}
