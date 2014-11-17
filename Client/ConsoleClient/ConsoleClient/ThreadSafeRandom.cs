using System;

namespace Hermes
{
    class ThreadSafeRandom : Random
    {
        /* Fields */

        private static readonly Random global = new Random();

        [ThreadStatic]
        private static Random local;

        /* Constructor */

        public ThreadSafeRandom()
        {
            if (local == null)
            {
                int seed;
                lock (global)
                {
                    seed = global.Next();
                }
                local = new Random(seed);
            }
        }

        /* Methods */

        public override int Next()
        {
            return local.Next();
        }

        public override int Next(int maxValue)
        {
            return local.Next(maxValue);
        }

        public override int Next(int minValue, int maxValue)
        {
            return local.Next(minValue, maxValue);
        }

        public override double NextDouble()
        {
            return local.NextDouble();
        }
    }
}
