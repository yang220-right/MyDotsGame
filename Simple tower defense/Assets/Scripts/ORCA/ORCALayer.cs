﻿namespace Dots.RVO {
    /// <summary>
    /// Available layers to manage Agents, Obstacles & Raycasts
    /// </summary>
    [System.Flags]
    public enum ORCALayer {
        NONE = 0,
        L0 = 1,
        L1 = 2,
        L2 = 4,
        L3 = 8,
        L4 = 16,
        L5 = 32,
        L6 = 64,
        L7 = 128,
        L8 = 256,
        L9 = 512,
        L10 = 1024,
        L11 = 2048,
        L12 = 4096,
        L13 = 8192,
        L14 = 16384,
        L15 = 32768,
        L16 = 65536,
        L17 = 131072,
        L18 = 262144,
        L19 = 524288,
        L20 = 1048576,
        L21 = 2097152,
        L22 = 4194304,
        L23 = 8388608,
        L24 = 16777216,
        L25 = 33554432,
        L26 = 67108864,
        L27 = 134217728,
        L28 = 268435456,
        L29 = 536870912,
        L30 = 1073741824,
        ANY =
            L0 | L1 | L2 | L3 |
            L4 | L5 | L6 | L7 |
            L8 | L9 | L10 | L11 |
            L12 | L13 | L14 | L15 |
            L16 | L17 | L18 | L19 |
            L20 | L21 | L22 | L23 |
            L24 | L25 | L26 | L27 |
            L28 | L29 | L30
    }
}
