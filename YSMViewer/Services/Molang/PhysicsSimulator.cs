namespace YSMViewer.Services.Molang;

public sealed class PhysicsSimulator
{
    private readonly Dictionary<string, FirstOrderState> _firstOrderStates = [];
    private readonly Dictionary<string, SecondOrderState> _secondOrderStates = [];

    private static readonly byte[] StbPerlinRandTab =
    [
        23, 125, 161, 52, 103, 117, 70, 37, 247, 101, 203, 169, 124, 126, 44, 123,
        152, 238, 145, 45, 171, 114, 253, 10, 192, 136, 4, 157, 249, 30, 35, 72,
        175, 63, 77, 90, 181, 16, 96, 111, 133, 104, 75, 162, 93, 56, 66, 240,
        8, 50, 84, 229, 49, 210, 173, 239, 141, 1, 87, 18, 2, 198, 143, 57,
        225, 160, 58, 217, 168, 206, 245, 204, 199, 6, 73, 60, 20, 230, 211, 233,
        94, 200, 88, 9, 74, 155, 33, 15, 219, 130, 226, 202, 83, 236, 42, 172,
        165, 218, 55, 222, 46, 107, 98, 154, 109, 67, 196, 178, 127, 158, 13, 243,
        65, 79, 166, 248, 25, 224, 115, 80, 68, 51, 184, 128, 232, 208, 151, 122,
        26, 212, 105, 43, 179, 213, 235, 148, 146, 89, 14, 195, 28, 78, 112, 76,
        250, 47, 24, 251, 140, 108, 186, 190, 228, 170, 183, 139, 39, 188, 244, 246,
        132, 48, 119, 144, 180, 138, 134, 193, 82, 182, 120, 121, 86, 220, 209, 3,
        91, 241, 149, 85, 205, 150, 113, 216, 31, 100, 41, 164, 177, 214, 153, 231,
        38, 71, 185, 174, 97, 201, 29, 95, 7, 92, 54, 254, 191, 118, 34, 221,
        131, 11, 163, 99, 234, 81, 227, 147, 156, 176, 17, 142, 69, 12, 110, 62,
        27, 255, 0, 194, 59, 116, 242, 252, 19, 21, 187, 53, 207, 129, 64, 135,
        61, 40, 167, 237, 102, 223, 106, 159, 197, 189, 215, 137, 36, 32, 22, 5,

        23, 125, 161, 52, 103, 117, 70, 37, 247, 101, 203, 169, 124, 126, 44, 123,
        152, 238, 145, 45, 171, 114, 253, 10, 192, 136, 4, 157, 249, 30, 35, 72,
        175, 63, 77, 90, 181, 16, 96, 111, 133, 104, 75, 162, 93, 56, 66, 240,
        8, 50, 84, 229, 49, 210, 173, 239, 141, 1, 87, 18, 2, 198, 143, 57,
        225, 160, 58, 217, 168, 206, 245, 204, 199, 6, 73, 60, 20, 230, 211, 233,
        94, 200, 88, 9, 74, 155, 33, 15, 219, 130, 226, 202, 83, 236, 42, 172,
        165, 218, 55, 222, 46, 107, 98, 154, 109, 67, 196, 178, 127, 158, 13, 243,
        65, 79, 166, 248, 25, 224, 115, 80, 68, 51, 184, 128, 232, 208, 151, 122,
        26, 212, 105, 43, 179, 213, 235, 148, 146, 89, 14, 195, 28, 78, 112, 76,
        250, 47, 24, 251, 140, 108, 186, 190, 228, 170, 183, 139, 39, 188, 244, 246,
        132, 48, 119, 144, 180, 138, 134, 193, 82, 182, 120, 121, 86, 220, 209, 3,
        91, 241, 149, 85, 205, 150, 113, 216, 31, 100, 41, 164, 177, 214, 153, 231,
        38, 71, 185, 174, 97, 201, 29, 95, 7, 92, 54, 254, 191, 118, 34, 221,
        131, 11, 163, 99, 234, 81, 227, 147, 156, 176, 17, 142, 69, 12, 110, 62,
        27, 255, 0, 194, 59, 116, 242, 252, 19, 21, 187, 53, 207, 129, 64, 135,
        61, 40, 167, 237, 102, 223, 106, 159, 197, 189, 215, 137, 36, 32, 22, 5,
    ];

