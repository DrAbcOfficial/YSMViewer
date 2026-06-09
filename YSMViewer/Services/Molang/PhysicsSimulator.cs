namespace YSMViewer.Services.Molang;

public sealed class PhysicsSimulator
{
    private readonly Dictionary<string, FirstOrderState> _firstOrderStates = [];
    private readonly Dictionary<string, SecondOrderState> _secondOrderStates = [];

    private static readonly int[] PerlinPerm = BuildPermutationTable();

    private static int[] BuildPermutationTable()
    {
        var p = new int[512];
        var perm = new int[256];
        for (int i = 0; i < 256; i++) perm[i] = i;
        var rng = new Random(42);
        for (int i = 255; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (perm[i], perm[j]) = (perm[j], perm[i]);
        }
        for (int i = 0; i < 256; i++)
        {
            p[i] = perm[i];
            p[i + 256] = perm[i];
        }
        return p;
    }

    public double FirstOrder(string id, double input, double response, double damping, double initialValue)
    {
        if (!_firstOrderStates.TryGetValue(id, out var state))
        {
            state = new FirstOrderState { Value = input, Response = response, Input = input };
            _firstOrderStates[id] = state;
            return input;
        }

        state.Input = input;
        state.Response = response;

        double dt = 0.05;
        double t = dt / state.Response;
        state.Value = (1.0 - t) * state.Value + t * state.Input;
        return state.Value;
    }

    public double SecondOrder(string id, double input, double frequency, double coefficient, double response, double initialValue)
    {
        if (!_secondOrderStates.TryGetValue(id, out var state))
        {
            double f = Math.Clamp(frequency, 0.01, 5.0);
            double c = Math.Clamp(coefficient, 0.0, 1.0);
            state = new SecondOrderState
            {
                Value = input,
                Velocity = 0,
                LastInput = input,
                Frequency = f,
                Coefficient = c,
                Response = response,
            };
            _secondOrderStates[id] = state;
            return input;
        }

        state.Frequency = Math.Clamp(frequency, 0.01, 5.0);
        state.Coefficient = Math.Clamp(coefficient, 0.0, 1.0);
        state.Response = response;

        double k1 = state.Coefficient / (Math.PI * state.Frequency);
        double k2 = 1.0 / ((2.0 * Math.PI * state.Frequency) * (2.0 * Math.PI * state.Frequency));
        double k3 = state.Response * state.Coefficient / (2.0 * Math.PI * state.Frequency);

        double dt = 0.05;
        double inputFunctionDot = (input - state.LastInput) / dt;
        state.LastInput = input;

        double maxTimeStep = Math.Sqrt(4.0 * k2 + k1 * k1) - k1;
        if (maxTimeStep <= 0) maxTimeStep = dt;
        int cycleTime = Math.Max(1, (int)Math.Ceiling(dt / maxTimeStep));
        double subDt = dt / cycleTime;

        double lastSimulation = state.Value;
        double lastDot = state.Velocity;

        for (int i = 0; i < cycleTime; i++)
        {
            lastSimulation += subDt * lastDot;
            lastDot += subDt * (k3 * inputFunctionDot + input - lastSimulation - k1 * lastDot) / k2;
        }

        state.Value = lastSimulation;
        state.Velocity = lastDot;
        return state.Value;
    }

    public static double PerlinNoise(double seed, double x, double y = 0.0, double z = 0.0)
    {
        int s = (int)seed & 255;
        int xi = (int)Math.Floor(x) & 255;
        int yi = (int)Math.Floor(y) & 255;
        int zi = (int)Math.Floor(z) & 255;
        double xf = x - Math.Floor(x);
        double yf = y - Math.Floor(y);
        double zf = z - Math.Floor(z);

        double u = Fade(xf);
        double v = Fade(yf);
        double w = Fade(zf);

        int aaa = PerlinPerm[PerlinPerm[PerlinPerm[xi] + yi] + zi + s] & 255;
        int aba = PerlinPerm[PerlinPerm[PerlinPerm[xi] + yi + 1] + zi + s] & 255;
        int aab = PerlinPerm[PerlinPerm[PerlinPerm[xi] + yi] + zi + 1 + s] & 255;
        int abb = PerlinPerm[PerlinPerm[PerlinPerm[xi] + yi + 1] + zi + 1 + s] & 255;
        int baa = PerlinPerm[PerlinPerm[PerlinPerm[xi + 1] + yi] + zi + s] & 255;
        int bba = PerlinPerm[PerlinPerm[PerlinPerm[xi + 1] + yi + 1] + zi + s] & 255;
        int bab = PerlinPerm[PerlinPerm[PerlinPerm[xi + 1] + yi] + zi + 1 + s] & 255;
        int bbb = PerlinPerm[PerlinPerm[PerlinPerm[xi + 1] + yi + 1] + zi + 1 + s] & 255;

        double x1 = Lerp(Grad3(aaa, xf, yf, zf), Grad3(baa, xf - 1, yf, zf), u);
        double x2 = Lerp(Grad3(aba, xf, yf - 1, zf), Grad3(bba, xf - 1, yf - 1, zf), u);
        double y1 = Lerp(x1, x2, v);

        x1 = Lerp(Grad3(aab, xf, yf, zf - 1), Grad3(bab, xf - 1, yf, zf - 1), u);
        x2 = Lerp(Grad3(abb, xf, yf - 1, zf - 1), Grad3(bbb, xf - 1, yf - 1, zf - 1), u);
        double y2 = Lerp(x1, x2, v);

        return Lerp(y1, y2, w);
    }

    public static void UpdateAll()
    {
    }

    private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
    private static double Lerp(double a, double b, double t) => a + t * (b - a);

    private static double Grad3(int hash, double x, double y, double z)
    {
        int h = hash & 15;
        double u = h < 8 ? x : y;
        double v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    private sealed class FirstOrderState
    {
        public double Value;
        public double Input;
        public double Response;
    }

    private sealed class SecondOrderState
    {
        public double Value;
        public double Velocity;
        public double LastInput;
        public double Frequency;
        public double Coefficient;
        public double Response;
    }
}