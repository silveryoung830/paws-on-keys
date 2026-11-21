namespace PawsOnKeys
{
    // We only need to know whether there were at least ten strokes in the window.
    // Each slot is a monotonic time in seconds and contains no key identity.
    internal sealed class TypingRhythm
    {
        private const int HelpThreshold = 10;
        private const int ReleaseThreshold = 8;
        private const double WindowSeconds = 4.0;
        private const double IdleSeconds = 1.2;
        private const double AgitationDelay = 4.0;

        private readonly double[] strokes = new double[HelpThreshold];
        private int oldest;
        private int count;
        private double lastStroke;
        private double helpingSince;

        internal bool Helping { get; private set; }
        internal double AgitatedSince { get; private set; }

        internal TypingRhythm()
        {
            Reset();
        }

        internal void Press(double now)
        {
            Expire(now);
            if (count == strokes.Length)
                RemoveOldest();

            strokes[(oldest + count) % strokes.Length] = now;
            count++;
            lastStroke = now;
            Evaluate(now);
        }

        internal void Update(double now)
        {
            Expire(now);
            Evaluate(now);
        }

        private void Expire(double now)
        {
            if (count > 0 && now >= lastStroke + IdleSeconds)
            {
                Reset();
                return;
            }

            while (count > 0 && now >= strokes[oldest] + WindowSeconds)
                RemoveOldest();
        }

        private void RemoveOldest()
        {
            strokes[oldest] = 0;
            oldest = (oldest + 1) % strokes.Length;
            count--;
        }

        private void Evaluate(double now)
        {
            if (count < ReleaseThreshold)
            {
                Helping = false;
                AgitatedSince = -1;
                return;
            }

            if (!Helping && count >= HelpThreshold)
            {
                Helping = true;
                helpingSince = now;
            }

            if (Helping && AgitatedSince < 0 && count >= HelpThreshold && now >= helpingSince + AgitationDelay)
                AgitatedSince = now;
        }

        internal void Reset()
        {
            System.Array.Clear(strokes, 0, strokes.Length);
            oldest = count = 0;
            lastStroke = helpingSince = 0;
            Helping = false;
            AgitatedSince = -1;
        }
    }
}
