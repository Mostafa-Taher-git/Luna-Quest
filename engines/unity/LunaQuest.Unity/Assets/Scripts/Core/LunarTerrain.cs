using System;
namespace LunaQuest.Core.Terrain
{
    /// <summary>
    /// A deterministic, infinite lunar surface. The height of any point depends only on its
    /// (x, z) in metres and the site seed, so both engines draw the same ground, a saved game
    /// never needs to store terrain, and the whole Moon can be streamed instead of authored.
    ///
    /// Model: low-frequency relief (highland swells) + three scales of simple impact craters
    /// (bowl, raised rim, ejecta blanket; depth about 18% of diameter, the simple-crater
    /// ratio) + fine regolith roughness. Heights are blended to zero around the colony pad so
    /// every building keeps the flat foundation it was designed on.
    /// </summary>
    public sealed class LunarTerrain
    {
        public int Seed { get; }
        public double FlatCenterX { get; }
        public double FlatCenterZ { get; }
        public double FlatRadius { get; }
        public double FadeWidth { get; }
        /// <summary>Overall relief multiplier; highlands are rougher than maria.</summary>
        public double Ruggedness { get; }

        public LunarTerrain(int seed, double flatCenterX = 0, double flatCenterZ = 8,
            double flatRadius = 88, double fadeWidth = 90, double ruggedness = 1.0)
        {
            Seed = seed;
            FlatCenterX = flatCenterX;
            FlatCenterZ = flatCenterZ;
            FlatRadius = flatRadius;
            FadeWidth = Math.Max(1.0, fadeWidth);
            Ruggedness = ruggedness;
        }

        /// <summary>Height above the datum plane in metres.</summary>
        public double Height(double x, double z)
        {
            double dx = x - FlatCenterX, dz = z - FlatCenterZ;
            double dist = Math.Sqrt(dx * dx + dz * dz);
            double blend = Smooth((dist - FlatRadius) / FadeWidth);
            if (blend <= 0.0) return 0.0;
            double h = Relief(x, z) + Craters(x, z);
            return h * blend * Ruggedness;
        }

        /// <summary>Slope angle in degrees, from central differences over 2 m.</summary>
        public double SlopeDegrees(double x, double z)
        {
            const double d = 1.0;
            double gx = (Height(x + d, z) - Height(x - d, z)) / (2 * d);
            double gz = (Height(x, z + d) - Height(x, z - d)) / (2 * d);
            return Math.Atan(Math.Sqrt(gx * gx + gz * gz)) * 180.0 / Math.PI;
        }

        private double Relief(double x, double z)
        {
            double h = 0;
            h += (Fbm(x / 420.0, z / 420.0, 11, 4) - 0.5) * 2.0 * 16.0;
            h += (Fbm(x / 70.0, z / 70.0, 23, 3) - 0.5) * 2.0 * 1.4;
            h += (Fbm(x / 9.0, z / 9.0, 37, 2) - 0.5) * 2.0 * 0.12;
            return h;
        }

        private double Craters(double x, double z)
        {
            double h = 0;
            h += CraterLayer(x, z, 520.0, 70.0, 210.0, 0.55, 101);
            h += CraterLayer(x, z, 150.0, 14.0, 52.0, 0.75, 202);
            h += CraterLayer(x, z, 42.0, 2.5, 13.0, 0.80, 303);
            return h;
        }

        private double CraterLayer(double x, double z, double cell, double rMin, double rMax,
            double presence, int salt)
        {
            int cx = (int)Math.Floor(x / cell), cz = (int)Math.Floor(z / cell);
            double sum = 0;
            for (int i = -1; i <= 1; i++)
            for (int j = -1; j <= 1; j++)
            {
                int gx = cx + i, gz = cz + j;
                uint hsh = Hash(gx, gz, Seed + salt);
                if (Unit(hsh) > presence) continue;
                double r = rMin + (rMax - rMin) * Math.Pow(Unit(Next(hsh, 1)), 2.2);
                double px = (gx + 0.15 + 0.7 * Unit(Next(hsh, 2))) * cell;
                double pz = (gz + 0.15 + 0.7 * Unit(Next(hsh, 3))) * cell;
                double dx = x - px, dz = z - pz;
                double t = Math.Sqrt(dx * dx + dz * dz) / r;
                if (t > 2.6) continue;
                double depth = r * 0.19 * (0.55 + 0.45 * Unit(Next(hsh, 4))); // r is radius; shallow, walkable bowls (D/d about 0.12)
                double rim = depth * 0.22;
                double bowl = t < 1.0 ? depth * (t * t - 1.0) : 0.0;
                double ring = rim * Math.Exp(-Math.Pow((t - 1.0) / 0.30, 2.0));
                double ejecta = t > 1.0 ? rim * 0.35 * Math.Exp(-(t - 1.0) * 2.4) : 0.0;
                sum += bowl + ring + ejecta;
            }
            return sum;
        }

        // ---- noise ------------------------------------------------------------

        private double Fbm(double x, double z, int salt, int octaves)
        {
            double sum = 0, amp = 0.5, norm = 0;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * ValueNoise(x, z, salt + o * 17);
                norm += amp;
                x *= 2.03; z *= 2.03; amp *= 0.5;
            }
            return sum / norm;
        }

        private double ValueNoise(double x, double z, int salt)
        {
            int xi = (int)Math.Floor(x), zi = (int)Math.Floor(z);
            double fx = x - xi, fz = z - zi;
            double u = fx * fx * (3 - 2 * fx), v = fz * fz * (3 - 2 * fz);
            double a = Unit(Hash(xi, zi, Seed + salt));
            double b = Unit(Hash(xi + 1, zi, Seed + salt));
            double c = Unit(Hash(xi, zi + 1, Seed + salt));
            double d = Unit(Hash(xi + 1, zi + 1, Seed + salt));
            return (a + (b - a) * u) + ((c + (d - c) * u) - (a + (b - a) * u)) * v;
        }

        private static uint Hash(int x, int z, int salt)
        {
            unchecked
            {
                uint h = (uint)salt * 374761393u + (uint)x * 668265263u + (uint)z * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return h;
            }
        }

        private static uint Next(uint h, int n)
        {
            unchecked
            {
                h += (uint)n * 0x9E3779B9u;
                h = (h ^ (h >> 15)) * 2246822519u;
                h = (h ^ (h >> 13)) * 3266489917u;
                return h ^ (h >> 16);
            }
        }

        private static double Unit(uint h) => (h & 0xFFFFFF) / (double)0x1000000;

        private static double Smooth(double t)
        {
            if (t <= 0) return 0;
            if (t >= 1) return 1;
            return t * t * (3 - 2 * t);
        }
    }
}