    private static readonly byte[] StbPerlinRandTabGradIdx =
    [
        7, 9, 5, 0, 11, 1, 6, 9, 3, 9, 11, 1, 8, 10, 4, 7,
        8, 6, 1, 5, 3, 10, 9, 10, 0, 8, 4, 1, 5, 2, 7, 8,
        7, 11, 9, 10, 1, 0, 4, 7, 5, 0, 11, 6, 1, 4, 2, 8,
        8, 10, 4, 9, 9, 2, 5, 7, 9, 1, 7, 2, 2, 6, 11, 5,
        5, 4, 6, 9, 0, 1, 1, 0, 7, 6, 9, 8, 4, 10, 3, 1,
        2, 8, 8, 9, 10, 11, 5, 11, 11, 2, 6, 10, 3, 4, 2, 4,
        9, 10, 3, 2, 6, 3, 6, 10, 5, 3, 4, 10, 11, 2, 9, 11,
        1, 11, 10, 4, 9, 4, 11, 0, 4, 11, 4, 0, 0, 0, 7, 6,
        10, 4, 1, 3, 11, 5, 3, 4, 2, 9, 1, 3, 0, 1, 8, 0,
        6, 7, 8, 7, 0, 4, 6, 10, 8, 2, 3, 11, 11, 8, 0, 2,
        4, 8, 3, 0, 0, 10, 6, 1, 2, 2, 4, 5, 6, 0, 1, 3,
        11, 9, 5, 5, 9, 6, 9, 8, 3, 8, 1, 8, 9, 6, 9, 11,
        10, 7, 5, 6, 5, 9, 1, 3, 7, 0, 2, 10, 11, 2, 6, 1,
        3, 11, 7, 7, 2, 1, 7, 3, 0, 8, 1, 1, 5, 0, 6, 10,
        11, 11, 0, 2, 7, 0, 10, 8, 3, 5, 7, 1, 11, 1, 0, 7,
        9, 0, 11, 5, 10, 3, 2, 3, 5, 9, 7, 9, 8, 4, 6, 5,

        7, 9, 5, 0, 11, 1, 6, 9, 3, 9, 11, 1, 8, 10, 4, 7,
        8, 6, 1, 5, 3, 10, 9, 10, 0, 8, 4, 1, 5, 2, 7, 8,
        7, 11, 9, 10, 1, 0, 4, 7, 5, 0, 11, 6, 1, 4, 2, 8,
        8, 10, 4, 9, 9, 2, 5, 7, 9, 1, 7, 2, 2, 6, 11, 5,
        5, 4, 6, 9, 0, 1, 1, 0, 7, 6, 9, 8, 4, 10, 3, 1,
        2, 8, 8, 9, 10, 11, 5, 11, 11, 2, 6, 10, 3, 4, 2, 4,
        9, 10, 3, 2, 6, 3, 6, 10, 5, 3, 4, 10, 11, 2, 9, 11,
        1, 11, 10, 4, 9, 4, 11, 0, 4, 11, 4, 0, 0, 0, 7, 6,
        10, 4, 1, 3, 11, 5, 3, 4, 2, 9, 1, 3, 0, 1, 8, 0,
        6, 7, 8, 7, 0, 4, 6, 10, 8, 2, 3, 11, 11, 8, 0, 2,
        4, 8, 3, 0, 0, 10, 6, 1, 2, 2, 4, 5, 6, 0, 1, 3,
        11, 9, 5, 5, 9, 6, 9, 8, 3, 8, 1, 8, 9, 6, 9, 11,
        10, 7, 5, 6, 5, 9, 1, 3, 7, 0, 2, 10, 11, 2, 6, 1,
        3, 11, 7, 7, 2, 1, 7, 3, 0, 8, 1, 1, 5, 0, 6, 10,
        11, 11, 0, 2, 7, 0, 10, 8, 3, 5, 7, 1, 11, 1, 0, 7,
        9, 0, 11, 5, 10, 3, 2, 3, 5, 9, 7, 9, 8, 4, 6, 5,
    ];

    private static readonly (float X, float Y, float Z)[] StbPerlinBasis =
    [
        (1, 1, 0), (-1, 1, 0), (1, -1, 0), (-1, -1, 0),
        (1, 0, 1), (-1, 0, 1), (1, 0, -1), (-1, 0, -1),
        (0, 1, 1), (0, -1, 1), (0, 1, -1), (0, -1, -1),
    ];

    public double FirstOrder(string id, double input, double response, double damping, double initialValue)
    {
        if (!_firstOrderStates.TryGetValue(id, out var state))
        {
            _firstOrderStates[id] = new FirstOrderState { Input = input, Response = response };
            return input;
        }

        state.Input = input;
        state.Response = response;
        return state.Value;
    }

    public double SecondOrder(string id, double input, double frequency, double coefficient, double response, double initialValue)
    {
        if (!_secondOrderStates.TryGetValue(id, out var state))
        {
            _secondOrderStates[id] = new SecondOrderState
            {
                Input = input,
                Frequency = Math.Clamp(frequency, 0.0, 5.0),
                Coefficient = Math.Clamp(coefficient, 0.0, 1.0),
                Response = response,
            };
            return input;
        }

        state.Input = input;
        state.Frequency = Math.Clamp(frequency, 0.0, 5.0);
        state.Coefficient = Math.Clamp(coefficient, 0.0, 1.0);
        state.Response = response;
        return state.Value;
    }

