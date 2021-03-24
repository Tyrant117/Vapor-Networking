
namespace VaporNetworking
{
    public class ExponentialMovingAverage
    {
        private readonly float alpha;
        private bool initialized;

        public double Value { get; private set; }

        public double Variance { get; private set; }

        public ExponentialMovingAverage(int n)
        {
            // standard N-day EMA alpha calculation
            alpha = 2.0f / (n + 1);
        }

        public void Add(double newValue)
        {
            // simple algorithm for EMA described here:
            // https://en.wikipedia.org/wiki/Moving_average#Exponentially_weighted_moving_variance_and_standard_deviation
            if (initialized)
            {
                double delta = newValue - Value;
                Value = Value + alpha * delta;
                Variance = (1 - alpha) * (Variance + alpha * delta * delta);
            }
            else
            {
                Value = newValue;
                initialized = true;
            }
        }
    }
}