    public void UpdateAll(double deltaTime)
    {
        if (deltaTime <= 0.0 || double.IsNaN(deltaTime) || double.IsInfinity(deltaTime))
            return;

        foreach (var state in _firstOrderStates.Values)
            UpdateFirstOrder(state, deltaTime);

        foreach (var state in _secondOrderStates.Values)
            UpdateSecondOrder(state, deltaTime);
    }

    public void Clear()
    {
        _firstOrderStates.Clear();
        _secondOrderStates.Clear();
    }

    public static double PerlinNoise(double seed, double x, double y = 0.0, double z = 0.0)
    {
        int px = FastFloor((float)x);
        int py = FastFloor((float)y);
        int pz = FastFloor((float)z);
        int x0 = px & 255;
        int x1 = (px + 1) & 255;
        int y0 = py & 255;
        int y1 = (py + 1) & 255;
        int z0 = pz & 255;
        int z1 = (pz + 1) & 255;
        int s = (int)seed & 255;

        float xf = (float)x - px;
        float yf = (float)y - py;
        float zf = (float)z - pz;
        float u = Ease(xf);
        float v = Ease(yf);
        float w = Ease(zf);

        int r0 = StbPerlinRandTab[x0 + s];
        int r1 = StbPerlinRandTab[x1 + s];

        int r00 = StbPerlinRandTab[r0 + y0];
        int r01 = StbPerlinRandTab[r0 + y1];
        int r10 = StbPerlinRandTab[r1 + y0];
        int r11 = StbPerlinRandTab[r1 + y1];

        float n000 = Grad(StbPerlinRandTabGradIdx[r00 + z0], xf, yf, zf);
        float n001 = Grad(StbPerlinRandTabGradIdx[r00 + z1], xf, yf, zf - 1f);
        float n010 = Grad(StbPerlinRandTabGradIdx[r01 + z0], xf, yf - 1f, zf);
        float n011 = Grad(StbPerlinRandTabGradIdx[r01 + z1], xf, yf - 1f, zf - 1f);
        float n100 = Grad(StbPerlinRandTabGradIdx[r10 + z0], xf - 1f, yf, zf);
        float n101 = Grad(StbPerlinRandTabGradIdx[r10 + z1], xf - 1f, yf, zf - 1f);
        float n110 = Grad(StbPerlinRandTabGradIdx[r11 + z0], xf - 1f, yf - 1f, zf);
        float n111 = Grad(StbPerlinRandTabGradIdx[r11 + z1], xf - 1f, yf - 1f, zf - 1f);

        float n00 = Lerp(n000, n001, w);
        float n01 = Lerp(n010, n011, w);
        float n10 = Lerp(n100, n101, w);
        float n11 = Lerp(n110, n111, w);

        float n0 = Lerp(n00, n01, v);
        float n1 = Lerp(n10, n11, v);

        return Lerp(n0, n1, u);
    }

    private static void UpdateFirstOrder(FirstOrderState state, double deltaTime)
    {
        if (state.Response <= 0.0 || double.IsNaN(state.Response) || double.IsInfinity(state.Response))
        {
            state.Value = state.Input;
            return;
        }

        double t = deltaTime / state.Response;
        state.Value = ((1.0 - t) * state.Value) + (t * state.Input);
    }

    private static void UpdateSecondOrder(SecondOrderState state, double deltaTime)
    {
        double frequency = Math.Clamp(state.Frequency, 0.0001, 5.0);
        double coefficient = Math.Clamp(state.Coefficient, 0.0, 1.0);

        double k1 = coefficient / Math.PI / frequency;
        double k2 = 1.0 / (2.0 * Math.PI * frequency) / (2.0 * Math.PI * frequency);
        double k3 = state.Response * coefficient / 2.0 / Math.PI / frequency;

        double inputFunctionDot = (state.Input - state.InputFunction) / deltaTime;
        state.InputFunction = state.Input;

        double maxTimeStep = Math.Sqrt(4.0 * k2 + k1 * k1) - k1;
        if (maxTimeStep <= 0.0 || double.IsNaN(maxTimeStep) || double.IsInfinity(maxTimeStep))
            maxTimeStep = deltaTime;

        int cycleTime = Math.Max(1, (int)Math.Ceiling(deltaTime / maxTimeStep));
        double timeStep = deltaTime / cycleTime;

        double value = state.Value;
        double velocity = state.Velocity;
        for (int i = 0; i < cycleTime; i++)
        {
            value += timeStep * velocity;
            velocity += timeStep * (k3 * inputFunctionDot + state.Input - value - k1 * velocity) / k2;
        }

        state.Value = value;
        state.Velocity = velocity;
    }

    private static int FastFloor(float value)
    {
        int i = (int)value;
        return value < i ? i - 1 : i;
    }

    private static float Ease(float t) => ((t * 6f - 15f) * t + 10f) * t * t * t;
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float Grad(int gradIdx, float x, float y, float z)
    {
        var basis = StbPerlinBasis[gradIdx];
        return basis.X * x + basis.Y * y + basis.Z * z;
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
        public double InputFunction;
        public double Input;
        public double Frequency;
        public double Coefficient;
        public double Response;
    }
}
